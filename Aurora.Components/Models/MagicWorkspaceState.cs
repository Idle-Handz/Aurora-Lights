namespace Aurora.Components.Models;

/// <summary>
/// Browsing choices for one open character. The host keeps this object across page navigation
/// and releases it when the character closes; it is not part of the saved character data.
/// </summary>
public sealed class MagicWorkspaceState
{
    public const string KnownTabId = "known";
    public const string CastableTabId = "castable";

    public string SelectedTab { get; set; } = CastableTabId;
    public MagicSpellFilterState Known { get; } = new();
    public MagicSpellFilterState Castable { get; } = new();
    public MagicSpellFilterState Prepared { get; } = new();
    public bool PreparedOnly { get; set; }
}

public sealed class MagicSpellFilterState
{
    public string SearchText { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string School { get; set; } = string.Empty;
    public string CastingTime { get; set; } = string.Empty;
    public bool RitualOnly { get; set; }
    public bool ConcentrationOnly { get; set; }
}