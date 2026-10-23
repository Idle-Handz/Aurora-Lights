using Aurora.App.Services;
using Aurora.App.Services.Updates;
using Builder.Presentation.Models;
using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class WindowCloseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "window-close-" + Guid.NewGuid().ToString("N"));
    private readonly CharacterTabService _tabs = new();
    private readonly CharacterCloseCoordinator _characters;
    private readonly List<string> _errors = [];
    private Func<CharacterTab, Task<bool?>> _confirm = _ => Task.FromResult<bool?>(null);
    private Func<CharacterTab, Task<string?>> _save = _ => Task.FromResult<string?>(null);
    private Func<CharacterTab, Task> _release = _ => Task.CompletedTask;

    public WindowCloseTests() => _characters = new CharacterCloseCoordinator(_tabs);

    private CharacterTab OpenTab(bool dirty = false)
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
        tab.IsLoading = false;
        tab.Snapshot = new CharacterSnapshot { Notes1 = "Original notes", Notes2 = "" };
        if (dirty)
        {
            tab.Snapshot.Notes1 = "Unsaved notes";
            _tabs.MarkDirty(tab);
        }
        return tab;
    }

    private Task<bool> CloseTab(CharacterTab tab) => _characters.TryCloseAsync(
        tab, character => _confirm(character), character => _save(character), _errors.Add,
        character => _release(character));

    private WindowCloseCoordinator Window() => new(_tabs, _characters)
    {
        Handler = new(work => work(), CloseTab, () => true)
    };

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeCloseUsesTheSharedSaveDiscardCancelDecision(bool? choice)
    {
        var tab = OpenTab(dirty: true);
        int prompts = 0, saves = 0, releases = 0, nativeCloses = 0;
        _confirm = _ => { prompts++; return Task.FromResult(choice); };
        _save = _ => { saves++; return Task.FromResult<string?>(null); };
        _release = _ => { releases++; return Task.CompletedTask; };

        bool accepted = await Window().RequestCloseAsync(() => nativeCloses++);

        accepted.Should().Be(choice is not null);
        prompts.Should().Be(1);
        saves.Should().Be(choice == true ? 1 : 0);
        releases.Should().Be(choice is null ? 0 : 1);
        nativeCloses.Should().Be(choice is null ? 0 : 1);
        if (choice is null)
        {
            _tabs.Tabs.Should().ContainSingle().Which.Should().BeSameAs(tab);
            tab.IsDirty.Should().BeTrue();
            tab.Snapshot!.Notes1.Should().Be("Unsaved notes");
        }
        else _tabs.Tabs.Should().BeEmpty();
        _errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task TwoInitiallyDirtyTabsKeepTheSecondDraftUntilItsOwnDecision(bool? secondChoice)
    {
        var first = OpenTab(dirty: true);
        var second = OpenTab(dirty: true);
        first.Snapshot!.Notes1 = "First draft";
        second.Snapshot!.Notes1 = "Second draft";
        _tabs.ActivateTab(first);
        var prompts = new List<CharacterTab>();
        var saved = new List<CharacterTab>();
        var enteredSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int nativeCloses = 0;
        _confirm = tab =>
        {
            prompts.Add(tab);
            return Task.FromResult(tab == first ? (bool?)true : secondChoice);
        };
        _save = async tab =>
        {
            saved.Add(tab);
            enteredSave.TrySetResult();
            await finishSave.Task;
            return null;
        };
        var window = Window();

        Task<bool> closing = window.RequestCloseAsync(() => nativeCloses++);
        await enteredSave.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _tabs.Tabs.Should().Equal(first, second);
        _tabs.ActiveTab.Should().BeSameAs(first);
        prompts.Should().Equal(first);
        nativeCloses.Should().Be(0);
        finishSave.SetResult();

        (await closing.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(secondChoice is not null);
        prompts.Should().Equal(first, second);
        saved.Should().Equal(first);
        if (secondChoice is null)
        {
            nativeCloses.Should().Be(0);
            _tabs.Tabs.Should().ContainSingle().Which.Should().BeSameAs(second);
            _tabs.ActiveTab.Should().BeSameAs(second);
            second.IsDirty.Should().BeTrue();
            second.Snapshot.Notes1.Should().Be("Second draft");
            _confirm = _ => Task.FromResult<bool?>(false);
            (await window.RequestCloseAsync(() => nativeCloses++)).Should().BeTrue();
        }
        _tabs.Tabs.Should().BeEmpty();
        nativeCloses.Should().Be(1);
        _errors.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedSaveKeepsTheWindowAndDraftOpen()
    {
        var tab = OpenTab(dirty: true);
        _confirm = _ => Task.FromResult<bool?>(true);
        _save = _ => Task.FromResult<string?>("Disk is unavailable");
        bool closed = false;

        (await Window().RequestCloseAsync(() => closed = true)).Should().BeFalse();

        closed.Should().BeFalse();
        _tabs.Tabs.Should().Contain(tab);
        tab.IsDirty.Should().BeTrue();
        tab.Snapshot!.Notes1.Should().Be("Unsaved notes");
        _errors.Should().ContainSingle().Which.Should().Contain("Disk is unavailable");
    }

    [Fact]
    public async Task LoadingPlaceholderCanStillBeClosedWithoutACharacterSave()
    {
        var tab = OpenTab();
        tab.IsLoading = true;
        int saves = 0, releases = 0;
        _save = _ => { saves++; return Task.FromResult<string?>(null); };
        _release = _ => { releases++; return Task.CompletedTask; };

        (await CloseTab(tab)).Should().BeTrue();

        _tabs.Tabs.Should().BeEmpty();
        saves.Should().Be(0);
        releases.Should().Be(1);
        _errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Upload failed")]
    public async Task ExistingFullSaveFinishesBeforeCloseAndItsFailureIsRetained(string? saveError)
    {
        var tab = OpenTab(dirty: true);
        var save = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        tab.InFlightSave = save.Task;
        tab.InFlightSaveEditVersion = tab.EditVersion;
        tab.IsSaving = true;
        int prompts = 0, additionalSaves = 0, closes = 0;
        _confirm = _ => { prompts++; return Task.FromResult<bool?>(null); };
        _save = _ => { additionalSaves++; return Task.FromResult<string?>(null); };

        Task<bool> closing = Window().RequestCloseAsync(() => closes++);
        closing.IsCompleted.Should().BeFalse();
        closes.Should().Be(0);
        tab.InFlightSave = null;
        tab.IsSaving = false;
        save.SetResult(saveError);

        (await closing.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(saveError is null);
        closes.Should().Be(saveError is null ? 1 : 0);
        prompts.Should().Be(0);
        additionalSaves.Should().Be(0, "an existing save must be joined rather than replaced");
        if (saveError is not null)
        {
            tab.IsDirty.Should().BeTrue();
            _tabs.Tabs.Should().Contain(tab);
            _errors.Should().ContainSingle().Which.Should().Contain(saveError);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditsArrivingDuringSaveNeedAnotherCloseDecision(bool sessionEdit)
    {
        var tab = OpenTab(dirty: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int prompts = 0, closes = 0;
        _confirm = _ => Task.FromResult<bool?>(++prompts == 1 ? true : null);
        _save = async _ => { entered.SetResult(); await releaseSave.Task; return null; };

        Task<bool> closing = Window().RequestCloseAsync(() => closes++);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        tab.Snapshot!.Notes1 = "Later typing";
        if (sessionEdit) tab.SessionWrites.BeginChange();
        else _tabs.MarkDirty(tab);
        releaseSave.SetResult();

        (await closing.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeFalse();
        prompts.Should().Be(2);
        closes.Should().Be(0);
        tab.IsDirty.Should().BeTrue();
        tab.Snapshot.Notes1.Should().Be("Later typing");
        _tabs.Tabs.Should().Contain(tab);
    }

    [Fact]
    public async Task QueuedSessionWriteFinishesBeforeAClosedWindowCanStartItsUpdater()
    {
        var tab = OpenTab();
        tab.Snapshot!.Notes1 = "Session note being written";
        tab.Session.TempHp = 4;
        await tab.FileSaveSemaphore.WaitAsync();
        Task<CharacterFileWriteResult> save;
        Task<bool> closing;
        int prompts = 0, closes = 0;
        _confirm = _ => { prompts++; return Task.FromResult<bool?>(null); };
        try
        {
            save = CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot);
            closing = Window().RequestCloseAsync(() => closes++);
            closing.IsCompleted.Should().BeFalse();
            closes.Should().Be(0);
        }
        finally { tab.FileSaveSemaphore.Release(); }

        (await save).Succeeded.Should().BeTrue();
        (await closing.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        prompts.Should().Be(0);
        closes.Should().Be(1);
        File.ReadAllText(tab.File.FilePath).Should().Contain("Session note being written");
        SessionStore.Load(tab.File.FilePath).TempHp.Should().Be(4);
    }

    [Fact]
    public async Task WindowCloseWaitsForEngineReleaseAndClosesWithinTheFinalUiCallback()
    {
        OpenTab();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEngine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = async _ => { entered.SetResult(); await releaseEngine.Task; };
        bool insideUiCallback = false, closed = false;
        var window = new WindowCloseCoordinator(_tabs, _characters)
        {
            Handler = new(async work =>
            {
                insideUiCallback = true;
                try { await work(); }
                finally { insideUiCallback = false; }
            }, CloseTab, () => true)
        };

        Task<bool> closing = window.RequestCloseAsync(() =>
        {
            insideUiCallback.Should().BeTrue();
            _tabs.Tabs.Should().BeEmpty();
            closed = true;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        closing.IsCompleted.Should().BeFalse();
        closed.Should().BeFalse();
        releaseEngine.SetResult();

        (await closing.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task TabCloseAndRepeatedNativeCloseShareOnePromptAndCanRetryAfterCancel()
    {
        var tab = OpenTab(dirty: true);
        var choice = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int prompts = 0, closes = 0;
        _confirm = _ => { prompts++; return choice.Task; };
        Task<bool> tabClosing = CloseTab(tab);
        var window = Window();
        Task<bool> first = window.RequestCloseAsync(() => closes++);
        Task<bool> repeated = window.RequestCloseAsync(() => closes++);

        repeated.Should().BeSameAs(first);
        CloseTab(tab).Should().BeSameAs(tabClosing);
        prompts.Should().Be(1);
        choice.SetResult(null);
        (await tabClosing.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeFalse();
        (await first.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeFalse();
        closes.Should().Be(0);
        _tabs.Tabs.Should().Contain(tab);

        _confirm = _ => Task.FromResult<bool?>(false);
        (await window.RequestCloseAsync(() => closes++)).Should().BeTrue();
        closes.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateNativeCloseAndImmediateUpdateCannotBypassAnAlreadyRemovedTabsEngineRelease(bool updateAlreadyDownloading)
    {
        bool downloaded = false, restarted = false;
        var downloadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDownload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updateCoordinator = new AppUpdateRestartCoordinator(_tabs, _characters);
        Task<string?> StartUpdate() => updateCoordinator.RunAsync(
            async () =>
            {
                downloaded = true;
                downloadEntered.SetResult();
                if (updateAlreadyDownloading) await releaseDownload.Task;
                return null;
            },
            () => restarted = true,
            action => { action(); return Task.CompletedTask; },
            () => true);
        Task<string?>? update = null;
        if (updateAlreadyDownloading)
        {
            update = StartUpdate();
            await downloadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        var tab = OpenTab();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEngine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = async _ => { entered.SetResult(); await releaseEngine.Task; };
        Task<bool> tabClosing = CloseTab(tab);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _tabs.Tabs.Should().BeEmpty("the tab is removed before engine release completes");
        _characters.HasPendingClosures.Should().BeTrue();
        int closes = 0;
        Task<bool> windowClosing = Window().RequestCloseAsync(() => closes++);
        windowClosing.IsCompleted.Should().BeFalse();
        (await new WindowCloseCoordinator(_tabs, _characters).RequestCloseAsync(() => closes++))
            .Should().BeFalse("an unavailable layout must not bypass a pending engine release");

        if (updateAlreadyDownloading) releaseDownload.SetResult();
        else update = StartUpdate();
        (await update!.WaitAsync(TimeSpan.FromSeconds(5)))
            .Should().Be(AppUpdateRestartCoordinator.CloseCharactersMessage);
        downloaded.Should().Be(updateAlreadyDownloading);
        restarted.Should().BeFalse();
        closes.Should().Be(0);

        releaseEngine.SetResult();
        (await tabClosing.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await windowClosing.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        closes.Should().Be(1);
        _characters.HasPendingClosures.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CharacterOpenedWhileCloseAwaitsIsIncludedBeforeFinalNativeClose(bool laterTabDirty)
    {
        var first = OpenTab(dirty: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _confirm = tab => Task.FromResult<bool?>(tab == first ? true : null);
        _save = async _ => { entered.SetResult(); await releaseSave.Task; return null; };
        int closes = 0;

        Task<bool> closing = Window().RequestCloseAsync(() => closes++);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var later = OpenTab(laterTabDirty);
        releaseSave.SetResult();

        (await closing.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(!laterTabDirty);
        closes.Should().Be(laterTabDirty ? 0 : 1);
        if (laterTabDirty) _tabs.Tabs.Should().ContainSingle().Which.Should().BeSameAs(later);
        else _tabs.Tabs.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingLayoutCannotSilentlyDiscardExistingCharacters()
    {
        var window = new WindowCloseCoordinator(_tabs, _characters);
        var tab = OpenTab(dirty: true);
        bool closed = false;
        (await window.RequestCloseAsync(() => closed = true)).Should().BeFalse();
        closed.Should().BeFalse();
        _tabs.Tabs.Should().Contain(tab);
        _tabs.CloseTab(tab);

        (await window.RequestCloseAsync(() => closed = true)).Should().BeTrue();
        closed.Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
