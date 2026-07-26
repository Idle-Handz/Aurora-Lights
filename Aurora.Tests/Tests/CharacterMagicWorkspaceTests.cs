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
    public void PreparedTabShowsExternalAccessWithoutChangingLocalPreparation()
    {
        var model = BuildDruidMagicModel();
        MagicSpellListEntryModel druidBless = model.Sections[0].SpellLevels[0].Spells
            .Single(spell => spell.Name == "Bless");
        druidBless.IsPrepared = false;
        druidBless.DisplayState = MagicSpellDisplayState.Available;
        MagicSpellListEntryModel cureWounds = model.Sections[0].SpellLevels[0].Spells
            .Single(spell => spell.Name == "Cure Wounds");
        cureWounds.GrantedBy = "Life Domain";
        var mistyStep = Spell("ID_SPELL_MISTY_STEP", "Misty Step", 2, isPrepared: false);
        model.Sections[0].SpellLevels =
        [
            model.Sections[0].SpellLevels[0],
            new MagicSpellLevelModel(2, [mistyStep], totalSlots: 2, usedSlots: 0)
        ];
        model.Sections =
        [
            model.Sections[0],
            new MagicSpellcastingSectionModel
            {
                Id = "sorcerer",
                Label = "Sorcerer",
                IsPreparedCaster = false,
                SpellLevels =
                [
                    new MagicSpellLevelModel(
                        1,
                        [Spell("ID_SPELL_BLESS", "Bless", 1, isPrepared: true, isAlwaysPrepared: true)],
                        totalSlots: 4,
                        usedSlots: 0)
                ]
            }
        ];
        model.KnownSpellGroups =
        [
            new MagicKnownSpellGroupModel(
                "Sorcerer",
                "sorcerer",
                [
                    new MagicKnownSpellEntryModel(
                        "known:0",
                        "Known Spell",
                        "Bless",
                        SpellLevel: 1,
                        SpellId: "ID_SPELL_BLESS",
                        SelectionAccess: MagicSpellSelectionAccess.Known,
                        AccessSource: "Sorcerer")
                ]),
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
                        GrantedBy: "Fey Touched",
                        SelectionAccess: MagicSpellSelectionAccess.Granted,
                        AccessSource: "Fey Touched")
                ],
                ReadOnlyGroup: true)
        ];
        MagicSpellAccessClassifier.Apply(model);

        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, model));

        cut.FindAll("button.magic-view-tab")
            .First(button => button.TextContent.Contains("Druid", StringComparison.Ordinal))
            .Click();

        cut.Markup.Should().Contain("Already Known — Sorcerer");
        cut.Markup.Should().Contain("Granted — Fey Touched");
        cut.Markup.Should().Contain("Always Prepared — Life Domain");
        model.Sections[0].PreparedCount.Should().Be(0);
        druidBless.IsPrepared.Should().BeFalse();
        druidBless.IsAlwaysPrepared.Should().BeFalse();
        mistyStep.IsPrepared.Should().BeFalse();
        mistyStep.IsAlwaysPrepared.Should().BeFalse();
    }

    [Fact]
    public void AllShowsUnpreparedWizardRitualsButNotOtherUnpreparedBookSpells()
    {
        MagicSpellListEntryModel detectMagic = Spell(
            "ID_SPELL_DETECT_MAGIC",
            "Detect Magic",
            1,
            isPrepared: false,
            isRitual: true);
        MagicSpellListEntryModel magicMissile = Spell(
            "ID_SPELL_MAGIC_MISSILE",
            "Magic Missile",
            1,
            isPrepared: false);
        var model = new MagicOverviewModel
        {
            HasSpellcasting = true,
            KnownSpellGroups =
            [
                new MagicKnownSpellGroupModel(
                    "Wizard",
                    "wizard",
                    [
                        new MagicKnownSpellEntryModel(
                            "book:0",
                            "Spellbook (Wizard)",
                            detectMagic.Name,
                            SpellLevel: 1,
                            SpellId: detectMagic.Id,
                            IsRitual: true,
                            SelectionAccess: MagicSpellSelectionAccess.Spellbook,
                            AccessSource: "Wizard Spellbook"),
                        new MagicKnownSpellEntryModel(
                            "book:1",
                            "Spellbook (Wizard)",
                            magicMissile.Name,
                            SpellLevel: 1,
                            SpellId: magicMissile.Id,
                            SelectionAccess: MagicSpellSelectionAccess.Spellbook,
                            AccessSource: "Wizard Spellbook")
                    ])
            ],
            Sections =
            [
                new MagicSpellcastingSectionModel
                {
                    Id = "wizard",
                    Label = "Wizard",
                    IsPreparedCaster = true,
                    IsSpellbookCaster = true,
                    RitualCastingMode = MagicRitualCastingMode.Spellbook,
                    SpellLevels =
                    [
                        new MagicSpellLevelModel(
                            1,
                            [detectMagic, magicMissile],
                            totalSlots: 4,
                            usedSlots: 0)
                    ]
                }
            ]
        };
        MagicSpellAccessClassifier.Apply(model);

        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(p => p.Model, model));

        cut.Markup.Should().Contain("Detect Magic");
        cut.Markup.Should().Contain("Ritual Only — Wizard Spellbook");
        cut.Markup.Should().NotContain("Magic Missile");
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
        cut.Find(".magic-spell-badge-granted").TextContent.Trim()
            .Should().Be("Granted — Fey Touched");
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
        string castingTime = "Action",
        bool isRitual = false) =>
        new(
            id,
            name,
            level,
            "Player's Handbook",
            "Evocation",
            isRitual,
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
