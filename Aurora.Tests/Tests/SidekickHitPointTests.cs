using System.Text.Json;
using Aurora.App.Services;
using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class SidekickHitPointTests
{
    private static void Choose(SidekickProfile p, string key, string value) => SidekickChoices.Change(p, new(key, value));
    private static void Level(SidekickProfile p, int level) { p.Set("level", level.ToString()); SidekickProgression.Refresh(p); }
    private static SidekickProfile Creature(string role = "expert", int con = 14, string dice = "2d8")
    {
        var p = SidekickService.Create("sidekick-template:" + role);
        Choose(p, "base:constitution", con.ToString());
        Choose(p, "base:hp", "13");
        Choose(p, "base:hit-dice", dice);
        return p;
    }

    [Theory]
    [InlineData("expert")]
    [InlineData("warrior")]
    [InlineData("spellcaster")]
    public void AverageGainsHitDiceAndConstitutionChangesRecalculateWithoutCompounding(string role)
    {
        var p = Creature(role);
        p.Get("hp").Should().Be("13"); p.Get("hit-dice").Should().Be("2d8");
        Level(p, 4);
        p.Get("hp").Should().Be("34"); p.Get("hit-dice").Should().Be("5d8");
        Choose(p, "asi:4:0", "Constitution"); Choose(p, "asi:4:1", "Constitution");
        p.Get("hp").Should().Be("39", "the new Constitution bonus also applies to the two original dice");
        for (int i = 0; i < 3; i++) SidekickProgression.Refresh(p);
        p.Get("hp").Should().Be("39");
        Level(p, 2); p.Get("hp").Should().Be("20"); p.Get("hit-dice").Should().Be("3d8");
        Level(p, 4); p.Get("hp").Should().Be("39");
    }

    [Fact]
    public void RolledGainsAreSavedAndNeverRerolledByRefreshClassOrLevelChanges()
    {
        var p = Creature(); Choose(p, "hp:method", "Roll"); Level(p, 6);
        var gains = p.Choices!.HitPoints!.Gains;
        gains.Should().HaveCount(5);
        gains.Values.Should().OnlyContain(g => g.Method == "Roll" && g.Roll >= 1 && g.Roll <= 8);
        string saved = JsonSerializer.Serialize(p.Choices);
        string hp = p.Get("hp");
        Level(p, 2); Level(p, 6);
        p.ProgressionClass = "Warrior"; SidekickProgression.Refresh(p);
        p.Get("hp").Should().Be(hp);
        JsonSerializer.Serialize(p.Choices!.HitPoints).Should().Be(JsonSerializer.Serialize(JsonSerializer.Deserialize<SidekickChoiceState>(saved)!.HitPoints));
        var loaded = p.Copy(); loaded.Choices = JsonSerializer.Deserialize<SidekickChoiceState>(saved);
        SidekickProgression.Refresh(loaded); loaded.Get("hp").Should().Be(hp);
        loaded.Choices!.HitPoints!.Gains.Values.Select(g => g.Roll).Should().Equal(gains.Values.Select(g => g.Roll));
    }

    [Fact]
    public void PerLevelChoicesPhysicalRollsAndDraftCopiesAreIndependent()
    {
        var p = Creature(); Level(p, 3);
        Choose(p, "hp:level:2:method", "Roll"); Choose(p, "hp:level:2:roll", "8");
        p.Get("hp").Should().Be("30");
        Choose(p, "hp:level:2:method", "Average"); p.Get("hp").Should().Be("27");
        Choose(p, "hp:level:2:method", "Roll"); p.Get("hp").Should().Be("30");
        var draft = p.Copy(); Choose(draft, "hp:level:2:roll", "1");
        draft.Get("hp").Should().Be("23"); p.Get("hp").Should().Be("30");
        Choose(p, "hp:method", "Roll"); Level(p, 4);
        p.Choices!.HitPoints!.Gains[2].Roll.Should().Be(8);
        p.Choices.HitPoints.Gains[3].Method.Should().Be("Average", "changing the default only changes new levels");
        p.Choices.HitPoints.Gains[4].Method.Should().Be("Roll");
        Choose(p, "hp:level:2:roll", "9"); SidekickChoices.Errors(p).Should().Contain(e => e.Contains("Level 2: enter a roll from 1 to 8"));
        Choose(p, "hp:level:2:roll", ""); SidekickChoices.Errors(p).Should().Contain(e => e.Contains("Level 2:"));
        Choose(p, "hp:level:2:method", "Average"); SidekickChoices.Errors(p).Should().BeEmpty();
    }

    [Theory]
    [InlineData("1d4", 3)]
    [InlineData("2d6", 4)]
    [InlineData("2 D8 + 4", 5)]
    [InlineData("1d10", 6)]
    [InlineData("1d12", 7)]
    [InlineData("1d20", 11)]
    public void AverageUsesTheCreatureDieRoundedUp(string dice, int average)
    {
        var p = Creature(con: 10, dice: dice); Level(p, 2);
        p.Get("hp").Should().Be((13 + average).ToString());
    }

    [Fact]
    public void EachLevelGainsAtLeastOneHpAndUsesOverriddenConstitution()
    {
        var p = Creature(con: 2, dice: "1d4"); Level(p, 3);
        p.Get("hp").Should().Be("15");
        Choose(p, "hp:level:2:method", "Roll"); Choose(p, "hp:level:2:roll", "1");
        p.Get("hp").Should().Be("15");
        p.Set("constitution", "14"); SidekickProgression.Refresh(p);
        p.Get("hp").Should().Be("27", "base HP gains six Constitution points, then the levels add three and five");
    }

    [Fact]
    public void OldProfilesKeepTheirCurrentLevelAsTheHpBaselineAndRetainOverrides()
    {
        var p = Creature(); Level(p, 5);
        // This is the previous format: its base values already included manually entered level gains.
        p.Choices!.HitPoints = null;
        p.Choices.Base["hp"] = "40"; p.Choices.Base["hit-dice"] = "6d8";
        p.Fields["hp"] = p.LevelDefaults["hp"] = "40";
        p.Fields["hit-dice"] = p.LevelDefaults["hit-dice"] = "6d8";
        SidekickProgression.Refresh(p);
        p.Choices.HitPoints!.BaseLevel.Should().Be(5);
        p.Get("hp").Should().Be("40"); p.Get("hit-dice").Should().Be("6d8");
        Level(p, 6); p.Get("hp").Should().Be("47"); p.Get("hit-dice").Should().Be("7d8");
        p.Set("hp", "99"); p.Set("hit-dice", "9d10"); Level(p, 7);
        p.Get("hp").Should().Be("99"); p.Get("hit-dice").Should().Be("9d10");
        SidekickProgression.UseLevelDefault(p, "hp"); SidekickProgression.UseLevelDefault(p, "hit-dice");
        p.Get("hp").Should().Be("54"); p.Get("hit-dice").Should().Be("8d8");
    }

    [Fact]
    public void BaselineCanBeCorrectedAndIncompleteCreatureStatsDoNotInventDice()
    {
        var p = SidekickService.Create("sidekick-template:expert"); Level(p, 5);
        Choose(p, "base:hp", "20"); p.Get("hp").Should().Be("20");
        p.Choices!.HitPoints!.Gains.Should().BeEmpty();
        SidekickChoices.Model(p).HitPoints!.Help.Should().Contain("Enter Base Maximum HP");
        Choose(p, "hp:base-level", "5"); Choose(p, "base:hit-dice", "3d6");
        Level(p, 6); p.Get("hp").Should().Be("24"); p.Get("hit-dice").Should().Be("4d6");
        Choose(p, "hp:base-level", "4"); p.Get("hp").Should().Be("28");
        Level(p, 3); p.Get("hp").Should().Be("20");
        Level(p, 6); p.Get("hp").Should().Be("28");
    }

    [Fact]
    public void LevelingPreservesCombatDamageAndNewDiceAreAvailableForRest()
    {
        var p = Creature(); CreatureCombat.Damage(p, 4); p.Combat.HitDiceUsed = 1;
        Level(p, 3);
        CreatureCombat.MaximumHp(p).Should().Be(27); CreatureCombat.CurrentHp(p).Should().Be(9);
        CreatureCombat.HitDice(p).Should().Be(4); p.Combat.HitDiceUsed.Should().Be(1);
        CreatureCombat.Rest(p, longRest: true);
        CreatureCombat.CurrentHp(p).Should().Be(27); p.Combat.HitDiceUsed.Should().Be(0);
    }
}

public sealed class SidekickHitPointPanelTests : BunitContext
{
    [Fact]
    public void HpControlsDispatchMethodsAndPhysicalRollsAndRespectBusyState()
    {
        var p = SidekickService.Create("sidekick-template:warrior");
        SidekickChoices.Change(p, new("base:hp", "11")); SidekickChoices.Change(p, new("base:hit-dice", "2d8"));
        SidekickChoices.Change(p, new("hp:method", "Roll")); p.Set("level", "2"); SidekickProgression.Refresh(p);
        SidekickChoiceChange? change = null;
        var cut = Render<SidekickChoicesPanel>(args => args.Add(c => c.Model, SidekickChoices.Model(p))
            .Add(c => c.OnChange, (SidekickChoiceChange c) => change = c));
        cut.FindAll("label").Single(l => l.TextContent.StartsWith("HP for new levels")).QuerySelector("select")!.Change("Average");
        change.Should().Be(new SidekickChoiceChange("hp:method", "Average"));
        cut.FindAll("label").Single(l => l.TextContent.StartsWith("Level 2 HP method")).QuerySelector("select")!.Change("Average");
        change.Should().Be(new SidekickChoiceChange("hp:level:2:method", "Average"));
        cut.FindAll("label").Single(l => l.TextContent.Trim() == "Level 2 roll (d8)").QuerySelector("input")!.Change("6");
        change.Should().Be(new SidekickChoiceChange("hp:level:2:roll", "6"));
        cut.Render(args => args.Add(c => c.Busy, true));
        cut.FindAll("input, select").Should().OnlyContain(e => e.HasAttribute("disabled"));
    }
}
