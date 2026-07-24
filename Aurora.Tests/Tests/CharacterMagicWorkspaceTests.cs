using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class CharacterMagicWorkspaceTests : BunitContext
{
    [Fact]
    public void AllTabShowsCantripsAndPreparedSpellsOnly()
    {
        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, BuildDruidMagicModel()));

        var tabs = cut.FindAll("button.magic-view-tab");
        tabs.Select(button => button.TextContent.Trim())
            .Should()
            .ContainInOrder("All", "Known");
        tabs[0].ClassList.Should().Contain("selected");

        cut.Markup.Should().Contain("Message");
        cut.Markup.Should().Contain("Cure Wounds");
        cut.Markup.Should().Contain("Bless");
        cut.Markup.Should().NotContain("Detect Magic");
    }

    [Fact]
    public void PreparedTabStillShowsAvailablePreparationOptions()
    {
        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, BuildDruidMagicModel()));

        cut.FindAll("button.magic-view-tab")
            .First(button => button.TextContent.Contains("Druid", StringComparison.Ordinal))
            .Click();

        cut.Markup.Should().Contain("Detect Magic");
    }

    private static MagicOverviewModel BuildDruidMagicModel() => new()
    {
        HasSpellcasting = true,
        Sections =
        [
            new MagicSpellcastingSectionModel
            {
                Id = "druid",
                Label = "Druid",
                IsPreparedCaster = true,
                SpellcastingAbility = "Wisdom",
                SpellcastingDc = "14",
                SpellcastingAttack = "+6",
                PreparedCount = 2,
                MaxPrepared = 9,
                Cantrips =
                [
                    Spell("ID_SPELL_MESSAGE", "Message", 0, isPrepared: true, isAlwaysPrepared: true, isCantrip: true)
                ],
                SpellLevels =
                [
                    new MagicSpellLevelModel(
                        1,
                        [
                            Spell("ID_SPELL_CURE_WOUNDS", "Cure Wounds", 1, isPrepared: true, isAlwaysPrepared: true),
                            Spell("ID_SPELL_BLESS", "Bless", 1, isPrepared: true),
                            Spell("ID_SPELL_DETECT_MAGIC", "Detect Magic", 1, isPrepared: false)
                        ],
                        totalSlots: 4,
                        usedSlots: 0)
                ]
            }
        ]
    };

    private static MagicSpellListEntryModel Spell(
        string id,
        string name,
        int level,
        bool isPrepared,
        bool isAlwaysPrepared = false,
        bool isCantrip = false) =>
        new(
            id,
            name,
            level,
            "Player's Handbook",
            "Evocation",
            isRitual: false,
            isConcentration: false,
            isPrepared,
            isAlwaysPrepared,
            isCantrip,
            isCantrip
                ? MagicSpellDisplayState.Known
                : isAlwaysPrepared
                    ? MagicSpellDisplayState.AlwaysPrepared
                    : isPrepared
                        ? MagicSpellDisplayState.Prepared
                        : MagicSpellDisplayState.Available);
}
