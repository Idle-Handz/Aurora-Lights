using AngleSharp.Dom;
using Aurora.App.Services;
using Aurora.Components.Models;
using Aurora.Components.Shared;
using Builder.Presentation.Models;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class MagicWorkspacePersistenceTests : BunitContext
{
    [Theory]
    [InlineData("All")]
    [InlineData("Known")]
    [InlineData("Prepared — Cleric")]
    public void EveryFilterAndTheSelectedViewSurvivePageRecreation(string view)
    {
        var character = NewTab();
        var first = RenderWorkspace(character);
        SelectView(first, view);
        first.Find("input.magic-filter-input").Input("Detect");
        string[] choices = ["1", "Player's Handbook", "Divination", "action"];
        for (int i = 0; i < choices.Length; i++)
            first.FindAll("select.magic-filter-select")[i].Change(choices[i]);
        Checkbox(first, "Rituals").Change(true);
        Checkbox(first, "Concentration").Change(true);
        if (view.StartsWith("Prepared", StringComparison.Ordinal))
            Checkbox(first, "Prepared only").Change(true);
        first.Dispose();

        // Navigation recreates the component and the spell model, but keeps the open character.
        var returned = RenderWorkspace(character);
        returned.Find("button.magic-view-tab.selected").TextContent.Trim().Should().Be(view);
        returned.Find("input.magic-filter-input").GetAttribute("value").Should().Be("Detect");
        for (int i = 0; i < choices.Length; i++)
            returned.FindAll("select.magic-filter-select")[i].GetAttribute("value").Should().Be(choices[i]);
        Checkbox(returned, "Rituals").HasAttribute("checked").Should().BeTrue();
        Checkbox(returned, "Concentration").HasAttribute("checked").Should().BeTrue();
        if (view.StartsWith("Prepared", StringComparison.Ordinal))
            Checkbox(returned, "Prepared only").HasAttribute("checked").Should().BeTrue();
        returned.Markup.Should().Contain("Detect Magic").And.NotContain("Cure Wounds");
    }

    [Fact]
    public void ViewsKeepIndependentFiltersAndChangedChoicesReplaceSavedOnes()
    {
        var character = NewTab();
        var first = RenderWorkspace(character);
        Checkbox(first, "Rituals").Change(true);
        SelectView(first, "Known");
        Checkbox(first, "Rituals").HasAttribute("checked").Should().BeFalse();
        first.Find("select.magic-casting-time-filter").Change("bonus-action");
        SelectView(first, "Prepared — Cleric");
        Checkbox(first, "Prepared only").Change(true);
        first.Dispose();

        var returned = RenderWorkspace(character);
        Checkbox(returned, "Prepared only").HasAttribute("checked").Should().BeTrue();
        SelectView(returned, "Known");
        returned.Find("select.magic-casting-time-filter").GetAttribute("value").Should().Be("bonus-action");
        SelectView(returned, "All");
        Checkbox(returned, "Rituals").HasAttribute("checked").Should().BeTrue();
        returned.Find("select.magic-casting-time-filter").GetAttribute("value").Should().BeEmpty();
        Checkbox(returned, "Rituals").Change(false);
        returned.Dispose();

        var changed = RenderWorkspace(character);
        Checkbox(changed, "Rituals").HasAttribute("checked").Should().BeFalse();
        changed.Markup.Should().Contain("Cure Wounds");
    }

    [Fact]
    public void SwitchingCharactersKeepsTheirOwnChoicesAndClosingResetsThem()
    {
        var tabs = new CharacterTabService();
        var file = NewFile();
        var firstCharacter = tabs.OpenLoadingTab(file);
        var first = RenderWorkspace(firstCharacter);
        Checkbox(first, "Rituals").Change(true);
        first.Dispose();

        var otherCharacter = tabs.OpenLoadingTab(NewFile());
        var other = RenderWorkspace(otherCharacter);
        Checkbox(other, "Rituals").HasAttribute("checked").Should().BeFalse();
        other.Find("select.magic-casting-time-filter").Change("bonus-action");
        other.Dispose();

        tabs.ActivateTab(firstCharacter);
        var returned = RenderWorkspace(tabs.ActiveTab!);
        Checkbox(returned, "Rituals").HasAttribute("checked").Should().BeTrue();
        returned.Find("select.magic-casting-time-filter").GetAttribute("value").Should().BeEmpty();
        returned.Dispose();

        tabs.CloseTab(firstCharacter);
        var reopened = RenderWorkspace(tabs.OpenLoadingTab(file));
        Checkbox(reopened, "Rituals").HasAttribute("checked").Should().BeFalse();
        reopened.Find("button.magic-view-tab.selected").TextContent.Trim().Should().Be("All");
        reopened.Markup.Should().Contain("Detect Magic").And.Contain("Cure Wounds");
    }

    [Fact]
    public void ANewAppSessionStartsWithDefaultFilters()
    {
        var file = NewFile();
        var firstSession = new CharacterTabService();
        var first = RenderWorkspace(firstSession.OpenLoadingTab(file));
        Checkbox(first, "Rituals").Change(true);
        SelectView(first, "Known");
        first.Dispose();

        var newSession = new CharacterTabService();
        var reopened = RenderWorkspace(newSession.OpenLoadingTab(file));
        reopened.Find("button.magic-view-tab.selected").TextContent.Trim().Should().Be("All");
        Checkbox(reopened, "Rituals").HasAttribute("checked").Should().BeFalse();
    }

    private IRenderedComponent<CharacterMagicWorkspace> RenderWorkspace(CharacterTab character) =>
        Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, BuildModel())
            .Add(p => p.State, character.MagicWorkspace));

    private static IElement Checkbox(IRenderedComponent<CharacterMagicWorkspace> component, string label) =>
        component.FindAll("label.magic-filter-check")
            .Single(element => element.TextContent.Trim() == label).QuerySelector("input")!;

    private static void SelectView(IRenderedComponent<CharacterMagicWorkspace> component, string view) =>
        component.FindAll("button.magic-view-tab")
            .Single(button => button.TextContent.Trim() == view).Click();

    private static CharacterFile NewFile() =>
        new(Path.Combine(Path.GetTempPath(), $"magic-filter-test-{Guid.NewGuid():N}.dnd5e"));

    private static CharacterTab NewTab() => new(NewFile());

    private static MagicOverviewModel BuildModel() => new()
    {
        HasSpellcasting = true,
        Sections =
        [
            new MagicSpellcastingSectionModel
            {
                Id = "cleric",
                Label = "Cleric",
                IsPreparedCaster = true,
                SpellLevels =
                [
                    new MagicSpellLevelModel(1,
                    [
                        new("ID_DETECT_MAGIC", "Detect Magic", 1, "Player's Handbook", "Divination",
                            isRitual: true, isConcentration: true, isPrepared: true, isAlwaysPrepared: false,
                            castingTime: "1 action"),
                        new("ID_CURE_WOUNDS", "Cure Wounds", 1, "Player's Handbook", "Evocation",
                            isRitual: false, isConcentration: false, isPrepared: true, isAlwaysPrepared: false,
                            castingTime: "1 action")
                    ], totalSlots: 4, usedSlots: 0)
                ]
            }
        ],
        KnownSpellGroups =
        [
            new MagicKnownSpellGroupModel("Cleric spells", "cleric",
            [
                new("known:0", "Spell", "Detect Magic", SpellLevel: 1, SpellId: "ID_DETECT_MAGIC",
                    Source: "Player's Handbook", School: "Divination", IsRitual: true,
                    IsConcentration: true, CastingTime: "1 action"),
                new("known:1", "Spell", "Cure Wounds", SpellLevel: 1, SpellId: "ID_CURE_WOUNDS",
                    Source: "Player's Handbook", School: "Evocation", CastingTime: "1 action")
            ])
        ]
    };
}
