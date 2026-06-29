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

    [Theory]
    [InlineData("multiclass-prepared-caster.dnd5e")]
    [InlineData("prepared-paladin.dnd5e")]
    [InlineData("prepared-domain-cleric.dnd5e")]
    public async Task CharacterFixture_CoreStateAndSelectedChoicesSurviveSaveReload(string fileName)
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await LoadFixture(fileName);
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
            SelectedChoiceSlots(reloaded).Should().Equal(SelectedChoiceSlots(original));
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
    [InlineData("legacy-edited-arilith.dnd5e")]
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

    [Fact]
    public async Task LegacyEditedFixture_LoadsGroupDisplayAndCoreState()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        string sourcePath = ContentFixture.GetCharacterFixturePath("legacy-edited-arilith.dnd5e");
        var handler = new TestSpellHandler();
        SpellcastingSectionContext.Current = handler;
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var file = new CharacterFile(sourcePath);
        await file.Load();

        file.CollectionGroupName.Should().Be("Test Fixtures");
        file.DisplayName.Should().Be("Fixture Legacy Edited Arilith");
        CharacterManager.Current.Character.Name.Should().Be("Fixture Legacy Edited Arilith");
        CharacterManager.Current.Character.Level.Should().BeGreaterThan(0);
        CharacterManager.Current.GetElements().Should().NotBeEmpty();
    }

    private static IReadOnlyList<string> SelectedChoiceSlots(CharacterParitySnapshot snapshot) =>
        snapshot.SelectionRules
            .Where(rule => rule.SelectedIds.Any(id => !string.IsNullOrWhiteSpace(id)))
            .Select(rule => string.Join("|", new[]
            {
                rule.OwnerId,
                rule.Type,
                rule.Name,
                rule.RequiredLevel.ToString(),
                rule.Number.ToString(),
                string.Join(",", rule.SelectedIds),
            }))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

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
