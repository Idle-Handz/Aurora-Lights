using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class SelectionDescriptionMarkupTests : IAsyncLifetime
{
    private static readonly HashSet<string> ClassLikeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Class",
        "Multiclass",
        "Archetype",
    };

    private readonly ITestOutputHelper _output;

    public SelectionDescriptionMarkupTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void EveryLoadedClassAndSubclassDescriptionHasAFeatureProgressionSummary()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        List<ElementBase> classLikeElements = DataManager.Current.ElementsCollection
            .Where(element => ClassLikeTypes.Contains(element.Type))
            .ToList();

        classLikeElements.Should().Contain(element => element.Type == "Class");
        classLikeElements.Should().Contain(element => element.Type == "Multiclass");
        classLikeElements.Should().Contain(element => element.Type == "Archetype");

        var missing = classLikeElements
            .Where(element =>
            {
                string markup = SelectionDescriptionMarkup.WithFeatureProgression(
                    element,
                    element.Description);
                return !SelectionDescriptionMarkup.HasFeatureProgression(markup);
            })
            .Select(element => $"{element.Type}: {element.Name} ({element.Id})")
            .ToList();

        missing.Should().BeEmpty(
            "every class, generated multiclass, and subclass picker needs a level-by-level feature table or list");
    }

    [Fact]
    public void SubclassWithoutAuthoredTableGetsFeatureTableFromGrantRules()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        ElementBase subclass = DataManager.Current.ElementsCollection
            .First(element =>
                element.Type == "Archetype"
                && !SelectionDescriptionMarkup.HasFeatureProgression(element.Description));

        string markup = SelectionDescriptionMarkup.WithFeatureProgression(
            subclass,
            subclass.Description);

        markup.Should().Contain("<h5>Features by Level</h5>");
        markup.Should().Contain("<table>");
        SelectionDescriptionMarkup.BuildFeatureProgression(subclass).Should().NotBeEmpty();
    }

    [Fact]
    public void FeatureProgressionIncludesDirectFeatureSelections()
    {
        var subclass = new ElementBase(
            "Test Patron",
            "Archetype",
            "Test",
            "ID_TEST_ARCHETYPE_DIRECT_FEATURE_SELECTION");
        var selection = new SelectRule(subclass.ElementHeader);
        selection.Attributes.Type = "Archetype Feature";
        selection.Attributes.Name = "Patron Kind";
        selection.Attributes.RequiredLevel = 1;
        subclass.Rules.Add(selection);

        IReadOnlyList<SelectionFeatureLevel> progression =
            SelectionDescriptionMarkup.BuildFeatureProgression(subclass);

        progression.Should().ContainSingle();
        progression[0].Level.Should().Be(1);
        progression[0].Features.Should().Equal("Patron Kind");
    }

    [Theory]
    [InlineData("Class")]
    [InlineData("Multiclass")]
    [InlineData("Archetype")]
    public void SelectionResolverCarriesFeatureProgressionMarkup(string elementType)
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var rule = new SelectRule(new ElementHeader(
            "Selection Description Test",
            "Feature",
            "Test",
            "ID_TEST_SELECTION_DESCRIPTION_OWNER"));
        rule.Attributes.Type = elementType;
        rule.Attributes.Name = $"Choose {elementType}";

        IReadOnlyList<BuildSelectionOption> options =
            BuildSelectionOptionResolver.ResolveOptions(rule);

        options.Should().NotBeEmpty();
        options.Should().OnlyContain(option =>
            SelectionDescriptionMarkup.HasFeatureProgression(option.DescriptionMarkup));
    }
}
