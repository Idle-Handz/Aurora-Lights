using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

/// <summary>
/// The sources tree is category-first: a source's rules context decides where it lives, and the
/// engine's publisher grouping survives as the level beneath. A publisher may appear under two
/// categories — Wizards of the Coast published both 2014 and 2024 books — so each node has to carry
/// the sources it actually stands for.
/// </summary>
public sealed class SourceRestrictionTreeBuilderTests
{
    private static SourceRestrictionItemModel Source(
        string id, string name, SourceRestrictionCategory? category, bool enabled = true, bool optional = true) =>
        new(id, name, enabled, optional, !optional, category);

    private static SourceRestrictionGroupModel Group(
        string name, params SourceRestrictionItemModel[] sources) =>
        new(name, name, "", AllowUnchecking: true, IsChecked: true, sources);

    private static readonly SourceRestrictionGroupModel[] Catalog =
    [
        new(SourceRestrictionTreeBuilder.RequiredNodeId, "Required builder sources", "", false, true,
        [
            Source("ale", "Aurora Legacy Essentials", SourceRestrictionCategory.Official5E, optional: false),
            Source("internal", "Internal", null, optional: false)
        ]),
        Group("Wizards of the Coast",
            Source("phb", "Player's Handbook", SourceRestrictionCategory.Official5E),
            Source("phb24", "Player's Handbook (2024)", SourceRestrictionCategory.Official55E)),
        Group("Unearthed Arcana",
            Source("ua-feats", "Unearthed Arcana: Feats", SourceRestrictionCategory.Official5E)),
        Group("Third Party",
            Source("ancients", "Arcana of the Ancients", SourceRestrictionCategory.ThirdParty, enabled: false)),
        Group("Homebrew",
            Source("xellarant", "The Book of Xellarant", SourceRestrictionCategory.Homebrew))
    ];

    private static SourceRestrictionNodeModel Node(string label) =>
        SourceRestrictionTreeBuilder.Build(Catalog).Single(node => node.Label == label);

    [Fact]
    public void CategoriesComeFirstInReadingOrder()
    {
        SourceRestrictionTreeBuilder.Build(Catalog).Select(node => node.Label)
            .Should().Equal("5e official", "5.5e official", "3rd party", "Homebrew", "Required builder sources");
    }

    [Fact]
    public void ACategoryDrawingOnSeveralPublishersNestsThem()
    {
        var official = Node("5e official");

        official.Children.Select(child => child.Label).Should().Equal("Wizards of the Coast", "Unearthed Arcana");
        official.Sources.Should().BeEmpty("the sources hang off the publishers, not the category");
        official.AllSources.Select(source => source.Id).Should().Equal("phb", "ua-feats");
    }

    [Fact]
    public void ACategoryWithOnePublisherSkipsThatLevel()
    {
        var homebrew = Node("Homebrew");

        homebrew.Children.Should().BeEmpty("repeating 'Homebrew' under 'Homebrew' tells the user nothing");
        homebrew.Sources.Select(source => source.Id).Should().Equal("xellarant");
    }

    [Fact]
    public void APublisherThatSpansTwoErasAppearsUnderBoth()
    {
        Node("5e official").Children.Single(child => child.Label == "Wizards of the Coast")
            .AllSources.Select(source => source.Id).Should().Equal("phb");
        // The 2024 book is the only one under the revised-rules category, and it is the only thing
        // switching that publisher off there may touch.
        Node("5.5e official").Sources.Select(source => source.Id).Should().Equal("phb24");
    }

    [Fact]
    public void RequiredSourcesAreCollectedAtTheEndAndCannotBeSwitchedOff()
    {
        var required = Node("Required builder sources");

        required.AllowUnchecking.Should().BeFalse();
        required.Sources.Select(source => source.Id).Should().Equal("ale", "internal");
        required.ToggleableSources.Should().BeEmpty();
        required.IsChecked.Should().BeTrue();
    }

    [Fact]
    public void RequiredSourcesAreLeftOutOfTheCategoryTheyWouldOtherwiseJoin()
    {
        // "ale" classifies as 5e official, but a count the user cannot change is a misleading count.
        Node("5e official").AllSources.Select(source => source.Id).Should().NotContain("ale");
    }

    [Fact]
    public void CountsDescribeWhatIsEnabledBeneathTheNode()
    {
        var official = Node("5e official");
        official.EnabledCount.Should().Be(2);
        official.TotalCount.Should().Be(2);
        official.IsChecked.Should().BeTrue();

        var thirdParty = Node("3rd party");
        thirdParty.EnabledCount.Should().Be(0);
        thirdParty.IsChecked.Should().BeFalse();
    }

    [Fact]
    public void ACategoryIsMixedWhenOnlySomeOfItsSourcesAreEnabled()
    {
        SourceRestrictionGroupModel[] catalog =
        [
            Group("Third Party",
                Source("on", "Enabled", SourceRestrictionCategory.ThirdParty),
                Source("off", "Disabled", SourceRestrictionCategory.ThirdParty, enabled: false))
        ];

        var node = SourceRestrictionTreeBuilder.Build(catalog).Single();

        node.IsChecked.Should().BeNull();
        node.EnabledCount.Should().Be(1);
        node.TotalCount.Should().Be(2);
    }

    [Fact]
    public void SourcesWithNoRulesContextStillGetAHome()
    {
        SourceRestrictionGroupModel[] catalog =
        [
            Group("Undefined Sources", Source("mystery", "Mystery Book", null))
        ];

        var node = SourceRestrictionTreeBuilder.Build(catalog).Single();

        node.Label.Should().Be("Other sources");
        node.Sources.Select(source => source.Id).Should().Equal("mystery");
    }
}
