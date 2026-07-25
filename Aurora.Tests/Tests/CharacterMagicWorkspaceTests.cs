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

    [Fact]
    public void CastingTimeFilterShowsOnlyTheSelectedActionEconomy()
    {
        var model = BuildDruidMagicModel();
        model.Sections[0].SpellLevels[0].Spells =
        [
            .. model.Sections[0].SpellLevels[0].Spells,
            Spell("ID_SPELL_HEALING_WORD", "Healing Word", 1, isPrepared: true, castingTime: "1 bonus action")
        ];

        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, model));

        cut.Find("select.magic-casting-time-filter").Change("bonus-action");

        cut.Markup.Should().Contain("Healing Word");
        cut.Markup.Should().NotContain("Bless");
        cut.Markup.Should().NotContain("Detect Magic");
    }

    [Fact]
    public void CastingTimeFilterGroupsConditionalReactionsTogether()
    {
        var model = BuildDruidMagicModel();
        model.Sections[0].SpellLevels[0].Spells =
        [
            .. model.Sections[0].SpellLevels[0].Spells,
            Spell(
                "ID_SPELL_SHIELD",
                "Shield",
                1,
                isPrepared: true,
                castingTime: "1 reaction, which you take when you are hit by an attack")
        ];

        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, model));

        cut.Find("select.magic-casting-time-filter").Change("reaction");

        cut.Markup.Should().Contain("Shield");
        cut.Markup.Should().NotContain("Bless");
    }

    [Fact]
    public void GrantedFeatureSpellsAppearInAllAsGranted()
    {
        var model = BuildDruidMagicModel();
        model.KnownSpellGroups =
        [
            new MagicKnownSpellGroupModel(
                "Granted by Features",
                [
                    new MagicKnownSpellEntryModel(
                        "granted:0",
                        "Level 2",
                        "Misty Step",
                        SpellLevel: 2,
                        IsReadOnly: true,
                        SpellId: "ID_SPELL_MISTY_STEP",
                        Source: "Player's Handbook",
                        School: "Conjuration",
                        CastingTime: "1 bonus action",
                        GrantedBy: "Fey Touched")
                ],
                ReadOnlyGroup: true)
        ];

        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, model));

        cut.Markup.Should().Contain("Misty Step");
        cut.Markup.Should().Contain("Granted by Fey Touched");
        cut.Find(".magic-spell-badge-granted").TextContent.Trim().Should().Be("Granted");
    }

    [Fact]
    public void GrantedSpellAlreadyInAClassSectionIsNotDuplicatedInAll()
    {
        var model = BuildDruidMagicModel();
        model.KnownSpellGroups =
        [
            new MagicKnownSpellGroupModel(
                "Granted by Features",
                [
                    new MagicKnownSpellEntryModel(
                        "granted:0",
                        "Level 1",
                        "Cure Wounds",
                        SpellLevel: 1,
                        IsReadOnly: true,
                        SpellId: "ID_SPELL_CURE_WOUNDS",
                        GrantedBy: "Life Domain")
                ],
                ReadOnlyGroup: true)
        ];

        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, model));

        cut.FindAll(".magic-spell-name")
            .Count(name => name.TextContent.Trim() == "Cure Wounds")
            .Should()
            .Be(1);
    }

    [Fact]
    public void SpellDetailRendersStructuredDescriptionAndSeparatePropertyCells()
    {
        var detail = new MagicSpellDetailModel(
            "ID_SPELL_HYPNOTIC_PATTERN",
            "Hypnotic Pattern",
            "Player's Handbook",
            3,
            "3rd-level illusion",
            "Illusion",
            Ritual: false,
            Concentration: true,
            "Action",
            "120 feet",
            "S, M",
            "Concentration, up to 1 minute",
            "<p>First paragraph.</p><ul><li>One effect</li></ul>");

        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, BuildDruidMagicModel())
            .Add(p => p.SelectedSpell, detail));

        var propertyRows = cut.FindAll(".magic-prop-row");
        propertyRows.Should().HaveCount(4);
        propertyRows[0].QuerySelector(".magic-prop-label")!.TextContent.Should().Be("Casting Time");
        propertyRows[0].QuerySelector(".magic-prop-value")!.TextContent.Should().Be("Action");
        cut.Find(".magic-detail-description p").TextContent.Should().Be("First paragraph.");
        cut.Find(".magic-detail-description li").TextContent.Should().Be("One effect");
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
        bool isCantrip = false,
        string castingTime = "Action") =>
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
                        : MagicSpellDisplayState.Available,
            castingTime);
}
