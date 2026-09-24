namespace Aurora.Components.Models;

/// <summary>
/// A branch of the sources tree: a category ("5e official"), or a publisher inside one
/// ("Unearthed Arcana"). A node holds sources directly, child nodes, or — for a category whose
/// sources all come from one publisher — sources directly with no child level at all.
/// </summary>
public sealed record SourceRestrictionNodeModel(
    string Id,
    string Label,
    string Description,
    bool AllowUnchecking,
    IReadOnlyList<SourceRestrictionNodeModel> Children,
    IReadOnlyList<SourceRestrictionItemModel> Sources)
{
    /// <summary>Every source under this node, including those under its children.</summary>
    public IEnumerable<SourceRestrictionItemModel> AllSources =>
        Sources.Concat(Children.SelectMany(child => child.AllSources));

    /// <summary>
    /// The sources this node's checkbox acts on. Sources the builder requires are never among
    /// them, so a category's count describes what the user can actually change.
    /// </summary>
    public IReadOnlyList<SourceRestrictionItemModel> ToggleableSources =>
        AllSources.Where(source => source.AllowUnchecking).ToList();

    public int TotalCount => ToggleableSources.Count;

    public int EnabledCount => ToggleableSources.Count(source => source.IsChecked == true);

    /// <summary>True, false, or null when some of the sources beneath it are enabled and some are not.</summary>
    public bool? IsChecked => TotalCount == 0
        ? true
        : EnabledCount == 0
            ? false
            : EnabledCount == TotalCount
                ? true
                : null;
}

/// <summary>
/// A request to switch every source under one node on or off. The ids travel with it because a
/// publisher can appear under two categories — Wizards of the Coast has both 2014 and 2024 books —
/// so the node, not the engine's group, decides what the click changes.
/// </summary>
public sealed record SourceRestrictionNodeToggle(
    string NodeId,
    IReadOnlyList<string> SourceIds,
    bool IsEnabled);
