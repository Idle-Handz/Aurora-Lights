using Aurora.App.Services;
using Aurora.Content.Preparation;
using Aurora.Importer;
using Builder.Core.Events;
using Builder.Data;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services.Data;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

if (!OperatingSystem.IsWindows() || args.Length < 2)
    throw new ArgumentException("Windows rehearsal: <scan|refresh|load|snapshot|parity> <disposable-case-directory> [secondary-directory]");
string mode = args[0];
string caseRoot = Path.GetFullPath(args[1]);
if (!File.Exists(Path.Combine(caseRoot, ".aurora-rehearsal")))
    throw new InvalidOperationException("The case directory must contain .aurora-rehearsal; do not use installed content.");
// REHEARSAL_OUTPUT lets parallel read-only runs share one case while writing results separately.
string output = Environment.GetEnvironmentVariable("REHEARSAL_OUTPUT") is { Length: > 0 } requested
    ? Path.GetFullPath(requested) : caseRoot;
Directory.CreateDirectory(output);
var context = new RehearsalContext(caseRoot);
if (args.Length > 2 && mode is not ("characters" or "characters-after-reload" or "characters-edit-background")) context.Settings.AdditionalCustomDirectories.Add(Path.GetFullPath(args[2]));
Builder.Presentation.ApplicationContext.SetCurrent(context);
string primary = Path.Combine(caseRoot, "custom");
SetPath(nameof(DataManager.UserDocumentsRootDirectory), caseRoot);
SetPath(nameof(DataManager.UserDocumentsCustomElementsDirectory), primary);
foreach (var property in typeof(DataManager).GetProperties().Where(p => p.PropertyType == typeof(string)
    && p.Name.EndsWith("Directory", StringComparison.Ordinal) && p.SetMethod != null && p.GetValue(DataManager.Current) == null))
{
    string directory = Path.Combine(output, "runtime-directories", property.Name);
    Directory.CreateDirectory(directory);
    property.SetValue(DataManager.Current, directory);
}
DebugLogService.Instance.InitializePersistentLog(output, mode + "-app.log");
var stopwatch = Stopwatch.StartNew();
object? result = null;
bool success = false;
try
{
    var service = new ContentDatabaseService();
    switch (mode)
    {
        case "farmer-annotation": result = FarmerCorrectionRehearsal.Run(caseRoot); success = true; break;
        case "asi-check": result = await AsiCleanupRehearsal.Run(caseRoot); success = true; break;
        case "guard-correction": result = GuardCorrectionRehearsal.Run(caseRoot); success = true; break;
        case "scan": result = new { stale = service.CheckIsStale() }; success = true; break;
        case "characters":
        case "characters-after-reload":
        case "characters-edit-background":
            result = await CharacterRehearsal.Run(caseRoot, output, args.Length > 2 ? args[2] : null, mode == "characters-after-reload", mode == "characters-edit-background");
            success = !JsonSerializer.SerializeToElement(result).GetProperty("hasFailures").GetBoolean();
            break;
        case "reload-check": result = await ReloadRehearsal.Run(caseRoot); success = true; break;
        case "failure-check": result = await ReloadRehearsal.Run(caseRoot, false); success = true; break;
        case "fallback-check": result = FallbackRegressionChecks.Run(); success = true; break;
        case "profile-projection":
            using (var connection = ContentDatabase.OpenReadableConnection(Path.Combine(primary, ContentDatabaseService.DatabaseFileName)))
            {
                var projection = DbElementLoader.ReadPreparedProjection(connection);
                double before = GC.GetTotalMemory(true) / 1048576.0;
                var publishFallback = XmlContentFallbackService.PrepareProjection(projection);
                double after = GC.GetTotalMemory(true) / 1048576.0;
                result = new { count = projection.Elements.Count,
                    xmlCharacters = projection.Elements.Sum(e => (long)e.Xml.Length),
                    beforeMiB = before, afterMiB = after, fallbackRetainedMiB = after - before };
                GC.KeepAlive(publishFallback);
                GC.KeepAlive(projection);
                success = true;
            }
            break;
        case "legacy-import":
            var legacy = AuroraContentImporter.Import(primary, Path.Combine(primary, ContentDatabaseService.DatabaseFileName));
            result = legacy;
            success = legacy.Success;
            break;
        case "refresh":
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15)))
            {
                var import = await service.SyncAsync(cancellation.Token);
                result = new { import, service.SyncState, service.Progress, metadata = service.GetMetadata() };
                success = import.Success;
            }
            break;
        case "load":
        case "profile-load":
        case "snapshot":
            bool publish = mode != "snapshot";
            var elements = publish ? DataManager.Current.ElementsCollection : new ElementBaseCollection();
            var load = mode == "profile-load" ? LoadForMemory(elements)
                : publish ? await DbElementLoader.TryLoadAsync(elements) : await DbElementLoader.TryLoadSnapshotAsync(elements);
            object? memory = null;
            if (mode == "profile-load")
            {
                double beforeCollection = GC.GetTotalMemory(false) / 1048576.0;
                bool publisherRetained = HasFallbackPublisher();
                double retained = GC.GetTotalMemory(true) / 1048576.0;
                XmlContentFallbackService.Invalidate(); // Disposable process only; isolate retained fallback cost.
                double afterInvalidation = GC.GetTotalMemory(true) / 1048576.0;
                ClearFallbackPublisher();
                double withoutFallback = GC.GetTotalMemory(true) / 1048576.0;
                memory = new { beforeCollectionMiB = beforeCollection, retainedMiB = retained,
                    publisherRetained,
                    afterInvalidationMiB = afterInvalidation,
                    withoutFallbackMiB = withoutFallback, fallbackRetainedMiB = retained - withoutFallback };
            }
            result = new { load, runtimeCount = elements.Count,
                memory,
                duplicateIds = elements.GroupBy(e => e.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => new { id = g.Key, count = g.Count() }).ToArray(),
                aliases = elements.Count(e => e.Id.Contains("_LOCAL_", StringComparison.Ordinal)),
                types = elements.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count()),
                lookupCounts = new { archetypes = DbElementLoader.ArchetypeParentMap.Count, spells = DbElementLoader.SpellAccessMap.Count, sort = DbElementLoader.ElementSortMetadataMap.Count } };
            success = load.Success;
            break;
        case "dump":
            var dumpElements = DataManager.Current.ElementsCollection;
            var dumpLoad = await DbElementLoader.TryLoadAsync(dumpElements);
            var entries = dumpElements
                .Select(e => new { e.Id, e.Type, e.Name, fingerprint = Fingerprint(e),
                    provenance = RelativeTo(primary, e.ContentFilePath) })
                .OrderBy(e => e.Id, StringComparer.Ordinal).ThenBy(e => e.fingerprint, StringComparer.Ordinal)
                .ToArray();
            File.WriteAllText(Path.Combine(output, "projection-dump.json"),
                JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            result = new { load = dumpLoad, runtimeCount = dumpElements.Count, dumped = entries.Length };
            success = dumpLoad.Success;
            break;
        case "parity":
            // Avoid InitializeDirectories, which also touches the user's AppData.
            var method = typeof(ContentDatabaseParityService).GetMethod("LoadXmlSnapshotAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            var task = (Task)method.Invoke(null, [CancellationToken.None])!;
            await task;
            object xmlResult = task.GetType().GetProperty("Result")!.GetValue(task)!;
            var xmlType = xmlResult.GetType();
            var xmlElements = (ElementBaseCollection)xmlType.GetProperty("Elements")!.GetValue(xmlResult)!;
            var dbElements = new ElementBaseCollection();
            var dbResult = await DbElementLoader.TryLoadSnapshotAsync(dbElements);
            var missingDb = xmlElements.Select(e => e.Id).Except(dbElements.Select(e => e.Id), StringComparer.Ordinal).ToArray();
            var missingXml = dbElements.Select(e => e.Id).Except(xmlElements.Select(e => e.Id), StringComparer.Ordinal).ToArray();
            var xmlById = xmlElements.ToDictionary(e => e.Id);
            var differences = dbElements.Where(e => xmlById.TryGetValue(e.Id, out var x) &&
                Builder.Data.Files.LocalCorrectionDocument.Fingerprint(System.Xml.Linq.XElement.Parse(e.ElementNode.OuterXml)) !=
                Builder.Data.Files.LocalCorrectionDocument.Fingerprint(System.Xml.Linq.XElement.Parse(x.ElementNode.OuterXml)))
                .Select(e => e.Id).ToArray();
            bool xmlSuccess = (bool)xmlType.GetProperty("Success")!.GetValue(xmlResult)!;
            result = new { xmlSuccess, xmlFailure = xmlType.GetProperty("FailureReason")!.GetValue(xmlResult), dbResult,
                xmlCount = xmlElements.Count, dbCount = dbElements.Count, missingDb, missingXml, differences };
            success = xmlSuccess && dbResult.Success && missingDb.Length == 0 && missingXml.Length == 0 && differences.Length == 0;
            break;
        default: throw new ArgumentException("Unknown rehearsal mode.");
    }
}
catch (Exception error) { result = new { error = error.ToString() }; }
stopwatch.Stop();
using var current = Process.GetCurrentProcess();
var report = new { mode, caseRoot, success, elapsedSeconds = stopwatch.Elapsed.TotalSeconds,
    peakWorkingSetMiB = current.PeakWorkingSet64 / 1048576.0, managedMiB = GC.GetTotalMemory(false) / 1048576.0, result,
    warnings = DebugLogService.Instance.Entries.Where(e => e.Level != LogLevel.Info).ToArray() };
string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(output, mode + "-result.json"), json);
Console.WriteLine(json);
Environment.ExitCode = success ? 0 : 1;

static void SetPath(string name, string value) => typeof(DataManager).GetProperty(name)!.SetValue(DataManager.Current, value);

static string Fingerprint(ElementBase element)
{
    string? xml = element.ElementNode?.OuterXml ?? element.ElementNodeString;
    return string.IsNullOrEmpty(xml) ? ""
        : Builder.Data.Files.LocalCorrectionDocument.Fingerprint(System.Xml.Linq.XElement.Parse(xml));
}

static string? RelativeTo(string root, string? path)
    => string.IsNullOrEmpty(path) ? path : Path.GetRelativePath(root, path).Replace('\\', '/');

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static DbLoadResult LoadForMemory(ElementBaseCollection elements)
    => DbElementLoader.TryLoadAsync(elements).GetAwaiter().GetResult();

[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static void ClearFallbackPublisher()
{
    var lookups = typeof(DbElementLoader).GetField("_lookups", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    lookups.GetType().GetField("PublishFallback")!.SetValue(lookups, null);
}

static bool HasFallbackPublisher()
{
    var lookups = typeof(DbElementLoader).GetField("_lookups", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    return lookups.GetType().GetField("PublishFallback")!.GetValue(lookups) != null;
}

sealed class RehearsalContext(string root) : IApplicationContext
{
    public IEventAggregator EventAggregator { get; } = new EventAggregator();
    public Builder.Presentation.AppSettingsStore Settings { get; } = new() { DocumentsRootDirectory = root };
    public bool IsInDeveloperMode { get; set; }
    public bool EnableDiagnostics { get; set; }
    public string? LoadedCharacterFilePath { get; set; }
    public bool HasCharacterFileRequest => false;
    public void SendStatusMessage(string message) { }
}
