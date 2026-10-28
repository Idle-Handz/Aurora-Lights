// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Shell.ShellWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Data;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Extensions;
using Builder.Presentation.Models;
using Builder.Presentation.Models.Collections;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Calculator;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Telemetry;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.ViewModels.Shell.Items;
using Builder.Presentation.ViewModels.Shell.Manage;
using Builder.Presentation.Views;
using Builder.Presentation.Views.Dialogs;
using Builder.Presentation.Views.Sliders;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.ViewModels.Shell;

public sealed class ShellWindowViewModel : 
  ViewModelBase,
  ISubscriber<MainWindowStatusUpdateEvent>,
  ISubscriber<CharacterNameChangedEvent>,
  ISubscriber<CharacterBuildChangedEvent>,
  ISubscriber<CharacterManagerElementRegistered>,
  ISubscriber<CharacterManagerElementUnregistered>,
  ISubscriber<CharacterSavedEvent>,
  ISubscriber<SettingsChangedEvent>,
  ISubscriber<AdditionalContentUpdatedEvent>
{
  private string _statusMessage;
  private int _progressPercentage;
  private bool _isProgressVisible;
  private bool _isProgressIndeterminate;
  private bool _enableDeveloperTools;
  private bool _updateAvailable;
  private string _characterName;
  private string _characterBuild;
  private string _characterPortraitUri;
  private CharacterFile _selectedCharacter;
  private string _loadedFilepath;
  private bool _isCharacterLoaded;
  private bool _applicationInitialized;
  private int _listViewItemSize = 150;
  private bool _isCharacterInformationBlockVisible;
  private string _characterSummery;
  private string _updateNotificationContent = "1";
  private string _characterBackgroundBuildString;
  private string _characterAlignmentBuildString;
  private bool _showDonateButton = true;
  private bool _isCharacterLoadedFully;
  private string _updatesHeader = "NO UPDATE AVAILABLE";
  private string _characterLevelBuild;
  private string _characterClassBuild;
  private bool _isQuickSearchBarEnabled;
  private bool _isContentUpdated;
  private string _contentUpdatedMessage;

  public SelectionRuleNavigationService SelectionRuleNavigationService { get; }

  public SpellContentViewModel SpellContentViewModel { get; set; } = new SpellContentViewModel();

  /// <summary>Shadows ViewModelBase.Settings to expose ApplicationSettings (with ICommand properties) to XAML bindings.</summary>
  public new ApplicationSettings Settings => ApplicationManager.Current.Settings;

  public bool HasExpanders => true;

  public RefactoredEquipmentSectionViewModel RefactoredEquipmentSectionViewModel { get; } = new RefactoredEquipmentSectionViewModel();

  public ICommand QuickSearchCommand => (ICommand) new RelayCommand(new Action(this.QuickSearch));

  private void QuickSearch() { }

  public ShellWindowViewModel()
  {
    this.SelectionRuleNavigationService = new SelectionRuleNavigationService(this.EventAggregator);
    this.StatusMessage = "READY";
    ((CollectionView) CollectionViewSource.GetDefaultView((object) this.Characters)).GroupDescriptions.Add((GroupDescription) new PropertyGroupDescription("CollectionGroupName"));
    if (this.IsInDesignMode)
    {
      this.InitializeDesignData();
    }
    else
    {
      this.CharacterInformationSliderViewModel = new CharacterInformationSliderViewModel(this);
      if (ApplicationManager.Current.IsInDeveloperMode)
        this.EnableDeveloperTools = true;
      FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(DataManager.Current.UserDocumentsRootDirectory);
      fileSystemWatcher.Created += new FileSystemEventHandler(this._watcher_Created);
      fileSystemWatcher.Changed += new FileSystemEventHandler(this._watcher_Changed);
      fileSystemWatcher.Deleted += new FileSystemEventHandler(this._watcher_Deleted);
      fileSystemWatcher.Filter = "*.dnd5e";
      fileSystemWatcher.EnableRaisingEvents = true;
      this.LoadedFilepath = "no character file loaded";
      this.IsCharacterInformationBlockVisible = true;
      this.ListViewItemSize = ApplicationContext.Current.Settings.CharactersCollectionSize;
      this.ApplyRestrictionsCommand = (ICommand) new Builder.Presentation.Commands.ApplyRestrictionsCommand(this.CharacterManager);
      this.ShowDonateButton = !this.Settings.Settings.Bundle;
      this.SubscribeWithEventAggregator();
      CharacterManager.Current.Status.StatusChanged += new EventHandler<CharacterStatusChangedEventArgs>(this.Status_StatusChanged);
    }
  }

  private void Status_StatusChanged(object sender, CharacterStatusChangedEventArgs e)
  {
    if (!e.Status.IsLoaded)
      return;
    this.IsCharacterLoaded = e.Status.IsLoaded;
  }

  public CharacterInformationSliderViewModel CharacterInformationSliderViewModel { get; }

  public string CharacterSummery
  {
    get => this._characterSummery;
    set => this.SetProperty<string>(ref this._characterSummery, value, nameof (CharacterSummery));
  }

  public string Version => Resources.ApplicationVersion;

  public bool EnableDeveloperTools
  {
    get => this._enableDeveloperTools;
    set
    {
      this.SetProperty<bool>(ref this._enableDeveloperTools, value, nameof (EnableDeveloperTools));
    }
  }

  public bool UpdateAvailable
  {
    get => this._updateAvailable;
    set => this.SetProperty<bool>(ref this._updateAvailable, value, nameof (UpdateAvailable));
  }

  public string UpdateNotificationContent
  {
    get => this._updateNotificationContent;
    set
    {
      this.SetProperty<string>(ref this._updateNotificationContent, value, nameof (UpdateNotificationContent));
    }
  }

  public string StatusMessage
  {
    get => this._statusMessage;
    set => this.SetProperty<string>(ref this._statusMessage, value, nameof (StatusMessage));
  }

  public int ProgressPercentage
  {
    get => this._progressPercentage;
    set => this.SetProperty<int>(ref this._progressPercentage, value, nameof (ProgressPercentage));
  }

  public bool IsProgressVisible
  {
    get => this._isProgressVisible;
    set => this.SetProperty<bool>(ref this._isProgressVisible, value, nameof (IsProgressVisible));
  }

  public bool IsProgressIndeterminate
  {
    get => this._isProgressIndeterminate;
    set
    {
      this.SetProperty<bool>(ref this._isProgressIndeterminate, value, nameof (IsProgressIndeterminate));
    }
  }

  public ObservableCollection<CharacterFile> Characters { get; } = new ObservableCollection<CharacterFile>();

  public CharacterFile SelectedCharacter
  {
    get => this._selectedCharacter;
    set
    {
      this.SetProperty<CharacterFile>(ref this._selectedCharacter, value, nameof (SelectedCharacter));
    }
  }

  public string LoadedFilepath
  {
    get => this._loadedFilepath;
    set => this.SetProperty<string>(ref this._loadedFilepath, value, nameof (LoadedFilepath));
  }

  public bool IsCharacterLoaded
  {
    get => this._isCharacterLoaded;
    set => this.SetProperty<bool>(ref this._isCharacterLoaded, value, nameof (IsCharacterLoaded));
  }

  public Builder.Presentation.Models.Character Character => CharacterManager.Current.Character;

  public CharacterManager CharacterManager => CharacterManager.Current;

  public string CharacterName
  {
    get => this._characterName;
    set => this.SetProperty<string>(ref this._characterName, value, nameof (CharacterName));
  }

  public string CharacterBuild
  {
    get => this._characterBuild;
    private set
    {
      this.SetProperty<string>(ref this._characterBuild, value, nameof (CharacterBuild));
    }
  }

  public string CharacterPortraitUri
  {
    get => this._characterPortraitUri;
    set
    {
      this.SetProperty<string>(ref this._characterPortraitUri, value, nameof (CharacterPortraitUri));
    }
  }

  public bool ApplicationInitialized
  {
    get => this._applicationInitialized;
    set
    {
      this.SetProperty<bool>(ref this._applicationInitialized, value, nameof (ApplicationInitialized));
    }
  }

  public int ListViewItemSize
  {
    get => this._listViewItemSize;
    set => this.SetProperty<int>(ref this._listViewItemSize, value, nameof (ListViewItemSize));
  }

  public bool IsCharacterInformationBlockVisible
  {
    get => this._isCharacterInformationBlockVisible;
    set
    {
      this.SetProperty<bool>(ref this._isCharacterInformationBlockVisible, value, nameof (IsCharacterInformationBlockVisible));
    }
  }

  public string CharacterBackgroundBuildString
  {
    get => this._characterBackgroundBuildString;
    set
    {
      this.SetProperty<string>(ref this._characterBackgroundBuildString, value, nameof (CharacterBackgroundBuildString));
    }
  }

  public string CharacterAlignmentBuildString
  {
    get => this._characterAlignmentBuildString;
    set
    {
      this.SetProperty<string>(ref this._characterAlignmentBuildString, value, nameof (CharacterAlignmentBuildString));
    }
  }

  public bool ShowDonateButton
  {
    get => this._showDonateButton;
    set => this.SetProperty<bool>(ref this._showDonateButton, value, nameof (ShowDonateButton));
  }

  public ICommand NewCharacterCommand => (ICommand) new RelayCommand(new Action(this.NewCharacter));

  public ICommand SaveCommand => (ICommand) new RelayCommand(new Action(this.SaveCharacter));

  public ICommand LoadCommand => (ICommand) new RelayCommand(new Action(this.LoadCharacter));

  public ICommand DeleteCommand => (ICommand) new RelayCommand(new Action(this.DeleteCharacter));

  public ICommand LevelUpCommand => (ICommand) new RelayCommand(new Action(this.LevelUp));

  public ICommand LevelDownCommand => (ICommand) new RelayCommand(new Action(this.LevelDown));

  public ICommand ChangePortraitCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ChangePortrait));
  }

  public ICommand ToggleFavoriteCharacterCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ToggleFavoriteCharacter));
  }

  public ICommand UpdateCommand => (ICommand) new RelayCommand(new Action(this.Update));

  public ICommand JumpListSettingsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.JumpListSettings));
  }

  public ICommand CreateSummeryCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.CreateSummery));
  }

  public ICommand CreateCharacterSheetCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.CreateCharacterSheet));
  }

  public ICommand OpenCustomDocumentsFolderCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.OpenCustomDocumentsFolder));
  }

  public ICommand OpenCharacterFileCommand
  {
    get => (ICommand) new RelayCommand<string>(new Action<string>(this.OpenCharacterFile));
  }

  private void OpenCharacterFile(string parameter)
  {
    if (!File.Exists(parameter))
    {
      MessageDialogService.Show("File does not exist.");
    }
    else
    {
      this.SelectedCharacter = this.Characters.FirstOrDefault<CharacterFile>((Func<CharacterFile, bool>) (x => x.FilePath.Equals(parameter)));
      if (this.SelectedCharacter != null)
      {
        this.LoadCharacter();
      }
      else
      {
        try
        {
          CharacterFile characterFile = DataManager.Current.LoadCharacterFile(parameter);
          this.Characters.Add(characterFile);
          this.SelectedCharacter = characterFile;
          this.LoadCharacter();
        }
        catch (Exception ex)
        {
          Logger.Exception(ex, nameof (OpenCharacterFile));
          MessageDialogService.ShowException(ex);
        }
      }
    }
  }

  private void OpenCustomDocumentsFolder()
  {
    Process.Start(DataManager.Current.UserDocumentsCustomElementsDirectory);
  }

  private async void NewCharacter()
  {
    ShellWindowViewModel shellWindowViewModel = this;
    if (CharacterManager.Current.Status.HasChanges)
    {
      switch (MessageBox.Show((string.IsNullOrWhiteSpace(shellWindowViewModel.Character.Name) ? "The current character" : shellWindowViewModel.Character.Name) + " has unsaved changes. Do you want to save these changes?", Resources.ApplicationName, MessageBoxButton.YesNoCancel, MessageBoxImage.Exclamation))
      {
        case MessageBoxResult.Yes:
          shellWindowViewModel.CharacterManager.File.Save();
          break;
        case MessageBoxResult.No:
          break;
        default:
          return;
      }
    }
    shellWindowViewModel.IsCharacterInformationBlockVisible = true;
    Logger.Info("Creating a New Character");
    Builder.Presentation.Models.Character character = await CharacterManager.Current.New(true);
    shellWindowViewModel.CharacterManager.File = new CharacterFile("UNSAVED CHARACTER")
    {
      FileName = "NEW",
      IsNew = true
    };
    shellWindowViewModel.IsCharacterLoaded = true;
    shellWindowViewModel.EventAggregator.Send<CharacterLoadingCompletedEvent>(new CharacterLoadingCompletedEvent());
    CharacterManager.Current.Status.HasChanges = false;
    shellWindowViewModel.StatusMessage = "Created new character";
  }

  [Obsolete]
  private void SaveCharacter()
  {
    try
    {
      this.CharacterManager.File.InitializeDisplayPropertiesFromCharacter(this.Character);
      bool? nullable = new SaveCharacterWindow(this.CharacterManager.File).ShowDialog();
      if (nullable.HasValue && nullable.Value)
      {
        this.StatusMessage = $"'{this.Character}' Saved";
        CharacterManager.Current.Status.HasChanges = false;
      }
      else
        this.StatusMessage = "Canceled Saved";
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (SaveCharacter));
      MessageDialogService.ShowException(ex);
      this.StatusMessage = $"Error on saving current character. ({ex.Message})";
    }
  }

  private async void LoadCharacter()
  {
    ShellWindowViewModel shellWindowViewModel = this;
    var obj = (Exception) null;
    int num = 0;
    try
    {
      try
      {
        if (shellWindowViewModel.SelectedCharacter == null)
        {
          shellWindowViewModel.StatusMessage = "Select a character to load.";
        }
        else
        {
          if (CharacterManager.Current.Status.HasChanges)
          {
            switch (MessageBox.Show((string.IsNullOrWhiteSpace(shellWindowViewModel.Character.Name) ? "The current character" : shellWindowViewModel.Character.Name) + " has unsaved changes. Do you want to save these changes?", Resources.ApplicationName, MessageBoxButton.YesNoCancel, MessageBoxImage.Exclamation))
            {
              case MessageBoxResult.Yes:
                shellWindowViewModel.CharacterManager.File.Save();
                break;
              case MessageBoxResult.No:
                break;
              default:
                shellWindowViewModel.StatusMessage = $"Loading '{shellWindowViewModel.SelectedCharacter.DisplayName}' Canceled";
                goto label_14;
            }
          }
          shellWindowViewModel.IsCharacterLoaded = false;
          shellWindowViewModel.StatusMessage = $"Loading '{shellWindowViewModel.SelectedCharacter.DisplayName}'";
          shellWindowViewModel.SelectionRuleNavigationService.IsEnabled = false;
          shellWindowViewModel.IsCharacterInformationBlockVisible = true;
          if ((await shellWindowViewModel.SelectedCharacter.Load()).Success)
          {
            shellWindowViewModel.IsCharacterLoadedFully = true;
            Logger.Info($"IsCharacterLoadedFully: {shellWindowViewModel.IsCharacterLoadedFully}");
          }
          else
          {
            shellWindowViewModel.IsCharacterLoadedFully = false;
            Logger.Warning($"IsCharacterLoadedFully: {shellWindowViewModel.IsCharacterLoadedFully}");
          }
          shellWindowViewModel.CharacterManager.File = shellWindowViewModel.SelectedCharacter;
          shellWindowViewModel.LoadedFilepath = shellWindowViewModel.CharacterManager.File.FilePath;
          shellWindowViewModel.IsCharacterLoaded = CharacterManager.Current.Status.IsLoaded = true;
          CharacterManager.Current.Status.HasChanges = false;
          shellWindowViewModel.EventAggregator.Send<CharacterLoadingCompletedEvent>(new CharacterLoadingCompletedEvent());
          goto label_16;
        }
      }
      catch (Exception ex)
      {
        AnalyticsErrorHelper.Exception(ex, method: nameof (LoadCharacter), line: 515);
        string message = "There was an error while loading the character.  \r\n" + ex.Message;
        shellWindowViewModel.ProgressPercentage = 0;
        Logger.Exception(ex, nameof (LoadCharacter));
        MessageDialogService.Show(message);
        goto label_16;
      }
label_14:
      num = 1;
    }
    catch (Exception ex)
    {
      obj = ex;
    }
label_16:
    shellWindowViewModel.SelectionRuleNavigationService.IsEnabled = true;
    if (shellWindowViewModel.IsCharacterLoaded)
      shellWindowViewModel.StatusMessage = $"Loaded '{shellWindowViewModel.SelectedCharacter.DisplayName}'";
    await Task.Delay(2000);
    shellWindowViewModel.EventAggregator.Send<CharacterLoadingSliderEventArgs>(new CharacterLoadingSliderEventArgs(false));
    await Task.Delay(1000);
    shellWindowViewModel.IsProgressVisible = false;
    Exception obj1 = obj;
    if (obj1 != null)
    {
      if (!(obj1 is Exception source))
        throw obj1;
      ExceptionDispatchInfo.Capture(source).Throw();
    }
    if (num == 1)
      return;
    obj = (Exception) null;
  }

  public bool IsCharacterLoadedFully
  {
    get => this._isCharacterLoadedFully;
    set
    {
      this.SetProperty<bool>(ref this._isCharacterLoadedFully, value, nameof (IsCharacterLoadedFully));
    }
  }

  private void DeleteCharacter()
  {
    if (this.SelectedCharacter == null)
    {
      this.StatusMessage = "Select a character to delete.";
    }
    else
    {
      try
      {
        switch (MessageBox.Show($"Are you sure you want to delete {this.SelectedCharacter.DisplayName}?", Resources.ApplicationName, MessageBoxButton.YesNoCancel, MessageBoxImage.Exclamation))
        {
          case MessageBoxResult.Yes:
            string filePath = this.SelectedCharacter.FilePath;
            if (this.Characters.Contains(this.SelectedCharacter))
              this.Characters.Remove(this.SelectedCharacter);
            if (File.Exists(filePath))
              File.Delete(filePath);
            this.StatusMessage = "Deleted " + filePath;
            DataManager.Current.RemoveNonExistingCharacterFileLocations();
            break;
        }
      }
      catch (Exception ex)
      {
        Logger.Exception(ex, nameof (DeleteCharacter));
        MessageDialogService.ShowException(ex);
        this.StatusMessage = "Unable to delete character. " + ex.Message;
      }
    }
  }

  private void ChangePortrait()
  {
    try
    {
      OpenFileDialog openFileDialog1 = new OpenFileDialog();
      openFileDialog1.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
      OpenFileDialog openFileDialog2 = openFileDialog1;
      bool? nullable = openFileDialog2.ShowDialog();
      if (!nullable.HasValue || !nullable.Value)
        return;
      string portraitsDirectory = DataManager.Current.UserDocumentsPortraitsDirectory;
      FileInfo fileInfo = new FileInfo(openFileDialog2.FileName);
      string lower = fileInfo.Name.ToLower();
      string str = Path.Combine(portraitsDirectory, lower);
      if (!File.Exists(str))
      {
        using (Bitmap bitmap = new Bitmap(Image.FromFile(fileInfo.FullName, false)))
          bitmap.Save(str);
      }
      this.Character.PortraitFilename = str;
      CharacterManager.Current.Status.IsUserPortrait = true;
    }
    catch (IOException ex)
    {
      Logger.Exception((Exception) ex, nameof (ChangePortrait));
      int num = (int) MessageBox.Show(ex.Message, "IO Exception @ ChangePortrait");
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (ChangePortrait));
      int num = (int) MessageBox.Show(ex.Message, "Exception @ ChangePortrait");
    }
  }

  private void Update() => Logger.Warning("not implemented method Update");

  private void JumpListSettings()
  {
    new PreferencesWindow().ShowDialog();
    this.ListViewItemSize = ApplicationContext.Current.Settings.CharactersCollectionSize;
  }

  private void LevelUp()
  {
    CharacterManager.Current.LevelUp();
    this.StatusMessage = $"Your character is now level {this.Character.Level}";
  }

  private void LevelDown()
  {
    CharacterManager.Current.LevelDown();
    this.StatusMessage = $"Your character is now level {this.Character.Level}";
  }

  private void ToggleFavoriteCharacter()
  {
    if (this.SelectedCharacter == null)
      return;
    this.SelectedCharacter.IsFavorite = !this.SelectedCharacter.IsFavorite;
  }

  private void CreateSummery()
  {
    if (!this.IsCharacterLoaded)
    {
      this.CharacterSummery = "======================================== NO CHARACTER LOADED ========================================";
    }
    else
    {
      List<ElementBase> list = CharacterManager.Current.GetElements().ToList<ElementBase>();
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.AppendLine("======================================== CHARACTER SUMMARY ========================================");
      stringBuilder.AppendLine("Name: " + this.Character.Name);
      stringBuilder.AppendLine("Race: " + this.Character.Race);
      stringBuilder.AppendLine("Class: " + this.Character.Class);
      stringBuilder.AppendLine("Background: " + this.Character.Background);
      stringBuilder.AppendLine("Alignment: " + this.Character.Alignment);
      stringBuilder.AppendLine($"Experience: {this.Character.Experience}");
      stringBuilder.AppendLine("PlayerName: " + this.Character.PlayerName);
      stringBuilder.AppendLine("==========");
      stringBuilder.AppendLine("Age: " + this.Character.Age);
      stringBuilder.AppendLine("Height: " + this.Character.Height);
      stringBuilder.AppendLine("Weight: " + this.Character.Weight);
      stringBuilder.AppendLine("Eyes: " + this.Character.Eyes);
      stringBuilder.AppendLine("Skin: " + this.Character.Skin);
      stringBuilder.AppendLine("Hair: " + this.Character.Hair);
      stringBuilder.AppendLine("==========");
      stringBuilder.AppendLine($"ArmorClass: {this.Character.ArmorClass}");
      stringBuilder.AppendLine($"Initiative: {this.Character.Initiative}");
      stringBuilder.AppendLine($"Speed: {this.Character.Speed}");
      stringBuilder.AppendLine($"HP: {this.Character.MaxHp}");
      ElementBase elementBase1 = list.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Class"));
      if (elementBase1 != null)
      {
        stringBuilder.AppendLine($"hd: {elementBase1.ElementSetters.GetSetter("hd")}");
        stringBuilder.AppendLine("======================================== SETTERS ========================================");
        foreach (ElementSetters.Setter elementSetter in (List<ElementSetters.Setter>) elementBase1.ElementSetters)
          stringBuilder.AppendLine($"{elementSetter.Name}:{elementSetter.Value}");
      }
      AbilitiesCollection abilities = this.Character.Abilities;
      stringBuilder.AppendLine("==========");
      stringBuilder.AppendLine($"{abilities.Strength.Abbreviation.ToUpper()}: {abilities.Strength.AbilityAndModifierString}");
      stringBuilder.AppendLine($"{abilities.Dexterity.Abbreviation.ToUpper()}: {abilities.Dexterity.AbilityAndModifierString}");
      stringBuilder.AppendLine($"{abilities.Constitution.Abbreviation.ToUpper()}: {abilities.Constitution.AbilityAndModifierString}");
      stringBuilder.AppendLine($"{abilities.Intelligence.Abbreviation.ToUpper()}: {abilities.Intelligence.AbilityAndModifierString}");
      stringBuilder.AppendLine($"{abilities.Wisdom.Abbreviation.ToUpper()}: {abilities.Wisdom.AbilityAndModifierString}");
      stringBuilder.AppendLine($"{abilities.Charisma.Abbreviation.ToUpper()}: {abilities.Charisma.AbilityAndModifierString}");
      stringBuilder.AppendLine("======================================== SAVING THROWS ========================================");
      StatisticsHandler2 statisticsCalculator = CharacterManager.Current.StatisticsCalculator;
      int num1 = statisticsCalculator.StatisticValues.GetValue(statisticsCalculator.Names.StrengthSaveProficiency);
      int num2 = statisticsCalculator.StatisticValues.GetValue(statisticsCalculator.Names.DexteritySaveProficiency);
      int num3 = statisticsCalculator.StatisticValues.GetValue(statisticsCalculator.Names.ConstitutionSaveProficiency);
      int num4 = statisticsCalculator.StatisticValues.GetValue(statisticsCalculator.Names.IntelligenceSaveProficiency);
      int num5 = statisticsCalculator.StatisticValues.GetValue(statisticsCalculator.Names.WisdomSaveProficiency);
      int num6 = statisticsCalculator.StatisticValues.GetValue(statisticsCalculator.Names.CharismaSaveProficiency);
      stringBuilder.AppendLine($"{(abilities.Strength.Modifier + num1).ToValueString()} {abilities.Strength.Name}");
      stringBuilder.AppendLine($"{(abilities.Dexterity.Modifier + num2).ToValueString()} {abilities.Dexterity.Name}");
      stringBuilder.AppendLine($"{(abilities.Constitution.Modifier + num3).ToValueString()} {abilities.Constitution.Name}");
      stringBuilder.AppendLine($"{(abilities.Intelligence.Modifier + num4).ToValueString()} {abilities.Intelligence.Name}");
      stringBuilder.AppendLine($"{(abilities.Wisdom.Modifier + num5).ToValueString()} {abilities.Wisdom.Name}");
      stringBuilder.AppendLine($"{(abilities.Charisma.Modifier + num6).ToValueString()} {abilities.Charisma.Name}");
      foreach (ElementBase elementBase2 in list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Proficiency" && x.Name.StartsWith("Saving Throw"))))
        stringBuilder.AppendLine(elementBase2.ToString());
      stringBuilder.AppendLine("======================================== SKILLS ========================================");
      foreach (SkillItem skillItem in this.Character.Skills.GetCollection())
        stringBuilder.AppendLine($"{skillItem.FinalBonus.ToValueString()} {skillItem.Name} ({skillItem.KeyAbility.Abbreviation})");
      stringBuilder.AppendLine("==========");
      foreach (ElementBase elementBase3 in list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Proficiency" && x.Name.StartsWith("Skill"))))
        stringBuilder.AppendLine(elementBase3.ToString());
      stringBuilder.AppendLine("======================================== PROFICIENCY ========================================");
      foreach (ElementBase elementBase4 in list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Proficiency" && !x.Name.StartsWith("Skill") && !x.Name.StartsWith("Saving Throw"))))
        stringBuilder.AppendLine(elementBase4.ToString());
      stringBuilder.AppendLine("======================================== LANGUAGES ========================================");
      foreach (ElementBase elementBase5 in list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Language")))
        stringBuilder.AppendLine(elementBase5.ToString());
      stringBuilder.AppendLine("======================================== FEATURES & TRAITS ========================================");
      foreach (ElementBase elementBase6 in list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Class Feature" || x.Type == "Archetype" || x.Type == "Archetype Feature" || x.Type == "Feat" || x.Type == "Racial Trait" || x.Type == "Vision")))
        stringBuilder.AppendLine($"{elementBase6}");
      stringBuilder.AppendLine("======================================== EQUIPMENT ========================================");
      stringBuilder.AppendLine("======================================== BACKGROUNDS ========================================");
      foreach (ElementBase elementBase7 in list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Background" || x.Type == "Background Variant" || x.Type == "Background Feature")))
        stringBuilder.AppendLine(elementBase7.ToString());
      IEnumerable<SelectRule> selectRules = CharacterManager.Current.SelectionRules.Where<SelectRule>((Func<SelectRule, bool>) (x => x.ElementHeader.Type == "Background" && x.Attributes.IsList));
      string str1 = "";
      string str2 = "";
      string str3 = "";
      string str4 = "";
      bool flag = false;
      foreach (SelectRule selectRule in selectRules)
      {
        SelectRule selectionRule = selectRule;
        foreach (KeyValuePair<string, SelectionRuleListItem> selectionRuleListItem in CharacterManager.Current.GetElements().First<ElementBase>((Func<ElementBase, bool>) (x => x.Id == selectionRule.ElementHeader.Id)).SelectionRuleListItems)
        {
          if (!flag)
          {
            switch (selectionRuleListItem.Key)
            {
              case "Personality Trait":
                str1 += selectionRuleListItem.Value.Text;
                continue;
              case "Ideal":
                str2 += selectionRuleListItem.Value.Text;
                continue;
              case "Bond":
                str3 += selectionRuleListItem.Value.Text;
                continue;
              case "Flaw":
                str4 += selectionRuleListItem.Value.Text;
                continue;
              default:
                continue;
            }
          }
        }
        flag = true;
      }
      stringBuilder.AppendLine("Personality Traits: " + str1);
      stringBuilder.AppendLine("Ideals: " + str2);
      stringBuilder.AppendLine("Bonds: " + str3);
      stringBuilder.AppendLine("Flaws: " + str4);
      stringBuilder.AppendLine("======================================== ADDITIONAL FEATURES & TRAITS ========================================");
      stringBuilder.AppendLine("======================================== TREASURE ========================================");
      stringBuilder.AppendLine("======================================== INLINE VALUES ========================================");
      foreach (KeyValuePair<string, string> inlineValue in CharacterManager.Current.StatisticsCalculator.InlineValues)
        stringBuilder.AppendLine($"{inlineValue.Key}: {inlineValue.Value}");
      stringBuilder.AppendLine("======================================== STATISTIC VALUES ========================================");
      foreach (StatisticValuesGroup statisticValuesGroup in (IEnumerable<StatisticValuesGroup>) this.CharacterManager.StatisticsCalculator.StatisticValues.Where<StatisticValuesGroup>((Func<StatisticValuesGroup, bool>) (x => x.GetValues().Any<KeyValuePair<string, int>>())).OrderBy<StatisticValuesGroup, string>((Func<StatisticValuesGroup, string>) (x => x.GroupName)))
      {
        string str5 = $"{statisticValuesGroup.GroupName} = {statisticValuesGroup.Sum()}";
        string str6 = string.Join(", ", statisticValuesGroup.GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
        stringBuilder.AppendLine($"{str5.PadRight(60)} {str6}");
      }
      this.CharacterSummery = stringBuilder.ToString();
    }
  }

  private void CreateCharacterSheet()
  {
    try
    {
      CharacterManager.Current.GenerateCharacterSheet();
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  public override async Task InitializeAsync(InitializationArguments args)
  {
    ShellWindowViewModel shellWindowViewModel = this;
    shellWindowViewModel.ApplicationInitialized = false;
    foreach (CharacterFile characterFile in (IEnumerable<CharacterFile>) DataManager.Current.LoadCharacterFiles().OrderBy<CharacterFile, bool>((Func<CharacterFile, bool>) (x => !x.IsFavorite)).ThenBy<CharacterFile, string>((Func<CharacterFile, string>) (x => x.DisplayName)))
      shellWindowViewModel.Characters.Add(characterFile);
    CollectionView defaultView = (CollectionView) CollectionViewSource.GetDefaultView((object) shellWindowViewModel.Characters);
    defaultView.SortDescriptions.Add(new SortDescription("CollectionGroupName", ListSortDirection.Ascending));
    defaultView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
    defaultView.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));
    defaultView.Refresh();
    shellWindowViewModel.UpdateApplicationCharacterGroups();
    shellWindowViewModel.EnableDeveloperTools = ApplicationManager.Current.IsInDeveloperMode;
    shellWindowViewModel.ApplicationInitialized = true;
    CharacterManager.Current.Status.HasChanges = false;
    await Task.Delay(1000);
    shellWindowViewModel.IsQuickSearchBarEnabled = true;
    bool flag = await shellWindowViewModel.CheckUpdatesAvailableVersion();
    ApplicationManager.Current.UpdateAvailable = flag;
    shellWindowViewModel.UpdateAvailable = flag || shellWindowViewModel.IsInDebugMode;
  }

  private void SendSources()
  {
  }

  public string UpdatesHeader
  {
    get => this._updatesHeader;
    set => this.SetProperty<string>(ref this._updatesHeader, value, nameof (UpdatesHeader));
  }

  private async Task<bool> CheckUpdatesAvailableVersion(bool https = true)
  {
    ShellWindowViewModel shellWindowViewModel = this;
    try
    {
      shellWindowViewModel.ProgressPercentage = 25;
      shellWindowViewModel.StatusMessage = "";
      shellWindowViewModel.IsProgressVisible = true;
      System.Version local = new System.Version(Resources.ApplicationVersion);
      using (HttpClient client = new HttpClient())
      {
        string stringAsync = await client.GetStringAsync(Resources.ApplicationVersionUrl);
        shellWindowViewModel.ProgressPercentage = 50;
        if (new System.Version(stringAsync).CompareTo(local) == 1)
        {
          shellWindowViewModel.StatusMessage = $"Update Available ({stringAsync})";
          shellWindowViewModel.UpdatesHeader = $"Update Available ({stringAsync})";
          shellWindowViewModel.UpdateNotificationContent = stringAsync ?? "";
          MessageDialogService.Show("A new version is available for download.", $"Update Available ({stringAsync})");
          shellWindowViewModel.EventAggregator.Send<ShowSliderEvent>(new ShowSliderEvent(Slider.UpdateChangelog));
          return true;
        }
        shellWindowViewModel.StatusMessage = "";
        shellWindowViewModel.UpdateNotificationContent = "1";
      }
      local = (System.Version) null;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (CheckUpdatesAvailableVersion));
      shellWindowViewModel.StatusMessage = ex.Message;
    }
    finally
    {
      shellWindowViewModel.ProgressPercentage = 100;
      shellWindowViewModel.IsProgressVisible = false;
    }
    return false;
  }

  public void OnHandleEvent(CharacterNameChangedEvent args)
  {
    this.CharacterName = args.Name;
    this.Character.Name = args.Name;
  }

  public void OnHandleEvent(CharacterBuildChangedEvent args)
  {
    this.CharacterBuild = !string.IsNullOrWhiteSpace(args.Character.Race) || string.IsNullOrWhiteSpace(args.Character.Class) ? (string.IsNullOrWhiteSpace(args.Character.Race) || !string.IsNullOrWhiteSpace(args.Character.Class) ? $"Level {args.Character.Level} {args.Character.Race} {args.Character.Class}" : $"Level {args.Character.Level} {args.Character.Race}") : $"Level {args.Character.Level} {args.Character.Class}";
    this.GenerateBuildStrings();
  }

  public string CharacterLevelBuild
  {
    get => this._characterLevelBuild;
    set
    {
      this.SetProperty<string>(ref this._characterLevelBuild, value, nameof (CharacterLevelBuild));
    }
  }

  public string CharacterClassBuild
  {
    get => this._characterClassBuild;
    set
    {
      this.SetProperty<string>(ref this._characterClassBuild, value, nameof (CharacterClassBuild));
    }
  }

  private void GenerateBuildStrings()
  {
    this.CharacterLevelBuild = $"Level {this.Character.Level}";
    if (string.IsNullOrWhiteSpace(this.Character.Race) && !string.IsNullOrWhiteSpace(this.Character.Class))
      this.CharacterClassBuild = this.Character.Class ?? "";
    else if (!string.IsNullOrWhiteSpace(this.Character.Race) && string.IsNullOrWhiteSpace(this.Character.Class))
      this.CharacterClassBuild = this.Character.Race ?? "";
    else
      this.CharacterClassBuild = $"{this.Character.Race} {this.Character.Class}";
  }

  public void OnHandleEvent(MainWindowStatusUpdateEvent args)
  {
    this.StatusMessage = args.StatusMessage;
    this.ProgressPercentage = args.ProgressPercentage;
    this.IsProgressIndeterminate = args.IsIndeterminateProgress;
    if (args.ProgressPercentage > 0 || args.IsIndeterminateProgress || this.ProgressPercentage > 0)
      this.IsProgressVisible = true;
    else
      this.HandleHidingProgress();
  }

  private async void HandleHidingProgress()
  {
    await Task.Delay(2500);
    if (this.ProgressPercentage != -1)
      return;
    this.IsProgressVisible = false;
  }

  private async void _watcher_Created(object sender, FileSystemEventArgs e)
  {
    Logger.Info("FileSystemWatcher => Created: {0} | {1}", (object) e.FullPath, (object) e.ChangeType);
    await System.Windows.Application.Current.Dispatcher.BeginInvoke((Delegate) (async () =>
    {
      CharacterFile characterFile = DataManager.Current.LoadCharacterFile(e.FullPath);
      bool flag = false;
      foreach (CharacterFile character in (Collection<CharacterFile>) this.Characters)
      {
        if (character.FilePath.Equals(characterFile.FilePath))
        {
          flag = true;
          break;
        }
      }
      if (!flag)
        this.Characters.Add(characterFile);
      this.StatusMessage = $"{characterFile.DisplayName} ({e.Name}) was saved to the character folder, added to the list of characters.";
    }));
  }

  private async void _watcher_Changed(object sender, FileSystemEventArgs e)
  {
    Logger.Info("FileSystemWatcher => Changed: {0} | {1}", (object) e.FullPath, (object) e.ChangeType);
    await System.Windows.Application.Current.Dispatcher.BeginInvoke((Delegate) (() => this.StatusMessage = $"{DataManager.Current.LoadCharacterFile(e.FullPath).DisplayName} ({e.Name}) was changed in the character folder, updated the character in the list."));
    this.StatusMessage = e.Name + " was changed in the character folder.";
  }

  private async void _watcher_Deleted(object sender, FileSystemEventArgs e)
  {
    try
    {
      if (System.Windows.Application.Current.Dispatcher != null)
        await System.Windows.Application.Current.Dispatcher.BeginInvoke((Delegate) (() =>
        {
          CharacterFile characterFile = this.Characters.FirstOrDefault<CharacterFile>((Func<CharacterFile, bool>) (x => x.FilePath == e.FullPath));
          if (characterFile == null)
            return;
          if (this.SelectedCharacter == characterFile)
            this.SelectedCharacter = (CharacterFile) null;
          this.Characters.Remove(characterFile);
        }));
      this.StatusMessage = e.Name + " was deleted from the character folder.";
    }
    catch (Exception ex)
    {
      AnalyticsErrorHelper.Exception(ex, method: nameof (_watcher_Deleted), line: 1287);
      MessageDialogService.ShowException(ex);
    }
  }

  protected override void InitializeDesignData()
  {
    this.StatusMessage = "Status Message";
    this.ProgressPercentage = 67;
    this.IsProgressVisible = true;
    this.ListViewItemSize = 150;
    this.IsCharacterInformationBlockVisible = true;
    this.EnableDeveloperTools = true;
    this.IsCharacterLoaded = true;
    string[] characterNames = DesignData.CharacterNames;
    string[] portraitFilenames = DesignData.PortraitFilenames;
    Random random = new Random();
    for (int index = 0; index < 20; ++index)
    {
      CharacterFile characterFile = new CharacterFile("C:\\users\\a\\fake\\directory\\path\\5e Character Builder\\designdata.dnd5e")
      {
        DisplayName = characterNames[random.Next(characterNames.Length)],
        DisplayLevel = (index + 1).ToString(),
        DisplayPortraitFilePath = portraitFilenames[random.Next(portraitFilenames.Length)],
        IsFavorite = random.Next(5) == 1,
        DisplayRace = "Changeling",
        DisplayClass = "Wizard",
        DisplayBackground = "Sage",
        DisplayVersion = "1.17.1201",
        CollectionGroupName = index % 2 == 0 ? "Group 1" : "Group 2"
      };
      characterFile.FileName = characterFile.DisplayName.ToLower() + ".dnd5e";
      this.Characters.Add(characterFile);
    }
    this.CharacterManager.File = this.Characters[0];
    this.SelectedCharacter = this.Characters[0];
    this.LoadedFilepath = portraitFilenames[0];
    this.Character.Name = "Sei�r";
    this.Character.Level = 7;
    this.Character.Background = "Uthgardt Tribe Member";
    this.Character.Class = "Blood Hunter";
    this.Character.Race = "Lizardfolk";
    this.Character.PortraitFilename = portraitFilenames[0];
    this.Character.Experience = 23000;
    this.Character.Alignment = "Neutral Good";
    this.Character.Gender = "Female";
    this.Character.Proficiency = 4;
    this.Character.Initiative = 6;
    this.Character.ArmorClass = 21;
    this.Character.Speed = 35;
    this.Character.MaxHp = 109;
    this.CharacterBuild = $"Level {this.Character.Level} {this.Character.Race} {this.Character.Class}";
    this.Character.Companion.Name = "Guenhwyvar";
    this.Character.Companion.CompanionName.OriginalContent = "Guenhwyvar";
    this.Character.Companion.DisplayName = "Black Panther";
    this.Character.Companion.DisplayBuild = "Medium beast, unaligned";
    this.Character.Companion.Portrait.Content = portraitFilenames[1];
    this.Character.Companion.Initiative.OriginalContent = "6";
    this.Character.Companion.ArmorClass.OriginalContent = "11";
    this.Character.Companion.Speed.OriginalContent = "35";
    this.Character.Companion.MaxHp.OriginalContent = "3";
    this.SelectionRuleNavigationService.IsNextAvailable = true;
  }

  public ICommand CharacterSheetPreviousPageCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.CharacterSheetPreviousPage));
  }

  public ICommand CharacterSheetNextPageCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.CharacterSheetNextPage));
  }

  private void CharacterSheetPreviousPage()
  {
    this.EventAggregator.Send<CharacterSheetPreviousPageEvent>(new CharacterSheetPreviousPageEvent());
  }

  private void CharacterSheetNextPage()
  {
    this.EventAggregator.Send<CharacterSheetNextPageEvent>(new CharacterSheetNextPageEvent());
  }

  public void OnHandleEvent(CharacterManagerElementRegistered args)
  {
  }

  public void OnHandleEvent(CharacterManagerElementUnregistered args)
  {
  }

  public void OnHandleEvent(CharacterSavedEvent args)
  {
    if (args.File.IsNew)
    {
      this.CharacterManager.File = args.File;
      this.LoadedFilepath = this.CharacterManager.File.FilePath;
      this.CharacterManager.File.IsNew = false;
      CharacterManager.Current.Status.HasChanges = false;
    }
    bool flag = false;
    foreach (CharacterFile character in (Collection<CharacterFile>) this.Characters)
    {
      if (character.FilePath.Equals(args.File.FilePath))
      {
        flag = true;
        break;
      }
    }
    if (!flag)
      this.Characters.Add(args.File);
    ((CollectionView) CollectionViewSource.GetDefaultView((object) this.Characters)).Refresh();
  }

  public bool IsQuickSearchBarEnabled
  {
    get => this._isQuickSearchBarEnabled;
    set
    {
      this.SetProperty<bool>(ref this._isQuickSearchBarEnabled, value, nameof (IsQuickSearchBarEnabled));
    }
  }

  void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
  {
    this.IsQuickSearchBarEnabled = args.Settings.QuickSearchBarEnabled;
    this.ListViewItemSize = args.Settings.CharactersCollectionSize;
    this.ShowDonateButton = !this.Settings.Settings.Bundle;
  }

  public ICommand SaveDocumentAsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.SaveDocumentAs));
  }

  private void SaveDocumentAs()
  {
    try
    {
      string str = ((IEnumerable<char>) Path.GetInvalidFileNameChars()).Aggregate<char, string>(this.Character.Name, (Func<string, char, string>) ((current, invalidChar) => current.Replace(invalidChar.ToString(), "")));
      SaveFileDialog saveFileDialog = new SaveFileDialog();
      saveFileDialog.DefaultExt = "pdf";
      saveFileDialog.AddExtension = true;
      saveFileDialog.FileName = str.ToLower().Trim();
      saveFileDialog.InitialDirectory = DataManager.Current.UserDocumentsRootDirectory;
      saveFileDialog.Title = $"Save Character Sheet ({this.Character.Name})";
      saveFileDialog.Filter = "PDF (*.pdf)|*.pdf";
      bool? nullable = saveFileDialog.ShowDialog();
      bool flag = true;
      if (!(nullable.GetValueOrDefault() == flag & nullable.HasValue))
        return;
      string fileName = saveFileDialog.FileName;
      CharacterManager.Current.ReprocessCharacter();
      FileInfo newSheet = new CharacterSheetGenerator(CharacterManager.Current).GenerateNewSheet(fileName, false);
      this.EventAggregator.Send<CharacterSheetSavedEvent>(new CharacterSheetSavedEvent(fileName));
      int num = this.Settings.Settings.CharacterSheetOpenOnSave ? 1 : 0;
      Process.Start(newSheet.FullName);
    }
    catch (IOException ex)
    {
      MessageDialogService.ShowException((Exception) ex);
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  public void OnHandleEvent(AdditionalContentUpdatedEvent args)
  {
    this.IsContentUpdated = args.IsUpdated;
    this.ContentUpdatedMessage = args.Message;
  }

  public bool IsContentUpdated
  {
    get => this._isContentUpdated;
    set => this.SetProperty<bool>(ref this._isContentUpdated, value, nameof (IsContentUpdated));
  }

  public string ContentUpdatedMessage
  {
    get => this._contentUpdatedMessage;
    set
    {
      this.SetProperty<string>(ref this._contentUpdatedMessage, value, nameof (ContentUpdatedMessage));
    }
  }

  public ICommand RestartCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.RestartAfterContentUpdated));
  }

  private void RestartAfterContentUpdated()
  {
    if (MessageBox.Show("Your content files have been updated, do you want to restart the application to reload the content?", "Aurora Builder", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
      return;
    ApplicationManager.Current.RestartApplication();
  }

  private List<string> GetGroups()
  {
    return this.Characters.Select<CharacterFile, string>((Func<CharacterFile, string>) (x => x.CollectionGroupName)).Distinct<string>().OrderBy<string, string>((Func<string, string>) (x => x)).ToList<string>();
  }

  private void UpdateApplicationCharacterGroups()
  {
    ApplicationManager.Current.CharacterGroups.Clear();
    foreach (string group in this.GetGroups())
      ApplicationManager.Current.CharacterGroups.Add(group);
  }

  public ICommand EditGroupCommand
  {
    get => (ICommand) new RelayCommand<object>(new Action<object>(this.EditGroup));
  }

  public ICommand EditCharacterGroupCommand
  {
    get => (ICommand) new RelayCommand<object>(new Action<object>(this.EditCharacterGroup));
  }

  private async void EditGroup(object parameter)
  {
    string group = parameter != null ? parameter.ToString() : throw new ArgumentNullException();
    EditCharacterGroupWindow characterGroupWindow = new EditCharacterGroupWindow(group, this.GetGroups());
    bool? nullable = characterGroupWindow.ShowDialog();
    bool flag = true;
    if (!(nullable.GetValueOrDefault() == flag & nullable.HasValue))
      return;
    try
    {
      string newGroup = characterGroupWindow.NewGroupName;
      await Task.Run((Action) (() => System.Windows.Application.Current.Dispatcher.Invoke((Action) (() =>
      {
        foreach (CharacterFile character in (Collection<CharacterFile>) this.Characters)
        {
          if (character.CollectionGroupName.Equals(group))
            character.UpdateGroupName(newGroup);
        }
      }))));
      ((CollectionView) CollectionViewSource.GetDefaultView((object) this.Characters)).Refresh();
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
    this.UpdateApplicationCharacterGroups();
  }

  private void EditCharacterGroup(object parameter)
  {
    if (!(parameter is CharacterFile characterFile))
      return;
    EditCharacterGroupWindow characterGroupWindow = new EditCharacterGroupWindow(characterFile.CollectionGroupName, this.GetGroups());
    bool? nullable = characterGroupWindow.ShowDialog();
    bool flag = true;
    if (!(nullable.GetValueOrDefault() == flag & nullable.HasValue))
      return;
    try
    {
      string newGroupName = characterGroupWindow.NewGroupName;
      characterFile.UpdateGroupName(newGroupName);
      ((CollectionView) CollectionViewSource.GetDefaultView((object) this.Characters)).Refresh();
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
    this.UpdateApplicationCharacterGroups();
  }

  public ICommand ApplyRestrictionsCommand { get; }

  public ICommand OpenDeveloperToolsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.OpenDeveloperTools));
  }

  private void OpenDeveloperTools()
  {
  }

  public ICommand OpenToolsCommand => (ICommand) new RelayCommand(new Action(this.OpenTools));

  private void OpenTools()
  {
  }
}
