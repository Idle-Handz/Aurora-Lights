using Aurora.Components.Formatting;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class SelectionDescriptionReferenceTests : IDisposable
{
    private readonly List<ElementBase> _elements = [];

    public SelectionDescriptionReferenceTests() => TestApplicationContextInstaller.EnsureInstalled();

    [Fact]
    public void SubclassPreviewIncludesReferencedSpellTableAndLaterFeaturesInAuthoredOrder()
    {
        var spells = Add("Domain Spells", "<table><tr><th>Cleric Level</th><th>Spells</th></tr><tr><td>1st</td><td>Snow Shield, Ice Lance</td></tr></table>");
        var first = Add("A Cold Wind", "<p>You learn a cold-damage cantrip.</p>");
        var last = Add("Winter's Wrath", "<p>At 17th level, surround yourself with swirling snow.</p>");
        var owner = Add("Winter Test Domain", $"<p>Introduction.</p><div element=\"{spells.Id}\" /><div element='{first.Id}'></div><div element={last.Id} />", "Archetype");
        string original = owner.Description;

        string markup = MagicDescriptionFormatter.FromAuroraHtml(
            SelectionDescriptionMarkup.WithFeatureProgression(owner, owner.Description));

        markup.Should().Contain("<table>").And.Contain("Snow Shield, Ice Lance")
            .And.Contain("You learn a cold-damage cantrip.")
            .And.Contain("At 17th level, surround yourself with swirling snow.");
        markup.IndexOf("Domain Spells", StringComparison.Ordinal).Should().BeLessThan(markup.IndexOf("A Cold Wind", StringComparison.Ordinal));
        markup.IndexOf("A Cold Wind", StringComparison.Ordinal).Should().BeLessThan(markup.IndexOf("Winter&#39;s Wrath", StringComparison.Ordinal));
        markup.Should().NotContain("element=");
        owner.Description.Should().Be(original, "rendering must not rewrite the content catalog");
    }

    [Fact]
    public void ExistingProgressionTableDoesNotPreventReferenceExpansion()
    {
        var feature = Add("Domain Spells", "<p>Full feature rules.</p>");
        var owner = Add("Domain", $"<table><tr><th>Level</th><th>Features</th></tr><tr><td>1st</td><td>Domain Spells</td></tr></table><div element=\"{feature.Id}\" />", "Archetype");

        string markup = SelectionDescriptionMarkup.WithFeatureProgression(owner, owner.Description);

        markup.Should().Contain("<h5>Domain Spells</h5><p>Full feature rules.</p>")
            .And.NotContain("Features by Level");
        SelectionDescriptionMarkup.WithFeatureProgression(owner, markup).Should().Be(markup);
    }

    [Fact]
    public void NestedRepeatedAndCyclicReferencesAreBoundedWithoutDroppingOtherFeatures()
    {
        var first = Add("First & Feature", "<p>First rules.</p>");
        var second = Add("Nested Feature", $"<p>Nested rules.</p><div element=\"{first.Id}\" />");
        first.Description += $"<div element=\"{second.Id}\" />";
        var owner = Add("Owner", $"<div element=\"missing-feature\"/><div element=\"{first.Id}\"/><div element=\"{second.Id}\"/><p>Ending.</p>", "Archetype");

        string markup = SelectionDescriptionMarkup.WithFeatureProgression(owner, owner.Description);

        markup.Should().Be("<h5>First &amp; Feature</h5><p>First rules.</p><h5>Nested Feature</h5><p>Nested rules.</p><p>Ending.</p>");
    }

    private ElementBase Add(string name, string description, string type = "Archetype Feature")
    {
        var element = new ElementBase(name, type, "Internal Test", $"ID_DESCRIPTION_REF_{Guid.NewGuid():N}")
        {
            Description = description
        };
        DataManager.Current.ElementsCollection.Add(element);
        _elements.Add(element);
        return element;
    }

    public void Dispose()
    {
        foreach (var element in _elements)
            DataManager.Current.ElementsCollection.Remove(element);
    }
}
