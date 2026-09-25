namespace Builder.Presentation.Services;

/// <summary>
/// Classifies whether a candidate in a selection picker is unavailable because the character
/// already owns it: the same element, or - for types where a name identifies one thing - another
/// element of the same name, such as the 2024 Bane when the 2014 one is already known. The
/// selection currently being edited remains available, along with its twins, so users can keep it,
/// replace it, or swap it for the other ruleset's version.
/// </summary>
public static class SelectionOptionAvailability
{
    public static bool IsDisabled(
        string candidateId,
        bool candidateAllowsDuplicate,
        string? currentSelectionId,
        IReadOnlySet<string> ownedNonRepeatableElementIds,
        string? candidateName = null,
        IReadOnlySet<string>? ownedNonRepeatableElementNames = null)
    {
        if (string.IsNullOrWhiteSpace(candidateId) || candidateAllowsDuplicate)
            return false;

        if (string.Equals(candidateId, currentSelectionId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (ownedNonRepeatableElementIds.Contains(candidateId))
            return true;

        // The owned names never include the pick being edited, so its twin stays selectable.
        return !string.IsNullOrWhiteSpace(candidateName)
            && ownedNonRepeatableElementNames?.Contains(candidateName!) == true;
    }
}
