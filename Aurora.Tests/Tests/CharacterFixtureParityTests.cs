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

    [Fact]
    public async Task PreparedDomainCleric_DistinguishesManualAndAlwaysPreparedSpells()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await LoadFixture("prepared-domain-cleric.dnd5e");

        var snapshot = CharacterParitySnapshotter.Capture();
        var cleric = snapshot.Spellcasting.Should().ContainSingle(section => section.Name == "Cleric").Subject;

        snapshot.Level.Should().Be(5);
        snapshot.Combat.ArmorClass.Should().BeGreaterThan(0);
        snapshot.Combat.MaxHp.Should().BeGreaterThan(0);
        snapshot.Combat.FlySpeed.Should().BeGreaterThan(0);
        cleric.PreparedIds.Should().HaveCount(15);
        cleric.AlwaysPreparedIds.Should().NotBeEmpty();
        cleric.AlwaysPreparedIds.Should().Contain("ID_PHB_SPELL_FAERIE_FIRE");
    }

    [Fact]
    public async Task PreparedDomainCleric_ParitySnapshotSurvivesSaveReload()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await LoadFixture("prepared-domain-cleric.dnd5e");
        var original = CharacterParitySnapshotter.Capture();
        var bytes = CharacterManager.Current.File.SerializeCharacter(CharacterManager.Current.Character);
        string tempPath = Path.Combine(Path.GetTempPath(), $"aurora_fixture_parity_{Guid.NewGuid():N}.dnd5e");

        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);
            var handler = new TestSpellHandler();
            SpellcastingSectionContext.Current = handler;
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();

            var reloaded = CharacterParitySnapshotter.Capture();
            reloaded.Level.Should().Be(original.Level);
            reloaded.Combat.Should().Be(original.Combat);
            reloaded.AbilityScores.Should().BeEquivalentTo(original.AbilityScores);
            reloaded.Spellcasting.Should().BeEquivalentTo(original.Spellcasting);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Theory]
    [InlineData("multiclass-prepared-caster.dnd5e")]
    [InlineData("prepared-paladin.dnd5e")]
    [InlineData("prepared-domain-cleric.dnd5e")]
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
