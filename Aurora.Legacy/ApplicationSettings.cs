// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ApplicationSettings
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Presentation.Commands.Settings;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Services;
using Builder.Presentation.Telemetry;
using Builder.Presentation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation;

public class ApplicationSettings : ObservableObject, ISubscriber<SettingsChangedEvent>
{
  private readonly IEventAggregator _eventAggregator;
  private bool _isSaveSettingsOnChangeEnabled;
  private bool _characterSheetAbilitiesFlipped;

  public ApplicationSettings(IEventAggregator eventAggregator)
  {
    this._eventAggregator = eventAggregator;
    this.AbilitiesGenerationSelectionItems.Add(new SelectionItem("Roll 3d6", 0));
    this.AbilitiesGenerationSelectionItems.Add(new SelectionItem("Roll 4d6 - Discard Lowest", 1));
    this.AbilitiesGenerationSelectionItems.Add(new SelectionItem("Standard Array (15, 14, 13, 12, 10, 8)", 2));
    this.AbilitiesGenerationSelectionItems.Add(new SelectionItem("Point Buy", 3));
    this.Settings = AppSettingsStore.Load();
    this._eventAggregator.Subscribe((object) this);
  }

  internal AppSettingsStore Settings { get; private set; }

  public bool IsSaveSettingsOnChangeEnabled
  {
    get => this._isSaveSettingsOnChangeEnabled;
    set
    {
      this.SetProperty<bool>(ref this._isSaveSettingsOnChangeEnabled, value, nameof (IsSaveSettingsOnChangeEnabled));
    }
  }

  public ICommand Set3d6GenerateOptionCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.Set3d6GenerateOption));
  }

  public ICommand Set4d6GenerateOptionCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.Set4d6GenerateOption));
  }

  public ICommand SetPointBuyGenerateOptionCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.SetPointBuyGenerateOption));
  }

  public ICommand SetArrayGenerateOptionCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.SetArrayGenerateOption));
  }

  private void Set3d6GenerateOption()
  {
    this.Settings.AbilitiesGenerationOption = 0;
    this.Save();
  }

  private void Set4d6GenerateOption()
  {
    this.Settings.AbilitiesGenerationOption = 1;
    this.Save();
  }

  private void SetPointBuyGenerateOption()
  {
    this.Settings.AbilitiesGenerationOption = 3;
    this.Save();
  }

  private void SetArrayGenerateOption()
  {
    this.Settings.AbilitiesGenerationOption = 2;
    this.Save();
  }

  public ICommand ToggleAllowEditableSheetCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ToggleAllowEditableSheet));
  }

  private void ToggleAllowEditableSheet()
  {
    this.AllowEditableSheet = !this.AllowEditableSheet;
    this.Save();
  }

  public ICommand ToggleAutoGenerateSheetCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ToggleAutoGenerateSheet));
  }

  private void ToggleAutoGenerateSheet()
  {
    this.GenerateSheetOnCharacterChangedRegistered = !this.GenerateSheetOnCharacterChangedRegistered;
    this.Save();
  }

  public ICommand ToggleAutoSelectionNavigationCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ToggleAutoSelectionNavigation));
  }

  private void ToggleAutoSelectionNavigation()
  {
    this.AutoNavigateNextSelectionWhenAvailable = !this.AutoNavigateNextSelectionWhenAvailable;
    this.Save();
  }

  public ObservableCollection<SelectionItem> AbilitiesGenerationSelectionItems { get; } = new ObservableCollection<SelectionItem>();

  public int GetSelectionExpanderGridRowHeight()
  {
    int expanderGridRowHeight = 21;
    try
    {
      switch (this.Settings.SelectionExpanderGridRowSize)
      {
        case 1:
          expanderGridRowHeight = 17;
          break;
        case 2:
          expanderGridRowHeight = 21;
          break;
        case 3:
          expanderGridRowHeight = 25;
          break;
        default:
          expanderGridRowHeight = 21;
          this.Settings.SelectionExpanderGridRowSize = 2;
          this.Save();
          break;
      }
      return expanderGridRowHeight;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (GetSelectionExpanderGridRowHeight));
    }
    return expanderGridRowHeight;
  }

  public bool IncludeItemcards
  {
    get => this.Settings.IncludeItemcards;
    set
    {
      this.Settings.IncludeItemcards = value;
      this.OnPropertyChanged(nameof (IncludeItemcards));
      this.Save();
    }
  }

  public bool AllowEditableSheet
  {
    get => this.Settings.AllowEditableSheet;
    set
    {
      this.Settings.AllowEditableSheet = value;
      this.OnPropertyChanged(nameof (AllowEditableSheet));
      this.Save();
    }
  }

  public bool CharacterSheetAbilitiesFlipped
  {
    get => this.Settings.CharacterSheetAbilitiesFlipped;
    set
    {
      this.Settings.CharacterSheetAbilitiesFlipped = value;
      this.OnPropertyChanged(nameof (CharacterSheetAbilitiesFlipped));
      this.Save();
    }
  }

  public bool CharacterSheetIncludeSpellCards
  {
    get => this.Settings.IncludeSpellcards;
    set
    {
      this.Settings.IncludeSpellcards = value;
      this.OnPropertyChanged(nameof (CharacterSheetIncludeSpellCards));
      this.Save();
    }
  }

  public string PlayerName
  {
    get => this.Settings.PlayerName;
    set
    {
      this.Settings.PlayerName = value;
      this.OnPropertyChanged(nameof (PlayerName));
    }
  }

  public bool GenerateSheetOnCharacterChangedRegistered
  {
    get => this.Settings.GenerateSheetOnCharacterChangedRegistered;
    set
    {
      this.Settings.GenerateSheetOnCharacterChangedRegistered = value;
      this.OnPropertyChanged(nameof (GenerateSheetOnCharacterChangedRegistered));
    }
  }

  public bool AutoNavigateNextSelectionWhenAvailable
  {
    get => this.Settings.AutoNavigateNextSelectionWhenAvailable;
    set
    {
      this.Settings.AutoNavigateNextSelectionWhenAvailable = value;
      this.OnPropertyChanged(nameof (AutoNavigateNextSelectionWhenAvailable));
    }
  }

  public bool StartupCheckForContentUpdated
  {
    get => this.Settings.StartupCheckForContentUpdated;
    set
    {
      this.Settings.StartupCheckForContentUpdated = value;
      this.OnPropertyChanged(nameof (StartupCheckForContentUpdated));
      this.Save();
    }
  }

  public void Save(bool raiseSettingsChanged = true)
  {
    try
    {
      this.Settings.Save();
      if (!raiseSettingsChanged)
        return;
      this.RaiseSettingsChanged();
    }
    catch (ArgumentException ex)
    {
      Logger.Exception((Exception) ex, nameof (Save));
      AnalyticsErrorHelper.Exception((Exception) ex, method: nameof (Save), line: 269);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (Save));
      AnalyticsErrorHelper.Exception(ex, method: nameof (Save), line: 274);
      MessageDialogService.ShowException(ex);
    }
  }

  public void Reload()
  {
      this.Settings.Reload();
      // Notify the UI that all bound settings may have changed
      this.OnPropertyChanged(string.Empty);
  }

  public void Reset() => this.Settings.Reset();

  public void RaiseSettingsChanged()
  {
    this._eventAggregator.Send<SettingsChangedEvent>(new SettingsChangedEvent());
  }

  public ICommand AcitvateDarkThemeCommand => (ICommand) new ActivateDarkThemeCommand();

  public ICommand ActivateLightThemeCommand => (ICommand) new Builder.Presentation.Commands.Settings.ActivateLightThemeCommand();

  public ICommand ActivateDefaultAccentCommand => (ICommand) new Builder.Presentation.Commands.Settings.ActivateDefaultAccentCommand();

  void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
  {
    this.OnPropertyChanged("CharacterSheetAbilitiesFlipped");
    this.OnPropertyChanged("CharacterSheetIncludeSpellCards");
    this.OnPropertyChanged("StartupCheckForContentUpdated");
  }
}
