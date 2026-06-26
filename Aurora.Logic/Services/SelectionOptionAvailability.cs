namespace Builder.Presentation.Services;

/// <summary>
/// Classifies whether a candidate in a selection picker is unavailable because the
/// character already owns that non-repeatable element. The selection currently being
/// edited remains available so users can keep or replace it.
/// </summary>
public static class SelectionOptionAvailability
{
    public static bool IsDisabled(
        string candidateId,
        bool candidateAllowsDuplicate,
        string? currentSelectionId,
        IReadOnlySet<string> ownedNonRepeatableElementIds)
    {
        if (string.IsNullOrWhiteSpace(candidateId) || candidateAllowsDuplicate)
            return false;

        if (string.Equals(candidateId, currentSelectionId, StringComparison.OrdinalIgnoreCase))
            return false;

        return ownedNonRepeatableElementIds.Contains(candidateId);
    }
}
