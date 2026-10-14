using Aurora.App.Services;
using Builder.Presentation.Models;
using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class SessionSaveCompletionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "session-save-" + Guid.NewGuid().ToString("N"));

    private CharacterTab CreateTab()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "session.dnd5e");
        File.WriteAllText(path, """
            <character><build><input>
              <currency><copper>0</copper><silver>0</silver><electrum>0</electrum><gold>0</gold><platinum>0</platinum></currency>
              <notes><note column="left">Original notes</note><note column="right"></note></notes>
            </input></build></character>
            """);
        var file = new CharacterFile(path);
        file.RefreshKnownDiskStamp();
        return new CharacterTab(file) { Snapshot = new CharacterSnapshot { Notes1 = "Original notes", Notes2 = "" } };
    }

    [Fact]
    public async Task QueuedSaveStaysPendingUntilEveryWriteCompletes()
    {
        var tab = CreateTab();
        tab.Snapshot!.Notes1 = "Pending notes";
        await tab.FileSaveSemaphore.WaitAsync();
        Task<CharacterFileWriteResult> save;
        try
        {
            save = CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot);
            save.IsCompleted.Should().BeFalse();
            tab.IsDirty.Should().BeTrue("closing must offer to save while the write is queued");
        }
        finally { tab.FileSaveSemaphore.Release(); }
        (await save).Succeeded.Should().BeTrue();
        tab.IsDirty.Should().BeFalse();
        File.ReadAllText(tab.File.FilePath).Should().Contain("Pending notes");
    }

    [Fact]
    public async Task SidecarFailureRetainsPendingStateAndRetryWritesBothFiles()
    {
        var tab = CreateTab();
        tab.Snapshot!.Notes1 = "Retry notes";
        tab.Session.CurrentHp = 9;
        string sidecar = SessionStore.GetSidecarPath(tab.File.FilePath);
        Directory.CreateDirectory(sidecar);

        (await CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot)).Succeeded.Should().BeFalse();
        tab.IsDirty.Should().BeTrue();
        File.ReadAllText(tab.File.FilePath).Should().NotContain("Retry notes");
        Directory.Delete(sidecar);

        (await CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot)).Succeeded.Should().BeTrue();
        tab.IsDirty.Should().BeFalse();
        File.ReadAllText(tab.File.FilePath).Should().Contain("Retry notes");
        SessionStore.Load(tab.File.FilePath).CurrentHp.Should().Be(9);
    }

    [Fact]
    public async Task ExternalXmlChangeAfterSidecarSaveIsReportedWithoutOverwritingIt()
    {
        var tab = CreateTab();
        tab.Snapshot!.Notes1 = "Local pending notes";
        tab.Session.CurrentHp = 7;
        File.AppendAllText(tab.File.FilePath, "\n<!-- external edit -->");
        byte[] external = File.ReadAllBytes(tab.File.FilePath);

        var result = await CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot);
        result.IsExternalChange.Should().BeTrue();
        tab.IsDirty.Should().BeTrue();
        SessionStore.Load(tab.File.FilePath).CurrentHp.Should().Be(7);
        File.ReadAllBytes(tab.File.FilePath).Should().Equal(external);
    }

    [Fact]
    public async Task XmlWriteFailureAfterSuccessfulSidecarSaveRetainsPendingNotesForRetry()
    {
        var tab = CreateTab();
        tab.Snapshot!.Notes1 = "Notes waiting for file access";
        tab.Session.CurrentHp = 4;
        using (var reader = new FileStream(tab.File.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = await CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot);
            result.Succeeded.Should().BeFalse();
            tab.IsDirty.Should().BeTrue();
            SessionStore.Load(tab.File.FilePath).CurrentHp.Should().Be(4);
        }
        (await CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot)).Succeeded.Should().BeTrue();
        tab.IsDirty.Should().BeFalse();
        File.ReadAllText(tab.File.FilePath).Should().Contain("Notes waiting for file access");
    }

    [Fact]
    public async Task SuccessfulSessionSaveDoesNotClearUnrelatedCharacterDrafts()
    {
        var tab = CreateTab();
        tab.IsDirty = true;
        tab.Snapshot!.Notes1 = "Saved session notes";
        (await CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot)).Succeeded.Should().BeTrue();
        tab.SessionWrites.HasPendingChanges.Should().BeFalse();
        tab.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task HpAndConditionUpdatesDoNotRewriteCharacterXml()
    {
        var tab = CreateTab();
        File.SetLastWriteTimeUtc(tab.File.FilePath, DateTime.UtcNow.AddDays(-3));
        tab.File.RefreshKnownDiskStamp();
        var stamp = CharacterFileDiskStamp.Capture(tab.File.FilePath);
        byte[] original = File.ReadAllBytes(tab.File.FilePath);

        for (int i = 0; i < 10; i++)
        {
            tab.Session.CurrentHp = i;
            tab.Session.Conditions = ["Poisoned"];
            (await CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot!)).Succeeded.Should().BeTrue();
        }
        CharacterFileDiskStamp.Capture(tab.File.FilePath).Should().Be(stamp,
            "ten session-only edits should produce zero character XML writes");
        File.ReadAllBytes(tab.File.FilePath).Should().Equal(original);
        SessionStore.Load(tab.File.FilePath).CurrentHp.Should().Be(9);
    }

    [Fact]
    public async Task ACompletedFullSaveSupersedesAnOlderQueuedSessionPatch()
    {
        var tab = CreateTab();
        tab.Snapshot!.Notes1 = "Older queued notes";
        await tab.FileSaveSemaphore.WaitAsync();
        Task<CharacterFileWriteResult> pending;
        try
        {
            pending = CharacterFileWriteCoordinator.SaveSessionAsync(tab, tab.Session, tab.Snapshot);
            tab.Snapshot.Notes1 = "Newer fully saved notes";
            tab.File.SaveNotes(tab.Snapshot).Should().BeTrue();
            tab.SessionWrites.Saved(tab.SessionWrites.Version, SessionCharacterEdits.From(tab.Snapshot));
        }
        finally { tab.FileSaveSemaphore.Release(); }
        (await pending).Succeeded.Should().BeTrue();
        File.ReadAllText(tab.File.FilePath).Should().Contain("Newer fully saved notes").And.NotContain("Older queued notes");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
