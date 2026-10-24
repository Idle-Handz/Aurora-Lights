using Aurora.Reflections.Services;
using Aurora.Reflections.Services.Updates;
using Builder.Presentation.Models;
using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class AppUpdateRestartTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "update-restart-" + Guid.NewGuid().ToString("N"));
    private readonly CharacterTabService _tabs = new();
    private readonly CharacterCloseCoordinator _characters;

    public AppUpdateRestartTests() => _characters = new CharacterCloseCoordinator(_tabs);

    private CharacterTab OpenTab(string state = "clean")
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".dnd5e");
        File.WriteAllText(path, """
            <character><build><input>
              <currency><copper>0</copper><silver>0</silver><electrum>0</electrum><gold>0</gold><platinum>0</platinum></currency>
              <notes><note column="left">Original notes</note><note column="right"></note></notes>
            </input></build></character>
            """);
        var file = new CharacterFile(path);
        file.RefreshKnownDiskStamp();
        var tab = _tabs.OpenLoadingTab(file);
        tab.IsLoading = state == "loading";
        tab.Snapshot = new CharacterSnapshot { Notes1 = "Original notes", Notes2 = "" };
        if (state == "draft")
        {
            tab.Snapshot.Notes1 = "Unsaved narrative";
            _tabs.MarkDirty(tab);
        }
        if (state == "saving") tab.IsSaving = true;
        return tab;
    }

    private static Task Dispatch(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("draft")]
    [InlineData("saving")]
    [InlineData("loading")]
    public async Task OpenCharacterBlocksDownloadAndRestartWithoutChangingItsState(string state)
    {
        var tab = OpenTab(state);
        byte[] saved = File.ReadAllBytes(tab.File.FilePath);
        bool dirty = tab.IsDirty;
        string notes = tab.Snapshot!.Notes1;
        long editVersion = tab.EditVersion;
        bool downloaded = false, restarted = false;

        string? error = await new AppUpdateRestartCoordinator(_tabs, _characters).RunAsync(
            () => { downloaded = true; return Task.FromResult<string?>(null); },
            () => restarted = true, Dispatch, () => true);

        error.Should().Be(AppUpdateRestartCoordinator.CloseCharactersMessage);
        downloaded.Should().BeFalse();
        restarted.Should().BeFalse();
        _tabs.Tabs.Should().ContainSingle().Which.Should().BeSameAs(tab);
        tab.IsDirty.Should().Be(dirty);
        tab.EditVersion.Should().Be(editVersion);
        tab.Snapshot.Notes1.Should().Be(notes);
        File.ReadAllBytes(tab.File.FilePath).Should().Equal(saved);
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("draft")]
    [InlineData("saving")]
    [InlineData("loading")]
    public async Task CharacterOpenedDuringDownloadBlocksRestartAndPreservesLaterWork(string state)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool restarted = false;
        Task<string?> update = new AppUpdateRestartCoordinator(_tabs, _characters).RunAsync(
            async () => { entered.SetResult(); await release.Task; return null; },
            () => restarted = true, Dispatch, () => true);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var tab = OpenTab(state);
        string notes = tab.Snapshot!.Notes1;
        release.SetResult();

        (await update.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(AppUpdateRestartCoordinator.CloseCharactersMessage);
        restarted.Should().BeFalse();
        tab.Snapshot.Notes1.Should().Be(notes);
        if (state == "draft") tab.IsDirty.Should().BeTrue();
        if (state == "saving") tab.IsSaving.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSessionWriteCannotBeInterruptedByRestart(bool startDuringDownload)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool restarted = false;
        var coordinator = new AppUpdateRestartCoordinator(_tabs, _characters);
        Task<string?>? update = null;
        if (startDuringDownload)
        {
            update = coordinator.RunAsync(
                async () => { entered.SetResult(); await release.Task; return null; },
                () => restarted = true, Dispatch, () => true);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        var tab = OpenTab();
        tab.Snapshot!.Notes1 = "Session notes waiting for disk";
        tab.Session.TempHp = 3;
        await tab.FileSaveSemaphore.WaitAsync();
        Task<CharacterFileWriteResult> save;
        try
        {
            save = CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot);
            save.IsCompleted.Should().BeFalse();
            tab.IsSaving.Should().BeFalse("session writes have independent pending tracking");
            tab.IsDirty.Should().BeTrue();

            if (startDuringDownload) release.SetResult();
            else update = coordinator.RunAsync(
                () => Task.FromResult<string?>(null), () => restarted = true, Dispatch, () => true);

            (await update!.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(AppUpdateRestartCoordinator.CloseCharactersMessage);
            restarted.Should().BeFalse();
            save.IsCompleted.Should().BeFalse();
            tab.Snapshot.Notes1.Should().Be("Session notes waiting for disk");
            tab.IsDirty.Should().BeTrue();
        }
        finally { tab.FileSaveSemaphore.Release(); }

        (await save).Succeeded.Should().BeTrue();
        SessionStore.Load(tab.File.FilePath).TempHp.Should().Be(3);
        File.ReadAllText(tab.File.FilePath).Should().Contain("Session notes waiting for disk");
    }

    [Fact]
    public async Task FinalCheckRunsOnDispatcherAfterAnyAlreadyQueuedEditorEvent()
    {
        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int dispatchCount = 0;
        bool restarted = false;
        async Task QueuedDispatch(Action action)
        {
            if (++dispatchCount == 2)
            {
                queued.SetResult();
                await release.Task;
            }
            action();
        }

        Task<string?> update = new AppUpdateRestartCoordinator(_tabs, _characters).RunAsync(
            () => Task.FromResult<string?>(null), () => restarted = true, QueuedDispatch, () => true);
        await queued.Task.WaitAsync(TimeSpan.FromSeconds(5));
        OpenTab("draft");
        release.SetResult();

        (await update.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(AppUpdateRestartCoordinator.CloseCharactersMessage);
        restarted.Should().BeFalse();
    }

    [Fact]
    public async Task LeavingSettingsDuringDownloadCancelsRestartEvenWithoutOpenCharacters()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool currentView = true, restarted = false;
        Task<string?> update = new AppUpdateRestartCoordinator(_tabs, _characters).RunAsync(
            async () => { entered.SetResult(); await release.Task; return null; },
            () => restarted = true, Dispatch, () => currentView);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        currentView = false;
        release.SetResult();

        (await update.WaitAsync(TimeSpan.FromSeconds(5))).Should().Contain("left Settings");
        restarted.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadFailureIsReturnedWithoutAttemptingRestart()
    {
        bool restarted = false;
        string? error = await new AppUpdateRestartCoordinator(_tabs, _characters).RunAsync(
            () => Task.FromResult<string?>("Download failed"),
            () => restarted = true, Dispatch, () => true);

        error.Should().Be("Download failed");
        restarted.Should().BeFalse();
    }

    [Fact]
    public async Task RetryAfterClosingCharactersRestartsInsideFinalSynchronousDispatch()
    {
        var tab = OpenTab();
        var coordinator = new AppUpdateRestartCoordinator(_tabs, _characters);
        bool insideDispatch = false, downloaded = false, restarted = false;
        int dispatchCount = 0;
        Task TrackedDispatch(Action action)
        {
            dispatchCount++;
            insideDispatch = true;
            try { action(); }
            finally { insideDispatch = false; }
            return Task.CompletedTask;
        }
        Task<string?> Download()
        {
            downloaded = true;
            return Task.FromResult<string?>(null);
        }
        void Restart()
        {
            insideDispatch.Should().BeTrue("no editor event may run between the final check and termination");
            downloaded.Should().BeTrue();
            restarted = true;
        }

        (await coordinator.RunAsync(Download, Restart, TrackedDispatch, () => true))
            .Should().Be(AppUpdateRestartCoordinator.CloseCharactersMessage);
        downloaded.Should().BeFalse();
        _tabs.CloseTab(tab);
        dispatchCount = 0;

        (await coordinator.RunAsync(Download, Restart, TrackedDispatch, () => true)).Should().BeNull();
        restarted.Should().BeTrue();
        dispatchCount.Should().Be(2);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
