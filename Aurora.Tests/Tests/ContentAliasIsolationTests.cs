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
