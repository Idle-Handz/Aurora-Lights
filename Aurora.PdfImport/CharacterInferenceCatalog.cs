using System.Xml.Linq;

namespace Aurora.PdfImport;

/// <summary>A definition from the host's effective catalog, after content conflict decisions.</summary>
public sealed record InferenceCatalogElement(string Xml, string? PackageName, string PackageKind,
    int PrecedenceRank = int.MinValue);

/// <summary>Identity and rule lookups over the same complete definitions the character builder loads.</summary>
internal sealed class CharacterInferenceCatalog
{
    private sealed record Entry(XElement Definition, ResolvedElement Element, int OfficialRank, int PrecedenceRank);
    private readonly Entry[] _entries;

    public CharacterInferenceCatalog(IEnumerable<InferenceCatalogElement> elements)
    {
        _entries = elements.Select(element =>
        {
            var definition = XElement.Parse(element.Xml);
            return new Entry(definition, new ResolvedElement
            {
                AuroraId = (string?)definition.Attribute("id") ?? "",
                TypeName = (string?)definition.Attribute("type") ?? "",
                Name = (string?)definition.Attribute("name") ?? "",
                PackageName = element.PackageName
            }, element.PackageKind switch { "core" => 2, "official" => 1, _ => 0 }, element.PrecedenceRank);
        }).ToArray();
    }

    public List<ResolvedElement> Find(string type, string name)
    {
        // A missing spell must not silently become a different spell (Light -> Daylight).
        var matches = _entries.Where(entry => entry.Element.TypeName == type
            && (type == "Spell" ? entry.Element.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                : entry.Element.Name.Contains(name, StringComparison.OrdinalIgnoreCase))).ToArray();
        int officialRank = matches.Select(entry => entry.OfficialRank).DefaultIfEmpty().Max();
        return matches.Where(entry => entry.OfficialRank == officialRank)
            .OrderBy(entry => entry.Element.Name == name ? 0
                : entry.Element.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenByDescending(entry => entry.PrecedenceRank)
            .ThenBy(entry => entry.Element.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Element.AuroraId, StringComparer.Ordinal)
            .Take(50).Select(entry => entry.Element with { }).ToList();
    }

    public ResolvedElement? FindParentRace(string subraceId)
    {
        var subrace = _entries.FirstOrDefault(entry => entry.Element.AuroraId == subraceId);
        var supports = subrace?.Definition.Elements("supports").Select(element => element.Value.Trim()).ToArray() ?? [];
        return _entries.Where(entry => entry.Element.TypeName == "Race" && supports.Contains(entry.Element.Name))
            .OrderByDescending(entry => entry.OfficialRank)
            .ThenByDescending(entry => entry.PrecedenceRank)
            .Select(entry => entry.Element with { }).FirstOrDefault();
    }

    public int CountAutoGrantedFeats(IReadOnlyCollection<string> resolvedIds) =>
        _entries.Where(entry => resolvedIds.Contains(entry.Element.AuroraId)).Sum(entry =>
        {
            var rules = entry.Definition.Elements("rules");
            int grants = rules.Elements("grant").Count(rule => (string?)rule.Attribute("type") == "Feat");
            int choices = entry.Element.TypeName is "Class Feature" or "Level" or "Grants" ? 0
                : rules.Elements("select").Count(rule => (string?)rule.Attribute("type") == "Feat");
            return grants + choices;
        });
}
