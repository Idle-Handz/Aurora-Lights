using Aurora.Components.Models;
using Aurora.Components.Shared;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Aurora.Tests.Tests;

public sealed class SourceRestrictionsEditorTests : BunitContext
{
    private static SourceRestrictionItemModel Source(
        string id, string name, SourceRestrictionCategory? category, bool enabled = true, bool optional = true) =>
        new(id, name, enabled, optional, !optional, category);

    private static SourceRestrictionGroupModel Group(
        string name, params SourceRestrictionItemModel[] sources) =>
        new(name, name, "", AllowUnchecking: true, IsChecked: true, sources);

    private static readonly SourceRestrictionGroupModel[] Catalog =
    [
        new("required", "Required builder sources", "", false, true,
        [
            Source("ale", "Aurora Legacy Essentials", null, optional: false),
            Source("internal", "Internal", null, optional: false)
        ]),
        Group("Wizards of the Coast",
            Source("phb", "Player's Handbook", SourceRestrictionCategory.Official5E),
            Source("dmg", "Dungeon Master's Guide", SourceRestrictionCategory.Official5E)),
        Group("Unearthed Arcana",
            Source("ua-feats", "Unearthed Arcana: Feats", SourceRestrictionCategory.Official5E, enabled: false)),
        Group("Third Party",
            Source("ancients", "Arcana of the Ancients", SourceRestrictionCategory.ThirdParty))
    ];

    private IRenderedComponent<SourceRestrictionsEditor> RenderEditor(
        Action<ComponentParameterCollectionBuilder<SourceRestrictionsEditor>>? extra = null) =>
        Render<SourceRestrictionsEditor>(parameters =>
        {
            parameters.Add(component => component.Groups, Catalog);
            extra?.Invoke(parameters);
        });

    private static IElement Header(IRenderedComponent<SourceRestrictionsEditor> cut, string label) =>
        cut.FindAll("button.source-restrictions-toggle")
            .Single(button => button.QuerySelector("strong")?.TextContent == label);

    [Fact]
    public void TheTreeStartsCollapsedWithACategoryPerRulesContext()
    {
        var cut = RenderEditor();

        cut.FindAll("button.source-restrictions-toggle strong").Select(node => node.TextContent)
            .Should().Equal("5e official", "3rd party", "Required builder sources");
        cut.FindAll("button.source-restrictions-item").Should().BeEmpty("nothing is expanded yet");
        Header(cut, "5e official").TextContent.Should().Contain("2 / 3");
    }

    [Fact]
    public void ExpandingACategoryRevealsItsPublishersAndThenItsBooks()
    {
        var cut = RenderEditor();

        cut.FindAll("button.source-restrictions-disclosure")[0].Click();

        cut.FindAll("button.source-restrictions-toggle strong").Select(node => node.TextContent)
            .Should().Contain(["Wizards of the Coast", "Unearthed Arcana"]);
        cut.FindAll("button.source-restrictions-item").Should().BeEmpty("the publishers are still closed");

        cut.FindAll("section.source-restrictions-node.publisher button.source-restrictions-disclosure")[0].Click();

        cut.FindAll("button.source-restrictions-item").Select(row => row.TextContent.Trim())
            .Should().Equal("Player's Handbook", "Dungeon Master's Guide");
    }

    [Fact]
    public void ACategoryWithOnePublisherShowsItsBooksDirectly()
    {
        var cut = RenderEditor();

        cut.FindAll("button.source-restrictions-disclosure")
            .Single(button => button.GetAttribute("aria-label") == "Expand 3rd party").Click();

        cut.FindAll("button.source-restrictions-item").Select(row => row.TextContent.Trim())
            .Should().Equal("Arcana of the Ancients");
    }

    [Fact]
    public void TogglingACategorySendsEverySourceUnderIt()
    {
        SourceRestrictionNodeToggle? requested = null;
        var cut = RenderEditor(parameters => parameters.Add(
            component => component.OnToggleNode,
            EventCallback.Factory.Create<SourceRestrictionNodeToggle>(this, toggle => requested = toggle)));

        Header(cut, "5e official").Click();

        requested.Should().NotBeNull();
        requested!.SourceIds.Should().Equal("phb", "dmg", "ua-feats");
        requested.IsEnabled.Should().BeTrue("a partly enabled category switches the rest on");
    }

    [Fact]
    public void TogglingAPublisherSendsOnlyThatPublishersSources()
    {
        SourceRestrictionNodeToggle? requested = null;
        var cut = RenderEditor(parameters => parameters.Add(
            component => component.OnToggleNode,
            EventCallback.Factory.Create<SourceRestrictionNodeToggle>(this, toggle => requested = toggle)));

        cut.FindAll("button.source-restrictions-disclosure")[0].Click();
        Header(cut, "Unearthed Arcana").Click();

        // Only this publisher's book: switching it must not touch its siblings under the category.
        requested!.SourceIds.Should().Equal("ua-feats");
        requested.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AFullyEnabledCategoryTurnsItselfOff()
    {
        SourceRestrictionNodeToggle? requested = null;
        var cut = RenderEditor(parameters => parameters.Add(
            component => component.OnToggleNode,
            EventCallback.Factory.Create<SourceRestrictionNodeToggle>(this, toggle => requested = toggle)));

        Header(cut, "3rd party").Click();

        requested!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void AMixedCategoryReportsItselfAsMixed()
    {
        var cut = RenderEditor();

        Header(cut, "5e official").GetAttribute("aria-checked").Should().Be("mixed");
        Header(cut, "3rd party").GetAttribute("aria-checked").Should().Be("true");
    }

    [Fact]
    public void RequiredSourcesAreVisiblyEnabledAndLocked()
    {
        string? requestedItem = null;
        var cut = RenderEditor(parameters => parameters.Add(
            component => component.OnToggleItem,
            EventCallback.Factory.Create<string>(this, id => requestedItem = id)));

        var required = Header(cut, "Required builder sources");
        required.HasAttribute("disabled").Should().BeTrue();
        required.GetAttribute("aria-checked").Should().Be("true");
        required.TextContent.Should().Contain("Always enabled");
        required.GetAttribute("title").Should().Contain("Required for the builder");

        cut.FindAll("button.source-restrictions-disclosure")
            .Single(button => button.GetAttribute("aria-label") == "Expand Required builder sources").Click();

        var locked = cut.FindAll("button.source-restrictions-item.locked");
        locked.Should().HaveCount(2);
        foreach (var row in locked)
        {
            // Disabled, so a click never reaches the handler in the first place.
            row.HasAttribute("disabled").Should().BeTrue();
            row.GetAttribute("aria-checked").Should().Be("true");
            row.QuerySelector(".source-restrictions-checkbox.checked.locked").Should().NotBeNull();
            row.TextContent.Should().Contain("Always enabled");
        }
        requestedItem.Should().BeNull();
    }

    [Fact]
    public void ABookCanBeToggledOnItsOwn()
    {
        string? requestedItem = null;
        var cut = RenderEditor(parameters => parameters.Add(
            component => component.OnToggleItem,
            EventCallback.Factory.Create<string>(this, id => requestedItem = id)));

        cut.FindAll("button.source-restrictions-disclosure")
            .Single(button => button.GetAttribute("aria-label") == "Expand 3rd party").Click();
        cut.FindAll("button.source-restrictions-item")
            .Single(row => row.TextContent.Contains("Arcana of the Ancients")).Click();

        requestedItem.Should().Be("ancients");
    }

    [Fact]
    public void FilteringOpensWhatItFindsAndHidesTheRest()
    {
        var cut = RenderEditor();

        cut.Find("input.source-restrictions-search").Input("Feats");

        cut.FindAll("button.source-restrictions-toggle strong").Select(node => node.TextContent)
            .Should().Equal("5e official", "Unearthed Arcana");
        cut.FindAll("button.source-restrictions-item").Select(row => row.TextContent.Trim())
            .Should().Equal("Unearthed Arcana: Feats");
    }

    [Theory]
    [InlineData("Wizards of the Coast")]
    [InlineData("  wizards OF THE coast  ")]
    public void FilteringByPublisherShowsItsBooksAndCategory(string query)
    {
        var cut = RenderEditor();

        cut.Find("input.source-restrictions-search").Input(query);

        cut.FindAll("button.source-restrictions-toggle strong").Select(node => node.TextContent)
            .Should().Equal("5e official", "Wizards of the Coast");
        cut.FindAll("button.source-restrictions-item").Select(row => row.TextContent.Trim())
            .Should().Equal("Player's Handbook", "Dungeon Master's Guide");
    }

    [Fact]
    public void FilteringByCategoryShowsEveryPublisherAndBookBelowIt()
    {
        var cut = RenderEditor();

        cut.Find("input.source-restrictions-search").Input("5e official");

        cut.FindAll("button.source-restrictions-toggle strong").Select(node => node.TextContent)
            .Should().Equal("5e official", "Wizards of the Coast", "Unearthed Arcana");
        cut.FindAll("button.source-restrictions-item").Select(row => row.TextContent.Trim())
            .Should().Equal("Player's Handbook", "Dungeon Master's Guide", "Unearthed Arcana: Feats");
        cut.FindAll("button.source-restrictions-disclosure")
            .Should().OnlyContain(button => button.GetAttribute("aria-expanded") == "true");
    }

    [Fact]
    public void ClearingTheFilterRestoresManualExpansions()
    {
        var cut = RenderEditor();
        cut.Find("button[aria-label='Expand 5e official']").Click();
        cut.Find("button[aria-label='Expand Wizards of the Coast']").Click();

        cut.Find("input.source-restrictions-search").Input("5e official");
        cut.FindAll("button.source-restrictions-disclosure")
            .Should().OnlyContain(button => button.HasAttribute("disabled"),
                "search expansion must not change the remembered manual state");
        cut.FindAll("button.source-restrictions-button").Single(button => button.TextContent.Trim() == "Clear").Click();

        cut.Find("button[aria-label='Collapse 5e official']").GetAttribute("aria-expanded").Should().Be("true");
        cut.Find("button[aria-label='Collapse Wizards of the Coast']").GetAttribute("aria-expanded").Should().Be("true");
        cut.Find("button[aria-label='Expand Unearthed Arcana']").GetAttribute("aria-expanded").Should().Be("false");
        cut.Find("button[aria-label='Expand 3rd party']").GetAttribute("aria-expanded").Should().Be("false");
        cut.FindAll("button.source-restrictions-item").Select(row => row.TextContent.Trim())
            .Should().Equal("Player's Handbook", "Dungeon Master's Guide");
    }

    [Fact]
    public void FilteringDoesNotChangeCategoryCountsOrBulkToggleScope()
    {
        SourceRestrictionNodeToggle? requested = null;
        var cut = RenderEditor(parameters => parameters.Add(
            component => component.OnToggleNode,
            EventCallback.Factory.Create<SourceRestrictionNodeToggle>(this, toggle => requested = toggle)));

        cut.Find("input.source-restrictions-search").Input("Handbook");
        cut.FindAll("button.source-restrictions-item").Should().ContainSingle();
        Header(cut, "5e official").TextContent.Should().Contain("2 / 3");
        Header(cut, "5e official").Click();

        requested.Should().NotBeNull();
        requested!.SourceIds.Should().Equal("phb", "dmg", "ua-feats");
        requested.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AFilterThatMatchesNothingSaysSo()
    {
        var cut = RenderEditor();

        cut.Find("input.source-restrictions-search").Input("zzzz");

        cut.FindAll("section.source-restrictions-node").Should().BeEmpty();
        cut.Find("p.source-restrictions-empty").TextContent.Should().Contain("No sources match \"zzzz\"");
    }
}
