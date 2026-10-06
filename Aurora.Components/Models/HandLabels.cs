namespace Aurora.Components.Models;

/// <summary>Which hand or hands an inventory row occupies.</summary>
public enum HandHeld
{
    None,
    Main,
    Off,

    /// <summary>One row in both slots: a two-handed weapon, or a versatile one held in both hands.</summary>
    Both,
}

/// <summary>
/// Tells apart rows that share a name. Two daggers are two rows, one per hand, and the lists show
/// them under the same name; the one in the main hand reads "Dagger (M)" and the one in the off hand
/// "Dagger (O)". This only changes how a row is shown, never what it is called on the character.
/// </summary>
public static class HandLabels
{
    // The engine's own EquippedLocation strings for a row held in a hand.
    private const string PrimaryHand = "Primary Hand";
    private const string SecondaryHand = "Secondary Hand";
    private const string TwoHanded = "Two-Handed";
    private const string TwoHandedVersatile = "Two-Handed (Versatile)";

    public static HandHeld Parse(string? equippedLocation) => equippedLocation?.Trim() switch
    {
        PrimaryHand => HandHeld.Main,
        SecondaryHand => HandHeld.Off,
        TwoHanded or TwoHandedVersatile => HandHeld.Both,
        _ => HandHeld.None,
    };

    public static string Suffix(HandHeld hand) => hand switch
    {
        HandHeld.Main => " (M)",
        HandHeld.Off => " (O)",
        HandHeld.Both => " (M+O)",
        _ => string.Empty,
    };

    /// <summary>
    /// The shown name for each row that needs one: a row held in a hand while another row has the same
    /// name. Rows that are not in a hand, or whose name is theirs alone, are left out, so look a row up
    /// with <c>GetValueOrDefault(identifier, name)</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> For(
        IEnumerable<(string Identifier, string Name, string? EquippedLocation)> rows)
    {
        var all = rows.ToList();
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        if (all.Count < 2)
            return labels;

        var sharedNames = all
            .GroupBy(row => row.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (identifier, name, location) in all)
        {
            HandHeld hand = Parse(location);
            if (hand != HandHeld.None && sharedNames.Contains(name.Trim()))
                labels[identifier] = name + Suffix(hand);
        }

        return labels;
    }
}
