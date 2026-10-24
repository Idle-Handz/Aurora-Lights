using Aurora.Reflections.Services;

namespace Aurora.Tests.Tests;

public sealed class SidekickOwnerLevelTests
{
    [Theory]
    [InlineData("expert")]
    [InlineData("warrior")]
    [InlineData("spellcaster")]
    [InlineData("custom")]
    public void NewSidekicksFollowOwnerAndManualModeKeepsAnIndependentLevel(string role)
    {
        var p = SidekickService.Create("sidekick-template:" + role);
        p.FollowOwnerLevel.Should().BeTrue();
        SidekickService.MatchOwnerLevel(p, 7).Should().BeTrue(); p.Get("level").Should().Be("7");
        p.FollowOwnerLevel = false;
        p.Set("level", "3"); SidekickProgression.Refresh(p);
        SidekickService.MatchOwnerLevel(p, 12).Should().BeFalse(); p.Get("level").Should().Be("3");
        p.Copy().FollowOwnerLevel.Should().BeFalse();
        p.FollowOwnerLevel = true;
        SidekickService.MatchOwnerLevel(p, 12).Should().BeTrue(); p.Get("level").Should().Be("12");
        SidekickService.MatchOwnerLevel(p, 12).Should().BeFalse("matching again must not create another revision or roll");
    }

    [Fact]
    public void MatchingTotalLevelUpdatesChoicesHpAndSpellsAndKeepsOverrides()
    {
        var p = SidekickService.Create("sidekick-template:spellcaster");
        SidekickChoices.Change(p, new("base:hp", "16"));
        SidekickChoices.Change(p, new("base:hit-dice", "1d6"));
        SidekickChoices.Change(p, new("base:constitution", "14"));
        SidekickChoices.Change(p, new("base:intelligence", "16"));
        SidekickChoices.Change(p, new("asi:4:0", "Intelligence"));
        SidekickChoices.Change(p, new("asi:4:1", "Intelligence"));
        p.Set("ac", "22"); p.Set("notes", "Still my sidekick.");
        SidekickService.MatchOwnerLevel(p, 9);
        p.Get("hp").Should().Be("64"); p.Get("hit-dice").Should().Be("9d6");
        p.Get("proficiency").Should().Be("4"); p.Get("intelligence").Should().Be("18");
        p.Get("spell-save-dc").Should().Be("16"); p.Get("spell-slots").Should().Contain("3rd: 2");
        p.Get("class-features").Should().Contain("Potent Cantrips");
        SidekickService.MatchOwnerLevel(p, 2);
        p.Get("hp").Should().Be("22"); p.Get("intelligence").Should().Be("16");
        p.Get("class-features").Should().NotContain("Potent Cantrips");
        SidekickService.MatchOwnerLevel(p, 9);
        p.Get("hp").Should().Be("64"); p.Get("intelligence").Should().Be("18");
        p.Get("ac").Should().Be("22"); p.Get("notes").Should().Be("Still my sidekick.");
    }

    [Fact]
    public void LegacyHpBaselineIsCapturedBeforeMatchingTheOwner()
    {
        var p = SidekickService.Create("sidekick-template:warrior");
        p.Set("level", "4");
        p.Choices!.Base["hp"] = "30"; p.Choices.Base["hit-dice"] = "4d8";
        p.Set("hp", "30"); p.Set("hit-dice", "4d8");
        p.Choices.HitPoints = null;
        SidekickService.MatchOwnerLevel(p, 9);
        p.Choices.HitPoints!.BaseLevel.Should().Be(4);
        p.Choices.HitPoints.Gains.Keys.Should().BeEquivalentTo(new[] { 5, 6, 7, 8, 9 });
        p.Get("hp").Should().Be("30", "an explicit HP override remains customized");
        SidekickProgression.UseLevelDefault(p, "hp"); p.Get("hp").Should().Be("55");
    }

    [Fact]
    public void MissingOwnerLevelsAndRegularCompanionsAreNotChanged()
    {
        var p = SidekickService.Create("sidekick-template:expert");
        SidekickService.MatchOwnerLevel(p, 0).Should().BeFalse();
        SidekickService.MatchOwnerLevel(p, 21).Should().BeFalse(); p.Get("level").Should().Be("1");
        var companion = new SidekickProfile { Id = "companion-instance:test" };
        companion.Set("level", "4"); SidekickService.MatchOwnerLevel(companion, 9).Should().BeFalse();
        companion.Get("level").Should().Be("4");
    }
}
