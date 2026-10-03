using System.Text;
using System.Text.Json;
using Aurora.App.Services;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Storage;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class CloudCharacterSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aurora-cloud-test-" + Guid.NewGuid().ToString("N"));
    private static byte[] Character(string name) => Encoding.UTF8.GetBytes($"<character><build><name>{name}</name></build></character>");
    private static readonly CharacterDocumentReference Reference = new("remote-file");
    private static CharacterDocumentMetadata Metadata(string version = "1") =>
        new(Reference, "Aria.dnd5e", version, null, null, null);

    [Fact]
    public async Task FailedUpload_PreservesLocalCandidateAndOriginalReceipt()
    {
        var store = new FakeStore();
        using var session = await Open(store);
        byte[] edited = Character("Edited");
        File.WriteAllBytes(session.FilePath, edited);
        store.Save = (_, _, _) => throw new HttpRequestException("offline");
        await session.Invoking(s => s.SaveAsync()).Should().ThrowAsync<HttpRequestException>();
        File.ReadAllBytes(session.FilePath).Should().Equal(edited);
        File.Exists(session.FilePath + ".pending").Should().BeTrue();
        session.Metadata.ProviderVersion.Should().Be("1");
        session.HasPendingChanges.Should().BeTrue();
    }

    [Fact]
    public async Task SuccessfulSave_MirrorsConfirmedBytesAndIncludesSessionState()
    {
        var store = new FakeStore();
        using var session = await Open(store);
        File.WriteAllBytes(session.FilePath, Character("Edited"));
        File.WriteAllText(session.SessionPath, """{"CurrentHp":12}""");
        byte[]? uploaded = null;
        store.Save = (_, data, _) =>
        {
            uploaded = data.ToArray();
            return Task.FromResult(Metadata("2"));
        };
        await session.SaveAsync();
        session.HasPendingChanges.Should().BeFalse();
        session.Metadata.ProviderVersion.Should().Be("2");
        File.ReadAllBytes(session.FilePath).Should().Equal(uploaded!);
        Encoding.UTF8.GetString(uploaded!).Should().Contain("aurora-cloud-session").And.Contain("CurrentHp");
    }

    [Fact]
    public async Task EditDuringUpload_IsNotOverwrittenOrMarkedSaved()
    {
        var store = new FakeStore();
        using var session = await Open(store);
        File.WriteAllBytes(session.FilePath, Character("First"));
        byte[] later = Character("Later");
        store.Save = (_, _, _) =>
        {
            File.WriteAllBytes(session.FilePath, later);
            return Task.FromResult(Metadata("2"));
        };
        await session.SaveAsync();
        File.ReadAllBytes(session.FilePath).Should().Equal(later);
        session.HasPendingChanges.Should().BeTrue();
    }

    [Fact]
    public async Task LoadNewerDriveCopy_IsAuthoritativeAndArchivesDisplacedEdits()
    {
        var store = new FakeStore();
        using var session = await Open(store);
        byte[] local = Character("Unsent");
        File.WriteAllBytes(session.FilePath, local);
        File.WriteAllText(session.SessionPath, """{"CurrentHp":1}""");
        store.Remote = new(Metadata("9"), CloudCharacterSession.Pack(Character("Drive"), Encoding.UTF8.GetBytes("""{"CurrentHp":20}""")));
        await session.ReloadAuthoritativeAsync();
        File.ReadAllBytes(session.FilePath).Should().Equal(store.Remote.Content);
        File.ReadAllText(session.SessionPath).Should().Contain("20");
        File.ReadAllBytes(session.RecoveryPath!).Should().Equal(local);
        File.ReadAllText(session.RecoveryPath + ".session.json").Should().Contain(":1");
        session.Metadata.ProviderVersion.Should().Be("9");
        session.HasPendingChanges.Should().BeFalse();
    }

    [Fact]
    public async Task ReopenAfterFailedSave_LoadsDriveAndPreservesUnsentWork()
    {
        var store = new FakeStore();
        string path;
        byte[] unsent = Character("Offline");
        using (var first = await Open(store))
        {
            path = first.FilePath;
            File.WriteAllBytes(path, unsent);
        }
        store.Remote = new(Metadata("8"), Character("Cloud wins"));
        using var second = await Open(store);
        File.ReadAllBytes(path).Should().Equal(store.Remote.Content);
        string backup = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(path)!, "recovery"), "*.dnd5e").Single();
        File.ReadAllBytes(backup).Should().Equal(unsent);
    }

    [Fact]
    public async Task InvalidRemoteDownload_DoesNotReplaceWorkingCopy()
    {
        var store = new FakeStore();
        using var session = await Open(store);
        byte[] local = Character("Working");
        File.WriteAllBytes(session.FilePath, local);
        store.Remote = new(Metadata("2"), "<html />"u8.ToArray());
        await session.Invoking(s => s.ReloadAuthoritativeAsync()).Should().ThrowAsync<InvalidDataException>();
        File.ReadAllBytes(session.FilePath).Should().Equal(local);
        session.Metadata.ProviderVersion.Should().Be("1");
    }

    [Fact]
    public async Task Workspaces_AreIsolatedByAccountAndHaveExclusiveWriter()
    {
        var store = new FakeStore();
        using var first = await Open(store);
        var openAgain = () => Open(store);
        await openAgain.Should().ThrowAsync<IOException>();
        using var other = await CloudCharacterSession.OpenAsync(store, _root, "other-account", Reference);
        other.FilePath.Should().NotBe(first.FilePath);
    }

    [Fact]
    public async Task MarkClean_DoesNotHideEditsMadeDuringCloudSave()
    {
        var store = new FakeStore();
        using var session = await Open(store);
        var tab = new CharacterTab(new CharacterFile(session.FilePath)) { CloudSession = session };
        var tabs = new CharacterTabService();
        tabs.MarkDirty(tab);
        long captured = tab.EditVersion;
        tabs.MarkDirty(tab);
        tab.CloudSavedEditVersion = captured;
        tabs.MarkClean(tab);
        tab.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_DoesNotAcknowledgeCloudSave()
    {
        var store = new FakeStore();
        using var session = await Open(store);
        File.WriteAllBytes(session.FilePath, Character("Changed"));
        store.Save = (_, _, ct) => Task.FromCanceled<CharacterDocumentMetadata>(new CancellationToken(true));
        await session.Invoking(s => s.SaveAsync()).Should().ThrowAsync<OperationCanceledException>();
        session.Metadata.ProviderVersion.Should().Be("1");
        session.HasPendingChanges.Should().BeTrue();
        session.IsSaving.Should().BeFalse();
    }

    [Theory]
    [InlineData("<!DOCTYPE character [<!ENTITY x SYSTEM 'file:///secret'>]><character><build>&x;</build></character>")]
    [InlineData("<not-a-character />")]
    public void Import_RejectsUnsafeOrNonCharacterXml(string xml)
    {
        Action validate = () => CloudCharacterSession.ValidateCharacter(Encoding.UTF8.GetBytes(xml));
        validate.Should().Throw<Exception>();
    }

    [Fact]
    public async Task SaveCommand_WhenDriveChanges_LoadsAuthoritativeCharacterAndSession()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var catalog = DataManager.Current.ElementsCollection;
        var originalElements = catalog.ToArray();
        var manager = CharacterManager.Current;
        Directory.CreateDirectory(_root);
        var oldSelection = SelectionRuleExpanderContext.Current;
        var oldSpellcasting = SpellcastingSectionContext.Current;
        CharacterTab? tab = null;
        try
        {
            SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
            SpellcastingSectionContext.Current = new TestSpellHandler();
            catalog.Clear();
            catalog.Add(new Builder.Data.Elements.LevelElement
            {
                ElementHeader = new Builder.Data.ElementHeader("Level 1", "Level", "Test", "ID_TEST_CLOUD_LEVEL_ONE"),
                Level = 1
            });
            catalog.Add(new Builder.Data.Elements.LevelElement
            {
                ElementHeader = new Builder.Data.ElementHeader("Level 2", "Level", "Test", "ID_TEST_CLOUD_LEVEL_TWO"),
                Level = 2, RequiredExperience = 300
            });
            await manager.New(true);
            manager.Character.Name = "Drive Original";
            string seedPath = Path.Combine(_root, "seed.dnd5e");
            var seed = new CharacterFile(seedPath);
            seed.Save(manager.Character).Should().BeTrue();
            byte[] original = File.ReadAllBytes(seedPath);
            manager.Character.Name = "Drive Newer";
            seed.Save(manager.Character).Should().BeTrue();
            byte[] authoritative = CloudCharacterSession.Pack(File.ReadAllBytes(seedPath),
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new SessionState { CurrentHp = 7 })));
            var store = new FakeStore { Remote = new(Metadata(), original) };
            using var session = await Open(store);
            tab = new CharacterTab(new CharacterFile(session.FilePath)) { CloudSession = session };
            using (await CharacterContext.EnterAsync(tab))
            {
                tab.Snapshot = CharacterSnapshot.From(tab.Character!);
                tab.Snapshot.Name = "Unsent Local Name";
                tab.Session = new SessionState { CurrentHp = 1 };
                tab.IsDirty = true;
            }
            store.Remote = new(Metadata("2"), authoritative);
            store.Save = (_, _, _) => throw new CharacterDocumentConflictException(Metadata(), Metadata("2"));
            (await BuildService.SaveTabAsync(tab)).Should().BeNull();
            tab.Character!.Name.Should().Be("Drive Newer");
            tab.Snapshot!.Name.Should().Be("Drive Newer");
            tab.Session.CurrentHp.Should().Be(7);
            tab.IsDirty.Should().BeFalse();
            tab.CloudReloadVersion.Should().Be(1);
            File.ReadAllText(session.RecoveryPath!).Should().Contain("Unsent Local Name");
            File.ReadAllBytes(session.FilePath).Should().Equal(authoritative);
        }
        finally
        {
            if (tab is not null) await CharacterContext.ReleaseAsync(tab);
            SelectionRuleExpanderContext.Current?.RemoveAllExpanders();
            await manager.New(false);
            oldSelection?.RemoveAllExpanders();
            SelectionRuleExpanderContext.Current = oldSelection;
            SpellcastingSectionContext.Current = oldSpellcasting;
            catalog.Clear();
            catalog.AddRange(originalElements);
        }
    }

    private Task<CloudCharacterSession> Open(FakeStore store) =>
        CloudCharacterSession.OpenAsync(store, _root, "account", Reference);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private sealed class FakeStore : ICharacterDocumentStore
    {
        public string ProviderId => "fake";
        public CharacterDocument Remote { get; set; } = new(Metadata(), Character("Original"));
        public Func<CharacterDocumentMetadata, ReadOnlyMemory<byte>, CancellationToken, Task<CharacterDocumentMetadata>> Save { get; set; } =
            (_, _, _) => Task.FromResult(Metadata("2"));
        public Task<CharacterDocument> OpenAsync(CharacterDocumentReference reference, CancellationToken cancellationToken = default) =>
            Task.FromResult(Remote);
        public Task<CharacterDocumentMetadata> CreateAsync(string fileName, ReadOnlyMemory<byte> content,
            CharacterDocumentReference? parentFolder = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Metadata());
        public Task<CharacterDocumentMetadata> SaveAsync(CharacterDocumentMetadata expected, ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default) => Save(expected, content, cancellationToken);
    }
}
