using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Extensions;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Provenance lives beside elements, so a clone does not inherit it by itself. Code that derives one
/// element from another has to carry it across, and the synthesized class features are the case that
/// actually depends on it.
/// </summary>
public sealed class ElementProvenanceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public ElementProvenanceTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static ElementBase Element(string id) =>
        new() { ElementHeader = new ElementHeader(id, "Class Feature", "Test", id) };

    [Fact]
    public void ProvenanceIsRecordedAndCleared()
    {
        var element = Element("ID_PROVENANCE_ONE");
        ElementProvenance.GetContentFilePath(element).Should().BeNull();

        ElementProvenance.SetContentFilePath(element, "core/features.xml");
        ElementProvenance.GetContentFilePath(element).Should().Be("core/features.xml");

        ElementProvenance.SetContentFilePath(element, null);
        ElementProvenance.GetContentFilePath(element).Should().BeNull();
    }

    [Fact]
    public void ACloneTakesProvenanceOnlyWhenItIsCarriedAcross()
    {
        var original = Element("ID_PROVENANCE_TWO");
        ElementProvenance.SetContentFilePath(original, "core/features.xml");

        var clone = original.Copy<ElementBase>();
        ElementProvenance.GetContentFilePath(clone).Should().BeNull("a clone is a different element");

        ElementProvenance.CopyTo(original, clone);
        ElementProvenance.GetContentFilePath(clone).Should().Be("core/features.xml");
    }

    [Fact]
    public void SynthesizedClassFeaturesKeepTheProvenanceOfTheirTemplate()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        // The engine builds per-class Ability Score Improvement features by cloning a built-in
        // template. They are the elements whose provenance a clone would otherwise drop, which a
        // database-backed load records as the built-in resource.
        var elements = DataManager.Current.ElementsCollection;
        ElementBase? template = elements.FirstOrDefault(element =>
            element.Id.StartsWith("ID_INTERNAL_TEMPLATE_CLASS_FEATURE_ABILITY_4", StringComparison.Ordinal));
        template.Should().NotBeNull("the ability score improvement template defines the synthesized features");

        var synthesized = elements
            .Where(element => element.Id.StartsWith("ID_INTERNAL_CLASS_FEATURE_ASI_", StringComparison.Ordinal))
            .ToList();
        synthesized.Should().NotBeEmpty("the content defines classes with ability score improvements");

        string? expected = ElementProvenance.GetContentFilePath(template);
        synthesized.Select(element => ElementProvenance.GetContentFilePath(element)).Distinct()
            .Should().Equal([expected], "each synthesized feature reports where its template came from");
    }
}
