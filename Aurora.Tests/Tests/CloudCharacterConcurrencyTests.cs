using Aurora.Reflections.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Storage;

namespace Aurora.Tests.Tests;

public sealed class CloudCharacterConcurrencyTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("conflict")]
    public Task StalledUploadLeavesOtherCharactersEditableAndRetainsLaterEdits(string outcome) =>
        WithTabs(async (cloudTab, localTab, store) =>
        {
            store.SaveOutcome = outcome;
            store.PauseSave = true;
            Task<string?> saving = BuildService.SaveTabAsync(cloudTab);
            await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task<string?> localSave;
            bool completedBeforeRelease;
            try
            {
                localTab.Snapshot!.Name = "Edited local B";
                localSave = BuildService.SaveTabAsync(localTab);
                completedBeforeRelease = await Task.WhenAny(localSave, Task.Delay(2000)) == localSave;
                cloudTab.Snapshot!.Name = "Later edit to A";
                cloudTab.IsDirty = true;
            }
            finally { store.SaveRelease.TrySetResult(); }

            (await localSave).Should().BeNull();
            string? error = await saving;
            completedBeforeRelease.Should().BeTrue("a paused cloud provider must not own the global character context");
            File.ReadAllText(localTab.File.FilePath).Should().Contain("Edited local B");
            if (outcome == "conflict")
            {
                error.Should().Contain("not saved to Google Drive");
                cloudTab.CloudNotice.Should().Be(error);
                File.ReadAllText(cloudTab.CloudSession!.RecoveryPath!).Should().Contain("Later edit to A");
                cloudTab.Snapshot!.Name.Should().Be("Later edit to A");
                File.ReadAllText(cloudTab.File.FilePath).Should().Contain("Later edit to A");
                cloudTab.IsDirty.Should().BeTrue();
                cloudTab.CloudReloadVersion.Should().Be(0);
                store.OpenCalls.Should().Be(1, "a conflict must not reload the remote character");
            }
            else
            {
                cloudTab.Snapshot!.Name.Should().Be("Later edit to A");
                cloudTab.IsDirty.Should().BeTrue();
                if (outcome == "failure") error.Should().NotBeNullOrWhiteSpace();
                else error.Should().BeNull();
            }
        });

    [Fact]
    public Task StalledDownloadLeavesOtherCharactersEditableAndPreservesRecovery() =>
        WithTabs(async (cloudTab, localTab, store) =>
        {
            cloudTab.Snapshot!.Name = "Unsent A before reload";
            cloudTab.IsDirty = true;
            store.PauseOpen = true;
            Task<string?> reload = BuildService.ReloadCloudTabAsync(cloudTab);
            await store.OpenEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task<string?> localSave;
            bool completedBeforeRelease;
            try
            {
                localTab.Snapshot!.Name = "Local B during download";
                localSave = BuildService.SaveTabAsync(localTab);
                completedBeforeRelease = await Task.WhenAny(localSave, Task.Delay(2000)) == localSave;
                cloudTab.Snapshot!.Name = "Later A during download";
                cloudTab.IsDirty = true;
            }
            finally { store.OpenRelease.TrySetResult(); }
            (await localSave).Should().BeNull();
            (await reload).Should().BeNull();
            completedBeforeRelease.Should().BeTrue();
            File.ReadAllText(cloudTab.CloudSession!.RecoveryPath!).Should().Contain("Later A during download");
            cloudTab.Snapshot!.Name.Should().Be("Cloud A");
            localTab.Snapshot!.Name.Should().Be("Local B during download");
        });

    [Fact]
    public async Task AnEditToTheUploadingCharacterDoesNotHoldTheGlobalEngineWhileWaitingForDrive()
    {
        await WithTabs(async (cloudTab, localTab, store) =>
        {
            store.PauseSave = true;
            Task<string?> upload = BuildService.SaveTabAsync(cloudTab);
            await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> edit = Task.Run(async () =>
            {
                using (await CharacterContext.EnterAsync(cloudTab))
                {
                    entered.TrySetResult();
                    cloudTab.Snapshot!.Notes1 = "Later notes during upload";
                    cloudTab.IsDirty = true;
                    return CharacterFileWriteCoordinator.Write(cloudTab.FileSaveSemaphore,
                        cloudTab.File, "Later notes", () => cloudTab.File.SaveNotes(cloudTab.Snapshot)).Succeeded;
                }
            });
            Task<string?> localSave;
            bool bothCompletedBeforeRelease;
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                localTab.Snapshot!.Name = "Local B after A edit";
                localSave = BuildService.SaveTabAsync(localTab);
                Task both = Task.WhenAll(edit, localSave);
                bothCompletedBeforeRelease = await Task.WhenAny(both, Task.Delay(2000)) == both;
            }
            finally { store.SaveRelease.TrySetResult(); }
            (await edit).Should().BeTrue();
            (await localSave).Should().BeNull();
            (await upload).Should().BeNull();
            bothCompletedBeforeRelease.Should().BeTrue();
            File.ReadAllText(cloudTab.File.FilePath).Should().Contain("Later notes during upload");
            cloudTab.IsDirty.Should().BeTrue();
            localTab.Snapshot!.Name.Should().Be("Local B after A edit");
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SameVersionLocalSaveRequestsShareTheActualSuccessOrFailure(bool failWrite) =>
        WithTabs(async (_, tab, _) =>
        {
            tab.Snapshot!.Backstory = "The draft covered by both save requests.";
            tab.IsDirty = true;
            if (failWrite)
                Directory.CreateDirectory(SessionStore.GetSidecarPath(tab.File.FilePath));

            int savesStarted = 0;
            tab.SaveStateChanged += () => { if (tab.IsSaving) savesStarted++; };
            await tab.FileSaveSemaphore.WaitAsync();
            Task<string?> first;
            Task<string?> duplicate;
            try
            {
                first = BuildService.SaveTabAsync(tab, coalesceRequests: true);
                duplicate = BuildService.SaveTabAsync(tab, coalesceRequests: true);
                duplicate.Should().BeSameAs(first, "blur and Ctrl+S describe the same accepted input version");
                first.IsCompleted.Should().BeFalse();
                (await BuildService.SaveTabAsync(tab)).Should().Be("A save is already in progress.",
                    "unversioned mutation callers must retain their existing busy protection");
            }
            finally { tab.FileSaveSemaphore.Release(); }

            string? firstError = await first;
            (await duplicate).Should().Be(firstError);
            savesStarted.Should().Be(1);
            tab.IsSaving.Should().BeFalse();
            if (failWrite)
            {
                firstError.Should().NotBeNullOrWhiteSpace().And.NotContain("already in progress");
                tab.IsDirty.Should().BeTrue();
            }
            else
            {
                firstError.Should().BeNull();
                File.ReadAllText(tab.File.FilePath).Should().Contain("The draft covered by both save requests.");
            }
        });

    [Fact]
    public Task NewerNarrativeRequestWaitsThenSavesItsOwnDraft() =>
        WithTabs(async (tab, _, store) =>
        {
            tab.Snapshot!.Backstory = "First captured draft.";
            tab.IsDirty = true;
            store.PauseSave = true;
            Task<string?> first = BuildService.SaveTabAsync(tab, coalesceRequests: true);
            await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task<string?> newer;
            try
            {
                tab.Snapshot.Backstory = "Later accepted draft.";
                tab.IsDirty = true;
                newer = BuildService.SaveTabAsync(tab, coalesceRequests: true);
                newer.Should().NotBeSameAs(first);
                newer.IsCompleted.Should().BeFalse();
                File.ReadAllText(tab.File.FilePath).Should().Contain("First captured draft.")
                    .And.NotContain("Later accepted draft.");
            }
            finally { store.SaveRelease.TrySetResult(); }

            (await first).Should().BeNull();
            (await newer).Should().BeNull();
            store.SaveCalls.Should().Be(2);
            File.ReadAllText(tab.File.FilePath).Should().Contain("Later accepted draft.");
            tab.CloudSavedEditVersion.Should().Be(tab.EditVersion);
        });

    [Fact]
    public Task FailedInFlightSaveLeavesNewerQueuedNarrativeDirtyForRetry() =>
        WithTabs(async (tab, _, store) =>
        {
            tab.Snapshot!.Backstory = "First captured draft.";
            tab.IsDirty = true;
            store.SaveOutcome = "failure";
            store.PauseSave = true;
            Task<string?> first = BuildService.SaveTabAsync(tab, coalesceRequests: true);
            await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task<string?> newer;
            try
            {
                tab.Snapshot.Backstory = "Later unsaved draft.";
                tab.IsDirty = true;
                newer = BuildService.SaveTabAsync(tab, coalesceRequests: true);
            }
            finally { store.SaveRelease.TrySetResult(); }

            string? error = await first;
            error.Should().NotBeNullOrWhiteSpace();
            (await newer).Should().Be(error);
            store.SaveCalls.Should().Be(1, "an overlapping commit must not repeatedly retry a failed provider");
            tab.Snapshot.Backstory.Should().Be("Later unsaved draft.");
            tab.IsDirty.Should().BeTrue();
            File.ReadAllText(tab.File.FilePath).Should().Contain("First captured draft.")
                .And.NotContain("Later unsaved draft.");
        });

    private static async Task WithTabs(Func<CharacterTab, CharacterTab, PausedStore, Task> test)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var catalog = DataManager.Current.ElementsCollection;
        var originals = catalog.ToArray();
        var previousSelection = SelectionRuleExpanderContext.Current;
        var previousSpells = SpellcastingSectionContext.Current;
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "cloud-concurrency-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var manager = CharacterManager.Current;
        CloudCharacterSession? session = null;
        try
        {
            await CharacterContext.InvalidateAsync();
            await manager.New(false);
            catalog.Clear();
            SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
            SpellcastingSectionContext.Current = new TestSpellHandler();
            for (int number = 1; number <= 20; number++)
            {
                var document = new System.Xml.XmlDocument();
                document.LoadXml($"<element name='{number}' type='Level' source='Internal' id='ID_CONCURRENCY_LEVEL_{number}' />");
                var level = new ElementParser().ParseElement(document.DocumentElement!).Construct<LevelElement>();
                level.Level = number;
                level.RequiredExperience = (number - 1) * 300;
                level.ElementSetters.Add(new("Level", number.ToString()));
                catalog.Add(level);
            }
            await manager.New(true);
            manager.Character.Name = "Cloud A";
            var seed = new CharacterFile(Path.Combine(root, "seed.dnd5e"));
            seed.Save(manager.Character).Should().BeTrue();
            var store = new PausedStore(File.ReadAllBytes(seed.FilePath));
            session = await CloudCharacterSession.OpenAsync(store, Path.Combine(root, "cloud"), "account", new("A"));
            var cloudTab = new CharacterTab(new CharacterFile(session.FilePath)) { CloudSession = session };
            using (await CharacterContext.EnterAsync(cloudTab))
                cloudTab.Snapshot = CharacterSnapshot.From(cloudTab.Character!);

            string localPath = Path.Combine(root, "local.dnd5e");
            File.Copy(seed.FilePath, localPath);
            var localTab = new CharacterTab(new CharacterFile(localPath));
            using (await CharacterContext.EnterAsync(localTab))
                localTab.Snapshot = CharacterSnapshot.From(localTab.Character!);
            await test(cloudTab, localTab, store);
        }
        finally
        {
            await CharacterContext.InvalidateAsync();
            session?.Dispose();
            await manager.New(false);
            catalog.Clear();
            catalog.AddRange(originals);
            SelectionRuleExpanderContext.Current = previousSelection;
            SpellcastingSectionContext.Current = previousSpells;
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class PausedStore(byte[] content) : ICharacterDocumentStore
    {
        public string ProviderId => "test";
        public bool PauseSave { get; set; }
        public bool PauseOpen { get; set; }
        public string SaveOutcome { get; set; } = "success";
        public int SaveCalls { get; private set; }
        public int OpenCalls { get; private set; }
        public TaskCompletionSource SaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SaveRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource OpenEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource OpenRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static CharacterDocumentMetadata Metadata => new(new("A"), "A.dnd5e", "1", null, null, null);
        public async Task<CharacterDocument> OpenAsync(CharacterDocumentReference reference, CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            if (PauseOpen) { OpenEntered.TrySetResult(); await OpenRelease.Task.WaitAsync(cancellationToken); }
            return new CharacterDocument(Metadata, content);
        }
        public Task<CharacterDocumentMetadata> CreateAsync(string fileName, ReadOnlyMemory<byte> bytes,
            CharacterDocumentReference? parentFolder = null, CancellationToken cancellationToken = default) => Task.FromResult(Metadata);
        public async Task<CharacterDocumentMetadata> SaveAsync(CharacterDocumentMetadata expected, ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            if (PauseSave) { SaveEntered.TrySetResult(); await SaveRelease.Task.WaitAsync(cancellationToken); }
            if (SaveOutcome == "failure") throw new OperationCanceledException("Provider request canceled.");
            if (SaveOutcome == "conflict") throw new CharacterDocumentConflictException(expected, Metadata with { ProviderVersion = "2" });
            return Metadata with { ProviderVersion = "2" };
        }
    }
}
