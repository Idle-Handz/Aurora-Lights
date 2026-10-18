using System.Text.Json;
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

public sealed class SidekickPendingChoiceTests
{
    private static void Choose(SidekickProfile p, string key, string value) => SidekickChoices.Change(p, new(key, value));
    private static void Level(SidekickProfile p, int level) { p.Set("level", level.ToString()); SidekickProgression.Refresh(p); }
    private static SidekickProfile Expert()
    {
        var p = SidekickService.Create("sidekick-template:expert");
        Choose(p, "save", "Dexterity");
        string[] skills = ["Acrobatics", "Athletics", "Arcana", "Insight", "Perception"];
        for (int i = 0; i < skills.Length; i++) Choose(p, "skill:" + i, skills[i]);
        return p;
    }

    [Fact]
    public void UnlockedChoicesAppearClearWhenCompletedAndStayHiddenAboveCurrentLevel()
    {
        var p = Expert();
        SidekickChoices.Pending(p).Should().BeEmpty();
        Level(p, 3);
        SidekickChoices.Pending(p).Select(c => c.Key).Should().Equal("expertise:3:0", "expertise:3:1");
        Choose(p, "expertise:3:0", "Acrobatics"); Choose(p, "expertise:3:1", "Athletics");
        Level(p, 4);
        SidekickChoices.Pending(p).Select(c => c.Key).Should().Equal("asi:4:0", "asi:4:1");
        Choose(p, "asi:4:0", "Dexterity");
        SidekickChoices.Pending(p).Select(c => c.Key).Should().Equal("asi:4:1");
        Choose(p, "asi:4:1", "Dexterity"); SidekickChoices.Pending(p).Should().BeEmpty();
        SidekickService.MatchOwnerLevel(p, 8);
        SidekickChoices.Pending(p).Select(c => c.Key).Should().Equal("asi:8:0", "asi:8:1");
        SidekickService.MatchOwnerLevel(p, 4); SidekickChoices.Pending(p).Should().BeEmpty();
        p.FollowOwnerLevel = false; Level(p, 8);
        SidekickChoices.Pending(p).Should().HaveCount(2, "manual levels need the same choices");
        p.ProgressionClass = "Custom"; SidekickProgression.Refresh(p);
        SidekickChoices.Pending(p).Should().BeEmpty();
    }

    [Fact]
    public void InitialProficienciesToolsAndLaterExpertChoicesAreFlaggedWithoutMutatingProfiles()
    {
        var p = SidekickService.Create("sidekick-template:expert");
        SidekickChoices.Pending(p).Should().HaveCount(6);
        Choose(p, "base:weapon-training", "Yes");
        SidekickChoices.Pending(p).Where(c => c.Key.StartsWith("tool:")).Should().HaveCount(2);
        Level(p, 18);
        SidekickChoices.Pending(p).Select(c => c.Key).Should().Contain("sharp-mind").And.Contain("expertise:15:0").And.NotContain("asi:19:0");
        p.Set("hp", "99"); p.Set("notes", "Keep this.");
        string choices = JsonSerializer.Serialize(p.Choices);
        var fields = p.Fields.ToDictionary(k => k.Key, v => v.Value);
        CreatureManagementService.Model(p).PendingChoices.Should().NotBeEmpty();
        JsonSerializer.Serialize(p.Choices).Should().Be(choices); p.Fields.Should().BeEquivalentTo(fields);
        var legacy = new SidekickProfile { ProgressionClass = "Warrior" };
        legacy.Set("level", "4");
        SidekickChoices.Pending(legacy).Should().NotBeEmpty(); legacy.Choices.Should().BeNull();
        SidekickChoices.Pending(new SidekickProfile { Id = "companion-instance:test", ProgressionClass = "Warrior" }).Should().BeEmpty();
    }

    [Fact]
    public void NewSpellsCantripsAndEmpoweredSchoolAreRequiredButOptionalReplacementsAreNot()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var catalog = DataManager.Current.ElementsCollection; var original = catalog.ToArray();
        try
        {
            catalog.Clear();
            foreach (var (id, level) in new[] { ("c1", 0), ("c2", 0), ("c3", 0), ("s1", 1), ("s2", 1) })
            {
                var spell = new Spell { ElementHeader = new ElementHeader(id, "Spell", "Choice test", id), Level = level, MagicSchool = "Evocation" };
                spell.Supports.Add("Wizard"); catalog.Add(spell);
            }
            var p = SidekickService.Create("sidekick-template:spellcaster");
            Choose(p, "save", "Wisdom"); Choose(p, "skill:0", "Arcana"); Choose(p, "skill:1", "History");
            SidekickChoices.Pending(p).Select(c => c.Key).Should().Equal("cantrip:0", "cantrip:1", "spell:0");
            Choose(p, "cantrip:0", "c1"); Choose(p, "cantrip:1", "c2"); Choose(p, "spell:0", "s1");
            SidekickChoices.Pending(p).Should().BeEmpty();
            Level(p, 2); SidekickChoices.Pending(p).Select(c => c.Key).Should().Equal("spell:1");
            Choose(p, "spell:1", "s2"); SidekickChoices.Pending(p).Should().BeEmpty("replacing a spell on level-up is optional");
            Level(p, 4);
            SidekickChoices.Pending(p).Select(c => c.Key).Should().BeEquivalentTo(new[] { "asi:4:0", "asi:4:1", "cantrip:2", "spell:2" });
            Level(p, 14); SidekickChoices.Pending(p).Select(c => c.Key).Should().Contain("field:empowered-school");
            p.Set("empowered-school", "Evocation");
            SidekickChoices.Pending(p).Select(c => c.Key).Should().NotContain("field:empowered-school");
            Level(p, 1); SidekickChoices.Pending(p).Should().BeEmpty();
            catalog.Remove(catalog.GetElement("s1")!);
            SidekickChoices.Pending(p).Select(c => c.Key).Should().Equal(new[] { "spell:0" }, "unavailable selections need a replacement");
        }
        finally { catalog.Clear(); foreach (var element in original) catalog.Add(element); }
    }
    [Fact]
    public void OwnerLevelPreviewFindsNewSpellsAndCantripsWithoutChangingSavedState()
    {
        WithSpellcaster(p =>
        {
            Choose(p, "base:hp", "12"); Choose(p, "base:hit-dice", "2d8"); Choose(p, "hp:method", "Roll");
            string original = JsonSerializer.Serialize(p);
            for (int i = 0; i < 3; i++)
            {
                SidekickChoices.Pending(p, 2).Select(c => c.Key).Should().Equal("spell:1");
                SidekickChoices.Pending(p, 4).Select(c => c.Key).Should().BeEquivalentTo(
                    "asi:4:0", "asi:4:1", "cantrip:2", "spell:1", "spell:2");
            }
            JsonSerializer.Serialize(p).Should().Be(original, "the badge must not write fields, defaults, revisions, choices, or HP rolls");
            p.Choices!.HitPoints!.Gains.Should().BeEmpty();
            p.FollowOwnerLevel = false;
            SidekickChoices.Pending(p, 4).Should().BeEmpty("a manually leveled sidekick ignores its owner's level");
            Level(p, 2);
            SidekickChoices.Pending(p, 1).Select(c => c.Key).Should().Equal("spell:1");
            p.FollowOwnerLevel = true;
            original = JsonSerializer.Serialize(p);
            SidekickChoices.Pending(p, 1).Should().BeEmpty("leveling down hides future spell slots");
            SidekickChoices.Pending(p, 5).Select(c => c.Key).Should().Contain("spell:3");
            JsonSerializer.Serialize(p).Should().Be(original, "previewing also preserves existing rolls and does not roll future levels");
        });
    }

    [Fact]
    public void OwnerLevelPreviewUsesCurrentSpellEligibilityAndHonorsCustomLimits()
    {
        WithSpellcaster(p =>
        {
            Level(p, 5);
            Choose(p, "spell:0", "s3"); // A second-level spell.
            SidekickChoices.Pending(p, 4).Select(c => c.Key).Should().Contain("spell:0").And.NotContain("spell:3");
            Level(p, 4);
            SidekickChoices.Pending(p, 5).Select(c => c.Key).Should().NotContain("spell:0").And.Contain("spell:3");
            p.Set("spell-slots", "1st: 2");
            SidekickChoices.Pending(p, 5).Select(c => c.Key).Should().Contain("spell:0", "custom slots still restrict spell eligibility");
            p.Set("cantrips-known", "2"); p.Set("spells-known", "0");
            string original = JsonSerializer.Serialize(p);
            SidekickChoices.Pending(p, 10).Where(c => c.Key.StartsWith("cantrip:") || c.Key.StartsWith("spell:")).Should().BeEmpty();
            JsonSerializer.Serialize(p).Should().Be(original, "custom limits remain unchanged during a badge preview");
        });
    }

    private static void WithSpellcaster(Action<SidekickProfile> test)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var catalog = DataManager.Current.ElementsCollection; var original = catalog.ToArray();
        try
        {
            catalog.Clear();
            foreach (var (id, level) in new[] { ("c1", 0), ("c2", 0), ("c3", 0), ("s1", 1), ("s2", 1), ("s3", 2) })
            {
                var spell = new Spell { ElementHeader = new ElementHeader(id, "Spell", "Choice test", id), Level = level, MagicSchool = "Evocation" };
                spell.Supports.Add("Wizard"); catalog.Add(spell);
            }
            var p = SidekickService.Create("sidekick-template:spellcaster");
            Choose(p, "save", "Wisdom"); Choose(p, "skill:0", "Arcana"); Choose(p, "skill:1", "History");
            Choose(p, "cantrip:0", "c1"); Choose(p, "cantrip:1", "c2"); Choose(p, "spell:0", "s1");
            SidekickChoices.Pending(p).Should().BeEmpty();
            test(p);
        }
        finally { catalog.Clear(); foreach (var element in original) catalog.Add(element); }
    }
}

public sealed class SidekickPendingChoiceComponentTests : BunitContext
{
    [Fact]
    public void CardOpensTheFirstUnfinishedChoiceAndHidesWarningWhenDone()
    {
        string? target = null;
        var pending = new[] { new SidekickPendingChoice("asi:4:0", "Level 4 improvement", "Ability improvements"), new SidekickPendingChoice("spell:2", "Known spell 3", "Known spells") };
        var cut = Render<CreatureManagementCard>(p => p.Add(c => c.Model, new CreatureManagementModel { Name = "Rook", PendingChoices = pending })
            .Add(c => c.OnResolveChoices, (string key) => target = key));
        cut.Find(".creature-pending").TextContent.Should().Contain("Choices needed (2)").And.Contain("Known spells: 1");
        cut.Find("[data-action=finish-choices]").Click(); target.Should().Be("asi:4:0");
        cut.Render(p => p.Add(c => c.Busy, true));
        cut.Find("[data-action=finish-choices]").HasAttribute("disabled").Should().BeTrue();
        cut.Render(p => p.Add(c => c.Model, new CreatureManagementModel { Name = "Rook" }));
        cut.FindAll(".creature-pending").Should().BeEmpty();
    }

    [Fact]
    public void EditorLinksTargetTheUnfinishedControlsAndUpdateAsChoicesAreCompleted()
    {
        var p = SidekickService.Create("sidekick-template:expert");
        p.Set("level", "4"); SidekickProgression.Refresh(p);
        string? target = null;
        var cut = Render<SidekickChoicesPanel>(args => args.Add(c => c.Model, SidekickChoices.Model(p))
            .Add(c => c.OnJumpToChoice, (string key) => target = key));
        cut.Find("button[data-sidekick-jump='asi:4:0']").Click(); target.Should().Be("asi:4:0");
        cut.Find("[data-sidekick-choice='asi:4:0']").GetAttribute("data-choice-pending").Should().Be("true");
        SidekickChoices.Change(p, new("asi:4:0", "Dexterity"));
        cut.Render(args => args.Add(c => c.Model, SidekickChoices.Model(p)));
        cut.Find("[data-sidekick-choice='asi:4:0']").GetAttribute("data-choice-pending").Should().Be("false");
        cut.Find("button[data-sidekick-jump='asi:4:1']").Click(); target.Should().Be("asi:4:1");
        cut.Render(args => args.Add(c => c.Busy, true));
        cut.FindAll(".sidekick-pending button").Should().OnlyContain(b => b.HasAttribute("disabled"));
    }
}
