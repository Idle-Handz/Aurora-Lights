using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class MagicSpellAccessClassifierTests
{
    [Fact]
    public void KeepsExternalKnownAccessIndependentFromLocalPreparation()
    {
        var druidShield = Spell("ID_SPELL_SHIELD", "Shield");
        var sorcererShield = Spell(
            "ID_SPELL_SHIELD",
            "Shield",
            isPrepared: true,
            isAlwaysPrepared: true);
        var model = new MagicOverviewModel
        {
            KnownSpellGroups =
            [
                new MagicKnownSpellGroupModel(
                    "Sorcerer",
                    "sorcerer",
                    [
                        new MagicKnownSpellEntryModel(
                            "known:0",
                            "Known Spell",
                            "Shield",
                            SpellLevel: 1,
                            SpellId: "ID_SPELL_SHIELD",
                            SelectionAccess: MagicSpellSelectionAccess.Known,
                            AccessSource: "Sorcerer")
                    ])
            ],
            Sections =
            [
                Section("druid", "Druid", isPreparedCaster: true, druidShield),
                Section("sorcerer", "Sorcerer", isPreparedCaster: false, sorcererShield)
            ]
        };

        MagicSpellAccessClassifier.Apply(model);

        druidShield.IsPrepared.Should().BeFalse();
        druidShield.IsAlwaysPrepared.Should().BeFalse();
        druidShield.AccessPaths.Should().ContainSingle(path =>
            path.Kind == MagicSpellAccessKind.Known
            && path.CastingSectionId == "sorcerer"
            && path.SourceLabel == "Sorcerer"
            && path.CanCastNormally);
        model.Sections[0].PreparedCount.Should().Be(0);
    }

    [Fact]
    public void KeepsSpellbookSpellsPreparableAndExposesUnpreparedWizardRituals()
    {
        var detectMagic = Spell("ID_SPELL_DETECT_MAGIC", "Detect Magic", isRitual: true);
        var magicMissile = Spell("ID_SPELL_MAGIC_MISSILE", "Magic Missile");
        var model = new MagicOverviewModel
        {
            KnownSpellGroups =
            [
                new MagicKnownSpellGroupModel(
                    "Wizard",
                    "wizard",
                    [
                        SpellbookEntry("book:0", detectMagic),
                        SpellbookEntry("book:1", magicMissile)
                    ])
            ],
            Sections =
            [
                Section(
                    "wizard",
                    "Wizard",
                    isPreparedCaster: true,
                    detectMagic,
                    magicMissile,
                    isSpellbookCaster: true,
                    ritualCastingMode: MagicRitualCastingMode.Spellbook)
            ]
        };

        MagicSpellAccessClassifier.Apply(model);

        detectMagic.IsPrepared.Should().BeFalse();
        detectMagic.IsAlwaysPrepared.Should().BeFalse();
        detectMagic.AccessPaths.Should().ContainSingle(path =>
            path.Kind == MagicSpellAccessKind.Spellbook
            && path.SourceLabel == "Wizard Spellbook"
            && !path.CanCastNormally
            && path.CanCastAsRitual);

        magicMissile.IsPrepared.Should().BeFalse();
        magicMissile.IsAlwaysPrepared.Should().BeFalse();
        magicMissile.AccessPaths.Should().BeEmpty();
    }

    [Fact]
    public void TreatsRitualBookSelectionsAsRitualOnly()
    {
        var detectMagic = Spell(
            "ID_SPELL_DETECT_MAGIC",
            "Detect Magic",
            isPrepared: true,
            isAlwaysPrepared: true,
            isRitual: true);
        var model = new MagicOverviewModel
        {
            KnownSpellGroups =
            [
                new MagicKnownSpellGroupModel(
                    "Ritual Caster",
                    [
                        new MagicKnownSpellEntryModel(
                            "book:0",
                            "Ritual Book",
                            "Detect Magic",
                            SpellLevel: 1,
                            SpellId: detectMagic.Id,
                            IsRitual: true,
                            SelectionAccess: MagicSpellSelectionAccess.RitualBook,
                            AccessSource: "Ritual Caster Book")
                    ])
            ],
            Sections =
            [
                Section(
                    "warlock",
                    "Warlock",
                    isPreparedCaster: false,
                    detectMagic,
                    ritualCastingMode: MagicRitualCastingMode.KnownSpells)
            ]
        };

        MagicSpellAccessClassifier.Apply(model);

        detectMagic.IsPrepared.Should().BeFalse();
        detectMagic.IsAlwaysPrepared.Should().BeFalse();
        detectMagic.DisplayState.Should().Be(MagicSpellDisplayState.Available);
        detectMagic.AccessPaths.Should().ContainSingle(path =>
            path.Kind == MagicSpellAccessKind.RitualBook
            && !path.CanCastNormally
            && path.CanCastAsRitual);
    }

    [Fact]
    public void CountsOnlyLocallyPreparedChoices()
    {
        var grantedSpell = Spell(
            "ID_SPELL_CURE_WOUNDS",
            "Cure Wounds",
            isPrepared: true,
            isAlwaysPrepared: true,
            grantedBy: "Life Domain");
        var preparedChoice = Spell("ID_SPELL_BLESS", "Bless", isPrepared: true);
        var model = new MagicOverviewModel
        {
            Sections =
            [
                Section(
                    "cleric",
                    "Cleric",
                    isPreparedCaster: true,
                    grantedSpell,
                    preparedChoice,
                    ritualCastingMode: MagicRitualCastingMode.PreparedSpells)
            ]
        };

        MagicSpellAccessClassifier.Apply(model);

        grantedSpell.AccessPaths.Should().ContainSingle(path =>
            path.Kind == MagicSpellAccessKind.AlwaysPrepared
            && path.SourceLabel == "Life Domain");
        preparedChoice.AccessPaths.Should().ContainSingle(path =>
            path.Kind == MagicSpellAccessKind.Prepared
            && path.SourceLabel == "Cleric");
        model.Sections[0].PreparedCount.Should().Be(1);
    }

    private static MagicKnownSpellEntryModel SpellbookEntry(
        string id,
        MagicSpellListEntryModel spell) =>
        new(
            id,
            "Spellbook (Wizard)",
            spell.Name,
            SpellLevel: spell.Level,
            SpellId: spell.Id,
            IsRitual: spell.IsRitual,
            SelectionAccess: MagicSpellSelectionAccess.Spellbook,
            AccessSource: "Wizard Spellbook");

    private static MagicSpellcastingSectionModel Section(
        string id,
        string label,
        bool isPreparedCaster,
        MagicSpellListEntryModel firstSpell,
        MagicSpellListEntryModel? secondSpell = null,
        bool isSpellbookCaster = false,
        MagicRitualCastingMode ritualCastingMode = MagicRitualCastingMode.None)
    {
        List<MagicSpellListEntryModel> spells = [firstSpell];
        if (secondSpell is not null)
        {
            spells.Add(secondSpell);
        }

        return new MagicSpellcastingSectionModel
        {
            Id = id,
            Label = label,
            IsPreparedCaster = isPreparedCaster,
            IsSpellbookCaster = isSpellbookCaster,
            RitualCastingMode = ritualCastingMode,
            SpellLevels = [new MagicSpellLevelModel(1, spells, totalSlots: 4, usedSlots: 0)],
            PreparedCount = spells.Count(spell => spell.IsPrepared && !spell.IsAlwaysPrepared),
            MaxPrepared = 4
        };
    }

    private static MagicSpellListEntryModel Spell(
        string id,
        string name,
        bool isPrepared = false,
        bool isAlwaysPrepared = false,
        bool isRitual = false,
        string grantedBy = "") =>
        new(
            id,
            name,
            1,
            "Player's Handbook",
            string.Empty,
            isRitual,
            isConcentration: false,
            isPrepared,
            isAlwaysPrepared,
            isCantrip: false,
            isAlwaysPrepared
                ? MagicSpellDisplayState.AlwaysPrepared
                : isPrepared
                    ? MagicSpellDisplayState.Prepared
                    : MagicSpellDisplayState.Available,
            grantedBy: grantedBy);
}
