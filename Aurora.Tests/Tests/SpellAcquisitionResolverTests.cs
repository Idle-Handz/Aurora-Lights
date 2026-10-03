using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Data.Rules.Parsers;
using Builder.Presentation.Services;
using System.Xml;

namespace Aurora.Tests.Tests;

public sealed class SpellAcquisitionResolverTests
{
    private static ElementHeader Header(string id, string type = "Feat Feature") => new(id, type, "Test", id);
    private static SpellcastingInformation Caster(string name, bool prepared = false) => new(Header("CLASS_" + name, "Class"))
        { Name = name, AbilityName = "Intelligence", Prepare = prepared, PrepareFromSpellList = name != "Wizard" };
    private static Spell SpellFrom(string origin, string type = "Feat Feature", string metadata = "", int level = 1)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"<select type='Spell' name='Choice' {metadata}/>");
        var rule = (SelectRule)new SelectRuleParser().Parse(doc.DocumentElement!, Header(origin, type));
        var spell = new ElementBase("Test spell", "Spell", "Test", "ID_TEST_SPELL").Construct<Spell>();
        spell.Level = level;
        spell.Aquisition.SelectedBy(rule);
        return spell;
    }

    [Fact]
    public void FeatureListMembershipDoesNotAssignTheFirstCaster()
    {
        var spell = SpellFrom("FEATURE");
        var result = SpellAcquisitionResolver.Resolve([spell], [Caster("Wizard"), Caster("Sorcerer")]).Single();
        result.Profile.Should().BeNull();
        result.Slots.Should().Be(SpellSlotPermission.None);
        result.Diagnostic.Should().NotBeEmpty();
    }

    [Fact]
    public void NoncasterSubclassCantripRemainsAFeatureAcquisition()
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom("GIANT_POWER", "Archetype Feature", level: 0)], []).Single();
        result.OriginId.Should().Be("GIANT_POWER");
        result.Profile.Should().BeNull();
        result.Slots.Should().Be(SpellSlotPermission.None);
    }

    [Fact]
    public void ExplicitForeignListPermissionDoesNotNeedAClassAssociation()
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom("FEATURE", metadata:
            "spell-access='always-prepared' spell-slots='any' spell-ability='Wisdom' spell-uses='1' spell-recharge='Long Rest'")], [Caster("Wizard")]).Single();
        result.Profile.Should().BeNull();
        result.CanUseSlots(false).Should().BeTrue();
        result.FreeUses.Should().Be(1);
        result.Ability.Should().Be("Wisdom");
        result.CountsAgainstKnownLimit.Should().BeFalse();
    }

    [Theory]
    [InlineData("Sorcerer", true)]
    [InlineData("Wizard", false)]
    public void LegacyMagicInitiateRequiresItsChosenClass(string characterClass, bool slots)
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom("ID_PHB_FEAT_MAGIC_INITIATE_SORCERER")], [Caster(characterClass)]).Single();
        result.CanUseSlots(false).Should().Be(slots);
        result.IsAlwaysPrepared.Should().BeFalse();
        result.FreeUses.Should().Be(1);
        result.Ability.Should().Be("Charisma");
    }

    [Fact]
    public void LegacyWizardFeatDoesNotAutomaticallyTranscribeItsSpell()
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom("ID_PHB_FEAT_MAGIC_INITIATE_WIZARD")], [Caster("Wizard", true)]).Single();
        result.Profile.Should().BeNull();
        result.CanUseSlots(true).Should().BeFalse();
        result.FreeUses.Should().Be(1);
    }

    [Fact]
    public void RevisedFeatKeepsItsOwnAbilityAndFreeRoute()
    {
        var spell = SpellFrom("ID_WOTC_PHB24_FEAT_MAGIC_INITIATE_WIZARD");
        var ability = new ElementBase(Header("ID_WOTC_PHB24_FEAT_FEATURE_MAGIC_INITIATE_WIZARD_WISDOM"));
        ability.Aquisition.SelectedBy(new SelectRule(Header("ID_WOTC_PHB24_FEAT_MAGIC_INITIATE_WIZARD")));
        var result = SpellAcquisitionResolver.Resolve([spell, ability], [Caster("Sorcerer")]).Single();
        result.Profile.Should().BeNull();
        result.IsAlwaysPrepared.Should().BeTrue();
        result.CanUseSlots(false).Should().BeTrue();
        result.Ability.Should().Be("Wisdom");
        result.FreeUses.Should().Be(1);
    }

    [Fact]
    public void ListExpansionIsNotAnAcquisitionOrPermission()
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom("PATRON", metadata: "spellcasting='Warlock' spell-access='list'")], [Caster("Warlock")]).Single();
        result.Preparation.Should().Be(SpellPreparation.ListOnly);
        result.CanUseSlots(true).Should().BeFalse();
    }

    [Fact]
    public void TwoOriginsRetainIndependentPreparationAndRemoval()
    {
        var wizardSpell = SpellFrom("WIZARD_CHOICE", "Class Feature", "spellcasting='Wizard'");
        var innateSpell = SpellFrom("INNATE", metadata: "spell-access='feature' spell-uses='1' spell-recharge='Long Rest'");
        var profiles = new[] { Caster("Wizard", true) };
        var results = SpellAcquisitionResolver.Resolve([wizardSpell, innateSpell], profiles);
        results.Should().HaveCount(2);
        results[0].CanUseSlots(false).Should().BeFalse();
        results[0].CanUseSlots(true).Should().BeTrue();
        results[1].CanUseSlots(true).Should().BeFalse();
        SpellAcquisitionResolver.Resolve([innateSpell], profiles).Single().FreeUses.Should().Be(1);
    }

    [Fact]
    public void AmbiguousExplicitClassDoesNotPickFirstProfile()
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom("FEATURE", metadata: "spellcasting='Wizard'")],
            [Caster("Wizard"), Caster("Wizard")]).Single();
        result.Profile.Should().BeNull();
    }

    [Fact]
    public void InheritsAnUnambiguousClassThroughTheActualFeatureChain()
    {
        var caster = Caster("Sorcerer");
        var parent = new ElementBase(Header("SUBCLASS", "Archetype"));
        parent.Aquisition.GrantedBy(new GrantRule(caster.ElementHeader));
        var feature = new ElementBase(Header("FEATURE", "Archetype Feature"));
        feature.Aquisition.GrantedBy(new GrantRule(parent.ElementHeader));
        var spell = SpellFrom("FEATURE", "Archetype Feature");
        var resolved = SpellAcquisitionResolver.Resolve([parent, feature, spell], [Caster("Wizard"), caster]).Single();
        resolved.Profile.Should().BeSameAs(caster);
        resolved.CanUseSlots(false).Should().BeTrue();
    }

    [Fact]
    public void VileHeresiesIsLimitedSlotCastingRatherThanAFreeOrUnrestrictedSpell()
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom(
            "ID_GFP_COFSA_ARCHETYPE_FEATURE_ACCURSED_ARCHIVE_VILEHERESIES", "Archetype Feature", "spellcasting='Warlock'")], [Caster("Warlock")]).Single();
        result.IsFeature.Should().BeTrue();
        result.FreeUses.Should().BeNull();
        result.SlotUses.Should().Be(1);
        result.Recharge.Should().Be("Long Rest");
        result.CanUseSlots(false).Should().BeTrue();
    }

    [Fact]
    public void RevisedPatronSpellsAreAlwaysPreparedAndDoNotUseKnownCapacity()
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom(
            "ID_WOTC_PHB24_ARCHETYPE_FEATURE_WARLOCK_FIEND_PATRON_FIEND_SPELLS", "Archetype Feature", "spellcasting='Warlock'")], [Caster("Warlock", true)]).Single();
        result.IsAlwaysPrepared.Should().BeTrue();
        result.CountsAgainstKnownLimit.Should().BeFalse();
    }

    [Fact]
    public void ExplicitRitualAccessDoesNotGrantSlots()
    {
        var result = SpellAcquisitionResolver.Resolve([SpellFrom("BOOK", metadata: "spellcasting='Wizard' spell-access='ritual'")], [Caster("Wizard", true)]).Single();
        result.Preparation.Should().Be(SpellPreparation.RitualOnly);
        result.CanUseSlots(true).Should().BeFalse();
    }

    [Fact]
    public void DistinctOriginsCanSelectTheSameSpellButTheSameClassCannotSelectItTwice()
    {
        var first = SpellFrom("FIRST", "Class Feature", "spellcasting='Wizard'");
        var second = SpellFrom("SECOND", "Class Feature", "spellcasting='Wizard'");
        var feat = SpellFrom("FEAT");
        var caster = Caster("Wizard", true);
        SpellAcquisitionResolver.SameSelectionDomain(first.Aquisition.SelectRule, second.Aquisition.SelectRule, [], [caster]).Should().BeTrue();
        SpellAcquisitionResolver.SameSelectionDomain(first.Aquisition.SelectRule, feat.Aquisition.SelectRule, [], [caster]).Should().BeFalse();
    }
}
