using System.Reflection;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class CharacterFileSelectionRestoreTests
{
    private const string SmallDrakeId = "ID_TBOX_COMPANION_DRAKEWARDEN_DRAKE_COMPANION_SMALL";
    private const string MediumDrakeId = "ID_TBOX_COMPANION_DRAKEWARDEN_DRAKE_COMPANION_MEDIUM";
    private const string LargeDrakeId = "ID_TBOX_COMPANION_DRAKEWARDEN_DRAKE_COMPANION_LARGE";

    [Fact]
    public void ResolveSavedSelectRule_UsesRegisteredIdToDisambiguateSameChecksumRows()
    {
        var small = CreateDrakeCompanionRule(SmallDrakeId);
        var medium = CreateDrakeCompanionRule(MediumDrakeId);
        var large = CreateDrakeCompanionRule(LargeDrakeId);

        string checksum = CharacterFileVerification.GenerateCrC(medium, 1);
        CharacterFileVerification.GenerateCrC(small, 1).Should().Be(checksum);
        CharacterFileVerification.GenerateCrC(large, 1).Should().Be(checksum);

        var resolved = ResolveSavedSelectRule(
            [small, medium, large],
            checksum,
            MediumDrakeId,
            number: 1);

        resolved.Should().BeSameAs(medium);
    }

    [Fact]
    public void ResolveSavedSelectRule_UsesChecksumToDisambiguateListRowsWithSameName()
    {
        var first = CreateListRule("ID_TEST_BACKGROUND", requiredLevel: 1);
        var second = CreateListRule("ID_TEST_BACKGROUND", requiredLevel: 3);

        string checksum = CharacterFileVerification.GenerateCrC(second, 1);
        CharacterFileVerification.GenerateCrC(first, 1).Should().NotBe(checksum);

        var resolved = ResolveSavedSelectRule(
            [first, second],
            checksum,
            registeredElementId: "2",
            number: 1);

        resolved.Should().BeSameAs(second);
    }

    [Fact]
    public void ResolveSavedSelectRule_PrefersChoiceRowKeyBeforeChecksumFallback()
    {
        var first = CreateListRule("ID_TEST_BACKGROUND", requiredLevel: 1);
        var second = CreateListRule("ID_TEST_BACKGROUND", requiredLevel: 3);

        string checksumForFirst = CharacterFileVerification.GenerateCrC(first, 1);
        string rowKeyForSecond = SelectionRuleIdentityService.Create(second, 1).ChoiceRowKey;

        var resolved = ResolveSavedSelectRule(
            [first, second],
            checksumForFirst,
            registeredElementId: "1",
            number: 1,
            choiceRowKey: rowKeyForSecond,
            choiceKey: null,
            selectId: null);

        resolved.Should().BeSameAs(second);
    }

    [Fact]
    public void SelectionRuleIdentity_ProducesDistinctRowsForSameVisibleLabels()
    {
        var paladin = CreateWeaponMasteryRule(
            "ID_WOTC_PHB24_CLASS_FEATURE_PALADIN_WEAPON_MASTERY");
        var ranger = CreateWeaponMasteryRule(
            "ID_WOTC_PHB24_CLASS_FEATURE_RANGER_WEAPON_MASTERY");

        var paladinIdentity = SelectionRuleIdentityService.Create(paladin, 1);
        var rangerIdentity = SelectionRuleIdentityService.Create(ranger, 1);

        paladinIdentity.ChoiceRowKey.Should().NotBe(rangerIdentity.ChoiceRowKey);
        paladinIdentity.ChoiceKey.Should().NotBe(rangerIdentity.ChoiceKey);
        paladinIdentity.SelectId.Should().NotBe(rangerIdentity.SelectId);
    }

    private static SelectRule CreateDrakeCompanionRule(string defaultId)
    {
        var rule = new SelectRule(new ElementHeader(
            "Drake Companion Revised",
            "Archetype Feature",
            "The Book of Xellarant",
            "ID_TBOX_ARCHETYPE_FEATURE_DRAKEWARDEN_DRAKE_COMPANION"));

        rule.Attributes.Type = "Companion";
        rule.Attributes.Name = "Drake Companion";
        rule.Attributes.RequiredLevel = 1;
        rule.Attributes.Default = defaultId;
        return rule;
    }

    private static SelectRule CreateWeaponMasteryRule(string ownerId)
    {
        var rule = new SelectRule(new ElementHeader(
            "Level 1: Weapon Mastery",
            "Class Feature",
            "Player's Handbook (2024)",
            ownerId));

        rule.Attributes.Type = "Class Feature";
        rule.Attributes.Name = "Weapon Mastery";
        rule.Attributes.RequiredLevel = 1;
        return rule;
    }

    private static SelectRule CreateListRule(string ownerId, int requiredLevel)
    {
        var rule = new SelectRule(new ElementHeader(
            "Acolyte",
            "Background",
            "Player's Handbook",
            ownerId));

        rule.Attributes.Type = "List";
        rule.Attributes.Name = "Personality Trait";
        rule.Attributes.RequiredLevel = requiredLevel;
        rule.Attributes.ListItems =
        [
            new SelectionRuleListItem(1, "I test the suspicious lever first."),
            new SelectionRuleListItem(2, "I keep careful notes.")
        ];
        return rule;
    }

    private static SelectRule? ResolveSavedSelectRule(
        IEnumerable<SelectRule> candidates,
        string existingChecksum,
        string registeredElementId,
        int number)
    {
        var method = typeof(CharacterFile).GetMethod(
            "ResolveSavedSelectRule",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(IEnumerable<SelectRule>),
                typeof(string),
                typeof(string),
                typeof(int)
            ],
            modifiers: null);

        method.Should().NotBeNull();
        return (SelectRule?)method!.Invoke(null, [candidates, existingChecksum, registeredElementId, number]);
    }

    private static SelectRule? ResolveSavedSelectRule(
        IEnumerable<SelectRule> candidates,
        string existingChecksum,
        string registeredElementId,
        int number,
        string? choiceRowKey,
        string? choiceKey,
        string? selectId)
    {
        var method = typeof(CharacterFile).GetMethod(
            "ResolveSavedSelectRule",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(IEnumerable<SelectRule>),
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(string),
                typeof(string),
                typeof(string)
            ],
            modifiers: null);

        method.Should().NotBeNull();
        return (SelectRule?)method!.Invoke(null,
        [
            candidates,
            existingChecksum,
            registeredElementId,
            number,
            choiceRowKey,
            choiceKey,
            selectId
        ]);
    }
}
