namespace Aurora.Web.Services;

public sealed record WebCharacterBuildState(
    ImportedCharacterSummary Summary,
    IReadOnlyList<WebBuildSelectionGroup> Groups,
    int OpenSelectionCount,
    string StatusMessage);

public sealed record WebBuildSelectionGroup(
    string Id,
    string Label,
    IReadOnlyList<WebBuildSelectionEntry> Entries);

public sealed record WebBuildSelectionEntry(
    string Key,
    string Label,
    string Type,
    string? CurrentName,
    int RequiredLevel);

public sealed record WebBuildSelectionOption(
    string Id,
    string Name,
    string Description,
    string Source,
    string Requirements);
