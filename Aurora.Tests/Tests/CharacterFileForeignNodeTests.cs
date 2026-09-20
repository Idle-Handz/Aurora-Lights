using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// The shared writer rebuilds the character document from the character, so anything a client
/// stores at the root would be dropped when another client saves. Aurora Legacy has no
/// re-adding step, so a Legacy save would otherwise discard Reflections' custom features.
/// </summary>
public sealed class CharacterFileForeignNodeTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public CharacterFileForeignNodeTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SavingKeepsRootNodesThisWriterDoesNotProduce()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var document = new XmlDocument();
        document.Load(ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e"));
        var root = document.DocumentElement!;
        var features = document.CreateElement("custom-features");
        var feature = document.CreateElement("feature");
        feature.SetAttribute("id", "ID_INTERNAL_ASI_DEXTERITY");
        features.AppendChild(feature);
        root.AppendChild(features);
        var laterClient = document.CreateElement("some-later-client");
        laterClient.InnerText = "payload";
        root.AppendChild(laterClient);

        string path = Path.Combine(Path.GetTempPath(), $"aurora-foreign-nodes-{Guid.NewGuid():N}.dnd5e");
        try
        {
            document.Save(path);
            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            var file = new CharacterFile(path);
            (await file.Load()).Should().NotBeNull();

            var saved = new XmlDocument();
            using (var bytes = new MemoryStream(file.SerializeCharacter(CharacterManager.Current.Character)))
                saved.Load(bytes);
            var savedRoot = saved.DocumentElement!;

            savedRoot.SelectNodes("custom-features")!.Count.Should().Be(1, "the node must survive exactly once");
            savedRoot.SelectSingleNode("custom-features/feature")!.Attributes!["id"]!.Value
                .Should().Be("ID_INTERNAL_ASI_DEXTERITY");
            savedRoot.SelectSingleNode("some-later-client")!.InnerText.Should().Be("payload",
                "preservation must not be limited to nodes this version knows about");

            // The writer still owns the nodes it produces: they are rebuilt, not copied twice.
            foreach (string owned in new[] { "information", "display-properties", "build", "sources" })
                savedRoot.SelectNodes(owned)!.Count.Should().Be(1, $"{owned} is written from the character");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
