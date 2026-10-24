// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.ShellWindow
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data.Elements;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Events.Global;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Extensions;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.QuickBar.Commands;
using Builder.Presentation.Telemetry;
using Builder.Presentation.UserControls;
using Builder.Presentation.UserControls.Spellcasting;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.ViewModels.Shell;
using Builder.Presentation.Views._obsolete;
using Builder.Presentation.Views.Development;
using Builder.Presentation.Views.Flyouts;
using Builder.Presentation.Views.Sliders;
using Builder.Presentation.Views.Sliders.Content;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.Views;

public partial class ShellWindow :
  MetroWindow,
  ISubscriber<CharacterLoadingCompletedEvent>,
  ISubscriber<SelectionRuleNavigationArgs>,
  ISubscriber<NavigationServiceEvaluationEvent>,
  ISubscriber<MainWindowStatusUpdateEvent>,
  ISubscriber<CharacterLoadingSliderEventArgs>,
  ISubscriber<ShowSliderEvent>,
  ISubscriber<SettingsChangedEvent>,
  ISubscriber<SpellcastingInformationCreatedEvent>,
  ISubscriber<SpellcastingInformationRemovedEvent>
{
    private readonly IEventAggregator _eventAggregator;
    private readonly ResourceDocumentDisplayService _resourceDocumentDisplayService;
    private CharacterViewer _viewer;
    private bool _statusForegroundIsNormal = true;
    private bool _themeToggle;

    public ShellWindow()
    {
        this.InitializeComponent();
        try
        {
            this.Top = ApplicationManager.Current.Settings.Settings.ShellWindowTop;
            this.Left = ApplicationManager.Current.Settings.Settings.ShellWindowLeft;
            this.Width = ApplicationManager.Current.Settings.Settings.ShellWindowWidth;
            this.Height = ApplicationManager.Current.Settings.Settings.ShellWindowHeight;
            if (ApplicationManager.Current.Settings.Settings.ShellWindowState)
                this.WindowState = WindowState.Maximized;
            else if (this.Top < 1.0)
            {
                if (this.Left < 1.0)
                    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        catch (Exception ex)
        {
            MessageDialogService.ShowException(ex, "Error Initializing Window State");
        }
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            return;
        this._eventAggregator = ApplicationManager.Current.EventAggregator;
        this._eventAggregator.Subscribe((object)this);
        this._resourceDocumentDisplayService = new ResourceDocumentDisplayService(this._eventAggregator);
        this.Flyouts.Loaded += new RoutedEventHandler(this.FlyoutsLoaded);
    }

    private async void FlyoutsLoaded(object sender, RoutedEventArgs e)
    {
        ShellWindow window = this;
        await Task.Delay(2000);
        window.ApplyTheme();
    }

    private void ShellWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        this.ShellWindowLoaded(sender, e);
    }

    private void ShellWindow_OnClosing(object sender, CancelEventArgs e)
    {
        this.ShellWindowClosing(sender, e);
    }

    private void ShellWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
    }

    private void ShellWindow_OnLocationChanged(object sender, EventArgs e)
    {
    }

    private async void ShellWindowLoaded(object sender, RoutedEventArgs e)
    {
        ShellWindow window = this;
        window.ApplyTheme();
        System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        await window.GetViewModel().InitializeAsync((InitializationArguments)null);
        if (window.SelectionFlowNotificationSlider.SelectionRuleNavigationService == null)
            window.SelectionFlowNotificationSlider.SelectionRuleNavigationService = window.GetViewModel<ShellWindowViewModel>().SelectionRuleNavigationService;
        int num = ApplicationManager.Current.IsInDeveloperMode ? 1 : 0;
        if (ApplicationManager.Current.EnableDiagnostics)
            ApplicationManager.Current.ShowDiagnosticsWindow();
        try
        {
            ApplicationManager.Current.Settings.RaiseSettingsChanged();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(ShellWindowLoaded));
            Dictionary<string, string> properties = AnalyticsErrorHelper.CreateProperties("shell on load", "RaiseSettingsChanged");
            AnalyticsErrorHelper.Exception(ex, properties, method: nameof(ShellWindowLoaded), line: 181);
        }
        if (!ApplicationManager.Current.HasCharacterFileRequest)
            return;
        try
        {
            string path = ApplicationManager.Current.LoadedCharacterFilePath;
            if (!File.Exists(path))
            {
                MessageDialogService.Show("The character file you're trying to open doesn't exist.");
            }
            else
            {
                ShellWindowViewModel viewModel = window.GetViewModel<ShellWindowViewModel>();
                viewModel.SelectedCharacter = viewModel.Characters.FirstOrDefault<CharacterFile>((Func<CharacterFile, bool>)(x => x.FilePath.Equals(path)));
                if (viewModel.SelectedCharacter == null)
                {
                    DataManager.Current.AppendCharacterFileLocation(path);
                    AnalyticsEventHelper.CharacterLoad(true, true);
                    CharacterFile characterFile = DataManager.Current.LoadCharacterFile(path);
                    viewModel.Characters.Add(characterFile);
                    viewModel.SelectedCharacter = characterFile;
                    window.LoadSelectedCharacter();
                }
                else
                {
                    AnalyticsEventHelper.CharacterLoad(true);
                    window.LoadSelectedCharacter();
                }
            }
        }
        catch (Exception ex)
        {
            AnalyticsErrorHelper.Exception(ex, method: nameof(ShellWindowLoaded), line: 222);
            Logger.Exception(ex, nameof(ShellWindowLoaded));
            MessageDialogService.ShowException(ex);
        }
    }

    private void ShellWindowClosing(object sender, CancelEventArgs e)
    {
        try
        {
            if (this.WindowState == WindowState.Maximized)
            {
                ApplicationManager.Current.Settings.Settings.ShellWindowTop = this.RestoreBounds.Top;
                ApplicationManager.Current.Settings.Settings.ShellWindowLeft = this.RestoreBounds.Left;
                ApplicationManager.Current.Settings.Settings.ShellWindowWidth = this.RestoreBounds.Width;
                ApplicationManager.Current.Settings.Settings.ShellWindowHeight = this.RestoreBounds.Height;
            }
            else
            {
                ApplicationManager.Current.Settings.Settings.ShellWindowTop = this.Top;
                ApplicationManager.Current.Settings.Settings.ShellWindowLeft = this.Left;
                ApplicationManager.Current.Settings.Settings.ShellWindowWidth = this.Width;
                ApplicationManager.Current.Settings.Settings.ShellWindowHeight = this.Height;
            }
            ApplicationManager.Current.Settings.Settings.ShellWindowState = this.WindowState == WindowState.Maximized;
            ApplicationManager.Current.Settings.Save(false);
        }
        catch (Exception ex)
        {
            AnalyticsErrorHelper.Exception(ex, method: nameof(ShellWindowClosing), line: 253);
        }
        Logger.Info("ShellWindow_Closing");
        if (CharacterManager.Current.Status.HasChanges)
        {
            switch (MessageBox.Show((string.IsNullOrWhiteSpace(CharacterManager.Current.Character.Name) ? "The current character" : CharacterManager.Current.Character.Name) + " has unsaved changes. Do you want to save these changes?", Builder.Presentation.Properties.Resources.ApplicationName, MessageBoxButton.YesNoCancel, MessageBoxImage.Exclamation))
            {
                case MessageBoxResult.Yes:
                    e.Cancel = true;
                    this.OpenSaveCharacterSlider();
                    break;
                case MessageBoxResult.No:
                    System.Windows.Application.Current.Shutdown();
                    break;
                default:
                    e.Cancel = true;
                    break;
            }
        }
        else
        {
            e.Cancel = true;
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void CharactersListViewOnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        this.LoadSelectedCharacter();
    }

    private void BuildTabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || !(e.AddedItems[0] is MetroTabItem addedItem))
            return;
        string lower = addedItem.Header.ToString().ToLower();
        switch (lower)
        {
            case "background":
                this._resourceDocumentDisplayService.DisplayBackgroundDescription();
                break;
            case "feats":
                this._resourceDocumentDisplayService.DisplayFeatsDescription();
                break;
            case "race":
                this._resourceDocumentDisplayService.DisplayRaceDescription();
                break;
            case "class":
                this._resourceDocumentDisplayService.DisplayClassDescription();
                break;
            case "languages":
                this._resourceDocumentDisplayService.DisplayLanguagesDescription();
                break;
            case "ability scores":
                this._resourceDocumentDisplayService.DisplayAbilitiesDescription(true);
                break;
            case "proficiencies":
                this._resourceDocumentDisplayService.DisplayProficienciesDescription();
                break;
        }
    }

    private void ItemsTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.Count == 0 || !(e.AddedItems[0] is MetroTabItem))
                return;
            this._resourceDocumentDisplayService.DisplayEquipmentDescription(true);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(ItemsTabSelectionChanged));
        }
    }

    private void OpenSettingsWindowClicked(object sender, RoutedEventArgs e)
    {
        new ApplicationSettingsWindow().ShowDialog();
    }

    private void OpenSheetSettingsWindowClicked(object sender, RoutedEventArgs e)
    {
        new ApplicationSettingsWindow(true).ShowDialog();
    }

    private void OnPortraitClick(object sender, RoutedEventArgs e) => this.ToggleQuickview();

    private void UpdateNotificationButtonClicked(object sender, RoutedEventArgs e)
    {
        this.ToggleUpdateNotification();
    }

    private void ToggleSideBar(object sender, RoutedEventArgs e) => this.ToggleLeftSidebar();

    private void BrowseHomepageClick(object sender, RoutedEventArgs e)
    {
        this.LaunchCharacterBuilderWebsite();
    }

    private void BrowseNewsClick(object sender, RoutedEventArgs e)
    {
        Process.Start(Builder.Presentation.Properties.Resources.WebsiteUrl + "/news");
    }

    private void BrowseContentClick(object sender, RoutedEventArgs e)
    {
        Process.Start(Builder.Presentation.Properties.Resources.WebsiteUrl + "/content");
    }

    private void BrowseDocumentationPageClick(object sender, RoutedEventArgs e)
    {
        Process.Start(Builder.Presentation.Properties.Resources.WebsiteUrl + "/documentation");
    }

    private void BrowseContactPageClick(object sender, RoutedEventArgs e)
    {
        Process.Start(Builder.Presentation.Properties.Resources.WebsiteUrl + "/support");
    }

    private void BrowseGetStartedPageClick(object sender, RoutedEventArgs e)
    {
        Process.Start(Builder.Presentation.Properties.Resources.WebsiteUrl + "/get-started");
    }

    private void BrowseChangelogClick(object sender, RoutedEventArgs e)
    {
        this.LaunchCharacterBuilderWebsiteReleasesPage();
    }

    private void NewCharacterClick(object sender, RoutedEventArgs e)
    {
        this.CreateNewCharacterClickEvent();
    }

    private void NavigateManage(object sender, RoutedEventArgs e)
    {
        this.NavigateToLocation(NavigationLocation.ManageCharacter);
    }

    private void NavigateSheet(object sender, RoutedEventArgs e)
    {
        this.NavigateToLocation(NavigationLocation.Sheet);
    }

    private void NavigateCustomContent(object sender, RoutedEventArgs e)
    {
        this.NavigateToLocation(NavigationLocation.StartCustomContent);
    }

    private void TogglePortraitsGallerySlider(object sender, RoutedEventArgs e)
    {
        this.TogglePortraitsGallery();
    }

    private void QuickSaveClicked(object sender, RoutedEventArgs e) => this.OpenSaveCharacterSlider();

    private void CharactersListViewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Return:
            case Key.Space:
                this.CharactersListViewOnMouseDoubleClick(sender, (MouseButtonEventArgs)null);
                break;
            case Key.Delete:
                this.GetViewModel<ShellWindowViewModel>().DeleteCommand.Execute((object)null);
                break;
        }
    }

    private void OpenViewerClick(object sender, RoutedEventArgs e) => this.OpenViewer();

    private void OpenAdvancementSliderClick(object sender, RoutedEventArgs e)
    {
        this.OpenAdvancementSlider();
    }

    private void OpenGenerateElementClick(object sender, RoutedEventArgs e)
    {
        new GenerateElementWindow().Show();
    }

    private void MenuItemShutdown_OnClick(object sender, RoutedEventArgs e) => this.Close();

    public void OnHandleEvent(CharacterLoadingCompletedEvent args)
    {
        this.NavigateToLocation(NavigationLocation.Build);
    }

    private async void CreateNewCharacterClickEvent()
    {
        if (CharacterManager.Current.Status.HasChanges)
        {
            switch (MessageBox.Show((string.IsNullOrWhiteSpace(CharacterManager.Current.Character.Name) ? "The current character" : CharacterManager.Current.Character.Name) + " has unsaved changes. Do you want to save these changes?", Builder.Presentation.Properties.Resources.ApplicationName, MessageBoxButton.YesNoCancel, MessageBoxImage.Exclamation))
            {
                case MessageBoxResult.Yes:
                    this.OpenSaveCharacterSlider();
                    return;
                case MessageBoxResult.No:
                    break;
                default:
                    return;
            }
        }
        this.NewCharacterSlider.IsOpen = true;
    }

    private async void LoadSelectedCharacter(bool fromFile = false)
    {
        ShellWindow window = this;
        if (window.GetViewModel<ShellWindowViewModel>().SelectedCharacter == null)
            return;
        if (!fromFile)
            AnalyticsEventHelper.CharacterLoad();
        window.Cursor = Cursors.Wait;
        window.LoadingCharacterSlider.DataContext = (object)window.GetViewModel<ShellWindowViewModel>().SelectedCharacter;
        window.LoadingCharacterSlider.IsOpen = true;
        await Task.Delay(2000);
        window.GetViewModel<ShellWindowViewModel>().LoadCommand.Execute((object)null);
        window.Cursor = Cursors.Wait;
        while (!window.GetViewModel<ShellWindowViewModel>().IsCharacterLoaded)
            await Task.Delay(1000);
        int count = 0;
        while (!window.GetViewModel<ShellWindowViewModel>().IsCharacterLoadedFully)
        {
            ++count;
            await Task.Delay(500);
            if (count >= 5)
                break;
        }
        await Task.Delay(1000);
        if (!Debugger.IsAttached)
            window.NavigateToLocation(NavigationLocation.BuildRace);
        window.Cursor = (Cursor)null;
    }

    private void ToggleLeftSidebar()
    {
        if (this.Toggle.IsChecked.HasValue && this.Toggle.IsChecked.Value)
        {
            if (this.Resources[(object)"SidePanelStoryboard1"] is Storyboard resource1)
                resource1.Begin();
        }
        else if (this.Resources[(object)"SidePanelStoryboard2"] is Storyboard resource2)
            resource2.Begin();
        if (this.Toggle.IsChecked.HasValue && this.Toggle.IsChecked.Value)
        {
            if (!(this.Resources[(object)"SidePanelStoryboard1a"] is Storyboard resource3))
                return;
            resource3.Begin();
        }
        else
        {
            if (!(this.Resources[(object)"SidePanelStoryboard2a"] is Storyboard resource4))
                return;
            resource4.Begin();
        }
    }

    private void ToggleQuickview() => this.NewPopupSlider.IsOpen = true;

    private void ToggleUpdateNotification() => this.UpdateNotificationSlider.IsOpen = true;

    private void TogglePortraitsGallery()
    {
        if (!CharacterManager.Current.Status.IsLoaded)
            return;
        this.SelectPortraitSlider.IsOpen = !this.SelectPortraitSlider.IsOpen;
    }

    private void ToggleCompanionGallery()
    {
        if (!CharacterManager.Current.Status.IsLoaded)
            return;
        this.CompanionGallerySlider.IsOpen = !this.CompanionGallerySlider.IsOpen;
    }

    private void ToggleSymbolsGallery()
    {
        if (!CharacterManager.Current.Status.IsLoaded)
            return;
        this.SymbolsGallerySlider.IsOpen = !this.SymbolsGallerySlider.IsOpen;
    }

    private void OpenAdvancementSlider()
    {
        if (!CharacterManager.Current.Status.IsLoaded)
            return;
        this.AdvancementSlider.IsOpen = true;
    }

    private void LaunchCharacterBuilderWebsite() => Process.Start(Builder.Presentation.Properties.Resources.WebsiteUrl);

    private void LaunchCharacterBuilderWebsiteReleasesPage() => Process.Start(Builder.Presentation.Properties.Resources.ReleasesUrl);

    private void OpenViewer()
    {
        try
        {
            if (this._viewer != null)
            {
                this._viewer.Close();
                this._viewer = (CharacterViewer)null;
            }
            this._viewer = new CharacterViewer();
            this._viewer.DataContext = (object)this.GetViewModel();
            this._viewer.Show();
        }
        catch (Exception ex)
        {
            MessageDialogService.ShowException(ex);
        }
    }

    void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
    {
    }

    private async void NavigateToLocation(NavigationLocation location)
    {
        try
        {
            string primary;
            switch (location)
            {
                case NavigationLocation.Start:
                case NavigationLocation.StartCollection:
                case NavigationLocation.StartSources:
                case NavigationLocation.StartCustomContent:
                    primary = "start";
                    break;
                case NavigationLocation.Build:
                case NavigationLocation.BuildRace:
                case NavigationLocation.BuildClass:
                case NavigationLocation.BuildBackground:
                case NavigationLocation.BuildAbilities:
                case NavigationLocation.BuildLanguages:
                case NavigationLocation.BuildProficiencies:
                case NavigationLocation.BuildFeats:
                case NavigationLocation.BuildCompanion:
                    primary = "build";
                    break;
                case NavigationLocation.Magic:
                case NavigationLocation.MagicSpells:
                case NavigationLocation.MagicCompendium:
                    primary = "magic";
                    break;
                case NavigationLocation.Equipment:
                    primary = "equipment";
                    break;
                case NavigationLocation.Manage:
                case NavigationLocation.ManageCharacter:
                case NavigationLocation.ManageBackground:
                case NavigationLocation.ManageAttacks:
                    primary = "manage";
                    break;
                case NavigationLocation.ManageCompanion:
                    return;
                case NavigationLocation.Sheet:
                case NavigationLocation.SheetCards:
                    primary = "character sheet";
                    break;
                default:
                    return;
            }
            await Task.Delay(5);
            MetroTabItem p = this.MainTabContol.Items.Cast<MetroTabItem>().First<MetroTabItem>((Func<MetroTabItem, bool>)(x => x.Header.ToString().ToLower() == primary));
            p.Focus();
            string secondary;
            switch (location)
            {
                case NavigationLocation.StartCollection:
                    secondary = "character collection";
                    break;
                case NavigationLocation.StartSources:
                    secondary = "sources";
                    break;
                case NavigationLocation.StartCustomContent:
                    secondary = "additional content";
                    break;
                case NavigationLocation.Build:
                    return;
                case NavigationLocation.Magic:
                    return;
                case NavigationLocation.Equipment:
                    return;
                case NavigationLocation.Manage:
                    return;
                case NavigationLocation.ManageCompanion:
                    return;
                case NavigationLocation.BuildRace:
                    secondary = "race";
                    break;
                case NavigationLocation.BuildClass:
                    secondary = "class";
                    break;
                case NavigationLocation.BuildBackground:
                    secondary = "background";
                    break;
                case NavigationLocation.BuildAbilities:
                    secondary = "ability scores";
                    break;
                case NavigationLocation.BuildLanguages:
                    secondary = "languages";
                    break;
                case NavigationLocation.BuildProficiencies:
                    secondary = "proficiencies";
                    break;
                case NavigationLocation.BuildFeats:
                    secondary = "feats";
                    break;
                case NavigationLocation.BuildCompanion:
                    secondary = "companion";
                    break;
                case NavigationLocation.MagicSpells:
                    secondary = "spellcasting";
                    break;
                case NavigationLocation.MagicCompendium:
                    secondary = "spell compendium";
                    break;
                case NavigationLocation.ManageCharacter:
                    secondary = "character";
                    break;
                case NavigationLocation.ManageBackground:
                    secondary = "backstory";
                    break;
                case NavigationLocation.ManageAttacks:
                    secondary = "attacks & spellcasting";
                    break;
                case NavigationLocation.Sheet:
                    secondary = "character sheet";
                    break;
                case NavigationLocation.SheetCards:
                    secondary = "cards";
                    break;
                default:
                    return;
            }
            await Task.Delay(5);
            MetroTabItem metroTabItem = p.FindChildren<MetroTabControl>().First<MetroTabControl>().FindChildren<MetroTabItem>().First<MetroTabItem>((Func<MetroTabItem, bool>)(x => x.Header.ToString().ToLower() == secondary));
            Logger.Info($"navigate location: {primary}>{secondary}");
            metroTabItem.Focus();
            p = (MetroTabItem)null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"error navigation to tabbed location: {location}");
            Logger.Exception(ex, nameof(NavigateToLocation));
        }
    }

    public void OnHandleEvent(NavigationServiceEvaluationEvent args)
    {
        this.SelectionAvailableVisualQue.BringIntoView();
    }

    public void OnHandleEvent(SelectionRuleNavigationArgs args)
    {
        this.NavigateToLocation(args.Location);
    }

    public ICommand KeyBindingNewCharacterCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.CreateNewCharacterClickEvent));
    }

    public ICommand KeyBindingSaveCharacterCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.OpenSaveCharacterSlider));
    }

    public ICommand KeyBindingPortraitsGalleryCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.TogglePortraitsGallery));
    }

    public ICommand KeyBindingSheetViewerCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.OpenViewer));
    }

    public ICommand KeyBindingQuickSearchBarCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.FocusQuickSearchBar));
    }

    public ICommand KeyBindingOpenCompendiumCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.OpenCompendiumCommandImplementation));
    }

    private void OpenCompendiumCommandImplementation() => this.CompendiumSlider.IsOpen = true;

    private void FocusQuickSearchBar() => this.QuickSearchBar.Focus();

    private void QuickSearchBar_OnLostFocus(object sender, RoutedEventArgs e)
    {
        this.QuickSearchBar.Text = "";
    }

    private void QuickSearchBar_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        string.IsNullOrWhiteSpace(this.QuickSearchBar.Text);
    }

    private void QuickSearchBar_OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Return:
                try
                {
                    if (!this.QuickSearchBar.Text.Trim().StartsWith("@"))
                        this.CompendiumSlider.IsOpen = true;
                    QuickSearchService.Current.Search(this.QuickSearchBar.Text.Trim());
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, nameof(QuickSearchBar_OnKeyDown));
                    MessageDialogService.ShowException(ex);
                    break;
                }
            case Key.Escape:
                this.QuickSearchBar.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                break;
        }
    }

    public void OnHandleEvent(MainWindowStatusUpdateEvent args)
    {
        if (this._statusForegroundIsNormal && !args.IsSuccess && !args.IsDanger)
            return;
        if (args.IsSuccess)
        {
            this.MainStatusMessageTextBlock.Foreground = (Brush)(System.Windows.Application.Current.Resources[(object)"SuccessColorBrush"] as SolidColorBrush);
            this._statusForegroundIsNormal = false;
        }
        else if (args.IsDanger)
        {
            this.MainStatusMessageTextBlock.Foreground = (Brush)(System.Windows.Application.Current.Resources[(object)"DangerColorBrush"] as SolidColorBrush);
            this._statusForegroundIsNormal = false;
        }
        else
        {
            this.MainStatusMessageTextBlock.Foreground = (Brush)(System.Windows.Application.Current.Resources[(object)"GrayBrush6"] as SolidColorBrush);
            this._statusForegroundIsNormal = true;
        }
    }

    private void OpenSaveCharacterSlider()
    {
        if (!CharacterManager.Current.Status.IsLoaded)
            return;
        this.SaveCharacterSlider.SetFile(CharacterManager.Current.File);
        this.SaveCharacterSlider.IsOpen = true;
    }

    public void OnHandleEvent(CharacterLoadingSliderEventArgs args)
    {
        if (args.Open)
        {
            this.LoadingCharacterSlider.Initialize(args.DisplayName, args.DisplayBuild, args.DisplayPortraitFilePath, args.DisplayLevel);
            this.LoadingCharacterSlider.IsOpen = true;
        }
        else
            this.LoadingCharacterSlider.IsOpen = false;
    }

    private void ToggleEditEquipmentItemFlyout()
    {
        this.EditEquipmentItemFlyout.DataContext = this.EquipementTab.DataContext;
        this.EditEquipmentItemFlyout.IsOpen = !this.EditEquipmentItemFlyout.IsOpen;
    }

    public ICommand EnableBundlesCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.EnableBundles));
    }

    private void EnableBundles()
    {
        this.NavigateToLocation(NavigationLocation.StartCustomContent);
        this._eventAggregator.Send<BundleCommandEvent>(new BundleCommandEvent("enable"));
    }

    public void OnHandleEvent(SpellcastingInformationCreatedEvent args)
    {
        if (args.Information.IsExtension)
            return;
        this.CreateSpellcasterSection(args.Information);
    }

    public void OnHandleEvent(SpellcastingInformationRemovedEvent args)
    {
        if (args.Information.IsExtension)
            return;
        this.RemoveSpellcasterSection(args.Information.Name);
    }

    public void CreateSpellcasterSection(SpellcastingInformation information)
    {
        if (this.MagicTabControl.Items.Cast<MetroTabItem>().FirstOrDefault<MetroTabItem>((Func<MetroTabItem, bool>)(x => x.HasHeader && x.Header.Equals((object)information.Name.ToUpper()))) != null)
            return;
        MetroTabItem metroTabItem = new MetroTabItem();
        metroTabItem.Content = (object)new SpellcasterSelectionControl();
        metroTabItem.DataContext = (object)new SpellcasterSelectionControlViewModel(information);
        metroTabItem.Header = (object)information.Name.ToUpper();
        metroTabItem.Tag = (object)information;
        MetroTabItem newItem = metroTabItem;
        SpellcastingSectionHandler.Current.Add(newItem.Content as SpellcasterSelectionControl);
        this.MagicTabControl.Items.Add((object)newItem);
        ApplicationManager.Current.SendStatusMessage("created spellcasting section for " + information.Name);
    }

    public void RemoveSpellcasterSection(string name)
    {
        MetroTabItem removeItem = this.MagicTabControl.Items.Cast<MetroTabItem>().FirstOrDefault<MetroTabItem>((Func<MetroTabItem, bool>)(x => x.HasHeader && x.Header.Equals((object)name.ToUpper())));
        if (removeItem != null)
        {
            this.MagicTabControl.Items.Remove((object)removeItem);
            SpellcastingSectionHandler.Current.Remove(removeItem.Tag is SpellcastingInformation tag ? tag.UniqueIdentifier : (string)null);
            ApplicationManager.Current.SendStatusMessage("removed spellcasting section for " + name);
        }
        else
            ApplicationManager.Current.SendStatusMessage("unable to remove spellcasting section for " + name);
    }

    private void DebugToggleFlyoutClick(object sender, RoutedEventArgs e)
    {
    }

    private void DebugControlsDemoWindowClick(object sender, RoutedEventArgs e)
    {
        new ControlsDemoWindow().Show();
    }

    private static Stream GetResourceStream(string resourcePath)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
    }

    private void DebugTestMethod_Click(object sender, RoutedEventArgs e)
    {
        if (!Debugger.IsAttached)
            return;
        try
        {
            new Gen().Show();
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(DebugTestMethod_Click));
            MessageDialogService.ShowException(ex);
        }
    }

    public ICommand ToggleCharacterOptionsCommand
    {
        get => (ICommand)new RelayCommand(new Action(this.ToggleCharacterOptions));
    }

    private void ToggleCharacterOptions() => this.ManageCharacterOptionsSlider.IsOpen = true;

    private void ToggleCharacterOptionsClick(object sender, RoutedEventArgs e)
    {
        this.ToggleCharacterOptions();
    }

    private void ToggleManageCoinageClick(object sender, RoutedEventArgs e)
    {
        this.ManageCoinageSlider.IsOpen = true;
    }

    private void ToggleCompendiumClick(object sender, RoutedEventArgs e)
    {
        this.CompendiumSlider.IsOpen = !this.CompendiumSlider.IsOpen;
    }

    private void BrowseRedditPageClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://www.reddit.com/r/aurorabuilder/");
    }

    private void BrowseDiscordChannelClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://discord.gg/MmWvNFV");
    }

    private void BrowsePatreonPageClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://www.patreon.com/aurorabuilder");
    }

    private void BrowseDonatePageClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://www.aurorabuilder.com/donate");
        AnalyticsEventHelper.ApplicationEvent("browse_donate");
    }

    private void BrowseSupportPageClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://www.aurorabuilder.com/support");
    }

    private void BrowseGitHubPageClick(object sender, RoutedEventArgs e)
    {
        Process.Start("https://github.com/aurorabuilder");
    }

    public void OnHandleEvent(ShowSliderEvent args)
    {
        switch (args.Slider)
        {
            case Builder.Presentation.Views.Sliders.Slider.Gallery:
                this.TogglePortraitsGallery();
                break;
            case Builder.Presentation.Views.Sliders.Slider.CompanionGallery:
                this.ToggleCompanionGallery();
                break;
            case Builder.Presentation.Views.Sliders.Slider.OrganizationSymbolsGallery:
                this.ToggleSymbolsGallery();
                break;
            case Builder.Presentation.Views.Sliders.Slider.Details:
                this.NewPopupSlider.IsOpen = true;
                break;
            case Builder.Presentation.Views.Sliders.Slider.Advancement:
                this.AdvancementSlider.IsOpen = true;
                break;
            case Builder.Presentation.Views.Sliders.Slider.NewCharacter:
                this.NewCharacterSlider.IsOpen = true;
                break;
            case Builder.Presentation.Views.Sliders.Slider.UpdateChangelog:
                this.ToggleUpdateNotification();
                break;
            case Builder.Presentation.Views.Sliders.Slider.ManageCoinage:
                this.ManageCoinageSlider.IsOpen = true;
                break;
            case Builder.Presentation.Views.Sliders.Slider.EditEquipmentItem:
                this.ToggleEditEquipmentItemFlyout();
                break;
            case Builder.Presentation.Views.Sliders.Slider.Compendium:
                this.CompendiumSlider.IsOpen = true;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void OpenSupportsWindowClick(object sender, RoutedEventArgs e)
    {
        new SupportStringTester().Show();
    }

    private void PromoDarkThemeClick(object sender, RoutedEventArgs e)
    {
        ApplicationManager.Current.SetDarkTheme(true);
    }

    private void PromoLightThemeClick(object sender, RoutedEventArgs e)
    {
        ApplicationManager.Current.SetLightTheme(true);
    }

    private void ManageCampaignClick(object sender, RoutedEventArgs e)
    {
        this.NavigateToLocation(NavigationLocation.StartSources);
    }

    private void ManageAttacksClick(object sender, RoutedEventArgs e)
    {
        this.NavigateToLocation(NavigationLocation.ManageAttacks);
    }

    private void ManageCardsClick(object sender, RoutedEventArgs e)
    {
        this.NavigateToLocation(NavigationLocation.SheetCards);
    }

    private void CharacterSheetTabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int count = e.AddedItems.Count;
    }

    private void EditCharacterGroupClick(object sender, RoutedEventArgs e)
    {
    }

    private void DebugToggleThemeClick(object sender, RoutedEventArgs e)
    {
        if (this._themeToggle)
        {
            ApplicationManager.Current.SetDarkTheme(false);
            ApplicationManager.Current.Settings.RaiseSettingsChanged();
        }
        else
        {
            ApplicationManager.Current.SetLightTheme(false);
            ApplicationManager.Current.Settings.RaiseSettingsChanged();
        }
        this._themeToggle = !this._themeToggle;
    }



}
