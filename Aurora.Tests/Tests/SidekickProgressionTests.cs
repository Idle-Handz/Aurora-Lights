using Aurora.Reflections.Services;

namespace Aurora.Tests.Tests;

public sealed class SidekickProgressionTests
{
    [Fact]
    public void WarriorMilestonesUseThePublishedProgressionAndRemoveFeaturesOnLevelDown()
    {
        var profile = SidekickService.Create("sidekick-template:warrior");
        AtLevel(profile, 10).Should().Contain("Improved Defense").And.Contain("2 attacks")
            .And.NotContain("Level 10 — Ability Score Improvement").And.NotContain("Indomitable");
        AtLevel(profile, 14).Should().Contain("Level 14 — Ability Score Improvement")
            .And.Contain("Indomitable (1 use)").And.Contain("1d10 + 14 HP");
        AtLevel(profile, 15).Should().Contain("Extra Attack (3 attacks)");
        AtLevel(profile, 17).Should().Contain("Indomitable (1 use)");
        AtLevel(profile, 18).Should().Contain("Indomitable (2 uses)").And.Contain("Second Wind (1 use)");
        AtLevel(profile, 20).Should().Contain("Second Wind (2 uses)").And.Contain("1d10 + 20 HP");
        profile.Get("proficiency").Should().Be("6");
        AtLevel(profile, 1).Should().Contain("Martial Role — Attacker").And.NotContain("Second Wind")
            .And.NotContain("Extra Attack").And.NotContain("Improved Defense");
        profile.Get("proficiency").Should().Be("2");
        profile.Set("martial-role", "Defender");
        SidekickProgression.Refresh(profile);
        profile.Get("class-features").Should().Contain("impose disadvantage").And.NotContain("Add +2 to all attack rolls");
    }

    [Fact]
    public void ExpertUsesSupportFeaturesAndItsOwnAbilityImprovementLevels()
    {
        var profile = SidekickService.Create("sidekick-template:expert");
        AtLevel(profile, 6).Should().Contain("Helpful").And.Contain("Cunning Action")
            .And.Contain("Expertise").And.Contain("Coordinated Strike").And.NotContain("Extra Attack");
        AtLevel(profile, 10).Should().Contain("Evasion").And.Contain("Level 10 — Ability Score Improvement");
        AtLevel(profile, 15).Should().Contain("Inspiring Help (1d6)").And.Contain("Reliable Talent")
            .And.Contain("Expertise (two more skills)").And.NotContain("Level 14 — Ability Score Improvement");
        AtLevel(profile, 20).Should().Contain("Sharp Mind").And.Contain("Inspiring Help (2d6)")
            .And.Contain("Level 19 — Ability Score Improvement");
    }

    // Expected values transcribed from the published table, including the unusual early spell counts.
    [Theory]
    [InlineData(1, 2, 1, "1st: 2")]
    [InlineData(2, 2, 2, "1st: 2")]
    [InlineData(3, 2, 3, "1st: 3")]
    [InlineData(4, 3, 3, "1st: 3")]
    [InlineData(5, 3, 4, "1st: 4; 2nd: 2")]
    [InlineData(6, 3, 4, "1st: 4; 2nd: 2")]
    [InlineData(7, 3, 5, "1st: 4; 2nd: 3")]
    [InlineData(8, 3, 5, "1st: 4; 2nd: 3")]
    [InlineData(9, 3, 6, "1st: 4; 2nd: 3; 3rd: 2")]
    [InlineData(10, 4, 6, "1st: 4; 2nd: 3; 3rd: 2")]
    [InlineData(11, 4, 7, "1st: 4; 2nd: 3; 3rd: 3")]
    [InlineData(12, 4, 7, "1st: 4; 2nd: 3; 3rd: 3")]
    [InlineData(13, 4, 8, "1st: 4; 2nd: 3; 3rd: 3; 4th: 1")]
    [InlineData(14, 4, 8, "1st: 4; 2nd: 3; 3rd: 3; 4th: 1")]
    [InlineData(15, 4, 9, "1st: 4; 2nd: 3; 3rd: 3; 4th: 2")]
    [InlineData(16, 4, 9, "1st: 4; 2nd: 3; 3rd: 3; 4th: 2")]
    [InlineData(17, 4, 10, "1st: 4; 2nd: 3; 3rd: 3; 4th: 3; 5th: 1")]
    [InlineData(18, 4, 10, "1st: 4; 2nd: 3; 3rd: 3; 4th: 3; 5th: 1")]
    [InlineData(19, 4, 11, "1st: 4; 2nd: 3; 3rd: 3; 4th: 3; 5th: 2")]
    [InlineData(20, 4, 11, "1st: 4; 2nd: 3; 3rd: 3; 4th: 3; 5th: 2")]
    public void SpellcasterUsesItsPublishedSpellTable(int level, int cantrips, int spells, string slots)
    {
        var profile = SidekickService.Create("sidekick-template:spellcaster");
        AtLevel(profile, level);
        profile.Get("cantrips-known").Should().Be(cantrips.ToString());
        profile.Get("spells-known").Should().Be(spells.ToString());
        profile.Get("spell-slots").Should().Be(slots);
    }

    [Theory]
    [InlineData("Mage", "Intelligence", "14", "+6", "wizard")]
    [InlineData("Healer", "Wisdom", "13", "+5", "cleric and druid")]
    [InlineData("Prodigy", "Charisma", "15", "+7", "bard and warlock")]
    public void SpellcasterRolesUseTheirOwnAbilityAndLists(string role, string ability, string dc, string attack, string list)
    {
        var profile = SidekickService.Create("sidekick-template:spellcaster");
        profile.Set("intelligence", "16");
        profile.Set("wisdom", "14");
        profile.Set("charisma", "18");
        profile.Set("spellcasting-role", role);
        AtLevel(profile, 5).Should().Contain(list);
        profile.Get("spellcasting-ability").Should().Be(ability);
        profile.Get("spell-save-dc").Should().Be(dc);
        profile.Get("spell-attack").Should().Be(attack);
        AtLevel(profile, 6).Should().Contain("Potent Cantrips");
        profile.Set("empowered-school", "Evocation");
        AtLevel(profile, 14).Should().Contain("Chosen school: Evocation");
        AtLevel(profile, 18).Should().Contain("Level 18 — Ability Score Improvement").And.NotContain("Focused Casting");
        AtLevel(profile, 20).Should().Contain("Focused Casting").And.NotContain("Level 19 — Ability Score Improvement");
    }

    [Fact]
    public void OverridesAndAdditionalFeaturesSurviveLevelChangesAndCanResumeFollowingDefaults()
    {
        var profile = SidekickService.Create("sidekick-template:spellcaster");
        profile.Set("proficiency", "9");
        profile.Set("spell-slots", "Homebrew: 7 slots");
        profile.Set("features", "Custom talent");
        profile.Set("spells", "My chosen spells");
        profile.Set("intelligence", "16");
        AtLevel(profile, 8);
        profile.Get("proficiency").Should().Be("9");
        profile.Get("spell-save-dc").Should().Be("20", "automatic totals use the customized proficiency");
        profile.Get("spell-slots").Should().Be("Homebrew: 7 slots");
        profile.Get("features").Should().Be("Custom talent");
        profile.Get("spells").Should().Be("My chosen spells");

        SidekickProgression.UseLevelDefault(profile, "proficiency");
        profile.Get("proficiency").Should().Be("3");
        profile.Get("spell-save-dc").Should().Be("14");
        AtLevel(profile, 9);
        profile.Get("proficiency").Should().Be("4");
        profile.Get("spell-save-dc").Should().Be("15");
        profile.Get("spell-slots").Should().Be("Homebrew: 7 slots");
        SidekickProgression.UseLevelDefault(profile, "spell-slots");
        profile.Get("spell-slots").Should().Be("1st: 4; 2nd: 3; 3rd: 2");

        profile.Set("class-features", "My replacement class features");
        AtLevel(profile, 20).Should().Be("My replacement class features");
        SidekickProgression.UseLevelDefault(profile, "class-features");
        profile.Get("class-features").Should().Contain("Focused Casting");
        profile.Get("features").Should().Be("Custom talent");
    }

    [Fact]
    public void OverridesRemainExplicitEvenWhenAMatchingLevelDefaultPassesThrough()
    {
        var profile = SidekickService.Create("sidekick-template:expert");
        profile.Set("proficiency", "3");
        AtLevel(profile, 5);
        SidekickProgression.IsCustomized(profile, "proficiency").Should().BeTrue();
        AtLevel(profile, 9);
        profile.Get("proficiency").Should().Be("3");
        SidekickProgression.UseLevelDefault(profile, "proficiency");
        profile.Get("proficiency").Should().Be("4");
        AtLevel(profile, 13);
        profile.Get("proficiency").Should().Be("5");
    }

    [Fact]
    public void ManualProfilesAndInvalidDraftLevelsDoNotGetOverwritten()
    {
        var profile = SidekickService.Create("sidekick-template:custom");
        profile.Set("features", "My own rules");
        AtLevel(profile, 20).Should().BeEmpty();
        profile.Get("proficiency").Should().Be("2");
        profile.ProgressionClass = "Expert";
        SidekickProgression.Refresh(profile);
        profile.Get("class-features").Should().Contain("Sharp Mind");
        profile.Get("features").Should().Be("My own rules");
        var fields = new Dictionary<string, string>(profile.Fields);
        profile.Set("level", "");
        SidekickProgression.Refresh(profile);
        profile.Get("class-features").Should().Be(fields["class-features"]);
        profile.Get("proficiency").Should().Be("6");
    }

    [Fact]
    public void ChangingClassRemovesAutomaticSpellcastingButKeepsPersonalAdditions()
    {
        var profile = SidekickService.Create("sidekick-template:spellcaster");
        AtLevel(profile, 20);
        profile.Set("spells", "A creature's innate spell");
        profile.ProgressionClass = "Warrior";
        SidekickProgression.Refresh(profile);
        profile.Get("class-features").Should().Contain("Indomitable").And.NotContain("Focused Casting");
        profile.Get("spell-slots").Should().BeEmpty();
        profile.Get("spell-save-dc").Should().BeEmpty();
        profile.Get("spells").Should().Be("A creature's innate spell");
        var copy = profile.Copy();
        AtLevel(copy, 1);
        profile.LevelDefaults["class-features"].Should().Contain("Indomitable", "draft defaults must be independent too");
    }

    private static string AtLevel(SidekickProfile profile, int level)
    {
        profile.Set("level", level.ToString());
        SidekickProgression.Refresh(profile);
        return profile.Get("class-features");
    }
}
