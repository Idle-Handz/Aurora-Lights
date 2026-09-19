namespace Aurora.App.Services;

public sealed record ElementOption(
    string Id,
    string Name,
    string Description,
    string Source = "",
    string Requirements = "",
    int SpellLevel = 0,
    string School = "",
    bool IsRitual = false,
    bool IsConcentration = false,
    DateTimeOffset? SourceReleaseDate = null,
    DateTimeOffset? SourceFileModifiedUtc = null,
    bool IsDisabled = false,
    bool IsCurrentSelection = false,
    string DescriptionHtml = "");
