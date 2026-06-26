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
}
