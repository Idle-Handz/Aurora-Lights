using System.Text.Json.Nodes;
using System.Xml.Linq;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Storage;

namespace Aurora.Tests.Tests;

public sealed class LocalCharacterCloudUploadTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("offline")]
    [InlineData("cancelled")]
    [InlineData("external-change")]
    public async Task UploadFromPrivateStorage_CapturesCurrentCharacterAndSessionWithoutLosingLocalSave(string outcome)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var catalog = DataManager.Current.ElementsCollection;
        var originalElements = catalog.ToArray();
        var manager = CharacterManager.Current;
        var oldSelection = SelectionRuleExpanderContext.Current;
        var oldSpellcasting = SpellcastingSectionContext.Current;
        string root = Path.Combine(Path.GetTempPath(), "aurora-private-upload-" + Guid.NewGuid().ToString("N"));
        CharacterTab? tab = null;
        try
        {
            SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
            SpellcastingSectionContext.Current = new TestSpellHandler();
            catalog.Clear();
            catalog.Add(new Builder.Data.Elements.LevelElement
            {
                ElementHeader = new Builder.Data.ElementHeader("Level 1", "Level", "Test", "ID_UPLOAD_LEVEL_ONE"), Level = 1
            });
            catalog.Add(new Builder.Data.Elements.LevelElement
            {
                ElementHeader = new Builder.Data.ElementHeader("Level 2", "Level", "Test", "ID_UPLOAD_LEVEL_TWO"), Level = 2, RequiredExperience = 300
            });
            await manager.New(true);
            manager.Character.Name = "Previous name";
            string path = Path.Combine(root, "private", "files", "Aria.dnd5e");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            new CharacterFile(path).Save(manager.Character).Should().BeTrue();
            tab = new CharacterTab(new CharacterFile(path));
            using (await CharacterContext.EnterAsync(tab))
            {
                tab.Snapshot = CharacterSnapshot.From(tab.Character!);
                tab.Snapshot.Name = "Current unsaved name";
                tab.Session.CurrentHp = 9;
                tab.Session.TempHp = 3;
                tab.Session.DeathSaveSuccesses = 1;
                tab.Session.DeathSaveFailures = 2;
                tab.Session.Inspiration = true;
                tab.Session.Exhaustion = 1;
                tab.Session.Conditions.Add("Poisoned");
                tab.Session.SpellSlotsUsed[2] = 1;
                tab.Session.CustomResources.Add(new() { Name = "Ki", Max = 4, Used = 2, ResetOn = ResetOn.ShortRest });
                tab.Session.AttackReminderWeaponIds.Add("weapon-reminder");
                tab.Session.HiddenDefaultAttackReminderKeys.Add("unarmed-strike");
                tab.Session.CustomAttackReminders.Add(new() { Name = "Test attack", Attack = "+4", Damage = "1d6", Range = "5 ft." });
                tab.IsDirty = true;
            }
            if (outcome == "external-change") File.AppendAllText(path, "\n<!-- external edit -->");
            byte[] before = File.ReadAllBytes(path);
            using var cancellation = new CancellationTokenSource();
            if (outcome == "cancelled") cancellation.Cancel();
            var store = new UploadStore((fileName, content) =>
            {
                tab.IsSaving.Should().BeTrue("the tab cannot be closed while an upload owns its snapshot");
                fileName.Should().Be("Aria.dnd5e");
                using var stream = new MemoryStream(content);
                var xml = XDocument.Load(stream);
                xml.ToString().Should().Contain("Current unsaved name");
                JsonNode.DeepEquals(JsonNode.Parse(xml.Root!.Element("aurora-cloud-session")!.Value),
                    JsonNode.Parse(File.ReadAllText(SessionStore.GetSidecarPath(path)))).Should()
                    .BeTrue("the complete session sidecar travels with the character");
                if (outcome == "offline") throw new HttpRequestException("offline");
                return new(new("created-file"), fileName, "1", null, content.Length, null);
            });

            Func<Task> upload = async () => await LocalCharacterCloudUpload.UploadAsync(store, tab, cancellation.Token);
            if (outcome == "success") await upload.Should().NotThrowAsync();
            else if (outcome == "offline") await upload.Should().ThrowAsync<HttpRequestException>();
            else if (outcome == "cancelled") await upload.Should().ThrowAsync<OperationCanceledException>();
            else await upload.Should().ThrowAsync<IOException>();

            tab.IsSaving.Should().BeFalse();
            tab.CloudSession.Should().BeNull("upload creates a Drive copy without replacing the local character");
            File.Exists(path).Should().BeTrue();
            if (outcome is "cancelled" or "external-change")
            {
                store.Creates.Should().Be(0);
                File.ReadAllBytes(path).Should().Equal(before);
                tab.IsDirty.Should().BeTrue();
            }
            else
            {
                store.Creates.Should().Be(1);
                File.ReadAllText(path).Should().Contain("Current unsaved name");
                SessionStore.Load(path).CurrentHp.Should().Be(9);
                tab.IsDirty.Should().BeFalse("the local copy was saved even if the remote upload failed");
            }
            if (outcome == "success")
            {
                using var downloaded = await CloudCharacterSession.OpenAsync(store,
                    Path.Combine(root, "other-device"), "same-account", new("created-file"));
                JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(downloaded.SessionPath)),
                    JsonNode.Parse(File.ReadAllText(SessionStore.GetSidecarPath(path)))).Should().BeTrue();
                SessionStore.Load(downloaded.FilePath).Should().BeEquivalentTo(tab.Session,
                    "opening the uploaded character on another device restores every saved session field");
            }
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
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class UploadStore(Func<string, byte[], CharacterDocumentMetadata> upload) : ICharacterDocumentStore
    {
        public string ProviderId => "fake";
        public int Creates { get; private set; }
        private CharacterDocument? _document;
        public Task<CharacterDocumentMetadata> CreateAsync(string fileName, ReadOnlyMemory<byte> content,
            CharacterDocumentReference? parentFolder = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Creates++;
            byte[] bytes = content.ToArray();
            var metadata = upload(fileName, bytes);
            _document = new(metadata, bytes);
            return Task.FromResult(metadata);
        }
        public Task<CharacterDocument> OpenAsync(CharacterDocumentReference reference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _document.Should().NotBeNull();
            reference.Should().Be(_document!.Metadata.Reference);
            return Task.FromResult(_document with { Content = _document.Content.ToArray() });
        }
        public Task<CharacterDocumentMetadata> SaveAsync(CharacterDocumentMetadata expected, ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
