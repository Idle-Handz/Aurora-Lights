namespace Aurora.Components.Models;

public sealed record SourceRestrictionItemModel(
    string Id,
    string Name,
    bool? IsChecked,
    bool AllowUnchecking,
    bool IsCore,
    SourceRestrictionCategory? Category = null);
