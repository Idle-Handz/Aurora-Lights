using System.Xml.Linq;

namespace Builder.Presentation.Services;

/// <summary>Checks saved character usage after reconstruction, never net catalog/count differences.</summary>
public static class CharacterLoadValidation
{
    public sealed record MissingElement(string Id, int Count, string SavedPath);

    public static IReadOnlyList<MissingElement> FindMissing(XElement build, IEnumerable<string> loadedIds,
        IEnumerable<string>? beforeNormalization = null, IEnumerable<string>? afterNormalization = null)
    {
        static Dictionary<string, int> Counts(IEnumerable<string> ids) => ids
            .Where(id => !string.IsNullOrWhiteSpace(id)).GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // Source selections live outside <build>. A Source-only subtree is also
        // availability bookkeeping; never exempt an actual choice or its grant chain.
        var used = new Dictionary<string, string>(StringComparer.Ordinal);
        var availabilityOnly = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in build.Element("elements")?.Descendants("element") ?? [])
        {
            if (string.Equals((string?)node.Attribute("isList"), "true", StringComparison.OrdinalIgnoreCase)) continue;
            string id = (string?)node.Attribute("registered") ?? (string?)node.Attribute("id") ?? "";
            if (string.IsNullOrWhiteSpace(id)) continue;
            var chain = node.AncestorsAndSelf().TakeWhile(e => e.Name == "element").Reverse().ToArray();
            bool choice = chain.Any(e => (string?)e.Attribute("type") != "Source" &&
                !string.IsNullOrWhiteSpace((string?)e.Attribute("registered")));
            if (!choice && (string?)chain[0].Attribute("type") == "Source")
            {
                availabilityOnly.Add(id);
                continue;
            }
            used.TryAdd(id, string.Join(" > ", chain.Select(e =>
                (string?)e.Attribute("name") ?? (string?)e.Attribute("registered") ?? (string?)e.Attribute("id") ?? "element")));
        }
        var expected = Counts((build.Element("sum")?.Elements("element") ?? [])
            .Where(e => used.ContainsKey((string?)e.Attribute("id") ?? "") ||
                ((string?)e.Attribute("type") != "Source" && !availabilityOnly.Contains((string?)e.Attribute("id") ?? "")))
            .Select(e => (string?)e.Attribute("id") ?? ""));
        // An explicit saved choice is still significant in older/incomplete sums.
        foreach (string id in used.Keys) expected.TryAdd(id, 1);

        var loaded = Counts(loadedIds);
        var before = Counts(beforeNormalization ?? []);
        var after = Counts(afterNormalization ?? []);
        var missing = new List<MissingElement>();
        foreach (var (id, count) in expected)
        {
            // Discount only these exact occurrences actually removed by approved
            // duplicate/ASI cleanup. An unrelated addition/removal cannot mask a loss.
            int normalized = Math.Min(Math.Max(0, before.GetValueOrDefault(id) - after.GetValueOrDefault(id)),
                Math.Max(0, count - after.GetValueOrDefault(id)));
            int absent = Math.Max(0, count - normalized - loaded.GetValueOrDefault(id));
            if (absent > 0) missing.Add(new(id, absent, used.GetValueOrDefault(id, "saved character summary (origin unavailable)")));
        }
        return missing;
    }

    /// <summary>
    /// Separates entries the character itself keeps out. A source a character restricts stops
    /// granting it anything, so an element saved before that restriction is expected to be absent
    /// now — that is the restriction working, not a lost element.
    /// </summary>
    public static (IReadOnlyList<MissingElement> Lost, IReadOnlyList<MissingElement> Restricted) SplitRestricted(
        IReadOnlyList<MissingElement> missing,
        BuildSourceRestrictionSnapshot restrictions,
        Func<string, Builder.Data.ElementBase?> lookup)
    {
        ArgumentNullException.ThrowIfNull(missing);
        ArgumentNullException.ThrowIfNull(restrictions);
        ArgumentNullException.ThrowIfNull(lookup);

        if (restrictions.ElementIds.Count == 0 && restrictions.SourceNames.Count == 0)
            return (missing, []);

        var lost = new List<MissingElement>();
        var restricted = new List<MissingElement>();
        foreach (MissingElement element in missing)
        {
            Builder.Data.ElementBase? definition = lookup(element.Id);
            bool keptOut = definition is null
                ? restrictions.ElementIds.Contains(element.Id)
                : !restrictions.Allows(definition);
            (keptOut ? restricted : lost).Add(element);
        }
        return (lost, restricted);
    }
}
