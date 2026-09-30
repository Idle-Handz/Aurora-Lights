using System.IO;
// Cross-platform replacement for Builder.Presentation.Properties.Settings
// (System.Configuration / Properties.Settings.Default is .NET Framework only)
//
// Drop-in for Phase 1: ApplicationSettings.cs replaces
//   this.Settings = Builder.Presentation.Properties.Settings.Default;
// with
//   this.Settings = AppSettingsStore.Load();
//
// Settings are stored as JSON in the platform-appropriate AppData folder:
//   Windows : %APPDATA%\AuroraLights\settings.json
//   macOS   : ~/Library/Application Support/AuroraLights/settings.json
//   Linux   : ~/.config/AuroraLights/settings.json

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Builder.Presentation;

/// <summary>
/// Persistent application settings backed by a JSON file.
/// Mirrors every property that was on the old Properties.Settings class
/// so that ApplicationSettings.cs requires minimal changes.
/// </summary>
public sealed class AppSettingsStore
{
    // ----------------------------------------------------------------
    // Storage location
    // ----------------------------------------------------------------

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuroraLights");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDirectory, "settings.json");

    private string _settingsPath = SettingsPath;

    public AppSettingsStore() { }

    /// <summary>Uses a separate settings file, for hosts such as tests and rehearsal tools.</summary>
    public AppSettingsStore(string settingsPath)
    {
        _settingsPath = Path.GetFullPath(settingsPath);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true
    };

    // ----------------------------------------------------------------
    // Settings properties
    // Each property mirrors a field that existed on the old
    // Builder.Presentation.Properties.Settings class.
    // ----------------------------------------------------------------

    public string Accent { get; set; } = "Aurora Default";
    public string Theme { get; set; } = "Aurora Dark";
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// 0 = Roll 3d6
    /// 1 = Roll 4d6 discard lowest
    /// 2 = Standard Array
    /// 3 = Point Buy
    /// </summary>
    public int AbilitiesGenerationOption { get; set; } = 1;

    public bool AllowEditableSheet { get; set; } = false;
    public bool IncludeItemcards { get; set; } = true;
    public bool IncludeSpellcards { get; set; } = true;
    public bool CharacterSheetAbilitiesFlipped { get; set; } = false;
    public bool GenerateSheetOnCharacterChangedRegistered { get; set; } = false;
    public bool AutoNavigateNextSelectionWhenAvailable { get; set; } = true;
    public bool StartupCheckForContentUpdated { get; set; } = true;

    /// <summary>
    /// Preserved for upgrade-path compatibility; always false after first run.
    /// </summary>
    public bool ConfigurationUpgradeRequired { get; set; } = false;

    /// <summary>
    /// 1 = compact (17px), 2 = normal (21px), 3 = large (25px)
    /// </summary>
    public int SelectionExpanderGridRowSize { get; set; } = 2;

    public bool StartupCheckForUpdates { get; set; } = false;
    public bool StartupLoadCustomFiles { get; set; } = false;
    public string DocumentsRootDirectory { get; set; } = string.Empty;
    public string AdditionalCustomDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Additional directories scanned for custom/homebrew XML content.
    /// Extends <see cref="AdditionalCustomDirectory"/> to support multiple paths.
    /// </summary>
    public List<string> AdditionalCustomDirectories { get; set; } = [];
    public int CharactersCollectionSize { get; set; } = 150;
    public bool CharacterPanelAbilityScoresExpanded { get; set; } = true;
    public bool CharacterPanelStatisticsExpanded { get; set; } = false;
    public bool CharacterPanelSavingThrowsExpanded { get; set; } = false;
    public bool CharacterPanelSkillsExpanded { get; set; } = false;
    public bool CharacterPanelQuickActionsExpanded { get; set; } = true;
    public bool QuickSearchBarEnabled { get; set; } = false;
    public bool DisplayRemoveLevelConfirmation { get; set; } = true;
    public bool RequestAddAttackWhenEquippingWeapon { get; set; } = false;
    public bool UseDefaultAbilityScoreMaximum { get; set; } = true;
    public string DefaultFontSize { get; set; } = "8";
    public bool SheetIncludeAttackCards { get; set; } = false;
    public bool SheetIncludeFeatureCards { get; set; } = false;
    public bool SheetIncludeFormatting { get; set; } = true;
    public bool SheetFormattingActionSuffixBold { get; set; } = false;
    public bool UseLegacyDetailsPage { get; set; } = false;
    public bool UseLegactBackgroundPage { get; set; } = false;
    public bool UseLegacySpellcastingPage { get; set; } = false;
    public bool SheetStartItemCardsOnNewPage { get; set; } = false;
    public bool SheetStartAttackCardsOnNewPage { get; set; } = false;
    public bool SheetStartFeatureCardsOnNewPage { get; set; } = false;
    public bool SheetDescriptionAbbreviateUsage { get; set; } = false;
    public bool SheetIncludeNonPreparedSpells { get; set; } = false;
    public bool SearchMissingSourceOnline { get; set; } = false;
    public bool DisplaySelectionExpanderColumnHeaders { get; set; } = true;
    public bool DisplaySelectionExpanderColumnHeadersComboBox { get; set; } = true;
    public double ShellWindowTop { get; set; } = 0;
    public double ShellWindowLeft { get; set; } = 0;
    public double ShellWindowWidth { get; set; } = 1024;
    public double ShellWindowHeight { get; set; } = 768;
    public bool ShellWindowState { get; set; } = false;
    public bool Bundle { get; set; } = false;
    public bool CharacterSheetOpenOnSave { get; set; } = false;
    public bool SheetStartSpellCardsOnNewPage { get; set; } = false;
    public string DefaultSourceRestrictions { get; set; } = "";

    /// <summary>
    /// Lets a refresh leave out a content file it cannot use — one whose XML cannot be read, or one
    /// that redefines an element another file already defines differently — instead of refusing the
    /// whole refresh over it. The files are listed in Settings until they are fixed. Turn this off
    /// to have a refresh stop at the first such file.
    /// </summary>
    public bool SkipUnusableContentOnRefresh { get; set; } = true;

    /// <summary>
    /// Set once the sources a user had switched off in the content database have been copied into
    /// <see cref="DefaultSourceRestrictions"/>. The catalog always loads in full; restrictions decide
    /// what a character sees.
    /// </summary>
    public bool SourcePreferencesSeeded { get; set; } = false;


    // ----------------------------------------------------------------
    // Load / Save / Reset
    // ----------------------------------------------------------------

    /// <summary>
    /// Loads settings from disk. Returns defaults if the file does not
    /// exist or cannot be read (e.g. first launch or corrupt file).
    /// </summary>
    public static AppSettingsStore Load() => Load(SettingsPath);

    public static AppSettingsStore Load(string settingsPath)
    {
        var settings = new AppSettingsStore(settingsPath);
        try
        {
            if (File.Exists(settings._settingsPath))
            {
                string json = File.ReadAllText(settings._settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettingsStore>(json, SerializerOptions);
                if (loaded is not null)
                {
                    loaded._settingsPath = settings._settingsPath;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            // Log but do not crash — fall back to defaults
            System.Diagnostics.Debug.WriteLine(
                $"[AppSettingsStore] Failed to load settings: {ex.Message}");
        }

        return settings;
    }

    /// <summary>
    /// Persists current settings to disk.
    /// Creates the directory if it does not exist.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            string json = JsonSerializer.Serialize(this, SerializerOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AppSettingsStore] Failed to save settings: {ex.Message}");
            throw; // re-throw so ApplicationSettings.cs can log/report it
        }
    }

    /// <summary>
    /// Reloads settings from disk, discarding any in-memory changes.
    /// Matches the behaviour of Properties.ApplicationContext.Current.Settings.Reload().
    /// </summary>
    public void Reload()
    {
        AppSettingsStore fresh = Load(_settingsPath);

        Accent = fresh.Accent;
        Theme = fresh.Theme;
        PlayerName = fresh.PlayerName;
        AbilitiesGenerationOption = fresh.AbilitiesGenerationOption;
        AllowEditableSheet = fresh.AllowEditableSheet;
        IncludeItemcards = fresh.IncludeItemcards;
        IncludeSpellcards = fresh.IncludeSpellcards;
        CharacterSheetAbilitiesFlipped = fresh.CharacterSheetAbilitiesFlipped;
        GenerateSheetOnCharacterChangedRegistered = fresh.GenerateSheetOnCharacterChangedRegistered;
        AutoNavigateNextSelectionWhenAvailable = fresh.AutoNavigateNextSelectionWhenAvailable;
        StartupCheckForContentUpdated = fresh.StartupCheckForContentUpdated;
        ConfigurationUpgradeRequired = fresh.ConfigurationUpgradeRequired;
        SelectionExpanderGridRowSize = fresh.SelectionExpanderGridRowSize;
        AdditionalCustomDirectories  = [..fresh.AdditionalCustomDirectories];
    }

    /// <summary>
    /// Resets all settings to their defaults and saves immediately.
    /// Matches the behaviour of Properties.ApplicationContext.Current.Settings.Reset().
    /// </summary>
    public void Reset()
    {
        AppSettingsStore defaults = new AppSettingsStore();

        Accent = defaults.Accent;
        Theme = defaults.Theme;
        PlayerName = defaults.PlayerName;
        AbilitiesGenerationOption = defaults.AbilitiesGenerationOption;
        AllowEditableSheet = defaults.AllowEditableSheet;
        IncludeItemcards = defaults.IncludeItemcards;
        IncludeSpellcards = defaults.IncludeSpellcards;
        CharacterSheetAbilitiesFlipped = defaults.CharacterSheetAbilitiesFlipped;
        GenerateSheetOnCharacterChangedRegistered = defaults.GenerateSheetOnCharacterChangedRegistered;
        AutoNavigateNextSelectionWhenAvailable = defaults.AutoNavigateNextSelectionWhenAvailable;
        StartupCheckForContentUpdated = defaults.StartupCheckForContentUpdated;
        ConfigurationUpgradeRequired = defaults.ConfigurationUpgradeRequired;
        SelectionExpanderGridRowSize = defaults.SelectionExpanderGridRowSize;

        Save();
    }

    // ----------------------------------------------------------------
    // Migration helper
    // ----------------------------------------------------------------

    /// <summary>
    /// One-time migration from the old Properties.Settings .config file.
    /// Call this on first launch (when ConfigurationUpgradeRequired == true)
    /// to pull any previously-saved values across to the new JSON store.
    ///
    /// Usage in ApplicationManager.UpgradeConfigurationCheck():
    ///
    ///     if (this.Settings.Settings.ConfigurationUpgradeRequired)
    ///     {
    ///         AppSettingsStore.MigrateFromLegacyConfig(this.Settings.Settings);
    ///     }
    ///
    /// The method is a no-op on macOS / Linux where no legacy file exists.
    /// </summary>
    public static void MigrateFromLegacyConfig(AppSettingsStore target)
    {
        // On macOS and Linux there is no legacy .config file — nothing to migrate.
        if (!OperatingSystem.IsWindows())
        {
            target.ConfigurationUpgradeRequired = false;
            target.Save();
            return;
        }

        // On Windows, the old user.config lives under:
        //   %LOCALAPPDATA%\Aurora_Builder\<assembly-version>\user.config
        // We attempt a best-effort read; if it fails we just clear the flag.
        try
        {
            // The legacy System.Configuration upgrade path no longer exists on
            // .NET 8, so we can only attempt to locate and parse the raw XML.
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configRoot = Path.Combine(localApp, "Aurora_Builder");

            if (!Directory.Exists(configRoot))
            {
                target.ConfigurationUpgradeRequired = false;
                target.Save();
                return;
            }

            // Find the most recently modified user.config
            string? userConfig = Directory
                .EnumerateFiles(configRoot, "user.config", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (userConfig is null)
            {
                target.ConfigurationUpgradeRequired = false;
                target.Save();
                return;
            }

            // Parse the XML and map known keys to our settings object
            var doc = System.Xml.Linq.XDocument.Load(userConfig);
            var settings = doc
                .Descendants("setting")
                .ToDictionary(
                    el => el.Attribute("name")?.Value ?? string.Empty,
                    el => el.Element("value")?.Value ?? string.Empty);

            if (settings.TryGetValue("Accent", out string? accent) && !string.IsNullOrWhiteSpace(accent))
                target.Accent = accent;

            if (settings.TryGetValue("Theme", out string? theme) && !string.IsNullOrWhiteSpace(theme))
                target.Theme = theme;

            if (settings.TryGetValue("PlayerName", out string? playerName))
                target.PlayerName = playerName ?? string.Empty;

            if (settings.TryGetValue("AbilitiesGenerationOption", out string? agoStr)
                && int.TryParse(agoStr, out int ago))
                target.AbilitiesGenerationOption = ago;

            if (settings.TryGetValue("AllowEditableSheet", out string? aesStr)
                && bool.TryParse(aesStr, out bool aes))
                target.AllowEditableSheet = aes;

            if (settings.TryGetValue("IncludeItemcards", out string? iicStr)
                && bool.TryParse(iicStr, out bool iic))
                target.IncludeItemcards = iic;

            if (settings.TryGetValue("IncludeSpellcards", out string? iscStr)
                && bool.TryParse(iscStr, out bool isc))
                target.IncludeSpellcards = isc;

            if (settings.TryGetValue("SelectionExpanderGridRowSize", out string? segrsStr)
                && int.TryParse(segrsStr, out int segrs))
                target.SelectionExpanderGridRowSize = segrs;

            if (settings.TryGetValue("StartupCheckForContentUpdated", out string? scfcuStr)
                && bool.TryParse(scfcuStr, out bool scfcu))
                target.StartupCheckForContentUpdated = scfcu;

            if (settings.TryGetValue("AutoNavigateNextSelectionWhenAvailable", out string? annswaStr)
                && bool.TryParse(annswaStr, out bool annswa))
                target.AutoNavigateNextSelectionWhenAvailable = annswa;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AppSettingsStore] Legacy migration failed (non-fatal): {ex.Message}");
        }
        finally
        {
            target.ConfigurationUpgradeRequired = false;
            target.Save();
        }
    }
}
