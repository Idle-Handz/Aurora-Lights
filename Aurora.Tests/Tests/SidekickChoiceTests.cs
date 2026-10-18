using Aurora.App.Services;
using Aurora.Components.Models;
using Aurora.Components.Shared;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class SidekickChoiceTests
{
    private static void Set(SidekickProfile p, string key, string value) => SidekickChoices.Change(p, new(key, value));
    private static void Level(SidekickProfile p, int level) { p.Set("level", level.ToString()); SidekickProgression.Refresh(p); }

    [Fact]
    public void AbilityImprovementsRecalculateStatsWithoutCompoundingAndRespectOverrides()
    {
        var p = SidekickService.Create("sidekick-template:expert");
        Set(p, "base:dexterity", "14"); Set(p, "base:constitution", "14");
        Set(p, "base:ac", "13"); Set(p, "base:armor-dexterity", "Full Dexterity");
        Set(p, "base:hp", "24"); Set(p, "base:hit-dice", "4d8");
        Level(p, 8);
        Set(p, "asi:4:0", "Dexterity"); Set(p, "asi:4:1", "Dexterity");
        Set(p, "asi:8:0", "Constitution"); Set(p, "asi:8:1", "Constitution");
        p.Get("dexterity").Should().Be("16"); p.Get("constitution").Should().Be("16");
        p.Get("initiative").Should().Be("+3"); p.Get("ac").Should().Be("14"); p.Get("hp").Should().Be("84");
        for (int i = 0; i < 4; i++) SidekickProgression.Refresh(p);
        p.Get("hp").Should().Be("84");
        p.Set("ac", "21"); p.Set("skills", "Custom skill bonus");
        Level(p, 3);
        p.Get("dexterity").Should().Be("14"); p.Get("constitution").Should().Be("14"); p.Get("hp").Should().Be("38");
        p.Get("ac").Should().Be("21"); p.Get("skills").Should().Be("Custom skill bonus");
        Level(p, 8); SidekickProgression.UseLevelDefault(p, "ac");
        p.Get("ac").Should().Be("14"); p.Get("dexterity").Should().Be("16");
        p.Set("dexterity", "20"); SidekickProgression.Refresh(p);
        p.Get("initiative").Should().Be("+5", "derived values use the effective override");
    }

    [Fact]
    public void ExpertSkillSaveExpertiseAndToolsUseLevelAndAbilityBonuses()
    {
        var p = SidekickService.Create("sidekick-template:expert");
        Set(p, "base:dexterity", "14"); Set(p, "base:wisdom", "12"); Set(p, "base:weapon-training", "Yes");
        Set(p, "skill:0", "Stealth"); Set(p, "skill:1", "Perception"); Set(p, "save", "Dexterity");
        Set(p, "expertise:3:0", "Stealth"); Set(p, "expertise:3:1", "Perception");
        Set(p, "tool:0", "Thieves' tools"); Set(p, "tool:1", "Lute");
        Level(p, 5);
        p.Get("skills").Should().Contain("Stealth +8").And.Contain("Perception +7");
        p.Get("saves").Should().Contain("Dexterity +5"); p.Get("senses").Should().Contain("passive Perception 17");
        p.Get("proficiencies").Should().Contain("Thieves' tools").And.Contain("Lute");
        Level(p, 2);
        p.Get("skills").Should().Contain("Stealth +4").And.Contain("Perception +3");
        Level(p, 18); Set(p, "sharp-mind", "Wisdom");
        p.Get("saves").Should().Contain("Wisdom +7");
        SidekickChoices.Errors(p).Should().BeEmpty();
    }

    [Fact]
    public void ChoicesRejectCapsDuplicatesAndUnqualifiedExpertise()
    {
        var p = SidekickService.Create("sidekick-template:expert");
        Level(p, 4); Set(p, "base:dexterity", "19");
        Set(p, "asi:4:0", "Dexterity"); Set(p, "asi:4:1", "Dexterity");
        SidekickChoices.Errors(p).Should().Contain(e => e.Contains("above 20"));
        Set(p, "asi:4:1", "Wisdom");
        Set(p, "skill:0", "Stealth"); Set(p, "skill:1", "Stealth");
        SidekickChoices.Errors(p).Should().Contain(e => e.Contains("different new skill"));
        Set(p, "skill:1", "Perception"); Set(p, "expertise:3:0", "Arcana");
        SidekickChoices.Errors(p).Should().Contain(e => e.Contains("proficient skills"));
        Set(p, "expertise:3:0", "Stealth");
        SidekickChoices.Errors(p).Should().BeEmpty();
    }

    [Fact]
    public void ExistingManualTotalsArePreservedUntilTheUserOptsIntoChoiceCalculations()
    {
        var p = new SidekickProfile { ProgressionClass = "Warrior" };
        p.Set("name", "Old sidekick"); p.Set("level", "10"); p.Set("strength", "14");
        p.Set("ac", "18"); p.Set("actions", "Melee Weapon Attack: +8 to hit. Hit: 1d6 + 2 damage.");
        SidekickProgression.Refresh(p);
        SidekickChoices.Initialize(p); SidekickProgression.Refresh(p);
        p.Get("ac").Should().Be("18"); p.Get("actions").Should().Contain("+8 to hit");
        p.Overrides.Should().Contain("ac").And.Contain("actions");
        Set(p, "base:ac", "15"); SidekickProgression.UseLevelDefault(p, "ac");
        p.Get("ac").Should().Be("16");
    }

    [Fact]
    public void WarriorChoicesUpdateWeaponAttacksAndArmorButLeaveSpellAttacksAlone()
    {
        var p = SidekickService.Create("sidekick-template:warrior");
        Set(p, "base:strength", "14"); Set(p, "base:ac", "16");
        Set(p, "base:actions", "Bite\nMelee Weapon Attack: +4 to hit. Hit: 1d6 + 2 piercing damage.\nBow\nRanged Weapon Attack: +2 to hit. Hit: 1d6 piercing damage.\nRay\nRanged Spell Attack: +4 to hit. Hit: 1d6 fire damage.");
        Level(p, 10); Set(p, "asi:4:0", "Strength"); Set(p, "asi:4:1", "Strength");
        p.Get("actions").Should().Contain("Weapon Attack: +9 to hit").And.Contain("1d6 + 3 piercing")
            .And.Contain("Ranged Weapon Attack: +6 to hit").And.Contain("Spell Attack: +4 to hit");
        p.Get("ac").Should().Be("17");
        p.Set("martial-role", "Defender"); SidekickProgression.Refresh(p);
        p.Get("actions").Should().Contain("Weapon Attack: +7 to hit");
    }

    [Fact]
    public void SpellcastingTotalsUseSelectedImprovementsAndClassChangesKeepChoices()
    {
        var p = SidekickService.Create("sidekick-template:spellcaster");
        Set(p, "base:intelligence", "16"); Level(p, 4);
        Set(p, "asi:4:0", "Intelligence"); Set(p, "asi:4:1", "Intelligence");
        p.Get("intelligence").Should().Be("18"); p.Get("spell-save-dc").Should().Be("14"); p.Get("spell-attack").Should().Be("+6");
        p.ProgressionClass = "Warrior"; SidekickProgression.Refresh(p);
        p.Get("intelligence").Should().Be("16"); p.Get("spell-save-dc").Should().BeEmpty();
        p.ProgressionClass = "Spellcaster"; SidekickProgression.Refresh(p);
        p.Get("intelligence").Should().Be("18");
        var draft = p.Copy(); Set(draft, "asi:4:0", "Wisdom");
        p.Get("intelligence").Should().Be("18", "canceling an editor cannot mutate saved choices");
    }

    [Fact]
    public void SpellListsLevelsSourcesDuplicatesAndRoleChangesAreIndependentOfOwnerSpells()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var catalog = DataManager.Current.ElementsCollection;
        var original = catalog.ToArray();
        var spells = new[] { Spell("cantrip", "Light", 0, "Wizard", "Cleric"), Spell("one", "Shield", 1, "Wizard"),
            Spell("two", "Misty Step", 2, "Wizard"), Spell("heal", "Healing Word", 1, "Cleric", "Bard"), Spell("other", "Shield", 1, "Wizard") };
        try
        {
            catalog.Clear(); foreach (var spell in spells) catalog.Add(spell);
            var p = SidekickService.Create("sidekick-template:spellcaster");
            SidekickChoices.EligibleSpells(p, false, spells).Select(s => s.Id).Should().BeEquivalentTo("one", "other");
            var restricted = new BuildSourceRestrictionSnapshot(new HashSet<string> { "one" }, new HashSet<string>());
            SidekickChoices.EligibleSpells(p, false, spells, restricted).Select(s => s.Id).Should().Equal("other");
            Set(p, "spell:0", "one"); Set(p, "cantrip:0", "cantrip");
            p.Get("selected-spells").Should().Contain("Shield").And.Contain("Light");
            Level(p, 5); Set(p, "spell:1", "two");
            p.Get("selected-spells").Should().Contain("Misty Step");
            SidekickChoices.SpellOptions(p, false, 2).Where(s => s.Name == "Shield").Should().OnlyContain(s => s.IsDisabled);
            p.Set("spellcasting-role", "Healer"); SidekickProgression.Refresh(p);
            p.Get("selected-spells").Should().BeEmpty();
            SidekickChoices.EligibleSpells(p, false, spells).Select(s => s.Id).Should().Equal("heal");
            Set(p, "spell:0", "heal");
            p.Set("spellcasting-role", "Mage"); SidekickProgression.Refresh(p);
            p.Get("selected-spells").Should().Contain("Shield").And.Contain("Misty Step").And.NotContain("Healing Word");
            Level(p, 1); p.Get("selected-spells").Should().NotContain("Misty Step");
            Level(p, 5); p.Get("selected-spells").Should().Contain("Misty Step");
        }
        finally { catalog.Clear(); foreach (var element in original) catalog.Add(element); }
    }
    private static Spell Spell(string id, string name, int level, params string[] lists)
    {
        var spell = new Spell { ElementHeader = new ElementHeader(name, "Spell", "Choice test", id), Level = level,
            MagicSchool = "Evocation", CastingTime = "1 action", Range = "60 feet", Duration = "Instantaneous", Description = "<p>Test spell.</p>" };
        spell.Supports.AddRange(lists);
        return spell;
    }
}

public sealed class SidekickChoicesPanelTests : BunitContext
{
    [Fact]
    public void SelectorsSendChoicesAndSpellButtonsWithoutSavingAndHonorBusyState()
    {
        var p = SidekickService.Create("sidekick-template:spellcaster");
        p.Set("level", "4"); SidekickProgression.Refresh(p);
        SidekickChoiceChange? change = null; string? spell = null;
        var cut = Render<SidekickChoicesPanel>(args => args.Add(c => c.Model, SidekickChoices.Model(p))
            .Add(c => c.OnChange, (SidekickChoiceChange c) => change = c).Add(c => c.OnPickSpell, (string key) => spell = key));
        cut.FindAll("label").Single(l => l.TextContent.StartsWith("Level 4: +1 ability (1/2)")).QuerySelector("select")!.Change("Intelligence");
        change.Should().Be(new SidekickChoiceChange("asi:4:0", "Intelligence"));
        cut.Find("button[aria-label='Choose known spell 1']").Click(); spell.Should().Be("spell:0");
        cut.Render(args => args.Add(c => c.Busy, true));
        cut.FindAll("button, input, textarea, select").Should().OnlyContain(e => e.HasAttribute("disabled"));
    }
}
