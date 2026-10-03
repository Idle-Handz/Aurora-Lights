namespace Aurora.Components.Models;

/// <summary>
/// Turns the engine's source groups into the tree the editor renders: categories on top, the
/// publishers they draw from beneath, sources at the leaves. The engine's own grouping is kept as
/// the second level because it is the useful distinction inside a category — Unearthed Arcana alone
/// is a third of the official material — and because it preserves the order the engine sorts in.
/// </summary>
public static class SourceRestrictionTreeBuilder
{
    /// <summary>Sources the builder itself needs. They are shown, locked, under their own node.</summary>
    public const string RequiredNodeId = "required";

    private const string UncategorizedNodeId = "uncategorized";

    public static IReadOnlyList<SourceRestrictionNodeModel> Build(
        IReadOnlyList<SourceRestrictionGroupModel> groups)
    {
        // The engine's order within a group is deliberate (core rulebooks, then supplements), and
        // so is the order of the groups themselves; both are preserved by walking them in order.
        var placed = groups
            .SelectMany(group => group.Sources.Select(source => (Group: group, Source: source)))
            .ToList();

        var nodes = new List<SourceRestrictionNodeModel>();
        foreach (var definition in SourceRestrictionCategories.InDisplayOrder)
        {
            var members = placed
                .Where(entry => entry.Source.AllowUnchecking && entry.Source.Category == definition.Category)
                .ToList();
            if (members.Count == 0) continue;

            nodes.Add(CategoryNode(
                $"category:{definition.Category}", definition.Label, definition.Description, members));
        }

        var uncategorized = placed
            .Where(entry => entry.Source.AllowUnchecking && entry.Source.Category is null)
            .ToList();
        if (uncategorized.Count > 0)
        {
            nodes.Add(CategoryNode(UncategorizedNodeId, "Other sources",
                "Content that records no rules context", uncategorized));
        }

        // Last, because there is nothing to decide about them.
        var required = placed.Where(entry => !entry.Source.AllowUnchecking).Select(entry => entry.Source).ToList();
        if (required.Count > 0)
        {
            nodes.Add(new SourceRestrictionNodeModel(RequiredNodeId, "Required builder sources",
                "The builder needs these, so they are always enabled", AllowUnchecking: false, [], required));
        }

        return nodes;
    }

    private static SourceRestrictionNodeModel CategoryNode(
        string id,
        string label,
        string description,
        IReadOnlyList<(SourceRestrictionGroupModel Group, SourceRestrictionItemModel Source)> members)
    {
        var byPublisher = members
            .GroupBy(entry => entry.Group.Id, StringComparer.Ordinal)
            .ToList();

        // A category whose sources all come from one publisher says nothing by repeating its name.
        if (byPublisher.Count == 1)
        {
            return new SourceRestrictionNodeModel(id, label, description, AllowUnchecking: true, [],
                members.Select(entry => entry.Source).ToList());
        }

        var children = byPublisher
            .Select(group => new SourceRestrictionNodeModel(
                $"{id}/{group.Key}",
                group.First().Group.Name,
                group.First().Group.Summary,
                AllowUnchecking: true,
                [],
                group.Select(entry => entry.Source).ToList()))
            .ToList();

        return new SourceRestrictionNodeModel(id, label, description, AllowUnchecking: true, children, []);
    }
}
