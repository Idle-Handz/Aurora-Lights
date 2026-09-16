using Aurora.App.Services;
using Aurora.Importer;
using Builder.Data;
using Builder.Data.Files;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

internal static class ReloadRehearsal
{
    public static async Task<object> Run(string root, bool repeatLoads = true)
    {
        var checks = new List<string>();
        var retained = new List<double>();
        for (int i = 0; repeatLoads && i < 4; i++)
        {
            Load();
            retained.Add(GC.GetTotalMemory(true) / 1048576.0);
        }
        if (repeatLoads)
        {
            File.WriteAllText(Path.Combine(root, "reload-memory.json"), System.Text.Json.JsonSerializer.Serialize(retained));
            Require(retained.Skip(1).Max() - retained.Skip(1).Min() < 30, "full-corpus reload memory stabilizes", checks);
        }

        // Exercise actual app services on a tiny independently written catalog.
        string fixtureRoot = Path.Combine(root, "failure-fixture-" + Guid.NewGuid().ToString("N"));
        string primary = Path.Combine(fixtureRoot, "custom");
        string secondary = Path.Combine(fixtureRoot, "secondary");
        Directory.CreateDirectory(Path.Combine(primary, "core"));
        Directory.CreateDirectory(secondary);
        SetPrimary(primary);
        ApplicationContext.Current.Settings.AdditionalCustomDirectories.Clear();
        ApplicationContext.Current.Settings.AdditionalCustomDirectories.Add(secondary);
        string origin = Path.Combine(primary, "core", "base.xml");
        string baseline = "<elements><element id='ID_REHEARSAL_BASE' name='Base' type='Feat' source='Fixture'><description>base</description></element></elements>";
        File.WriteAllText(origin, baseline);
        string extra = Path.Combine(secondary, "extra.xml");
        File.WriteAllText(extra, "<elements><element id='ID_REHEARSAL_SECONDARY' name='Secondary' type='Feat' source='Fixture'/><append id='ID_REHEARSAL_BASE'><supports>First</supports></append></elements>");
        var service = new ContentDatabaseService();
        var sync = await service.SyncAsync();
        Require(sync.Success, "actual writer creates fixture", checks);
        Load();
        string database = Path.Combine(primary, ContentDatabaseService.DatabaseFileName);
        string originalHash = Hash(database);
        File.WriteAllText(extra, File.ReadAllText(extra).Replace("First", "Second"));
        Load();
        Require(DataManager.Current.ElementsCollection.First(e => e.Id == "ID_REHEARSAL_BASE").Supports.Contains("Second"), "secondary XML changes reload without import", checks);
        Require(Hash(database) == originalHash, "secondary reload leaves database unchanged", checks);

        long packageId;
        using (var connection = AuroraContentImporter.OpenReadableConnection(database))
        {
            using var query = connection.CreateCommand();
            query.CommandText = "SELECT content_package_id FROM source_files WHERE replace(relative_path,char(92),'/')='core/base.xml'";
            packageId = Convert.ToInt64(query.ExecuteScalar() ?? throw new InvalidOperationException("Fixture supplier missing"));
        }
        Require(await service.SetPackageEnabledAsync(packageId, false) == null, "disable supplier preference", checks);
        Load();
        Require(!DataManager.Current.ElementsCollection.Any(e => e.Id == "ID_REHEARSAL_BASE") && DataManager.Current.ElementsCollection.Any(e => e.Id == "ID_REHEARSAL_SECONDARY"), "disabled primary stays excluded; secondary remains", checks);
        using (var connection = AuroraContentImporter.OpenReadableConnection(database))
        {
            using var query = connection.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM elements WHERE aurora_id='ID_REHEARSAL_BASE'";
            Require(Convert.ToInt32(query.ExecuteScalar()) == 1, "disabled definition remains stored", checks);
        }
        Require(await service.SetPackageEnabledAsync(packageId, true) == null, "reenable supplier preference", checks);
        Load();
        Require(DataManager.Current.ElementsCollection.Any(e => e.Id == "ID_REHEARSAL_BASE"), "reenabled definition returns", checks);

        var priorElements = DataManager.Current.ElementsCollection.ToArray();
        var priorSort = DbElementLoader.ElementSortMetadataMap;
        var priorSpells = DbElementLoader.SpellAccessMap;
        var priorFallback = typeof(XmlContentFallbackService).GetField("_snapshot", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        string local = Path.Combine(primary, "user", "local", "broken.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        File.WriteAllText(local, LocalCorrectionDocument.Create(baseline.Replace(">base<", ">fixed<"), baseline,
            "core/base.xml", [new LocalCorrection("test", "replace", "ID_REHEARSAL_BASE", null, null)]).Replace("</elements>", ""));
        var failedLoad = await DbElementLoader.TryLoadAsync(DataManager.Current.ElementsCollection);
        Require(!failedLoad.Success && priorElements.SequenceEqual(DataManager.Current.ElementsCollection), "invalid correction preserves live elements", checks);
        Require(ReferenceEquals(priorSort, DbElementLoader.ElementSortMetadataMap) && ReferenceEquals(priorSpells, DbElementLoader.SpellAccessMap)
            && ReferenceEquals(priorFallback, typeof(XmlContentFallbackService).GetField("_snapshot", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)), "invalid correction preserves lookup and fallback state", checks);
        string beforeRejected = Hash(database);
        var rejected = await service.SyncAsync();
        Require(!rejected.Success && service.SyncState == ContentDatabaseSyncState.Failed && Hash(database) == beforeRejected,
            "rejected refresh preserves installed candidate and reports failure", checks);
        File.Move(local, local + ".invalid-fixture");

        using var cancel = new CancellationTokenSource();
        int importStages = 0;
        void OnState()
        {
            if (service.SyncState == ContentDatabaseSyncState.Syncing && service.Progress != null && ++importStages == 2)
                cancel.Cancel(); // Main import after successful capability probe.
        }
        service.StateChanged += OnState;
        bool canceled = false;
        try { await service.SyncAsync(cancel.Token); }
        catch (OperationCanceledException) { canceled = true; }
        finally { service.StateChanged -= OnState; }
        Require(canceled && service.SyncState == ContentDatabaseSyncState.Idle && Hash(database) == beforeRejected,
            "cancellation preserves database and clears syncing state", checks);
        Require(priorElements.SequenceEqual(DataManager.Current.ElementsCollection), "refresh failure/cancellation never replace live collection", checks);
        return new { passed = checks.Count, checks, retainedMiB = retained };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Load()
    {
        var result = DbElementLoader.TryLoadAsync(DataManager.Current.ElementsCollection).GetAwaiter().GetResult();
        if (!result.Success) throw new InvalidOperationException(result.Summary);
    }
    private static void SetPrimary(string primary)
    {
        ApplicationContext.Current.Settings.DocumentsRootDirectory = Path.GetDirectoryName(primary)!;
        typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsRootDirectory))!.SetValue(DataManager.Current, Path.GetDirectoryName(primary));
        typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!.SetValue(DataManager.Current, primary);
    }
    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static void Require(bool condition, string name, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException("Reload rehearsal: " + name);
        checks.Add(name);
    }
}
