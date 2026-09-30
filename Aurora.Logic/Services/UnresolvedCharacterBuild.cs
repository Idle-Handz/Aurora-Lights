using System.Xml.Linq;

namespace Builder.Presentation.Services;

/// <summary>Keeps unresolved save records through saves and in-memory tab snapshots.</summary>
internal sealed class UnresolvedCharacterBuild
{
    private readonly XElement _saved;
    private readonly HashSet<string> _missing;
    private readonly HashSet<string> _initiallyLoaded;

    internal UnresolvedCharacterBuild(XElement saved, IEnumerable<string> missing, IEnumerable<string> loaded)
    {
        _saved = new XElement(saved);
        _missing = missing.ToHashSet(StringComparer.Ordinal);
        _initiallyLoaded = loaded.ToHashSet(StringComparer.Ordinal);
    }

    private static string Id(XElement node) => (string?)node.Attribute("registered") ?? (string?)node.Attribute("id") ?? "";
    private static string ResolveId(string id) => ElementIdAliases.TryGetTarget(id, out string target) ? target : id;

    internal void MergeInto(XElement build, IEnumerable<string> currentIds)
    {
        var current = currentIds.ToHashSet(StringComparer.Ordinal);
        var retained = new HashSet<string>(StringComparer.Ordinal);
        if (_saved.Element("elements") is { } savedElements && build.Element("elements") is { } elements)
            MergeElements(savedElements, elements, current, retained);

        // Details rejected by the companion identity check still belong to an unresolved
        // saved creature. Keep them with its retained choice until it is restored or replaced.
        var savedCompanions = _saved.Element("companions")?.Elements("companion") ?? _saved.Elements("companion");
        var unresolvedCompanions = savedCompanions.Where(e =>
            _missing.Contains(Id(e)) && retained.Contains(Id(e)) && !current.Contains(ResolveId(Id(e)))).ToArray();
        if (unresolvedCompanions.Length > 0)
        {
            var companions = build.Element("companions");
            if (companions == null)
            {
                companions = new XElement("companions");
                build.Add(companions);
            }
            var available = companions.Elements("companion").ToList();
            foreach (var record in unresolvedCompanions)
            {
                var match = available.FirstOrDefault(e => XNode.DeepEquals(e, record));
                if (match == null) companions.Add(new XElement(record));
                else available.Remove(match);
            }
        }

        // Unavailable inventory entries are not constructed at all. Keep their complete
        // saved record, including identifier, quantity, location, and equipment state.
        if (build.Element("equipment") is { } destination)
        {
            var anonymousItems = destination.Elements("item")
                .Where(e => string.IsNullOrEmpty((string?)e.Attribute("identifier"))).ToList();
            foreach (var item in (_saved.Element("equipment")?.Elements("item") ?? []).Where(e => _missing.Contains(Id(e))))
            {
                string identifier = (string?)item.Attribute("identifier") ?? "";
                bool alreadyPresent;
                if (identifier.Length > 0)
                    alreadyPresent = destination.Elements("item").Any(e => (string?)e.Attribute("identifier") == identifier);
                else
                {
                    // Match the complete record once per occurrence. Equal quantities do not
                    // identify an instance, and even identical rows can represent separate items.
                    var match = anonymousItems.FirstOrDefault(e => XNode.DeepEquals(e, item));
                    alreadyPresent = match != null;
                    if (match != null) anonymousItems.Remove(match);
                }
                if (!alreadyPresent)
                    destination.Add(new XElement(item));
                retained.Add(Id(item));
            }
        }

        if (_saved.Element("sum") is not { } savedSum || build.Element("sum") is not { } sum) return;
        var treeIds = (_saved.Element("elements")?.Descendants("element") ?? []).Select(Id).ToHashSet(StringComparer.Ordinal);
        foreach (var group in savedSum.Elements("element").Where(e => _missing.Contains(Id(e))) .GroupBy(Id))
        {
            // Summary-only leftovers remain visible until explicitly adjudicated. A
            // choice intentionally replaced/removed after load must not be resurrected.
            if (treeIds.Contains(group.Key) && !retained.Contains(group.Key)) continue;
            int present = sum.Elements("element").Count(e => ResolveId(Id(e)) == ResolveId(group.Key));
            foreach (var node in group.Skip(present)) sum.Add(new XElement(node));
        }
    }

    private void MergeElements(XElement savedParent, XElement parent, HashSet<string> current, HashSet<string> retained)
    {
        var claimed = new HashSet<XElement>();
        foreach (var saved in savedParent.Elements("element"))
        {
            bool hasMissing = saved.DescendantsAndSelf("element").Any(e => _missing.Contains(Id(e)));
            bool unresolvedClass = _missing.Contains((string?)saved.Attribute("class") ?? "");
            if (!hasMissing && !unresolvedClass) continue;
            string id = Id(saved);
            bool isChoice = saved.Attribute("registered") is not null;
            var candidates = parent.Elements("element").Where(e => !claimed.Contains(e) &&
                (string?)e.Attribute("type") == (string?)saved.Attribute("type")).ToArray();
            var match = candidates.FirstOrDefault(e => ResolveId(Id(e)) == ResolveId(id) &&
                (!isChoice || SameSlot(saved, e)));
            // An explicit new value in this same choice slot supersedes the unresolved one.
            if (match is null && isChoice && candidates.Any(e => SameSlot(saved, e) && !string.IsNullOrWhiteSpace(Id(e)))) continue;
            if (match is null)
            {
                // Do not restore a previously loaded parent the user has since removed.
                if (_initiallyLoaded.Contains(ResolveId(id)) && !current.Contains(ResolveId(id))) continue;
                match = new XElement(saved);
                parent.Add(match);
                foreach (var node in saved.DescendantsAndSelf("element"))
                    if (_missing.Contains(Id(node))) retained.Add(Id(node));
            }
            else
            {
                if (_missing.Contains(id) && !current.Contains(ResolveId(id))) retained.Add(id);
                MergeElements(saved, match, current, retained);
            }
            claimed.Add(match);
            if (unresolvedClass && (match.Attribute("class") is null ||
                (string?)match.Attribute("class") == (string?)saved.Attribute("class")))
            {
                foreach (string attribute in new[] { "multiclass", "starting", "class", "rndhp" })
                    match.SetAttributeValue(attribute, (string?)saved.Attribute(attribute));
            }
        }
    }

    private static bool SameSlot(XElement first, XElement second) => second.Attribute("registered") is not null &&
        new[] { "name", "number", "requiredLevel", "replaceLevel" }
            .All(attribute => (string?)first.Attribute(attribute) == (string?)second.Attribute(attribute));
}
