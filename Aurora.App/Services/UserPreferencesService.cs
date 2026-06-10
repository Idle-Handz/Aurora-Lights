using Builder.Presentation;

namespace Aurora.App.Services;

public enum HpMethod { Average, Rolled }

/// <summary>
/// Persists user preferences using MAUI's platform Preferences API (registry on Windows).
/// Registered as a singleton so all pages share the same instance.
/// </summary>
public sealed class UserPreferencesService
{
    private const string KeyAutoSave           = "build.auto_save";
    private const string KeyHpMethod           = "build.hp_method";
    private const string KeyFeats              = "build.allow_feats";
    private const string KeyMulticlassing      = "build.allow_multiclassing";
    private const string KeyCustomOrigin       = "build.tce_custom_origin";
    private const string KeyCustomLanguage     = "build.tce_custom_language";
    private const string KeyCustomProficiency  = "build.tce_custom_proficiency";
    private const string KeyDevMode            = "dev.mode";
    private const string KeyMruCharacter       = "app.mru_character";
    private const string KeyLightMode          = "app.light_mode";

    public event Action? ThemeChanged;
    public event Action? DeveloperModeChanged;

    public UserPreferencesService()
    {
        SyncDeveloperMode(DevMode);
    }

    /// <summary>
    /// When true, the app uses a light colour palette instead of the default dark theme.
    /// Default: false.
    /// </summary>
    public bool LightMode
    {
        get => Preferences.Default.Get(KeyLightMode, defaultValue: false);
        set { Preferences.Default.Set(KeyLightMode, value); ThemeChanged?.Invoke(); }
    }

    // Character sheet page/card options — read directly by MauiCharacterSheetGenerator as well.
    public const string KeySpellCards      = "sheet.spellcards";
    public const string KeyItemCards       = "sheet.itemcards";
    public const string KeyAttackCards     = "sheet.attackcards";
    public const string KeyFeatureCards    = "sheet.featurecards";
    public const string KeyBackgroundPage  = "sheet.backgroundpage";
    public const string KeyEquipmentPage   = "sheet.equipmentpage";
    public const string KeyEditableSheet   = "sheet.editable";
    public const string KeyIncludeFormatting = "sheet.formatting";
    public const string KeyFlippedAbilities = "sheet.flipped_abilities";
    public const string KeyIncludeNonPreparedSpells = "sheet.include_non_prepared_spells";
    public const string KeyLegacySpellcastingPage = "sheet.legacy_spellcasting";
    public const string KeyStartSpellCardsOnNewPage = "sheet.start_spellcards_new_page";
    public const string KeyStartItemCardsOnNewPage = "sheet.start_itemcards_new_page";
    public const string KeyStartAttackCardsOnNewPage = "sheet.start_attackcards_new_page";
    public const string KeyStartFeatureCardsOnNewPage = "sheet.start_featurecards_new_page";

    /// <summary>
    /// When true, selecting a new option on the Build or Magic page immediately writes
    /// the character file to disk. When false, changes are applied in memory and the tab
    /// is marked dirty so the user can review and save manually.
    /// Default: true.
    /// </summary>
    public bool AutoSaveBuildChanges
    {
        get => Preferences.Default.Get(KeyAutoSave, defaultValue: true);
        set => Preferences.Default.Set(KeyAutoSave, value);
    }

    /// <summary>
    /// When true, a Console page is available in the nav menu showing captured
    /// runtime exceptions and diagnostic messages.
    /// Default: false.
    /// </summary>
    public bool DevMode
    {
        get => Preferences.Default.Get(KeyDevMode, defaultValue: false);
        set
        {
            Preferences.Default.Set(KeyDevMode, value);
            SyncDeveloperMode(value);
            DeveloperModeChanged?.Invoke();
        }
    }

    private static void SyncDeveloperMode(bool enabled)
    {
        try
        {
            ApplicationContext.Current.IsInDeveloperMode = enabled;
        }
        catch (InvalidOperationException)
        {
            // Unit-test or design-time construction before MauiProgram wires ApplicationContext.
        }
    }

    /// <summary>
    /// Global default for how HP is assigned on level-up.
    /// Applied when creating a new character; can be overridden per-character on the Build page.
    /// Default: Average.
    /// </summary>
    public HpMethod DefaultHpMethod
    {
        get => Preferences.Default.Get(KeyHpMethod, defaultValue: (int)HpMethod.Average) == (int)HpMethod.Rolled
               ? HpMethod.Rolled : HpMethod.Average;
        set => Preferences.Default.Set(KeyHpMethod, (int)value);
    }

    /// <summary>Default: true. Applied when creating a new character.</summary>
    public bool DefaultFeats
    {
        get => Preferences.Default.Get(KeyFeats, defaultValue: true);
        set => Preferences.Default.Set(KeyFeats, value);
    }

    /// <summary>Default: true. Applied when creating a new character.</summary>
    public bool DefaultMulticlassing
    {
        get => Preferences.Default.Get(KeyMulticlassing, defaultValue: true);
        set => Preferences.Default.Set(KeyMulticlassing, value);
    }

    /// <summary>
    /// When true, new characters default to using the TCE Customized Ability Score Increases option.
    /// Reflects the active character's state when one is open. Default: true.
    /// </summary>
    public bool DefaultCustomOrigin
    {
        get => Preferences.Default.Get(KeyCustomOrigin, defaultValue: true);
        set => Preferences.Default.Set(KeyCustomOrigin, value);
    }

    /// <summary>
    /// When true, new characters default to using the TCE Customized Language option.
    /// Reflects the active character's state when one is open. Default: true.
    /// </summary>
    public bool DefaultCustomLanguage
    {
        get => Preferences.Default.Get(KeyCustomLanguage, defaultValue: true);
        set => Preferences.Default.Set(KeyCustomLanguage, value);
    }

    /// <summary>
    /// When true, new characters default to using the TCE Customized Proficiencies option.
    /// Reflects the active character's state when one is open. Default: true.
    /// </summary>
    public bool DefaultCustomProficiency
    {
        get => Preferences.Default.Get(KeyCustomProficiency, defaultValue: true);
        set => Preferences.Default.Set(KeyCustomProficiency, value);
    }

    /// <summary>
    /// File path of the most recently opened character. Used to preload that character
    /// in the background so it is ready before the user clicks it.
    /// </summary>
    public string? MruCharacterPath
    {
        get { var v = Preferences.Default.Get(KeyMruCharacter, ""); return v.Length > 0 ? v : null; }
        set => Preferences.Default.Set(KeyMruCharacter, value ?? "");
    }

    // ── Character sheet card pages ────────────────────────────────────────────

    /// <summary>Include a spell-cards page at the end of the generated PDF.</summary>
    public bool IncludeSpellCards
    {
        get => Preferences.Default.Get(KeySpellCards,   defaultValue: false);
        set => Preferences.Default.Set(KeySpellCards,   value);
    }

    /// <summary>Include an equipment/item-cards page at the end of the generated PDF.</summary>
    public bool IncludeItemCards
    {
        get => Preferences.Default.Get(KeyItemCards,    defaultValue: false);
        set => Preferences.Default.Set(KeyItemCards,    value);
    }

    /// <summary>Include an attack-cards page at the end of the generated PDF.</summary>
    public bool IncludeAttackCards
    {
        get => Preferences.Default.Get(KeyAttackCards,  defaultValue: false);
        set => Preferences.Default.Set(KeyAttackCards,  value);
    }

    /// <summary>Include a feature-cards page at the end of the generated PDF.</summary>
    public bool IncludeFeatureCards
    {
        get => Preferences.Default.Get(KeyFeatureCards, defaultValue: false);
        set => Preferences.Default.Set(KeyFeatureCards, value);
    }

    // ── Character sheet page toggles ──────────────────────────────────────────

    /// <summary>Include the background/biography page in the generated PDF. Default: true.</summary>
    public bool IncludeBackgroundPage
    {
        get => Preferences.Default.Get(KeyBackgroundPage, defaultValue: true);
        set => Preferences.Default.Set(KeyBackgroundPage, value);
    }

    /// <summary>Include the equipment/inventory page in the generated PDF. Default: true.</summary>
    public bool IncludeEquipmentPage
    {
        get => Preferences.Default.Get(KeyEquipmentPage, defaultValue: true);
        set => Preferences.Default.Set(KeyEquipmentPage, value);
    }

    /// <summary>Generate the PDF as editable / form-fillable instead of flattening fields.</summary>
    public bool EditableSheet
    {
        get => Preferences.Default.Get(KeyEditableSheet, defaultValue: false);
        set => Preferences.Default.Set(KeyEditableSheet, value);
    }

    /// <summary>Apply styling/formatting to generated sheet content for readability.</summary>
    public bool IncludeSheetFormatting
    {
        get => Preferences.Default.Get(KeyIncludeFormatting, defaultValue: true);
        set => Preferences.Default.Set(KeyIncludeFormatting, value);
    }

    /// <summary>Swap the displayed position of ability scores and modifiers on the sheet.</summary>
    public bool FlippedSheetAbilities
    {
        get => Preferences.Default.Get(KeyFlippedAbilities, defaultValue: false);
        set => Preferences.Default.Set(KeyFlippedAbilities, value);
    }

    /// <summary>Include known but currently unprepared spells on prepared-caster spell pages.</summary>
    public bool IncludeNonPreparedSpellsOnSheet
    {
        get => Preferences.Default.Get(KeyIncludeNonPreparedSpells, defaultValue: false);
        set => Preferences.Default.Set(KeyIncludeNonPreparedSpells, value);
    }

    /// <summary>Use the older single-page spellcasting layout instead of the dynamic sheet page.</summary>
    public bool UseLegacySpellcastingPage
    {
        get => Preferences.Default.Get(KeyLegacySpellcastingPage, defaultValue: false);
        set => Preferences.Default.Set(KeyLegacySpellcastingPage, value);
    }

    /// <summary>Start spell cards on a new page instead of reusing the prior page when possible.</summary>
    public bool StartSpellCardsOnNewPage
    {
        get => Preferences.Default.Get(KeyStartSpellCardsOnNewPage, defaultValue: false);
        set => Preferences.Default.Set(KeyStartSpellCardsOnNewPage, value);
    }

    /// <summary>Start item cards on a new page instead of reusing the prior page when possible.</summary>
    public bool StartItemCardsOnNewPage
    {
        get => Preferences.Default.Get(KeyStartItemCardsOnNewPage, defaultValue: false);
        set => Preferences.Default.Set(KeyStartItemCardsOnNewPage, value);
    }

    /// <summary>Start attack cards on a new page instead of reusing the prior page when possible.</summary>
    public bool StartAttackCardsOnNewPage
    {
        get => Preferences.Default.Get(KeyStartAttackCardsOnNewPage, defaultValue: false);
        set => Preferences.Default.Set(KeyStartAttackCardsOnNewPage, value);
    }

    /// <summary>Start feature cards on a new page instead of reusing the prior page when possible.</summary>
    public bool StartFeatureCardsOnNewPage
    {
        get => Preferences.Default.Get(KeyStartFeatureCardsOnNewPage, defaultValue: false);
        set => Preferences.Default.Set(KeyStartFeatureCardsOnNewPage, value);
    }

    // ── Update channels ───────────────────────────────────────────────────────
    //
    private const string KeyStartupCheckForAppUpdates       = "updates.app.startup";
    private const string KeyAutoDownloadContentOnStartup    = "updates.content.auto_download";
    private const string KeyIncludePrereleasesInUpdateCheck = "updates.include_prereleases";

    /// <summary>Whether to check for a newer app binary on startup. Default: false (matches the WPF default).</summary>
    public bool StartupCheckForAppUpdates
    {
        get => Preferences.Default.Get(KeyStartupCheckForAppUpdates, defaultValue: false);
        set => Preferences.Default.Set(KeyStartupCheckForAppUpdates, value);
    }

    /// <summary>
    /// Whether installed .index content sources are checked in the background on startup.
    /// This is the active content delivery path users opt into by installing an .index file.
    /// On Updated the user gets a snackbar + an AppBar reload prompt; on no-op nothing
    /// surfaces. Default: true.
    /// </summary>
    public bool AutoDownloadContentOnStartup
    {
        get => Preferences.Default.Get(KeyAutoDownloadContentOnStartup, defaultValue: true);
        set => Preferences.Default.Set(KeyAutoDownloadContentOnStartup, value);
    }

    /// <summary>Whether the update check considers pre-releases (alphas, betas, RCs). Default: true
    /// while the app is still pre-1.0 — otherwise the check would silently never see anything.</summary>
    public bool IncludePrereleasesInUpdateCheck
    {
        get => Preferences.Default.Get(KeyIncludePrereleasesInUpdateCheck, defaultValue: true);
        set => Preferences.Default.Set(KeyIncludePrereleasesInUpdateCheck, value);
    }
}
