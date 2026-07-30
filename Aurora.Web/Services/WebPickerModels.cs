namespace Aurora.Web.Services;

public sealed record WebEquipmentSearchResult(
    string ElementId,
    string Name,
    string Type,
    string Source,
    string Description,
    string DescriptionHtml = "");

public sealed record WebEquipmentInventoryOption(
    string Identifier,
    string Name);

public sealed record WebMagicSelectionOption(
    string Id,
    string Name,
    string Source,
    string Description,
    string Requirements,
    string DescriptionHtml = "",
    bool IsDisabled = false,
    bool IsCurrentSelection = false);
