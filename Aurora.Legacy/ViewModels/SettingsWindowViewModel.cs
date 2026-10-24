// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.SettingsWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using MahApps.Metro;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.ViewModels;

public sealed class SettingsWindowViewModel : ViewModelBase
{
    private string _styleSheet;
    private string _changelog;
    private string _openGamingLicence;
    private bool _startupCheckForUpdates;
    private bool _startupLoadCustomFiles;
    private SelectionItem _selectedSelectionExpanderItemsSize;
    private bool _startupCheckForContentUpdated;
    private bool _autoNavigateNextSelectionWhenAvailable;
    private string _customRootDirectory;
    private string _additionalCustomDirectory;
    private Accent _selectedAccent;
    private AppTheme _selectedTheme;
    private int _charactersCollectionSize;
    private bool _characterPanelAbilitiesIsExpanded;
    private bool _characterPanelStatisticsIsExpanded;
    private bool _characterPanelSavingThrowsIsExpanded;
    private bool _characterPanelSkillsIsExpanded;
    private bool _characterPanelQuickActionsIsExpanded;
    private bool _enableQuickSearchBar;
    private SelectionItem _selectedAbilityGenerateOption;
    private bool _useDefaultAbilityScoreMaximum;
    private string _defaultFontSize;
    private string _defaultPlayerName;
    private bool _allowEditableSheet;
    private bool _includeSpellcards;
    private bool _includeItemcards;
    private bool _flippedAbilities;
    private bool _includeAttackCards;
    private bool _includeFeatureCards;
    private bool _includeFormatting;
    private bool _useLegacySpellcastingPage;
    private bool _startItemCardsOnNewPage;
    private bool _startAttackCardsOnNewPage;
    private bool _startFeatureCardsOnNewPage;
    private bool _sheetFormattingSuffixActionsBold;
    private bool _sheetDescriptionAbbreviateUsage;
    private bool _sheetIncludeNonPreparedSpells;
    private bool _useLegacyDetailsPage;
    private bool _useLegacyBackgroundPage;
    private bool _displayRemoveLevelConfirmation;
    private bool _requestAddAttackWhenEquippingWeapon;

    public string StyleSheet
    {
        get => this._styleSheet;
        set => this.SetProperty<string>(ref this._styleSheet, value, nameof(StyleSheet));
    }

    public string Changelog
    {
        get => this._changelog;
        set => this.SetProperty<string>(ref this._changelog, value, nameof(Changelog));
    }

    public string OpenGamingLicence
    {
        get => this._openGamingLicence;
        set => this.SetProperty<string>(ref this._openGamingLicence, value, nameof(OpenGamingLicence));
    }

    public SettingsWindowViewModel()
    {
        this.Version = "1.0.166.7407";
        this.AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        this.StyleSheet = ApplicationContext.Current.Settings.Theme.Contains("Dark") ? DataManager.Current.GetResourceWebDocument("stylesheet-dark.css") : DataManager.Current.GetResourceWebDocument("stylesheet.css");
        this.OpenGamingLicence = DataManager.Current.GetResourceWebDocument("description-ogl.html");
        int[] numArray = new int[5] { 6, 7, 8, 9, 10 };
        foreach (int num in numArray)
            this.FontSizeCollection.Add(num.ToString());
        this.AbilitiesGenerationSelectionItems.Add(new SelectionItem("Roll 3d6", 0));
        this.AbilitiesGenerationSelectionItems.Add(new SelectionItem("Roll 4d6 Discard Lowest", 1));
        this.AbilitiesGenerationSelectionItems.Add(new SelectionItem("Standard Array", 2));
        this.AbilitiesGenerationSelectionItems.Add(new SelectionItem("Point Buy System", 3));
        this.SelectionExpanderItemsSize.Add(new SelectionItem("Small", 1));
        this.SelectionExpanderItemsSize.Add(new SelectionItem("Medium (Default)", 2));
        this.SelectionExpanderItemsSize.Add(new SelectionItem("Large", 3));
        if (this.IsInDesignMode)
        {
            this.InitializeDesignData();
        }
        else
        {
            string[] strArray1 = new string[10]
            {
        "Black",
        "Brown",
        "Default",
        "Purple",
        "Green",
        "Aqua",
        "Blue",
        "Mauve",
        "Pink",
        "Red"
            };
            foreach (string str in strArray1)
            {
                string accentName = str;
                this.Accents.Add(ThemeManager.Accents.Single<Accent>((Func<Accent, bool>)(x => x.Name.Equals("Aurora " + accentName, StringComparison.OrdinalIgnoreCase))));
            }
            string[] strArray2 = new string[2]
            {
        "Aurora Light",
        "Aurora Dark"
            };
            foreach (string str in strArray2)
            {
                string theme = str;
                this.Themes.Add(ThemeManager.AppThemes.Single<AppTheme>((Func<AppTheme, bool>)(x => x.Name.Equals(theme, StringComparison.OrdinalIgnoreCase))));
            }
            this.PopulateProperties();
        }
    }

    public string Version { get; }

    public string AssemblyVersion { get; }

    public ObservableCollection<Accent> Accents { get; } = new ObservableCollection<Accent>();

    public ObservableCollection<AppTheme> Themes { get; } = new ObservableCollection<AppTheme>();

    public ObservableCollection<string> FontSizeCollection { get; } = new ObservableCollection<string>();

    public ObservableCollection<SelectionItem> AbilitiesGenerationSelectionItems { get; } = new ObservableCollection<SelectionItem>();

    public ObservableCollection<SelectionItem> SelectionExpanderItemsSize { get; } = new ObservableCollection<SelectionItem>();

    public bool StartupCheckForUpdates
    {
        get => this._startupCheckForUpdates;
        set
        {
            this.SetProperty<bool>(ref this._startupCheckForUpdates, value, nameof(StartupCheckForUpdates));
        }
    }

    public bool StartupLoadCustomFiles
    {
        get => this._startupLoadCustomFiles;
        set
        {
            this.SetProperty<bool>(ref this._startupLoadCustomFiles, value, nameof(StartupLoadCustomFiles));
        }
    }

    public bool StartupCheckForContentUpdated
    {
        get => this._startupCheckForContentUpdated;
        set
        {
            this.SetProperty<bool>(ref this._startupCheckForContentUpdated, value, nameof(StartupCheckForContentUpdated));
        }
    }

    public bool AutoNavigateNextSelectionWhenAvailable
    {
        get => this._autoNavigateNextSelectionWhenAvailable;
        set
        {
            this.SetProperty<bool>(ref this._autoNavigateNextSelectionWhenAvailable, value, nameof(AutoNavigateNextSelectionWhenAvailable));
        }
    }

    public SelectionItem SelectedSelectionExpanderItemsSize
    {
        get => this._selectedSelectionExpanderItemsSize;
        set
        {
            this.SetProperty<SelectionItem>(ref this._selectedSelectionExpanderItemsSize, value, nameof(SelectedSelectionExpanderItemsSize));
        }
    }

    public string CustomRootDirectory
    {
        get => this._customRootDirectory;
        set
        {
            this.SetProperty<string>(ref this._customRootDirectory, value, nameof(CustomRootDirectory));
        }
    }

    public string AdditionalCustomDirectory
    {
        get => this._additionalCustomDirectory;
        set
        {
            this.SetProperty<string>(ref this._additionalCustomDirectory, value, nameof(AdditionalCustomDirectory));
        }
    }

    public Accent SelectedAccent
    {
        get => this._selectedAccent;
        set
        {
            this.SetProperty<Accent>(ref this._selectedAccent, value, nameof(SelectedAccent));
            if (this._selectedAccent == null)
                return;
            this.SetAccent();
        }
    }

    public AppTheme SelectedTheme
    {
        get => this._selectedTheme;
        set
        {
            this.SetProperty<AppTheme>(ref this._selectedTheme, value, nameof(SelectedTheme));
            if (this._selectedTheme == null)
                return;
            this.SetTheme(false);
        }
    }

    public int CharactersCollectionSize
    {
        get => this._charactersCollectionSize;
        set
        {
            this.SetProperty<int>(ref this._charactersCollectionSize, value, nameof(CharactersCollectionSize));
        }
    }

    public bool CharacterPanelAbilitiesIsExpanded
    {
        get => this._characterPanelAbilitiesIsExpanded;
        set
        {
            this.SetProperty<bool>(ref this._characterPanelAbilitiesIsExpanded, value, nameof(CharacterPanelAbilitiesIsExpanded));
        }
    }

    public bool CharacterPanelStatisticsIsExpanded
    {
        get => this._characterPanelStatisticsIsExpanded;
        set
        {
            this.SetProperty<bool>(ref this._characterPanelStatisticsIsExpanded, value, nameof(CharacterPanelStatisticsIsExpanded));
        }
    }

    public bool CharacterPanelSavingThrowsIsExpanded
    {
        get => this._characterPanelSavingThrowsIsExpanded;
        set
        {
            this.SetProperty<bool>(ref this._characterPanelSavingThrowsIsExpanded, value, nameof(CharacterPanelSavingThrowsIsExpanded));
        }
    }

    public bool CharacterPanelSkillsIsExpanded
    {
        get => this._characterPanelSkillsIsExpanded;
        set
        {
            this.SetProperty<bool>(ref this._characterPanelSkillsIsExpanded, value, nameof(CharacterPanelSkillsIsExpanded));
        }
    }

    public bool CharacterPanelQuickActionsIsExpanded
    {
        get => this._characterPanelQuickActionsIsExpanded;
        set
        {
            this.SetProperty<bool>(ref this._characterPanelQuickActionsIsExpanded, value, nameof(CharacterPanelQuickActionsIsExpanded));
        }
    }

    public bool EnableQuickSearchBar
    {
        get => this._enableQuickSearchBar;
        set
        {
            this.SetProperty<bool>(ref this._enableQuickSearchBar, value, nameof(EnableQuickSearchBar));
        }
    }

    public SelectionItem SelectedAbilityGenerateOption
    {
        get => this._selectedAbilityGenerateOption;
        set
        {
            this.SetProperty<SelectionItem>(ref this._selectedAbilityGenerateOption, value, nameof(SelectedAbilityGenerateOption));
        }
    }

    public bool UseDefaultAbilityScoreMaximum
    {
        get => this._useDefaultAbilityScoreMaximum;
        set
        {
            this.SetProperty<bool>(ref this._useDefaultAbilityScoreMaximum, value, nameof(UseDefaultAbilityScoreMaximum));
        }
    }

    public string DefaultFontSize
    {
        get => this._defaultFontSize;
        set => this.SetProperty<string>(ref this._defaultFontSize, value, nameof(DefaultFontSize));
    }

    public string DefaultPlayerName
    {
        get => this._defaultPlayerName;
        set => this.SetProperty<string>(ref this._defaultPlayerName, value, nameof(DefaultPlayerName));
    }

    public bool AllowEditableSheet
    {
        get => this._allowEditableSheet;
        set => this.SetProperty<bool>(ref this._allowEditableSheet, value, nameof(AllowEditableSheet));
    }

    public bool FlippedAbilities
    {
        get => this._flippedAbilities;
        set => this.SetProperty<bool>(ref this._flippedAbilities, value, nameof(FlippedAbilities));
    }

    public bool IncludeSpellcards
    {
        get => this._includeSpellcards;
        set => this.SetProperty<bool>(ref this._includeSpellcards, value, nameof(IncludeSpellcards));
    }

    public bool IncludeItemcards
    {
        get => this._includeItemcards;
        set => this.SetProperty<bool>(ref this._includeItemcards, value, nameof(IncludeItemcards));
    }

    public bool IncludeAttackCards
    {
        get => this._includeAttackCards;
        set => this.SetProperty<bool>(ref this._includeAttackCards, value, nameof(IncludeAttackCards));
    }

    public bool IncludeFeatureCards
    {
        get => this._includeFeatureCards;
        set
        {
            this.SetProperty<bool>(ref this._includeFeatureCards, value, nameof(IncludeFeatureCards));
        }
    }

    public bool IncludeFormatting
    {
        get => this._includeFormatting;
        set => this.SetProperty<bool>(ref this._includeFormatting, value, nameof(IncludeFormatting));
    }

    public bool SheetFormattingSuffixActionsBold
    {
        get => this._sheetFormattingSuffixActionsBold;
        set
        {
            this.SetProperty<bool>(ref this._sheetFormattingSuffixActionsBold, value, nameof(SheetFormattingSuffixActionsBold));
        }
    }

    public bool UseLegacySpellcastingPage
    {
        get => this._useLegacySpellcastingPage;
        set
        {
            this.SetProperty<bool>(ref this._useLegacySpellcastingPage, value, nameof(UseLegacySpellcastingPage));
        }
    }

    public bool StartItemCardsOnNewPage
    {
        get => this._startItemCardsOnNewPage;
        set
        {
            this.SetProperty<bool>(ref this._startItemCardsOnNewPage, value, nameof(StartItemCardsOnNewPage));
        }
    }

    public bool StartAttackCardsOnNewPage
    {
        get => this._startAttackCardsOnNewPage;
        set
        {
            this.SetProperty<bool>(ref this._startAttackCardsOnNewPage, value, nameof(StartAttackCardsOnNewPage));
        }
    }

    public bool StartFeatureCardsOnNewPage
    {
        get => this._startFeatureCardsOnNewPage;
        set
        {
            this.SetProperty<bool>(ref this._startFeatureCardsOnNewPage, value, nameof(StartFeatureCardsOnNewPage));
        }
    }

    public bool SheetDescriptionAbbreviateUsage
    {
        get => this._sheetDescriptionAbbreviateUsage;
        set
        {
            this.SetProperty<bool>(ref this._sheetDescriptionAbbreviateUsage, value, nameof(SheetDescriptionAbbreviateUsage));
        }
    }

    public bool SheetIncludeNonPreparedSpells
    {
        get => this._sheetIncludeNonPreparedSpells;
        set
        {
            this.SetProperty<bool>(ref this._sheetIncludeNonPreparedSpells, value, nameof(SheetIncludeNonPreparedSpells));
        }
    }

    public bool UseLegacyDetailsPage
    {
        get => this._useLegacyDetailsPage;
        set
        {
            this.SetProperty<bool>(ref this._useLegacyDetailsPage, value, nameof(UseLegacyDetailsPage));
        }
    }

    public bool UseLegacyBackgroundPage
    {
        get => this._useLegacyBackgroundPage;
        set
        {
            this.SetProperty<bool>(ref this._useLegacyBackgroundPage, value, nameof(UseLegacyBackgroundPage));
        }
    }

    public bool DisplayRemoveLevelConfirmation
    {
        get => this._displayRemoveLevelConfirmation;
        set
        {
            this.SetProperty<bool>(ref this._displayRemoveLevelConfirmation, value, nameof(DisplayRemoveLevelConfirmation));
        }
    }

    public bool RequestAddAttackWhenEquippingWeapon
    {
        get => this._requestAddAttackWhenEquippingWeapon;
        set
        {
            this.SetProperty<bool>(ref this._requestAddAttackWhenEquippingWeapon, value, nameof(RequestAddAttackWhenEquippingWeapon));
        }
    }

    public RelayCommand SetDefaultsSettingsCommand
    {
        get => new RelayCommand(new Action(this.SetDefaultSettings));
    }

    public RelayCommand ApplySettingsCommand => new RelayCommand(new Action(this.ApplySettings));

    public RelayCommand SaveSettingsCommand => new RelayCommand(new Action(this.SaveSettings));

    public RelayCommand CancelSettingsCommand => new RelayCommand(new Action(this.CancelSettings));

    public RelayCommand BrowseCustomRootDirectoryCommand
    {
        get => new RelayCommand(new Action(this.BrowseCustomRootDirectory));
    }

    public RelayCommand BrowseAdditionalCustomDirectoryCommand
    {
        get => new RelayCommand(new Action(this.BrowseAdditionalCustomDirectory));
    }

    public ICommand ClearAdditionalCustomDirectoryCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.ClearAdditionalCustomDirectory));
    }

    private void ClearAdditionalCustomDirectory() => this.AdditionalCustomDirectory = "";

    private void BrowseCustomRootDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog()
        {
            InitialDirectory = this.CustomRootDirectory
        };
        if (dialog.ShowDialog() == true)
            this.CustomRootDirectory = dialog.FolderName;
    }

    private void BrowseAdditionalCustomDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog()
        {
            InitialDirectory = this.AdditionalCustomDirectory
        };
        if (dialog.ShowDialog() == true)
            this.AdditionalCustomDirectory = dialog.FolderName;
    }

    private void SetDefaultSettings()
    {
        ApplicationContext.Current.Settings.Reset();
        ApplicationContext.Current.Settings.ConfigurationUpgradeRequired = false;
        ApplicationContext.Current.Settings.Save();
        this.ResetSettings();
    }

    private void ApplySettings()
    {
        // ISSUE: variable of a compiler-generated type
        AppSettingsStore settings = ApplicationManager.Current.Settings.Settings;
        settings.StartupCheckForUpdates = this.StartupCheckForUpdates;
        settings.StartupLoadCustomFiles = this.StartupLoadCustomFiles;
        settings.SelectionExpanderGridRowSize = this.SelectedSelectionExpanderItemsSize.Value;
        settings.AutoNavigateNextSelectionWhenAvailable = this.AutoNavigateNextSelectionWhenAvailable;
        settings.RequestAddAttackWhenEquippingWeapon = this.RequestAddAttackWhenEquippingWeapon;
        settings.DocumentsRootDirectory = this.CustomRootDirectory;
        settings.AdditionalCustomDirectory = this.AdditionalCustomDirectory;
        settings.Accent = this.SelectedAccent?.Name ?? "Aurora Default";
        settings.Theme = this.SelectedTheme.Name;
        settings.CharactersCollectionSize = this.CharactersCollectionSize;
        settings.CharacterPanelAbilityScoresExpanded = this.CharacterPanelAbilitiesIsExpanded;
        settings.CharacterPanelStatisticsExpanded = this.CharacterPanelStatisticsIsExpanded;
        settings.CharacterPanelSavingThrowsExpanded = this.CharacterPanelSavingThrowsIsExpanded;
        settings.CharacterPanelSkillsExpanded = this.CharacterPanelSkillsIsExpanded;
        settings.CharacterPanelQuickActionsExpanded = this.CharacterPanelQuickActionsIsExpanded;
        settings.QuickSearchBarEnabled = this.EnableQuickSearchBar;
        settings.DisplayRemoveLevelConfirmation = this.DisplayRemoveLevelConfirmation;
        settings.StartupCheckForContentUpdated = this.StartupCheckForContentUpdated;
        settings.AbilitiesGenerationOption = this.SelectedAbilityGenerateOption.Value;
        settings.UseDefaultAbilityScoreMaximum = this.UseDefaultAbilityScoreMaximum;
        settings.PlayerName = this.DefaultPlayerName;
        settings.DefaultFontSize = this.DefaultFontSize;
        settings.AllowEditableSheet = this.AllowEditableSheet;
        settings.CharacterSheetAbilitiesFlipped = this.FlippedAbilities;
        settings.IncludeSpellcards = this.IncludeSpellcards;
        settings.IncludeItemcards = this.IncludeItemcards;
        settings.SheetIncludeAttackCards = this.IncludeAttackCards;
        settings.SheetIncludeFeatureCards = this.IncludeFeatureCards;
        settings.SheetIncludeFormatting = this.IncludeFormatting;
        settings.SheetFormattingActionSuffixBold = this.SheetFormattingSuffixActionsBold;
        settings.UseLegacyDetailsPage = this.UseLegacyDetailsPage;
        settings.UseLegactBackgroundPage = this.UseLegacyBackgroundPage;
        settings.UseLegacySpellcastingPage = this.UseLegacySpellcastingPage;
        settings.SheetStartItemCardsOnNewPage = this.StartItemCardsOnNewPage;
        settings.SheetStartAttackCardsOnNewPage = this.StartAttackCardsOnNewPage;
        settings.SheetStartFeatureCardsOnNewPage = this.StartFeatureCardsOnNewPage;
        settings.SheetDescriptionAbbreviateUsage = this.SheetDescriptionAbbreviateUsage;
        settings.SheetIncludeNonPreparedSpells = this.SheetIncludeNonPreparedSpells;
        settings.Save();
        ApplicationManager.Current.EventAggregator.Send<SettingsChangedEvent>(new SettingsChangedEvent());
        this.SetAccent();
        this.SetTheme(true);
    }

    private void SaveSettings()
    {
        this.ApplySettings();
        this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Your preferences have been saved."));
    }

    private void CancelSettings()
    {
        this.ResetSettings();
        this.ApplySettings();
    }

    private void ResetSettings()
    {
        ApplicationContext.Current.Settings.Reload();
        this.PopulateProperties();
    }

    private void SetAccent() => ApplicationManager.Current.SetAccent(this.SelectedAccent?.Name);

    private void SetTheme(bool saveTheme)
    {
        ApplicationManager.Current.SetTheme(this.SelectedTheme.Name, saveTheme);
    }

    private void PopulateProperties()
    {
        // ISSUE: variable of a compiler-generated type
        AppSettingsStore settings = ApplicationManager.Current.Settings.Settings;
        this.StartupCheckForUpdates = settings.StartupCheckForUpdates;
        this.StartupLoadCustomFiles = settings.StartupLoadCustomFiles;
        this.SelectedSelectionExpanderItemsSize = this.SelectionExpanderItemsSize.FirstOrDefault<SelectionItem>((Func<SelectionItem, bool>)(x => x.Value == settings.SelectionExpanderGridRowSize)) ?? this.SelectionExpanderItemsSize.FirstOrDefault<SelectionItem>((Func<SelectionItem, bool>)(x => x.Value == 2));
        this.AutoNavigateNextSelectionWhenAvailable = settings.AutoNavigateNextSelectionWhenAvailable;
        this.CustomRootDirectory = settings.DocumentsRootDirectory;
        this.AdditionalCustomDirectory = settings.AdditionalCustomDirectory;
        this.DisplayRemoveLevelConfirmation = settings.DisplayRemoveLevelConfirmation;
        this.RequestAddAttackWhenEquippingWeapon = settings.RequestAddAttackWhenEquippingWeapon;
        this.StartupCheckForContentUpdated = settings.StartupCheckForContentUpdated;
        this.SelectedTheme = this.Themes.FirstOrDefault<AppTheme>((Func<AppTheme, bool>)(x => x.Name.Equals(settings.Theme)));
        this.SelectedAccent = this.Accents.FirstOrDefault<Accent>((Func<Accent, bool>)(x => x.Name.Equals(settings.Accent)));
        if (this.SelectedAccent == null)
            this.SelectedAccent = this.Accents.FirstOrDefault<Accent>((Func<Accent, bool>)(x => x.Name.Equals("aurora default", StringComparison.OrdinalIgnoreCase)));
        this.CharactersCollectionSize = settings.CharactersCollectionSize;
        this.CharacterPanelAbilitiesIsExpanded = settings.CharacterPanelAbilityScoresExpanded;
        this.CharacterPanelStatisticsIsExpanded = settings.CharacterPanelStatisticsExpanded;
        this.CharacterPanelSavingThrowsIsExpanded = settings.CharacterPanelSavingThrowsExpanded;
        this.CharacterPanelSkillsIsExpanded = settings.CharacterPanelSkillsExpanded;
        this.CharacterPanelQuickActionsIsExpanded = settings.CharacterPanelQuickActionsExpanded;
        this.EnableQuickSearchBar = settings.QuickSearchBarEnabled;
        this.SelectedAbilityGenerateOption = this.AbilitiesGenerationSelectionItems.FirstOrDefault<SelectionItem>((Func<SelectionItem, bool>)(x => x.Value == settings.AbilitiesGenerationOption));
        this.UseDefaultAbilityScoreMaximum = settings.UseDefaultAbilityScoreMaximum;
        this.DefaultPlayerName = settings.PlayerName;
        this.DefaultFontSize = settings.DefaultFontSize;
        this.AllowEditableSheet = settings.AllowEditableSheet;
        this.FlippedAbilities = settings.CharacterSheetAbilitiesFlipped;
        this.IncludeSpellcards = settings.IncludeSpellcards;
        this.IncludeItemcards = settings.IncludeItemcards;
        this.IncludeAttackCards = settings.SheetIncludeAttackCards;
        this.IncludeFeatureCards = settings.SheetIncludeFeatureCards;
        this.IncludeFormatting = settings.SheetIncludeFormatting;
        this.SheetFormattingSuffixActionsBold = settings.SheetFormattingActionSuffixBold;
        this.UseLegacyDetailsPage = settings.UseLegacyDetailsPage;
        this.UseLegacyBackgroundPage = settings.UseLegactBackgroundPage;
        this.UseLegacySpellcastingPage = settings.UseLegacySpellcastingPage;
        this.StartItemCardsOnNewPage = settings.SheetStartItemCardsOnNewPage;
        this.StartAttackCardsOnNewPage = settings.SheetStartAttackCardsOnNewPage;
        this.StartFeatureCardsOnNewPage = settings.SheetStartFeatureCardsOnNewPage;
        this.SheetDescriptionAbbreviateUsage = settings.SheetDescriptionAbbreviateUsage;
        this.SheetIncludeNonPreparedSpells = settings.SheetIncludeNonPreparedSpells;
    }

    protected override void InitializeDesignData()
    {
        this.StartupLoadCustomFiles = true;
        string[] strArray = new string[10]
        {
      "Black",
      "Brown",
      "Default",
      "Purple",
      "Green",
      "Aqua",
      "Blue",
      "Mauve",
      "Pink",
      "Red"
        };
        foreach (string str in strArray)
            this.Accents.Add(new Accent("Aurora " + str, new Uri($"pack://application:,,,/Aurora.Presentation;component/Styles/Accents/{str}.xaml")));
        this.Themes.Add(new AppTheme("Aurora Light", new Uri("pack://application:,,,/Aurora.Presentation;component/Styles/Theme/AuroraLight.xaml")));
        this.Themes.Add(new AppTheme("Aurora Dark", new Uri("pack://application:,,,/Aurora.Presentation;component/Styles/Theme/AuroraDark.xaml")));
        this.CharactersCollectionSize = 150;
        this.CharacterPanelAbilitiesIsExpanded = true;
        this.CharacterPanelQuickActionsIsExpanded = true;
        this.SelectedAbilityGenerateOption = this.AbilitiesGenerationSelectionItems.FirstOrDefault<SelectionItem>();
        this.DefaultFontSize = "8";
        this.IncludeSpellcards = true;
        base.InitializeDesignData();
    }
}
