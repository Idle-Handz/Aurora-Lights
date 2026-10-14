using Aurora.Components.Models;
using Aurora.Tests.Helpers;
using Aurora.Web.Services;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using System.Reflection;

namespace Aurora.Tests.Tests;

public sealed class WebMagicProfileTests
{
    [Fact]
    public async Task EveryProfileHasItsOwnAbilityLimitsListAndTargetedExtensions()
    {
        await WithProfiles(async (cm, progression) =>
        {
            var wizard = Profile("Wizard", "Intelligence", 3);
            var cleric = Profile("Cleric", "Wisdom", 1);
            progression.SpellcastingInformations.Add(wizard);
            progression.SpellcastingInformations.Add(cleric);
            var extension = Profile("Cleric", "Wisdom", 0);
            extension.IsExtension = true;
            extension.ExtendedSupportedSpellsExpressions.Add(new("ID_WEB_EXTENDED", true));
            progression.SpellcastingInformations.Add(extension);
            AddSpell("ID_WEB_WIZARD", "Wizard spell", "Wizard");
            AddSpell("ID_WEB_CLERIC", "Cleric spell", "Cleric");
            AddSpell("ID_WEB_EXTENDED", "Extended spell", "Other");
            cm.StatisticsCalculator.StatisticValues.GetGroup(wizard.GetSpellcasterSpellSaveStatisticName()).AddValue("fixture", 15);
            cm.StatisticsCalculator.StatisticValues.GetGroup(cleric.GetSpellcasterSpellSaveStatisticName()).AddValue("fixture", 12);

            var model = Build(cm);
            model.Sections.Should().HaveCount(2);
            var first = model.Sections.Single(section => section.Label == "Wizard");
            var second = model.Sections.Single(section => section.Label == "Cleric");
            first.SpellcastingAbility.Should().Be("Intelligence");
            second.SpellcastingAbility.Should().Be("Wisdom");
            first.SpellcastingDc.Should().Be("15");
            second.SpellcastingDc.Should().Be("12");
            first.MaxPrepared.Should().Be(3);
            second.MaxPrepared.Should().Be(1);
            first.SpellLevels.SelectMany(level => level.Spells).Select(spell => spell.Id).Should().Equal("ID_WEB_WIZARD");
            second.SpellLevels.SelectMany(level => level.Spells).Select(spell => spell.Id).Should().BeEquivalentTo("ID_WEB_CLERIC", "ID_WEB_EXTENDED");
            await Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task SingleProfileAndUnattachedExtensionDoNotCreateUnrelatedProfiles(bool extensionOnly, int expected)
    {
        await WithProfiles(async (cm, progression) =>
        {
            var profile = Profile("Wizard", "Intelligence", 2);
            profile.IsExtension = extensionOnly;
            progression.SpellcastingInformations.Add(profile);
            Build(cm).Sections.Should().HaveCount(expected);
            await Task.CompletedTask;
        });
    }

    private static SpellcastingInformation Profile(string name, string ability, int limit)
    {
        var profile = new SpellcastingInformation(new ElementHeader(name, "Class", "Test", "ID_WEB_" + name))
        {
            Name = name, AbilityName = ability, Prepare = true, PrepareFromSpellList = true,
            InitialSupportedSpellsExpression = new(name, true)
        };
        var values = CharacterManager.Current.StatisticsCalculator.StatisticValues;
        values.GetGroup(profile.GetPrepareAmountStatisticName()).AddValue("fixture", limit);
        values.GetGroup(profile.GetSlotStatisticName(1)).AddValue("fixture", 4);
        return profile;
    }

    private static void AddSpell(string id, string name, string supports)
    {
        var spell = new ElementBase(name, "Spell", "Test", id).Construct<Spell>();
        spell.Level = 1;
        spell.Supports.Add(supports);
        DataManager.Current.ElementsCollection.Add(spell);
    }

    private static MagicOverviewModel Build(CharacterManager manager) =>
        (MagicOverviewModel)typeof(WebCharacterEngineService).GetMethod("BuildMagicModel", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [manager.Character])!;

    private static async Task WithProfiles(Func<CharacterManager, ProgressionManager, Task> test)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var cm = CharacterManager.Current;
        var catalog = DataManager.Current.ElementsCollection;
        var originals = catalog.ToArray();
        var previous = SpellcastingSectionContext.Current;
        var previousSelections = SelectionRuleExpanderContext.Current;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        try
        {
            await cm.New(false);
            catalog.Clear();
            var progression = (ProgressionManager)typeof(CharacterManager).GetField("_progressionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cm)!;
            progression.SpellcastingInformations.Clear();
            cm.ReprocessCharacter();
            await test(cm, progression);
        }
        finally
        {
            var progression = (ProgressionManager)typeof(CharacterManager).GetField("_progressionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cm)!;
            progression.SpellcastingInformations.Clear();
            await cm.New(false);
            catalog.Clear();
            foreach (var element in originals) catalog.Add(element);
            SpellcastingSectionContext.Current = previous;
            SelectionRuleExpanderContext.Current = previousSelections;
        }
    }
}
