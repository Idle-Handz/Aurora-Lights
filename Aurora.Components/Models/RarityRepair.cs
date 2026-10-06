namespace Aurora.Components.Models;

/// <summary>
/// Content spells rarity in many ways: "Very rare", "Very Rare", the odd "Vert Rare" or "unommon", and
/// a few values that are not rarities at all ("Rarity Varies", "Artificer Infusion"). Filters should
/// offer a short, fixed set, so every value lands in exactly one of these:
/// <list type="bullet">
/// <item>One of the seven real rarities. Case and padding are harmless, and a typo is a rarity the
/// content did not mean to invent, so a value that is exactly one edit (a letter added, dropped or
/// changed, or two swapped) from exactly one real rarity is read as that rarity. A value two edits
/// away, or equally near two rarities, is not a confident fix and is not guessed at.</item>
/// <item><see cref="Varies"/>: the rarity depends on something, or is a range ("Rarity Varies",
/// "Rarity varies by potion type", "Rare, Very Rare, or Legendary").</item>
/// <item><see cref="Infusion"/>: an artificer infusion rather than an item with a rarity.</item>
/// <item><see cref="Unknown"/>: anything else, including the content saying so.</item>
/// </list>
/// An empty value stays empty; that is a mundane item.
/// </summary>
public static class RarityRepair
{
    public const string Varies = "Varies";
    public const string Infusion = "Infusion";
    public const string Unknown = "Unknown";

    private static readonly string[] Order =
        ["Common", "Uncommon", "Rare", "Very Rare", "Legendary", "Artifact", "Unique"];

    private static readonly string[] GroupOrder = [Varies, Infusion, Unknown];

    private static readonly string[] Ranked = [.. Order, .. GroupOrder];

    /// <summary>The rarities the game knows, in rank order (common up to unique).</summary>
    public static IReadOnlyList<string> Known => Order;

    /// <summary>The groups that stand in for values that are not a rarity, after the real ones.</summary>
    public static IReadOnlyList<string> Groups => GroupOrder;

    /// <summary>
    /// Position of a normalized rarity: the real rarities in order, then <see cref="Varies"/>,
    /// <see cref="Infusion"/> and <see cref="Unknown"/>; anything else ranks last.
    /// </summary>
    public static int Rank(string? normalized)
    {
        int index = normalized is null ? -1 : Array.IndexOf(Ranked, normalized);
        return index >= 0 ? index : Ranked.Length;
    }

    /// <summary>
    /// The canonical spelling when <paramref name="raw"/> is a known rarity apart from case and padding,
    /// otherwise null.
    /// </summary>
    public static string? Canonical(string? raw)
    {
        string tidy = Tidy(raw);
        foreach (string known in Order)
        {
            if (string.Equals(known, tidy, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        return null;
    }

    /// <summary>
    /// The known rarity a typo is one edit from, or null when <paramref name="raw"/> is empty, already
    /// known, too far from every rarity, or as near to two of them as to one.
    /// </summary>
    public static string? Suggest(string? raw)
    {
        string tidy = Tidy(raw);
        if (tidy.Length == 0 || Canonical(tidy) is not null)
            return null;

        string? match = null;
        foreach (string known in Order)
        {
            if (!WithinOneEdit(tidy, known))
                continue;

            if (match is not null)
                return null;
            match = known;
        }

        return match;
    }

    /// <summary>
    /// The one value a rarity is filtered and shown as: a real rarity (typos read as the rarity meant),
    /// else <see cref="Infusion"/>, <see cref="Varies"/> or <see cref="Unknown"/>. Empty stays empty.
    /// </summary>
    public static string Normalize(string? raw)
    {
        string tidy = Tidy(raw);
        if (tidy.Length == 0)
            return string.Empty;

        return Canonical(tidy) ?? Suggest(tidy) ?? Group(tidy) ?? Unknown;
    }

    /// <summary>
    /// Whether the content means something by <paramref name="raw"/>: empty, a real rarity, or a value
    /// that belongs in <see cref="Varies"/>, <see cref="Infusion"/> or says <see cref="Unknown"/>. A typo
    /// is not accepted (it should be fixed), and neither is a value nothing recognises.
    /// </summary>
    public static bool IsAccepted(string? raw)
    {
        string tidy = Tidy(raw);
        return tidy.Length == 0
            || Canonical(tidy) is not null
            || (Suggest(tidy) is null && Group(tidy) is not null);
    }

    // The group a value that is not a real rarity belongs to, or null when it belongs to none.
    private static string? Group(string tidy)
    {
        if (tidy.Contains("infusion", StringComparison.OrdinalIgnoreCase))
            return Infusion;

        if (tidy.Contains("vary", StringComparison.OrdinalIgnoreCase)
            || tidy.Contains("varies", StringComparison.OrdinalIgnoreCase)
            || tidy.Contains("varying", StringComparison.OrdinalIgnoreCase)
            || tidy.Contains("variable", StringComparison.OrdinalIgnoreCase)
            || NamesSeveralRarities(tidy))
            return Varies;

        return string.Equals(tidy, Unknown, StringComparison.OrdinalIgnoreCase) ? Unknown : null;
    }

    // "Rare, Very Rare, or Legendary", "Uncommon to Rare": a range or a choice of real rarities.
    private static bool NamesSeveralRarities(string tidy)
    {
        string separated = tidy;
        foreach (string separator in new[] { " or ", " and ", " to ", "/", "&", ";" })
            separated = separated.Replace(separator, ",", StringComparison.OrdinalIgnoreCase);

        return separated.Split(',')
            .Select(part => Canonical(part))
            .Where(known => known is not null)
            .Distinct()
            .Count() >= 2;
    }

    private static string Tidy(string? raw) =>
        string.Join(' ', (raw ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Distance 0 or 1 under insert, delete, substitute and swap-adjacent, ignoring case.
    private static bool WithinOneEdit(string a, string b)
    {
        static bool Same(char x, char y) => char.ToUpperInvariant(x) == char.ToUpperInvariant(y);

        static bool TailsMatch(string x, int xFrom, string y, int yFrom)
        {
            if (x.Length - xFrom != y.Length - yFrom)
                return false;

            for (int i = xFrom, j = yFrom; i < x.Length; i++, j++)
            {
                if (!Same(x[i], y[j]))
                    return false;
            }

            return true;
        }

        if (a.Length == b.Length)
        {
            int i = 0;
            while (i < a.Length && Same(a[i], b[i]))
                i++;

            if (i == a.Length)
                return true;

            if (TailsMatch(a, i + 1, b, i + 1))
                return true;

            return i + 1 < a.Length && Same(a[i], b[i + 1]) && Same(a[i + 1], b[i]) && TailsMatch(a, i + 2, b, i + 2);
        }

        if (Math.Abs(a.Length - b.Length) != 1)
            return false;

        string longer = a.Length > b.Length ? a : b;
        string shorter = a.Length > b.Length ? b : a;
        int k = 0;
        while (k < shorter.Length && Same(longer[k], shorter[k]))
            k++;

        return TailsMatch(longer, k + 1, shorter, k);
    }
}
