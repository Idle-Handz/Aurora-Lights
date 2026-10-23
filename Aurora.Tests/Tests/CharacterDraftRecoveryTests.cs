using System.Xml;
using System.Xml.Linq;
using Aurora.App.Services;
using Builder.Presentation;
using Builder.Presentation.Models;

namespace Aurora.Tests.Tests;

public sealed class CharacterDraftRecoveryTests
{
    [Fact]
    public Task FreshInstallCleanTabsAndMissingDraftDeletionDoNotReportARecoveryFailure() =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            string root = Path.Combine(directory, "drafts-not-created");
            var recovery = new CharacterDraftRecovery(root);
            recovery.Delete(Guid.NewGuid());
            recovery.Track(tab);
            await recovery.FlushAsync();
            recovery.CheckpointOnBackground();
            recovery.Forget(tab);
            recovery.LastError.Should().BeNull();
            Directory.Exists(root).Should().BeFalse("clean tabs do not need a journal directory");
        });

    [Fact]
    public Task DirtyEditsRecoverAsNewCopyWithSessionAndCustomFeaturesWithoutSavingOriginal() =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            (await BuildService.AddCustomFeatureAsync(tab, "ID_PENDING_EXTRA")).Should().BeNull();
            var original = await File.ReadAllBytesAsync(tab.File.FilePath);
            string root = Path.Combine(directory, "drafts");
            var recovery = new CharacterDraftRecovery(root);
            recovery.Track(tab);
            BuildPendingSnapshotEditsTests.SetPendingEdits(tab.Snapshot!);
            tab.Session.CurrentHp = 7;
            tab.Session.PactSpellSlotsUsed[2] = 2;
            tab.IsDirty = true;
            await recovery.FlushAsync();
            tab.Snapshot!.Name = "Newest pending name";
            tab.IsDirty = true; // already-dirty tabs still checkpoint each accepted edit
            await recovery.FlushAsync();

            var restarted = new CharacterDraftRecovery(root);
            var info = restarted.List().Should().ContainSingle().Subject;
            info.Name.Should().Be("Newest pending name");
            recovery.List().Should().BeEmpty("live tabs are not interrupted-session recovery offers");
            string copy = restarted.OpenCopy(info.Id, Path.Combine(directory, "recovered"));
            copy.Should().NotBe(tab.File.FilePath);
            var xml = XDocument.Load(copy);
            var input = xml.Root!.Element("build")!.Element("input")!;
            input.Element("name")!.Value.Should().Be("Newest pending name");
            input.Element("backstory")!.Value.Should().Be("Pending backstory\nSecond line");
            input.Element("currency")!.Element("gold")!.Value.Should().Be("14");
            xml.Root.Element("custom-features")!.Elements("feature").Select(e => (string?)e.Attribute("id"))
                .Should().Contain("ID_PENDING_EXTRA");
            var session = SessionStore.Load(copy);
            session.CurrentHp.Should().Be(7);
            session.PactSpellSlotsUsed[2].Should().Be(2);
            (await File.ReadAllBytesAsync(tab.File.FilePath)).Should().Equal(original);
            tab.IsDirty.Should().BeTrue();
            restarted.List().Should().ContainSingle("a failed or cancelled open must not consume the checkpoint");
            restarted.Delete(info.Id);
            restarted.List().Should().BeEmpty();
        });

    [Fact]
    public Task StopDoesNotWaitForAnEngineOrFileWriteAndStaleCaptureCannotResurrectDiscardedDraft() =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            string root = Path.Combine(directory, "drafts");
            var recovery = new CharacterDraftRecovery(root);
            recovery.Track(tab);
            using (await CharacterContext.EnterAsync(tab))
            {
                await tab.FileSaveSemaphore.WaitAsync();
                try
                {
                    tab.Snapshot!.Name = "Latest before background";
                    tab.Snapshot.Backstory = "Background pending story";
                    tab.IsDirty = true;
                    recovery.CheckpointOnBackground();
                    var restarted = new CharacterDraftRecovery(root);
                    var info = restarted.List().Should().ContainSingle().Subject;
                    info.IncompleteOperation.Should().BeTrue();
                    string copy = restarted.OpenCopy(info.Id, Path.Combine(directory, "copies"));
                    XDocument.Load(copy).Root!.Element("build")!.Element("input")!.Element("name")!
                        .Value.Should().Be("Latest before background");
                    recovery.Forget(tab); // explicit discard while a capture is queued
                }
                finally { tab.FileSaveSemaphore.Release(); }
            }
            await recovery.FlushAsync();
            new CharacterDraftRecovery(root).List().Should().BeEmpty();
        });

    [Fact]
    public Task BackgroundCheckpointUsesTheInactiveTabsStateInsteadOfTheCurrentCharactersEngine() =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (manager, tab, directory) =>
        {
            string secondPath = Path.Combine(directory, "second.dnd5e");
            File.Copy(tab.File.FilePath, secondPath);
            var second = new CharacterTab(new CharacterFile(secondPath));
            string root = Path.Combine(directory, "drafts");
            var recovery = new CharacterDraftRecovery(root);
            using (await CharacterContext.EnterAsync(second))
            {
                manager.Character.Name = "Other character";
                recovery.Track(tab);
                tab.Snapshot!.Name = "Original tab draft";
                tab.IsDirty = true;
                recovery.CheckpointOnBackground();
                manager.Character.Name.Should().Be("Other character");
                var restarted = new CharacterDraftRecovery(root);
                var info = restarted.List().Should().ContainSingle().Subject;
                string copy = restarted.OpenCopy(info.Id, Path.Combine(directory, "copies"));
                XDocument.Load(copy).Root!.Element("build")!.Element("input")!.Element("name")!
                    .Value.Should().Be("Original tab draft");
            }
            await recovery.FlushAsync();
        });

    [Fact]
    public Task MultipleDirtyTabsRecoverTheirOwnBuildAndSessionWithoutChangingEitherOriginal() =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (manager, first, directory) =>
        {
            string secondPath = Path.Combine(directory, "second.dnd5e");
            File.Copy(first.File.FilePath, secondPath);
            byte[] original = await File.ReadAllBytesAsync(first.File.FilePath);
            var second = new CharacterTab(new CharacterFile(secondPath));
            var recovery = new CharacterDraftRecovery(Path.Combine(directory, "drafts"));
            first.Snapshot!.Name = "First tab draft";
            first.Session.CurrentHp = 7;
            first.IsDirty = true;
            using (await CharacterContext.EnterAsync(second))
            {
                second.Snapshot = CharacterSnapshot.From(manager.Character);
                second.Snapshot.Name = "Second tab draft";
                second.Session.CurrentHp = 3;
                second.IsDirty = true;
                recovery.Track(first);
                recovery.Track(second);
                recovery.CheckpointOnBackground();
            }
            try
            {
                await recovery.FlushAsync();
                var restarted = new CharacterDraftRecovery(Path.Combine(directory, "drafts"));
                var records = restarted.List();
                records.Select(info => info.Name).Should().BeEquivalentTo(new[] { "First tab draft", "Second tab draft" });
                foreach (var info in records)
                {
                    string copy = restarted.OpenCopy(info.Id, Path.Combine(directory, "copies"));
                    XDocument.Load(copy).Root!.Element("build")!.Element("input")!.Element("name")!.Value
                        .Should().Be(info.Name);
                    SessionStore.Load(copy).CurrentHp.Should().Be(info.Name == "First tab draft" ? 7 : 3);
                }
                (await File.ReadAllBytesAsync(first.File.FilePath)).Should().Equal(original);
                (await File.ReadAllBytesAsync(secondPath)).Should().Equal(original);
            }
            finally
            {
                recovery.Forget(first);
                recovery.Forget(second);
                await recovery.FlushAsync();
            }
        });

    [Fact]
    public Task SavedAndDiscardedTabsClearTheirOwnJournalsButKeepPreviousSessionRecovery() =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            string root = Path.Combine(directory, "drafts");
            var recovery = new CharacterDraftRecovery(root);
            var tabs = new CharacterTabService(recovery);
            var tracked = tabs.OpenTab(tab.File, tab.Character!);
            tracked.Snapshot = tab.Snapshot;
            CharacterContext.ClaimAfterLoad(tracked);
            tracked.Snapshot!.Name = "Pending name";
            tabs.MarkDirty(tracked);
            await recovery.FlushAsync();
            var previous = Guid.NewGuid();
            var oldFile = Path.Combine(root, tracked.Id.ToString("N") + ".draft.json");
            string old = (await File.ReadAllTextAsync(oldFile)).Replace(tracked.Id.ToString(), previous.ToString());
            await File.WriteAllTextAsync(Path.Combine(root, previous.ToString("N") + ".draft.json"), old);
            (await BuildService.SaveTabAsync(tracked)).Should().BeNull();
            tabs.MarkClean(tracked);
            await recovery.FlushAsync();
            var restarted = new CharacterDraftRecovery(root);
            restarted.List().Select(d => d.Id).Should().Equal(previous);
            tracked.Snapshot.Name = "Another edit";
            tabs.MarkDirty(tracked);
            await recovery.FlushAsync();
            tabs.CloseTab(tracked);
            await recovery.FlushAsync();
            restarted.List().Select(d => d.Id).Should().Equal(previous);
        });

    [Fact]
    public Task CaptureFailureRetainsLastGoodCheckpointAndReportsTheFailure() =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            bool fail = false;
            string root = Path.Combine(directory, "drafts");
            var recovery = new CharacterDraftRecovery(root, capture: t => fail
                ? Task.FromException<XmlDocument>(new IOException("Disk unavailable"))
                : BuildService.CaptureCharacterExportAsync(t));
            recovery.Track(tab);
            tab.Snapshot!.Name = "Checkpoint one";
            tab.IsDirty = true;
            await recovery.FlushAsync();
            fail = true;
            tab.Snapshot.Name = "Checkpoint two";
            tab.IsDirty = true;
            await recovery.FlushAsync();
            recovery.LastError.Should().NotBeNull();
            new CharacterDraftRecovery(root).List().Should().ContainSingle().Which.Name.Should().Be("Checkpoint one");
            fail = false;
            recovery.CheckpointOnBackground();
            recovery.LastError.Should().BeNull();
            new CharacterDraftRecovery(root).List().Should().ContainSingle().Which.Name.Should().Be("Checkpoint two");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task BackgroundCheckpointSupersedesAnInFlightCaptureAndDiscardWaitsForIt(bool discard) =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            string root = Path.Combine(directory, "drafts");
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int captures = 0;
            var recovery = new CharacterDraftRecovery(root, capture: async t =>
            {
                var document = await BuildService.CaptureCharacterExportAsync(t);
                if (Interlocked.Increment(ref captures) == 1)
                {
                    captured.SetResult();
                    await resume.Task;
                }
                return document;
            });
            tab.Snapshot!.Name = "Before background";
            tab.IsDirty = true;
            recovery.Track(tab);
            await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                tab.Snapshot.Name = "Latest background checkpoint";
                tab.IsDirty = true;
                recovery.CheckpointOnBackground();
                new CharacterDraftRecovery(root).List().Should().ContainSingle().Which.Name
                    .Should().Be("Latest background checkpoint");
                if (discard) recovery.Forget(tab);
                var flush = recovery.FlushAsync();
                flush.IsCompleted.Should().BeFalse("even a retired tab's capture must finish before flushing completes");
                resume.SetResult();
                await flush.WaitAsync(TimeSpan.FromSeconds(10));
                var restarted = new CharacterDraftRecovery(root);
                if (discard) restarted.List().Should().BeEmpty();
                else
                {
                    var info = restarted.List().Should().ContainSingle().Subject;
                    string copy = restarted.OpenCopy(info.Id, Path.Combine(directory, "copies"));
                    XDocument.Load(copy).Root!.Element("build")!.Element("input")!.Element("name")!.Value
                        .Should().Be("Latest background checkpoint");
                }
            }
            finally
            {
                resume.TrySetResult();
                await recovery.FlushAsync();
                recovery.Forget(tab);
            }
        });

    [Fact]
    public void CorruptDraftIsVisibleAndKeptUntilExplicitDiscard()
    {
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var id = Guid.NewGuid();
            string file = Path.Combine(root, id.ToString("N") + ".draft.json");
            File.WriteAllText(file, "{invalid");
            var recovery = new CharacterDraftRecovery(root);
            recovery.List().Should().ContainSingle().Which.IsCorrupt.Should().BeTrue();
            Action open = () => recovery.OpenCopy(id, Path.Combine(root, "copies"));
            open.Should().Throw<Exception>();
            File.Exists(file).Should().BeTrue();
            recovery.Delete(id);
            recovery.List().Should().BeEmpty();
        }
        finally { Directory.Delete(root, true); }
    }
}
