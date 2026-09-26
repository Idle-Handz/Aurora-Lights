using Aurora.App.Services;
using Aurora.Content;
using Aurora.Content.Contracts;
using Builder.Data;
using Builder.Presentation.Services.Data;
using System.Security.Cryptography;
using System.Xml.Linq;

// Tiny disposable fixtures exercise the same sync, reporting and loader services as Settings.
// The runner invokes each phase in a new process; no live corpus or settings are initialized.
internal static class ContentPolicyRehearsal
{
    private const string Conflict = "ID_POLICY_REHEARSAL_CONFLICT";
    private const string Feature = "ID_POLICY_REHEARSAL_FEATURE";
    private const string Proficiency = "ID_POLICY_REHEARSAL_PROFICIENCY";
    private static string Element(string id, string name, string type = "Feat", string rules = "") =>
        $"<element id='{id}' name='{name}' type='{type}' source='Policy Rehearsal'>{rules}</element>";
    private static string First => "<elements>" + Element(Conflict, "Conflict original") +
        Element(Feature, "Good feature", rules: $"<rules><grant type='Proficiency' id='{Proficiency}'/></rules>") + "</elements>";
    private static string Second(bool repaired) => "<elements>" + Element(Conflict, repaired ? "Conflict original" : "Conflict second") +
        Element(Proficiency, "Good proficiency", "Proficiency") + "</elements>";
    private static string Correction => LocalCorrectionDocument.Create(First.Replace("Good feature", "Protected feature"),
        First, "core/first.xml", [new("feature-name", "replace", Feature, null, null)]);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    internal static async Task<object> Run(string root, string mode)
    {
        var checks = new List<string>();
        void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks.Add(message);
        }
        string primary = Path.Combine(root, "custom");
        string first = Path.Combine(primary, "core", "first.xml");
        string second = Path.Combine(primary, "core", "second.xml");
        string local = Path.Combine(primary, "user", "local", "correction.xml");
        string database = Path.Combine(primary, ContentDatabaseService.DatabaseFileName);
        string marker = Path.Combine(root, ".policy-fixture");
        bool skip = bool.Parse(File.ReadAllText(Path.Combine(root, "policy-skip.txt")));
        Builder.Presentation.ApplicationContext.Current.Settings.SkipUnusableContentOnRefresh = skip;
        var service = new ContentDatabaseService();
        var elements = DataManager.Current.ElementsCollection;
        AuroraImportResult? import = null;
        DbLoadResult? load = null;
        bool? databaseUnchanged = null;
        object? coldFailure = null;

        async Task Load(bool conflictAvailable, bool protectedFeature = false)
        {
            load = await DbElementLoader.TryLoadAsync(elements);
            Require(load.Success, "Actual app loader succeeds: " + load.Summary);
            Require(elements.Count(e => e.Id == Conflict) == (conflictAvailable ? 1 : 0), "Conflict availability matches the persisted decision");
            var feature = elements.Single(e => e.Id == Feature);
            Require(feature.Name == (protectedFeature ? "Protected feature" : "Good feature"), "Effective feature name is preserved");
            Require(elements.Single(e => e.Id == Proficiency).Name == "Good proficiency", "Unaffected proficiency is usable");
            Require(XElement.Parse(feature.ElementNode.OuterXml).Element("rules")!.Elements("grant")
                .Any(g => (string?)g.Attribute("id") == Proficiency), "Unaffected feature retains its grant to the loaded proficiency");
        }

        async Task Sync(bool success)
        {
            import = await service.SyncAsync();
            Require(import.Success == success && service.LastResult == import &&
                service.SyncState == (success ? ContentDatabaseSyncState.Done : ContentDatabaseSyncState.Failed),
                "App sync result and observable state agree: " + import.Summary);
        }

        if (mode == "policy-first-import")
        {
            Require(!File.Exists(marker) && !File.Exists(database) &&
                (!Directory.Exists(primary) || !Directory.EnumerateFiles(primary, "*", SearchOption.AllDirectories).Any()),
                "First-import fixture starts empty");
            Directory.CreateDirectory(Path.GetDirectoryName(first)!);
            File.WriteAllText(first, First);
            File.WriteAllText(second, Second(false));
            File.WriteAllText(marker, "Disposable policy fixture v1");
        }
        else Require(File.Exists(marker), "Only a previously initialized disposable policy fixture is modified");

        switch (mode)
        {
            case "policy-first-import":
                await Sync(true);
                Require(import!.UnavailableDefinitions == (skip ? 0 : 1) && import.DefinitionCollisions == (skip ? 1 : 0) &&
                    import.FilesSkipped == 0 && import.AppendOperationsSkipped == 0,
                    "Settings summary distinguishes provisional collisions from unavailable IDs and skipped files");
                await Load(skip);
                break;
            case "policy-reopen-unavailable":
                Require(service.LastResult == null && elements.Count == 0, "Fresh process has no prior result or loaded catalog");
                await Load(skip);
                break;
            case "policy-repair-conflict":
                File.WriteAllText(second, Second(true));
                await Sync(true);
                Require(import!.UnavailableDefinitions == 0, "Repaired conflict clears the sync summary");
                await Load(true);
                break;
            case "policy-reopen-repaired":
                Require(service.LastResult == null && elements.Count == 0, "Fresh process has no prior result or loaded catalog");
                await Load(true, File.Exists(local));
                break;
            case "policy-refresh-conflict":
                await Load(true);
                var previous = elements.ToArray();
                File.WriteAllText(second, Second(false).Replace("</elements>", Element("ID_POLICY_REHEARSAL_NEW", "New unaffected") + "</elements>"));
                string beforeConflict = Hash(database);
                await Sync(skip);
                databaseUnchanged = Hash(database) == beforeConflict;
                if (!skip)
                {
                    Require(import!.ErrorMessage?.Contains("duplicate-element-id") == true, "Failure identifies the conflicting definition");
                    Require(databaseUnchanged.Value && previous.SequenceEqual(elements), "Strict rejection preserves database bytes and the live catalog");
                }
                else Require(import!.DefinitionCollisions > 0 && !databaseUnchanged.Value,
                    "Skip mode activates unaffected updates and reports retained definitions");
                await Load(true);
                Require(elements.Single(e => e.Id == Conflict).Name == "Conflict original", "The previous definition remains authoritative");
                Require(elements.Any(e => e.Id == "ID_POLICY_REHEARSAL_NEW") == skip, "Only successful best-effort refresh publishes the unrelated update");
                break;
            case "policy-protect-correction":
                Directory.CreateDirectory(Path.GetDirectoryName(local)!);
                File.WriteAllText(local, Correction);
                await Sync(true);
                await Load(true, true);
                Require(service.GetLocalCorrections().Single().Status == "review-required", "Protected correction is mirrored for review");
                break;
            case "policy-reject-correction":
                await Load(true, true);
                var prior = elements.ToArray();
                File.WriteAllText(local, Correction.Replace("version=\"1\"", "version=\"99\""));
                string beforeCorrection = Hash(database);
                string badInput = Hash(local);
                await Sync(false);
                Require(import!.ErrorMessage?.Contains("Correction preparation failed") == true, "Failure identifies invalid protected metadata");
                databaseUnchanged = Hash(database) == beforeCorrection;
                Require(databaseUnchanged.Value && Hash(local) == badInput && prior.SequenceEqual(elements),
                    "Rejected correction preserves database bytes, local XML and live catalog");
                load = await DbElementLoader.TryLoadAsync(elements);
                Require(!load.Success && prior.SequenceEqual(elements), "Failed runtime re-evaluation also retains the live catalog");
                Require(elements.Single(e => e.Id == Feature).Name == "Protected feature", "Protected feature remains usable in the running session");
                break;
            case "policy-probe-invalid-startup":
                Require(service.LastResult == null && elements.Count == 0, "Fresh process starts without a prior catalog");
                string beforeProbe = Hash(database);
                load = await DbElementLoader.TryLoadAsync(elements);
                bool fallbackBlocked = false;
                try { ContentDatabaseService.ValidateRawXmlFallback(database, load.FailureReason); }
                catch (InvalidDataException) { fallbackBlocked = true; }
                coldFailure = new { load.Success, load.FailureReason, rawFallbackBlocked = fallbackBlocked,
                    retainedDatabaseHasCorrection = service.GetLocalCorrections().Count == 1 };
                // This is an observation, not an assertion that failed startup is desired policy.
                Require(Hash(database) == beforeProbe, "Cold-start probe does not modify the preserved database");
                break;
            case "policy-repair-correction":
                File.WriteAllText(local, Correction);
                await Sync(true);
                await Load(true, true);
                Require(File.Exists(local) && service.GetLocalCorrections().Single().Status == "review-required",
                    "Repaired protected correction remains local and review-pending");
                break;
            default: throw new ArgumentException("Unknown policy rehearsal phase: " + mode);
        }

        var issues = service.GetSkippedContent();
        Require(service.LastReadFailure == null, "Persisted issue reports read successfully");
        if (mode is "policy-first-import" or "policy-reopen-unavailable")
        {
            Require(issues.Count == 1 && issues[0].Kind == (skip ? "definition-collision" : "definition-conflict") &&
                new[] { Conflict, "first.xml", "second.xml" }.All(s => issues[0].Detail.Contains(s)),
                "Persisted Settings report names the affected ID and both supplier files");
        }
        else if (mode == "policy-refresh-conflict" && skip)
            Require(issues.All(i => i.Kind == "definition-collision"), "Retained decisions survive the actual app loader");
        else Require(issues.Count == 0, "Resolved conflict report remains cleared in the database");
        return new { processId = Environment.ProcessId, skipUnusable = skip, checks, passed = checks.Count,
            import, load, service.SyncState, service.Progress, issues, databaseUnchanged, coldFailure,
            unavailable = ContentDatabaseReader.ReadUnavailableIds(database),
            relevantElements = elements.Where(e => e.Id.StartsWith("ID_POLICY_REHEARSAL_", StringComparison.Ordinal))
                .Select(e => new { e.Id, e.Name }).ToArray() };
    }
}
