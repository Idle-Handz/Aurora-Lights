using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

/// <summary>
/// Unit tests for BuildRuleClassifier.Classify — verifies that rule types and owner
/// types route to the correct Build tab bucket regardless of which parent element
/// (race, class, background) grants them.
/// </summary>
public sealed class BuildRuleClassifierTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static BuildRuleBucket Classify(
        string ruleType,
        string ownerType = "",
        string ruleName = "",
        string ownerName = "",
        bool hasClassManager = false)
        => BuildRuleClassifier.Classify(ruleType, ruleName.Length > 0 ? ruleName : ruleType, ownerType, ownerName, hasClassManager);

    private static OriginAbilityScoreSource ClassifyOriginAsi(
        string ruleType,
        string ownerType = "",
        string ruleName = "",
        string ownerName = "",
        bool hasClassManager = false)
        => BuildRuleClassifier.ClassifyOriginAbilityScoreSource(
            ruleType,
            ruleName.Length > 0 ? ruleName : ruleType,
            ownerType,
            ownerName,
            hasClassManager);

    // ── Language rules ────────────────────────────────────────────────────────────

    [Fact]
    public void Language_FromRace_RoutesToLanguage()
        => Classify("Language", ownerType: "Race").Should().Be(BuildRuleBucket.Language);

    [Fact]
    public void Language_FromSubRace_RoutesToLanguage()
        => Classify("Language", ownerType: "Sub Race").Should().Be(BuildRuleBucket.Language);

    [Fact]
    public void Language_FromClass_RoutesToLanguage()
        => Classify("Language", hasClassManager: true).Should().Be(BuildRuleBucket.Language);

    [Fact]
    public void Language_FromBackground_RoutesToLanguage()
        => Classify("Language", ownerType: "Background").Should().Be(BuildRuleBucket.Language);

    // ── Proficiency rules ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Proficiency")]
    [InlineData("Skill")]
    [InlineData("Tool Proficiency")]
    [InlineData("Armor Proficiency")]
    [InlineData("Weapon Proficiency")]
    [InlineData("Expertise")]
    public void Proficiency_FromRace_RoutesToProficiency(string ruleType)
        => Classify(ruleType, ownerType: "Racial Trait").Should().Be(BuildRuleBucket.Proficiency);

    [Theory]
    [InlineData("Proficiency")]
    [InlineData("Skill")]
    [InlineData("Tool Proficiency")]
    public void Proficiency_FromClass_RoutesToProficiency(string ruleType)
        => Classify(ruleType, hasClassManager: true).Should().Be(BuildRuleBucket.Proficiency);

    [Theory]
    [InlineData("Proficiency")]
    [InlineData("Skill")]
    public void Proficiency_FromBackground_RoutesToProficiency(string ruleType)
        => Classify(ruleType, ownerType: "Background Feature").Should().Be(BuildRuleBucket.Proficiency);

    // ── Feat rules ────────────────────────────────────────────────────────────────

    [Fact]
    public void Feat_FromRace_RoutesToFeat()
        => Classify("Feat", ownerType: "Variant").Should().Be(BuildRuleBucket.Feat);

    [Fact]
    public void VariantHumanFeat_RoutesToFeatAndUsesRacialGroup()
    {
        Classify("Feat", ownerType: "Variant", ownerName: "Human Variant")
            .Should().Be(BuildRuleBucket.Feat);

        BuildRuleClassifier.GetFeatGroupLabel("Variant").Should().Be("Racial");
    }

    [Fact]
    public void FeatFeature_FromRace_RoutesToFeat()
        => Classify("Feat Feature", ownerType: "Race").Should().Be(BuildRuleBucket.Feat);

    [Fact]
    public void Feat_FromClass_RoutesToFeat()
        => Classify("Feat", hasClassManager: true).Should().Be(BuildRuleBucket.Feat);

    [Fact]
    public void Feat_FromBackground_RoutesToFeat()
        => Classify("Feat", ownerType: "Background").Should().Be(BuildRuleBucket.Feat);

    // ── Race-owned non-typed rules ────────────────────────────────────────────────

    [Theory]
    [InlineData("Race")]
    [InlineData("Sub Race")]
    [InlineData("Racial Trait")]
    [InlineData("Dragonmark")]
    [InlineData("Variant")]
    [InlineData("Race Variant")]
    [InlineData("Heritage")]
    [InlineData("Lineage")]
    public void RaceType_NoClassManager_RoutesToRace(string ruleType)
        => Classify(ruleType).Should().Be(BuildRuleBucket.Race);

    [Fact]
    public void RacialTrait_OwnedByRace_RoutesToRace()
        => Classify("Racial Trait", ownerType: "Race").Should().Be(BuildRuleBucket.Race);

    // ── Class-owned non-typed rules ───────────────────────────────────────────────

    [Theory]
    [InlineData("Class")]
    [InlineData("Archetype")]
    [InlineData("Class Feature")]
    [InlineData("Archetype Feature")]
    [InlineData("Multiclass")]
    public void ClassTypes_NoClassManager_RouteToClass(string ruleType)
        => Classify(ruleType).Should().Be(BuildRuleBucket.Class);

    [Theory]
    [InlineData("Class Feature")]
    [InlineData("Archetype Feature")]
    [InlineData("Multiclass")]
    public void ClassTypes_WithClassManager_RouteToClass(string ruleType)
        => Classify(ruleType, hasClassManager: true).Should().Be(BuildRuleBucket.Class);

    [Fact]
    public void ClassFeature_OwnedByClass_NoClassManager_RoutesToClass()
        => Classify("Class Feature", ownerType: "Class").Should().Be(BuildRuleBucket.Class);

    // ── Background-owned rules ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Background")]
    [InlineData("Background Feature")]
    [InlineData("Background Variant")]
    [InlineData("Deity")]
    [InlineData("Alignment")]
    public void BackgroundType_RoutesToBackground(string ruleType)
        => Classify(ruleType).Should().Be(BuildRuleBucket.Background);

    [Fact]
    public void CustomRule_OwnedByBackground_RoutesToBackground()
        => Classify("Custom", ownerType: "Background").Should().Be(BuildRuleBucket.Background);

    // ── Ability Score Improvement ─────────────────────────────────────────────────

    [Fact]
    public void ASI_RoutesToAbilityScores()
        => Classify("Ability Score Improvement").Should().Be(BuildRuleBucket.AbilityScores);

    [Fact]
    public void RacialTrait_NamedAbilityScore_RoutesToAbilityScores()
        => Classify("Racial Trait", ownerType: "Race", ruleName: "Ability Score Increase")
               .Should().Be(BuildRuleBucket.AbilityScores);

    [Fact]
    public void VariantHumanAbilityScoreIncrease_RoutesToAbilityScores()
        => Classify(
                "Racial Trait",
                ownerType: "Variant",
                ruleName: "Ability Score Increase (Human Variant)",
                ownerName: "Human Variant")
            .Should().Be(BuildRuleBucket.AbilityScores);

    [Fact]
    public void RacialAbilityScoreIncrease_IsRaceOriginAsi()
        => ClassifyOriginAsi(
                "Racial Trait",
                ownerType: "Race",
                ruleName: "Ability Score Increase")
            .Should().Be(OriginAbilityScoreSource.Race);

    [Fact]
    public void BackgroundAbilityScoreImprovement_IsBackgroundOriginAsi()
        => ClassifyOriginAsi(
                "Ability Score Improvement",
                ownerType: "Background",
                ruleName: "Ability Score Increase")
            .Should().Be(OriginAbilityScoreSource.Background);

    [Fact]
    public void BackgroundOwnedAbilityScoreRule_RoutesToAbilityScores()
        => Classify(
                "Background Feature",
                ownerType: "Background",
                ruleName: "Ability Score Increase")
            .Should().Be(BuildRuleBucket.AbilityScores);

    [Fact]
    public void ClassAbilityScoreImprovement_IsNotOriginAsi()
        => ClassifyOriginAsi(
                "Ability Score Improvement",
                ownerType: "Class Feature",
                ruleName: "Ability Score Improvement",
                hasClassManager: true)
            .Should().Be(OriginAbilityScoreSource.None);

    [Fact]
    public void BackgroundFeat_IsNotOriginAsi()
        => ClassifyOriginAsi(
                "Feat",
                ownerType: "Background",
                ruleName: "Origin Feat")
            .Should().Be(OriginAbilityScoreSource.None);

    // ── Companion rules ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Companion")]
    [InlineData("Companion Feature")]
    public void Companion_RoutesToCompanion(string ruleType)
        => Classify(ruleType).Should().Be(BuildRuleBucket.Companion);

    [Fact]
    public void Companion_FromClass_RoutesToCompanion()
        => Classify("Companion", hasClassManager: true).Should().Be(BuildRuleBucket.Companion);

    [Fact]
    public void Companion_FromRace_RoutesToCompanion()
        => Classify("Companion", ownerType: "Race").Should().Be(BuildRuleBucket.Companion);

    // ── Overflow ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Unknown_NoOwner_RoutesToOverflow()
        => Classify("Some Custom Type").Should().Be(BuildRuleBucket.Overflow);

    // ── GetFeatGroupLabel ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Race",    "Racial")]
    [InlineData("Variant", "Racial")]
    [InlineData("Background", "Background")]
    [InlineData("Class",   "Class")]
    [InlineData("Archetype", "Class")]
    [InlineData("",        "")]
    [InlineData("Unknown", "")]
    public void GetFeatGroupLabel_ReturnsExpected(string ownerType, string expected)
        => BuildRuleClassifier.GetFeatGroupLabel(ownerType).Should().Be(expected);

    [Theory]
    [InlineData("Personality Trait")]
    [InlineData("Ideal")]
    [InlineData("Bond")]
    [InlineData("Flaw")]
    [InlineData("Variant Feature")]
    public void FlavorSelectionLabels_AreOptional(string label)
        => BuildRuleClassifier.IsOptionalFlavorSelection(label).Should().BeTrue();

    [Theory]
    [InlineData("Race")]
    [InlineData("Class")]
    [InlineData("Background")]
    [InlineData("Skill")]
    [InlineData("Feat")]
    [InlineData("Ability Score Increase")]
    public void FunctionalSelections_AreNotOptionalFlavor(string label)
        => BuildRuleClassifier.IsOptionalFlavorSelection(label).Should().BeFalse();
}
