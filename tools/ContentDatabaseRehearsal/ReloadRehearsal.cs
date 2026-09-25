using Aurora.App.Services;
using Aurora.Content.Preparation;
using Builder.Data;
using Aurora.Content.Contracts;
using Builder.Presentation;
using Builder.Presentation.Models.Sources;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Sources;
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

        // The catalog always loads in full now. What a character may use is decided by its source
        // restrictions, so switched-off content stays stored, loaded and ready to come back.
        Require(DataManager.Current.ElementsCollection.Any(e => e.Id == "ID_REHEARSAL_BASE")
            && DataManager.Current.ElementsCollection.Any(e => e.Id == "ID_REHEARSAL_SECONDARY"),
            "every source loads into the catalog", checks);
        var sources = new SourcesManager();
        SourceItem? fixtureSource = sources.SourceGroups.SelectMany(group => group.Sources)
            .FirstOrDefault(item => string.Equals(item.Source.Name, "Fixture", StringComparison.OrdinalIgnoreCase));
        Require(fixtureSource != null, "fixture source is offered for restriction", checks);
        fixtureSource!.SetIsChecked(false, updateChildren: true, updateParent: true);
        sources.ApplyRestrictions();
        Require(sources.GetRestrictedSources().Contains("Fixture"), "restricting a source records it", checks);
        Load();
        Require(DataManager.Current.ElementsCollection.Any(e => e.Id == "ID_REHEARSAL_BASE"),
            "a restricted source stays loaded and stored", checks);
        using (var connection = ContentDatabase.OpenReadableConnection(database))
        {
            using var query = connection.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM elements WHERE aurora_id='ID_REHEARSAL_BASE'";
            Require(Convert.ToInt32(query.ExecuteScalar()) == 1, "restricted definition remains stored", checks);
        }
        fixtureSource.SetIsChecked(true, updateChildren: true, updateParent: true);
        sources.ApplyRestrictions();
        Require(!sources.GetRestrictedSources().Any(), "clearing a restriction returns the source", checks);

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
        // Malformed XML under user/local refuses in BOTH skip modes: a truncated document can hide
        // a correction section, so the intent behind it cannot be read from what is left, and
        // skipping it would silently drop a protected correction.
        foreach (bool skipping in new[] { false, true })
        {
            ApplicationContext.Current.Settings.SkipUnusableContentOnRefresh = skipping;
            var rejected = await service.SyncAsync();
            Require(!rejected.Success && service.SyncState == ContentDatabaseSyncState.Failed && Hash(database) == beforeRejected,
                $"a malformed local correction refuses the refresh (skipping={skipping}) and preserves the database", checks);
        }
        File.Move(local, local + ".invalid-fixture");

        // An ordinary file that cannot be read, outside user/local, is the skippable case: it
        // carries no correction intent, so leaving it out costs the user only that file.
        string unreadable = Path.Combine(primary, "core", "zz-unreadable.xml");
        File.WriteAllText(unreadable,
            "<elements xmlns=\"urn:example:not-aurora\"><element name=\"Zz\" type=\"Proficiency\" source=\"Rehearsal\" id=\"ID_REHEARSAL_UNREADABLE\" /></elements>");
        ApplicationContext.Current.Settings.SkipUnusableContentOnRefresh = true;
        var skippedRefresh = await service.SyncAsync();
        var reportedSkips = service.GetSkippedContent();
        Require(skippedRefresh.Success && skippedRefresh.FilesSkipped == 1 && reportedSkips.Count == 1
            && reportedSkips[0].Path.Equals(unreadable, StringComparison.OrdinalIgnoreCase),
            "a refresh allowed to skip reports the file it left out", checks);
        // Into a collection of its own: the live one is what the checks below are about.
        Require((await DbElementLoader.TryLoadAsync(new ElementBaseCollection())).Success,
            "content still loads after a file was skipped", checks);

        File.Delete(unreadable);
        var repaired = await service.SyncAsync();
        Require(repaired.Success && repaired.FilesSkipped == 0 && service.GetSkippedContent().Count == 0,
            "removing the bad file clears the report", checks);

        // Two refreshes have legitimately rewritten the database since beforeRejected.
        string beforeCancel = Hash(database);
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
        Require(canceled && service.SyncState == ContentDatabaseSyncState.Idle && Hash(database) == beforeCancel,
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
