using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

public sealed class SelectionOptionAvailabilityTests
{
    private static readonly IReadOnlySet<string> OwnedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ID_LANGUAGE_ELVISH",
        "ID_SPELL_SHIELD",
    };

    [Fact]
    public void IsDisabled_DisablesAnOwnedNonRepeatableCandidateFromAnotherSlot()
    {
        SelectionOptionAvailability.IsDisabled(
                "ID_LANGUAGE_ELVISH",
                candidateAllowsDuplicate: false,
                currentSelectionId: "ID_LANGUAGE_DWARVISH",
                ownedNonRepeatableElementIds: OwnedIds)
            .Should().BeTrue();
    }

    [Fact]
    public void IsDisabled_LeavesTheCurrentSlotSelectionAvailable()
    {
        SelectionOptionAvailability.IsDisabled(
                "ID_LANGUAGE_ELVISH",
                candidateAllowsDuplicate: false,
                currentSelectionId: "ID_LANGUAGE_ELVISH",
                ownedNonRepeatableElementIds: OwnedIds)
            .Should().BeFalse();
    }

    [Fact]
    public void IsDisabled_LeavesRepeatableCandidatesAvailable()
    {
        SelectionOptionAvailability.IsDisabled(
                "ID_LANGUAGE_ELVISH",
                candidateAllowsDuplicate: true,
                currentSelectionId: null,
                ownedNonRepeatableElementIds: OwnedIds)
            .Should().BeFalse();
    }

    [Fact]
    public void IsDisabled_LeavesUnownedCandidatesAvailable()
    {
        SelectionOptionAvailability.IsDisabled(
                "ID_LANGUAGE_DRACONIC",
                candidateAllowsDuplicate: false,
                currentSelectionId: null,
                ownedNonRepeatableElementIds: OwnedIds)
            .Should().BeFalse();
    }

    // A name the character already holds, from the other ruleset's printing of the same thing.
    private static readonly IReadOnlySet<string> OwnedNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Bane" };

    [Fact]
    public void IsDisabled_DisablesTheOtherRulesetsPrintingOfAHeldName()
    {
        SelectionOptionAvailability.IsDisabled(
                "ID_WOTC_PHB24_SPELL_BANE",
                candidateAllowsDuplicate: false,
                currentSelectionId: null,
                ownedNonRepeatableElementIds: OwnedIds,
                candidateName: "Bane",
                ownedNonRepeatableElementNames: OwnedNames)
            .Should().BeTrue();
    }

    [Fact]
    public void IsDisabled_LeavesTheTwinOfTheSlotBeingEditedAvailable()
    {
        // The pick being edited is kept out of the owned names, so swapping 2014 Bane for the 2024
        // one is a replacement rather than a second copy.
        SelectionOptionAvailability.IsDisabled(
                "ID_WOTC_PHB24_SPELL_BANE",
                candidateAllowsDuplicate: false,
                currentSelectionId: "ID_PHB_SPELL_BANE",
                ownedNonRepeatableElementIds: OwnedIds,
                candidateName: "Bane",
                ownedNonRepeatableElementNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .Should().BeFalse();
    }

    [Fact]
    public void IsDisabled_MatchesHeldNamesWithoutRegardToCase()
    {
        SelectionOptionAvailability.IsDisabled(
                "ID_OTHER_BANE", candidateAllowsDuplicate: false, currentSelectionId: null,
                ownedNonRepeatableElementIds: OwnedIds,
                candidateName: "bane", ownedNonRepeatableElementNames: OwnedNames)
            .Should().BeTrue();
    }

    [Fact]
    public void IsDisabled_IgnoresNamesForTypesThatDoNotEnforceThem()
    {
        // The resolver passes no names for those types, so nothing is blocked by name.
        SelectionOptionAvailability.IsDisabled(
                "ID_SOME_TRAIT", candidateAllowsDuplicate: false, currentSelectionId: null,
                ownedNonRepeatableElementIds: OwnedIds,
                candidateName: "Bane", ownedNonRepeatableElementNames: null)
            .Should().BeFalse();
    }

    [Fact]
    public void IsDisabled_LeavesARepeatableCandidateAvailableEvenWhenItsNameIsHeld()
    {
        SelectionOptionAvailability.IsDisabled(
                "ID_WOTC_PHB24_SPELL_BANE", candidateAllowsDuplicate: true, currentSelectionId: null,
                ownedNonRepeatableElementIds: OwnedIds,
                candidateName: "Bane", ownedNonRepeatableElementNames: OwnedNames)
            .Should().BeFalse();
    }
}
