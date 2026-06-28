namespace Aurora.App.Services;

/// <summary>
/// Keeps routed-page UI state alive for the current app session. Blazor disposes
/// page components when navigating between sections, so purely local fields lose
/// search filters, selected rows, and active tabs unless they are held outside the
/// component.
/// </summary>
public sealed class AppPageStateService
{
    public CharacterBrowserPageState Characters { get; } = new();
    public CompendiumPageState Compendium { get; } = new();
    public SettingsPageState Settings { get; } = new();
    public BuildPageState Build { get; } = new();
}

public sealed class CharacterBrowserPageState
{
    public string SearchText { get; set; } = string.Empty;
}

public sealed class CompendiumPageState
{
    public string Query { get; set; } = string.Empty;
    public string SelectedType { get; set; } = "All";
    public string SelectedSource { get; set; } = "All";
    public string SelectedSpellLevel { get; set; } = "All";
    public string SelectedSpellSchool { get; set; } = "All";
    public string SelectedSpellClass { get; set; } = "All";
    public string SelectedItemRarity { get; set; } = "All";
    public string SelectedItemAttunement { get; set; } = "All";
    public string SelectedCreatureType { get; set; } = "All";
    public string SelectedCreatureSize { get; set; } = "All";
    public string SelectedCreatureChallenge { get; set; } = "All";
    public bool CurrentCharacterSourcesOnly { get; set; }
    public bool HasSearched { get; set; }
    public string? SelectedEntryId { get; set; }

    public void Reset()
    {
        Query = string.Empty;
        SelectedType = "All";
        SelectedSource = "All";
        SelectedSpellLevel = "All";
        SelectedSpellSchool = "All";
        SelectedSpellClass = "All";
        SelectedItemRarity = "All";
        SelectedItemAttunement = "All";
        SelectedCreatureType = "All";
        SelectedCreatureSize = "All";
        SelectedCreatureChallenge = "All";
        CurrentCharacterSourcesOnly = false;
        HasSearched = false;
        SelectedEntryId = null;
    }
}

public sealed class SettingsPageState
{
    public int ActiveSettingsTabIndex { get; set; }
    public int ActiveContentTabIndex { get; set; }
    public string PackageSearch { get; set; } = string.Empty;
}

public sealed class BuildPageState
{
    public int ActivePanelIndex { get; set; }
}
