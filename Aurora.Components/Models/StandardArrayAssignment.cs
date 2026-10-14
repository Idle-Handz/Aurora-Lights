using System.Collections.Generic;
using System.Linq;

namespace Aurora.Components.Models;

/// <summary>Reassigns an array value by swapping scores instead of blocking occupied values.</summary>
public static class StandardArrayAssignment
{
    public static Dictionary<string, int> Assign(
        IDictionary<string, int> assignments,
        IReadOnlyDictionary<string, int> scores,
        string stat,
        int value)
    {
        var updated = scores.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (value == 0 || !scores.ContainsKey(stat)) return updated;

        int previousAssignment = assignments.TryGetValue(stat, out int assigned) ? assigned : 0;
        string? occupiedStat = assignments
            .FirstOrDefault(pair => pair.Key != stat && pair.Value == value).Key;
        if (occupiedStat is not null)
        {
            // An unassigned row still has a live score. Swap that score too, so a transfer
            // never silently invents or drops a score while the array is being completed.
            assignments[occupiedStat] = previousAssignment;
            updated[occupiedStat] = scores[stat];
        }

        assignments[stat] = value;
        updated[stat] = value;
        return updated;
    }
}
