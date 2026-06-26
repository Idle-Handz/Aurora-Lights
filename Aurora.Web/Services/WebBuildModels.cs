namespace Aurora.Web.Services;

public sealed record WebCharacterBuildState(
    ImportedCharacterSummary Summary,
    IReadOnlyList<WebBuildSelectionGroup> Groups,
    int OpenSelectionCount,
    string StatusMessage,
    IReadOnlyList<string>? InvalidatedSelections = null);

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

public sealed record WebAbilityScoreState(
    ImportedCharacterSummary Summary,
    bool CanLevelUp,
    bool CanLevelDown,
    bool UseAverageHp,
    int Level,
    int MaxHp,
    IReadOnlyList<WebAbilityScoreEntry> Scores,
    int AvailablePoints,
    string StatusMessage);

public sealed record WebAbilityScoreEntry(
    string Stat,
    string Name,
    string Abbrev,
    int BaseScore,
    int AdditionalScore,
    int FinalScore,
    string ModifierString);
