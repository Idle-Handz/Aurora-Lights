using Aurora.Reflections.Services;
using Builder.Data;
using Builder.Presentation.Models;
using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

public sealed class PartialLoadReportTests
{
    private static CharacterFile.LoadResult Partial(params CharacterLoadValidation.MissingElement[] missing) =>
        new(false, "Saved character elements could not be restored: " + string.Join("; ", missing.Select(m => m.Id)), missing);

    private static CharacterLoadValidation.MissingElement Missing(string id, string? book = null, bool local = false) =>
        new(id, 1, "Class > Weapon Proficiency (Claw)")
        {
            SavedSource = book is null && !local ? null : new(book, "items-weapons.xml", local)
        };

    [Fact]
    public void NamesSourcesOnceAndKeepsElementDetailsOutOfTheToast()
    {
        string message = PartialLoadReport.Describe(Partial(
            Missing("ID_CLAW", "Ryoko's Guide"), Missing("ID_SAI", "Ryoko's Guide"),
            Missing("ID_OTHER", "Another Book")), []);

        message.Should().Be("⚠ Missing content from: Another Book, Ryoko's Guide. See Console for details.");
        message.Should().NotContain("ID_").And.NotContain("Claw").And.NotContain("saved picks")
            .And.NotContain("items-weapons.xml").And.NotContain("re-pick");
    }

    [Fact]
    public void LocalCustomizationIsNotPresentedAsAMissingPublishedBook()
    {
        var message = PartialLoadReport.Describe(Partial(
            Missing("ORIGIN_TOUGH", "Player's Handbook (2024)", local: true)), []);

        message.Should().Contain("Player's Handbook (2024) (local customization)")
            .And.NotContain("Origin Feat");
    }

    [Fact]
    public void KnownLocalContentWithoutABookNameStillHasAnActionableLabel()
    {
        PartialLoadReport.Describe(Partial(Missing("LOCAL", local: true)), [])
            .Should().Contain("Missing content from: Local customizations.");
    }

    [Fact]
    public void OldSavesWithoutSourceMetadataUseAnHonestFallback()
    {
        PartialLoadReport.Describe(Partial(Missing("ID_WOTC_PHB24_UNKNOWN")), [])
            .Should().Be("⚠ Required content is missing; its sources are unknown. See Console for details.");
    }

    [Fact]
    public void UnknownSourcesAreNotHiddenWhenOtherSourcesAreKnown()
    {
        PartialLoadReport.Describe(Partial(Missing("KNOWN", "Known Book"), Missing("UNKNOWN")), [])
            .Should().Contain("Missing content from: Known Book. Other sources are unknown.");
    }

    [Fact]
    public void InstalledDefinitionsAreNotCalledMissingSources()
    {
        var installed = new ElementBase("Background with a Feat", "Grants", "Internal", "MARKER");
        PartialLoadReport.Describe(Partial(Missing("MARKER", "Old Book")), [installed])
            .Should().Be("⚠ Some installed content could not be restored. See Console for details.");
    }

    [Fact]
    public void InstalledAliasTargetsAreNotCalledMissingSources()
    {
        using var scope = ElementIdAliases.BeginScope();
        ElementIdAliases.Set([new("OLD", "NEW")]);
        var installed = new ElementBase("Renamed feat", "Feat", "Installed Book", "NEW");
        PartialLoadReport.Describe(Partial(Missing("OLD", "Old Book")), [installed])
            .Should().Be("⚠ Some installed content could not be restored. See Console for details.");
    }

    [Fact]
    public void MixedFailuresShowOnlyMissingSourcesAndMentionRemainingRestoreIssues()
    {
        var installed = new ElementBase("Background with a Feat", "Grants", "Installed Book", "MARKER");
        PartialLoadReport.Describe(Partial(Missing("CLAW", "Ryoko's Guide"), Missing("MARKER")), [installed])
            .Should().Contain("Missing content from: Ryoko's Guide. Other restore issues remain.")
            .And.NotContain("Installed Book");
    }

    [Fact]
    public void LongListsCountAdditionalSourcesInsteadOfAdditionalElements()
    {
        var message = PartialLoadReport.Describe(Partial(
            Missing("A", "Alpha"), Missing("B", "Bravo"), Missing("B2", "Bravo"),
            Missing("C", "Charlie"), Missing("D", "Delta"), Missing("E", "Echo")), []);

        message.Should().Contain("Alpha, Bravo, Charlie, and 2 more").And.NotContain("Echo");
    }

    [Fact]
    public void LongSourceTitlesStayShortEnoughForMobile()
    {
        var message = PartialLoadReport.Describe(Partial(
            Missing("A", new string('A', 300)), Missing("B", new string('B', 300)),
            Missing("C", new string('C', 300)), Missing("UNKNOWN")), []);

        message.Length.Should().BeLessThan(260);
        message.Should().Contain("…").And.Contain("more").And.Contain("Other sources are unknown.");
    }

    [Fact]
    public void FailureWithoutStructuredMissingElementsKeepsItsMessage()
    {
        PartialLoadReport.Describe(new CharacterFile.LoadResult(false, "Something else went wrong"), [])
            .Should().Be("⚠ Partial load: Something else went wrong");
    }
}
