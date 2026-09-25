using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class CharacterPortraitTests : IAsyncLifetime
{
    // SHA256 of Aurora.Lights/resources/default-portrait.png, the actual Legacy asset.
    private const string LegacyPortraitHash = "FE88524168DC8953AF6B8BEAE33D89A7C5002F446BD55732B599F15E67272DC5";
    private const string CustomImage = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"aurora-portraits-{Guid.NewGuid():N}");
    private readonly CapturingDialog _dialog = new();
    private readonly PropertyInfo _portraitsProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsPortraitsDirectory))!;
    private string? _originalPortraitsDirectory;
    private IMessageDialogService? _originalDialog;

    public async Task InitializeAsync()
    {
        await ContentFixture.EnsureAvailableAsync();
        ContentFixture.SkipIfUnavailable();
        Directory.CreateDirectory(_directory);
        _originalPortraitsDirectory = DataManager.Current.UserDocumentsPortraitsDirectory;
        _portraitsProperty.SetValue(DataManager.Current, _directory);
        _originalDialog = MessageDialogContext.Current;
        MessageDialogContext.Current = _dialog;
    }

    public Task DisposeAsync()
    {
        _portraitsProperty.SetValue(DataManager.Current, _originalPortraitsDirectory);
        MessageDialogContext.Current = _originalDialog;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveWithoutPortrait_EmbedsLegacyImageAndRestoresItOnReload(string? portrait)
    {
        var character = await CharacterManager.Current.New(false);
        character.Name = "No portrait";
        character.PortraitFilename = portrait;
        var file = new CharacterFile(Path.Combine(_directory, "saved.dnd5e"));

        file.Save(character).Should().BeTrue();

        string savedPortrait = character.PortraitFilename!;
        AssertDefaultSaved(file.FilePath, savedPortrait);
        AssertLegacyImage(Convert.FromBase64String(file.DisplayPortraitBase64));
        // The save must be portable: neither the original file nor the local
        // portrait cache should be required to reconstruct the image.
        File.Delete(savedPortrait);
        var reloaded = new CharacterFile(file.FilePath);
        await reloaded.Load();
        AssertLegacyImage(File.ReadAllBytes(CharacterManager.Current.Character.PortraitFilename));
        _dialog.Messages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortraitlessFixture_LoadsWithoutDialogAndPersistsDefaultOnSave(bool omitPortraitNode)
    {
        var document = Fixture();
        if (omitPortraitNode)
        {
            var node = document.SelectSingleNode("/character/display-properties/portrait")!;
            node.ParentNode!.RemoveChild(node);
        }
        string path = Path.Combine(_directory, "portraitless.dnd5e");
        document.Save(path);
        byte[] original = File.ReadAllBytes(path);
        var file = new CharacterFile(path);

        file.InitializeDisplayPropertiesFromFilePath();

        file.IsInitialized.Should().BeTrue();
        _dialog.Messages.Should().BeEmpty();
        AssertLegacyImage(Convert.FromBase64String(file.DisplayPortraitBase64));
        File.ReadAllBytes(path).Should().Equal(original, "browsing the library must not rewrite character files");

        await file.Load();
        var character = CharacterManager.Current.Character;
        character.PortraitFilename.Should().Be(file.DisplayPortraitFilePath);
        file.Save(character);
        AssertDefaultSaved(path, character.PortraitFilename);
        _dialog.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task EmptyDefaultPlaceholder_IsRepairedBeforeSaving()
    {
        var character = await CharacterManager.Current.New(false);
        character.PortraitFilename = Path.Combine(_directory, "default-portrait.png");
        File.WriteAllBytes(character.PortraitFilename, []);
        var file = new CharacterFile(Path.Combine(_directory, "empty-default.dnd5e"));

        file.Save(character);

        AssertDefaultSaved(file.FilePath, character.PortraitFilename);
        AssertLegacyImage(File.ReadAllBytes(character.PortraitFilename));
    }

    [Fact]
    public async Task EmptyDefaultInOldLocation_UsesRepairedLocalPortraitOnLoad()
    {
        string oldDirectory = Path.Combine(_directory, "old-cache");
        Directory.CreateDirectory(oldDirectory);
        string oldPortrait = Path.Combine(oldDirectory, "default-portrait.png");
        File.WriteAllBytes(oldPortrait, []);
        var document = Fixture();
        document.SelectSingleNode("/character/display-properties/portrait/local")!.InnerText = oldPortrait;
        document.SelectSingleNode("/character/build/appearance/portrait")!.InnerText = oldPortrait;
        string path = Path.Combine(_directory, "old-placeholder.dnd5e");
        document.Save(path);
        var file = new CharacterFile(path);

        await file.Load();
        var character = CharacterManager.Current.Character;
        character.PortraitFilename.Should().Be(Path.Combine(_directory, "default-portrait.png"));
        file.Save(character);

        AssertDefaultSaved(path, character.PortraitFilename);
        _dialog.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ExplicitCustomPortrait_IsPreservedWhenSaving()
    {
        var character = await CharacterManager.Current.New(false);
        character.PortraitFilename = Path.Combine(_directory, "custom.png");
        byte[] image = Convert.FromBase64String(CustomImage);
        File.WriteAllBytes(character.PortraitFilename, image);
        var file = new CharacterFile(Path.Combine(_directory, "custom.dnd5e"));

        file.Save(character);

        var saved = new XmlDocument();
        saved.Load(file.FilePath);
        saved.SelectSingleNode("/character/display-properties/portrait/base64")!.InnerText.Should().Be(CustomImage);
        File.ReadAllBytes(character.PortraitFilename).Should().Equal(image);
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"Z:\another-machine\portraits\custom.png")]
    public async Task EmbeddedCustomPortrait_IsRestoredAndResavedInsteadOfDefault(string missingPath)
    {
        var document = Fixture();
        document.SelectSingleNode("/character/display-properties/portrait/local")!.InnerText = missingPath;
        document.SelectSingleNode("/character/display-properties/portrait/base64")!.InnerText = CustomImage;
        document.SelectSingleNode("/character/build/appearance/portrait")!.InnerText = missingPath;
        string path = Path.Combine(_directory, "embedded.dnd5e");
        document.Save(path);
        var file = new CharacterFile(path);

        await file.Load();
        var character = CharacterManager.Current.Character;
        File.ReadAllBytes(character.PortraitFilename).Should().Equal(Convert.FromBase64String(CustomImage));
        file.Save(character);

        document.Load(path);
        document.SelectSingleNode("/character/display-properties/portrait/base64")!.InnerText.Should().Be(CustomImage);
        document.SelectSingleNode("/character/build/appearance/portrait")!.InnerText.Should().Be(character.PortraitFilename);
        _dialog.Messages.Should().BeEmpty();
    }

    private static XmlDocument Fixture()
    {
        var document = new XmlDocument();
        document.Load(ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e"));
        return document;
    }

    private static void AssertDefaultSaved(string path, string portrait)
    {
        Path.GetFileName(portrait).Should().Be("default-portrait.png");
        var saved = new XmlDocument();
        saved.Load(path);
        saved.SelectSingleNode("/character/build/appearance/portrait")!.InnerText.Should().Be(portrait);
        saved.SelectSingleNode("/character/display-properties/portrait/local")!.InnerText.Should().Be(portrait);
        AssertLegacyImage(Convert.FromBase64String(saved.SelectSingleNode("/character/display-properties/portrait/base64")!.InnerText));
    }

    private static void AssertLegacyImage(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).Should().Be(LegacyPortraitHash);

    private sealed class CapturingDialog : IMessageDialogService
    {
        public List<string> Messages { get; } = [];
        public void Show(string message, string? caption = null) => Messages.Add(message);
        public void ShowException(Exception ex, string? message = null, string? caption = null) => Messages.Add(ex.Message);
        public bool Confirm(string message, string? caption = null) => false;
    }
}
