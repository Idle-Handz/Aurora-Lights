using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class CharacterFixtureParityTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public CharacterFixtureParityTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MulticlassPreparedCaster_RestoresProgressionAndPreparedSpells()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = await LoadFixture("multiclass-prepared-caster.dnd5e");
        var manager = CharacterManager.Current;

        manager.Character.Name.Should().Be("Fixture Multiclass Prepared Caster");
        manager.Character.Level.Should().Be(5);
        manager.ClassProgressionManagers.Should().ContainSingle(p =>
            p.IsMainClass && p.ClassElement != null && p.ClassElement.Name == "Barbarian" && p.ProgressionLevel == 3);
        manager.ClassProgressionManagers.Should().ContainSingle(p =>
            p.IsMulticlass && p.ClassElement != null && p.ClassElement.Name == "Druid" && p.ProgressionLevel == 2);

        var timeline = AdvancementTimelineQuery.Build(manager);
        timeline.Should().ContainSingle(t => t.ClassName == "Barbarian" && t.Levels.Count == 3);
        timeline.Should().ContainSingle(t => t.ClassName == "Druid" && t.Levels.Count == 2);

        handler.GetPreparedIds("Druid").Should().Contain("ID_PHB_SPELL_GOODBERRY");
    }

    [Fact]
    public async Task PreparedPaladin_RestoresPreparedSpellsAndEquippedArmor()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = await LoadFixture("prepared-paladin.dnd5e");
        var manager = CharacterManager.Current;

        manager.Character.Name.Should().Be("Fixture Prepared Paladin");
        manager.Character.Level.Should().Be(5);
        manager.ClassProgressionManagers.Should().ContainSingle(p =>
            p.IsMainClass && p.ClassElement != null && p.ClassElement.Name == "Paladin" && p.ProgressionLevel == 5);
        AdvancementTimelineQuery.Build(manager).Should().ContainSingle(t =>
            t.ClassName == "Paladin" && t.Levels.Select(level => level.Level).SequenceEqual(new[] { 1, 2, 3, 4, 5 }));

        handler.GetPreparedIds("Paladin").Should().Contain(new[] {
            "ID_PHB_SPELL_CURE_WOUNDS",
            "ID_PHB_SPELL_SHIELD_OF_FAITH"
        });
        manager.GetElements().Should().Contain(element => element.Id == "ID_WOTC_ARMOR_HEAVY_CHAIN_MAIL");
    }

    [Theory]
    [InlineData("multiclass-prepared-caster.dnd5e")]
    [InlineData("prepared-paladin.dnd5e")]
    public void SanitizedCharacterFixture_DoesNotContainPrivateOrLargeEmbeddedData(string fileName)
    {
        string path = ContentFixture.GetCharacterFixturePath(fileName);
        var document = new XmlDocument();
        document.Load(path);

        TextAt(document, "/character/information/group").Should().Be("Test Fixtures");
        TextAt(document, "/character/display-properties/portrait/base64").Should().BeEmpty();
        TextAt(document, "/character/display-properties/portrait/local").Should().BeEmpty();
        TextAt(document, "/character/build/input/player-name").Should().BeEmpty();
        TextAt(document, "/character/build/input/backstory").Should().BeEmpty();
        File.ReadAllText(path).Should().NotContain(@"C:\Users\");
    }

    private static async Task<TestSpellHandler> LoadFixture(string fileName)
    {
        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await new CharacterFile(ContentFixture.GetCharacterFixturePath(fileName)).Load();
        return handler;
    }

    private static string TextAt(XmlDocument document, string xpath) =>
        document.SelectSingleNode(xpath)?.InnerText ?? string.Empty;
}
