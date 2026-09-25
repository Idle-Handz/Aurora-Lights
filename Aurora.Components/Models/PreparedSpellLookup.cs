namespace Aurora.Components.Models;

/// <summary>
/// Prepared-state lookups over a character's spell list, for writing that state back into saved XML.
/// Neither key is unique enough to hand straight to ToDictionary: two rulesets can publish the same
/// spell name — Bane is in both Player's Handbooks, and both are in the catalog at once — and one
/// spell can appear at more than one level or from more than one grant.
/// </summary>
public static class PreparedSpellLookup
{
    /// <summary>
    /// Keyed by element id. An id names one spell, so a repeated id is that spell listed twice:
    /// it counts as prepared if any of its entries is.
    /// </summary>
    public static Dictionary<string, bool> ById<T>(
        IEnumerable<T> spells, Func<T, string?> id, Func<T, bool> isPrepared) =>
        spells
            .Where(spell => !string.IsNullOrWhiteSpace(id(spell)))
            .GroupBy(spell => id(spell)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Any(isPrepared), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Keyed by name, which is how spells saved without an id have to be matched. A name shared by
    /// several spells only answers when they agree; when they disagree it is left out, so the saved
    /// entry keeps the state it had instead of having a guess written over it. Matching by id stays
    /// the way to be certain.
    /// </summary>
    public static Dictionary<string, bool> ByName<T>(
        IEnumerable<T> spells, Func<T, string?> name, Func<T, bool> isPrepared) =>
        spells
            .Where(spell => !string.IsNullOrWhiteSpace(name(spell)))
            .GroupBy(spell => name(spell)!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(isPrepared).Distinct().Count() == 1)
            .ToDictionary(group => group.Key, group => group.Select(isPrepared).First(), StringComparer.OrdinalIgnoreCase);
}
