using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class SourceRestrictionCategoryClassifierTests
{
    [Theory]
    [InlineData(true, false, false, "Wizards of the Coast", "Player's Handbook", "20140714", SourceRestrictionCategory.Official5E)]
    [InlineData(true, false, false, "Wizards of the Coast", "Player's Handbook (2024)", "20240917", SourceRestrictionCategory.Official55E)]
    [InlineData(false, false, false, "Wizards of the Coast", "Eberron: Forge of the Artificer", "20251125", SourceRestrictionCategory.Official55E)]
    [InlineData(false, true, false, "Third Party Publisher", "Ryoko's Guide", "20250404", SourceRestrictionCategory.ThirdParty)]
    [InlineData(false, false, true, "Community Author", "Custom Rules", null, SourceRestrictionCategory.Homebrew)]
    public void Classify_assigns_broad_source_category(
        bool isOfficial,
        bool isThirdParty,
        bool isHomebrew,
        string author,
        string name,
        string? releaseDate,
        SourceRestrictionCategory expected)
    {
        SourceRestrictionCategoryClassifier.Classify(
                isOfficial,
                isThirdParty,
                isHomebrew,
                author,
                name,
                releaseDate)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void Classify_leaves_unidentified_sources_out_of_broad_categories()
    {
        SourceRestrictionCategoryClassifier.Classify(
                isOfficial: false,
                isThirdParty: false,
                isHomebrew: false,
                author: "Unknown",
                name: "Unclassified Source",
                releaseDate: null)
            .Should()
            .BeNull();
    }

    [Fact]
    public void Classify_prefers_explicit_third_party_flag_over_wizards_authorship()
    {
        SourceRestrictionCategoryClassifier.Classify(
                isOfficial: false,
                isThirdParty: true,
                isHomebrew: false,
                author: "Wizards of the Coast",
                name: "Misflagged Source",
                releaseDate: "20260101")
            .Should()
            .Be(SourceRestrictionCategory.ThirdParty);
    }
}
