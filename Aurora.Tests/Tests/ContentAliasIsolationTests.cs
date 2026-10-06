using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class ContentAliasIsolationTests
{
    [Theory]
    [InlineData("DROP TABLE content_element_aliases")]
    [InlineData("ALTER TABLE content_element_aliases RENAME COLUMN target_aurora_id TO broken_target")]
    public async Task UnreadableAliasContractRejectsCandidateAndPreservesLiveAliases(string damage)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var manager = DataManager.Current;
        var primaryProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        string? previousRoot = manager.UserDocumentsCustomElementsDirectory;
        var additional = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        string[] previousAdditional = additional.ToArray();
        try
        {
            File.WriteAllText(Path.Combine(root, "content.xml"), """
                <elements>
                  <element id="ID_TEST_CURRENT" name="Current" type="Proficiency" source="Test" />
                  <alias id="ID_TEST_OLD" target="ID_TEST_CURRENT" />
                </elements>
                """);
            string database = Path.Combine(root, ContentDatabaseService.DatabaseFileName);
            await ContentImport.ImportAsync(root, database);
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                   { DataSource = database, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = damage;
                command.ExecuteNonQuery();
            }
            primaryProperty.SetValue(manager, root);
            additional.Clear();
            ElementIdAliases.Set(new Dictionary<string, string> { ["ID_TEST_OLD"] = "ID_TEST_LIVE" });
            var live = new ElementBaseCollection { new ElementBase { ElementHeader = new("Live", "Proficiency", "Test", "ID_TEST_LIVE") } };
            var before = live.ToArray();

            var result = await DbElementLoader.TryLoadSnapshotAsync(live);

            result.Success.Should().BeFalse("missing forwarding addresses must not look like a complete successful load");
            live.Should().Equal(before);
            ElementIdAliases.TryGetTarget("ID_TEST_OLD", out string target).Should().BeTrue();
            target.Should().Be("ID_TEST_LIVE");
            Action fallback = () => ContentDatabaseService.ValidateRawXmlFallback(database, result.FailureReason);
            fallback.Should().Throw<InvalidDataException>().WithMessage("*aliases could not be read*forwarding*");
        }
        finally
        {
            ElementIdAliases.Clear();
            primaryProperty.SetValue(manager, previousRoot);
            additional.Clear();
            foreach (string directory in previousAdditional) additional.Add(directory);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RawFallbackCannotDiscardDeclaredSavedIdForwarding()
    {
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "content.xml"), """
                <elements>
                  <element id="ID_TEST_CURRENT" name="Current" type="Proficiency" source="Test" />
                  <alias id="ID_TEST_OLD" target="ID_TEST_CURRENT" />
                </elements>
                """);
            string database = Path.Combine(root, ContentDatabaseService.DatabaseFileName);
            await ContentImport.ImportAsync(root, database);

            Action fallback = () => ContentDatabaseService.ValidateRawXmlFallback(database);

            fallback.Should().Throw<InvalidDataException>().WithMessage("*aliases that raw XML fallback cannot preserve*");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnpublishedLoadPreservesAliasesForTheLiveCatalog(bool failFullLoad)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var manager = DataManager.Current;
        var primaryProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        string? previousRoot = manager.UserDocumentsCustomElementsDirectory;
        var additional = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        string[] previousAdditional = additional.ToArray();
        const string oldId = "ID_TEST_ALIAS_OLD";
        const string currentId = "ID_TEST_ALIAS_CURRENT";
        bool rejectedLoad = false;
        void RejectCompletedCandidate()
        {
            if (DebugLogService.Instance.Entries.Last().Message != "DbElementLoader: load complete.") return;
            rejectedLoad = true;
            throw new InvalidOperationException("Reject candidate after post-processing to exercise load rollback.");
        }
        try
        {
            File.WriteAllText(Path.Combine(root, "content.xml"), """
                <elements>
                  <element id="ID_TEST_ALIAS_SNAPSHOT" name="Snapshot definition" type="Proficiency" source="Test" />
                  <alias id="ID_TEST_ALIAS_OLD" target="ID_TEST_ALIAS_SNAPSHOT" />
                </elements>
                """);
            await ContentImport.ImportAsync(root, Path.Combine(root, ContentDatabaseService.DatabaseFileName));
            primaryProperty.SetValue(manager, root);
            additional.Clear();
            ElementIdAliases.Set(new Dictionary<string, string> { [oldId] = currentId });
            if (failFullLoad) DebugLogService.Instance.Changed += RejectCompletedCandidate;

            var snapshot = new ElementBaseCollection();
            var loaded = failFullLoad
                ? await DbElementLoader.TryLoadAsync(snapshot)
                : await DbElementLoader.TryLoadSnapshotAsync(snapshot);

            if (failFullLoad)
            {
                rejectedLoad.Should().BeTrue("the failure must happen after aliases have been read and generated");
                loaded.Success.Should().BeFalse();
                snapshot.Should().BeEmpty();
            }
            else
            {
                loaded.Success.Should().BeTrue(loaded.FailureReason);
                snapshot.GetElement("ID_TEST_ALIAS_SNAPSHOT").Should().NotBeNull();
            }
            ElementIdAliases.TryGetTarget(oldId, out string target).Should().BeTrue();
            target.Should().Be(currentId, "an unpublished load must not replace the live catalog's forwarding addresses");
        }
        finally
        {
            DebugLogService.Instance.Changed -= RejectCompletedCandidate;
            ElementIdAliases.Clear();
            primaryProperty.SetValue(manager, previousRoot);
            additional.Clear();
            foreach (string directory in previousAdditional) additional.Add(directory);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingAndGeneratedAliasesStayIsolatedUntilPublication(bool publish)
    {
        ElementIdAliases.Set(new Dictionary<string, string> { ["OLD"] = "LIVE" });
        var staged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var load = Task.Run(async () =>
        {
            using var scope = ElementIdAliases.BeginScope();
            ElementIdAliases.Set(new Dictionary<string, string> { ["OLD"] = "PENDING" });
            // Post-processing runs in another worker task but must extend this staged map.
            await Task.Run(() => ElementIdAliases.ForwardGeneratedIds("PENDING", id => "PROXY_" + id));
            staged.SetResult();
            await finish.Task;
            if (publish) scope.Publish();
        });
        try
        {
            await staged.Task.WaitAsync(TimeSpan.FromSeconds(10));
            ElementIdAliases.TryGetTarget("OLD", out string liveTarget).Should().BeTrue();
            liveTarget.Should().Be("LIVE");
            ElementIdAliases.TryGetTarget("PROXY_OLD", out _).Should().BeFalse();
            finish.SetResult();
            await load;

            ElementIdAliases.TryGetTarget("OLD", out string finalTarget).Should().BeTrue();
            finalTarget.Should().Be(publish ? "PENDING" : "LIVE");
            ElementIdAliases.TryGetTarget("PROXY_OLD", out string proxyTarget).Should().Be(publish);
            if (publish) proxyTarget.Should().Be("PROXY_PENDING");
        }
        finally
        {
            finish.TrySetResult();
            await load;
            ElementIdAliases.Clear();
        }
    }

    [Fact]
    public async Task DeferredNotificationsDoNotRetainAnAbandonedAliasScope()
    {
        ElementIdAliases.Set(new Dictionary<string, string> { ["OLD"] = "LIVE" });
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string> notification;
        using (ElementIdAliases.BeginScope())
        {
            ElementIdAliases.Set(new Dictionary<string, string> { ["OLD"] = "ABANDONED" });
            notification = Task.Run(async () =>
            {
                await release.Task;
                ElementIdAliases.TryGetTarget("OLD", out string target);
                return target;
            });
        }
        try
        {
            release.SetResult();
            (await notification).Should().Be("LIVE");
        }
        finally { ElementIdAliases.Clear(); }
    }
}
