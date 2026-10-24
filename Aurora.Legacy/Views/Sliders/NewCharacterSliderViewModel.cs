// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.NewCharacterSliderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Models;
using Builder.Presentation.Models.Collections;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Telemetry;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.ViewModels.Content;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public sealed class NewCharacterSliderViewModel : ViewModelBase
{
  private readonly Random _random = new Random();
  private string _name;
  private string _gender;
  private int _level;
  private string _portraitFilePath;
  private SelectionItem _selectedAbilityGenerateOption;
  private bool _isArrayOptionSelected;
  private string _previousPortraitPath;
  private string _nextPortraitsPath;

  public event EventHandler? CloseRequested;

  public NewCharacterSliderViewModel()
  {
    this.Name = "";
    this.Gender = "Male";
    this.Level = 1;
    foreach (SelectionItem generationSelectionItem in (Collection<SelectionItem>) ApplicationManager.Current.Settings.AbilitiesGenerationSelectionItems)
      this.AbilitiesGenerationSelectionItems.Add(generationSelectionItem);
    this.Abilities.Strength.BaseScore = 15;
    this.Abilities.Dexterity.BaseScore = 14;
    this.Abilities.Constitution.BaseScore = 13;
    this.Abilities.Intelligence.BaseScore = 12;
    this.Abilities.Wisdom.BaseScore = 10;
    this.Abilities.Charisma.BaseScore = 8;
    if (this.IsInDesignMode)
    {
      this.IsArrayOptionSelected = true;
      this.Name = "Jalan Melthrohe";
      this.Level = 12;
      this.PortraitFilePath = "default-portait.png";
      this.InitializeDesignData();
      this.Options.Add(new CharacterOption((ElementBase) null)
      {
        Header = "Header 1",
        Description = "A short description of the item."
      });
      this.Options.Add(new CharacterOption((ElementBase) null)
      {
        Header = "Header 2",
        Description = "Another short description of the item. Another short description of the item. Another short description of the item. Another short description of the item. Another short description of the item.",
        IsSelected = true,
        IsCustom = true,
        IsEnabled = true
      });
    }
    else
    {
      this.SelectedAbilityGenerateOption = this.AbilitiesGenerationSelectionItems.FirstOrDefault<SelectionItem>((Func<SelectionItem, bool>) (x => x.Value == this.Settings.AbilitiesGenerationOption));
      this.LoadCharacterOptions();
      this.RandomizePortrait();
    }
  }

  public CharacterManager Manager => CharacterManager.Current;

  public ObservableCollection<SelectionItem> AbilitiesGenerationSelectionItems { get; } = new ObservableCollection<SelectionItem>();

  public string Name
  {
    get => this._name;
    set => this.SetProperty<string>(ref this._name, value, nameof (Name));
  }

  public string Gender
  {
    get => this._gender;
    set => this.SetProperty<string>(ref this._gender, value, nameof (Gender));
  }

  public int Level
  {
    get => this._level;
    set => this.SetProperty<int>(ref this._level, value, nameof (Level));
  }

  public string PortraitFilePath
  {
    get => this._portraitFilePath;
    set
    {
      this.SetProperty<string>(ref this._portraitFilePath, value, nameof (PortraitFilePath));
      if (this.PortraitPaths == null)
        return;
      int num = this.PortraitPaths.IndexOf(this.PortraitFilePath);
      this.PreviousPortraitPath = num != 0 ? this.PortraitPaths[num - 1] : this.PortraitPaths[this.PortraitPaths.Count - 1];
      if (num == this.PortraitPaths.Count - 1)
        this.NextPortraitsPath = this.PortraitPaths[0];
      else
        this.NextPortraitsPath = this.PortraitPaths[num + 1];
    }
  }

  public SelectionItem SelectedAbilityGenerateOption
  {
    get => this._selectedAbilityGenerateOption;
    set
    {
      this.SetProperty<SelectionItem>(ref this._selectedAbilityGenerateOption, value, nameof (SelectedAbilityGenerateOption));
      this.IsArrayOptionSelected = this._selectedAbilityGenerateOption.Value == 2;
    }
  }

  public ICommand CancelCommand => (ICommand) new RelayCommand(new Action(this.Cancel));

  private void Cancel() { }

  public ICommand CreateNewCommand => (ICommand) new RelayCommand(new Action(this.CreateNew));

  public ICommand RandomizeNameCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.RandomizeName));
  }

  public ICommand RandomizePortraitCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.RandomizePortrait));
  }

  public AbilitiesCollection Abilities { get; } = new AbilitiesCollection();

  public bool IsArrayOptionSelected
  {
    get => this._isArrayOptionSelected;
    set
    {
      this.SetProperty<bool>(ref this._isArrayOptionSelected, value, nameof (IsArrayOptionSelected));
    }
  }

  public string PreviousPortraitPath
  {
    get => this._previousPortraitPath;
    set
    {
      this.SetProperty<string>(ref this._previousPortraitPath, value, nameof (PreviousPortraitPath));
    }
  }

  public string NextPortraitsPath
  {
    get => this._nextPortraitsPath;
    set => this.SetProperty<string>(ref this._nextPortraitsPath, value, nameof (NextPortraitsPath));
  }

  public ICommand PreviousPortraitCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.PreviousPortrait));
  }

  private void PreviousPortrait() => this.PortraitFilePath = this.PreviousPortraitPath;

  public ICommand NextPortraitCommand => (ICommand) new RelayCommand(new Action(this.NextPortrait));

  private void NextPortrait() => this.PortraitFilePath = this.NextPortraitsPath;

  public List<string> PortraitPaths { get; set; }

  public override Task InitializeAsync(InitializationArguments args) => base.InitializeAsync(args);

  private async void CreateNew()
  {
    NewCharacterSliderViewModel characterSliderViewModel = this;
    characterSliderViewModel.Settings.AbilitiesGenerationOption = characterSliderViewModel.SelectedAbilityGenerateOption.Value;
    characterSliderViewModel.Settings.Save();
    if (string.IsNullOrWhiteSpace(characterSliderViewModel.Name))
      characterSliderViewModel.Name = "The Nameless One";
    CharacterLoadingSliderEventArgs args = new CharacterLoadingSliderEventArgs(true)
    {
      DisplayName = characterSliderViewModel.Name,
      DisplayBuild = $"Level {characterSliderViewModel.Level}",
      DisplayPortraitFilePath = characterSliderViewModel.PortraitFilePath,
      DisplayLevel = characterSliderViewModel.Level.ToString()
    };
    characterSliderViewModel.EventAggregator.Send<CharacterLoadingSliderEventArgs>(args);
    await Task.Delay(1500);
    characterSliderViewModel.EventAggregator.Send<CharacterLoadingSliderStatusUpdateEvent>(new CharacterLoadingSliderStatusUpdateEvent("Preparing New Character"));
    Builder.Presentation.Models.Character character = await CharacterManager.Current.New(true);
    CharacterManager.Current.File = new CharacterFile("UNSAVED CHARACTER")
    {
      IsNew = true,
      FileName = "NEWCHAR"
    };
    characterSliderViewModel.Manager.Character.Name = characterSliderViewModel.Name;
    characterSliderViewModel.Manager.Character.Gender = characterSliderViewModel.Gender;
    characterSliderViewModel.Manager.Character.PortraitFilename = characterSliderViewModel.PortraitFilePath;
    foreach (CharacterOption option in (Collection<CharacterOption>) characterSliderViewModel.Options)
    {
      if (option.IsSelected)
        CharacterManager.Current.RegisterElement(option.Element);
    }
    characterSliderViewModel.EventAggregator.Send<CharacterLoadingSliderStatusUpdateEvent>(new CharacterLoadingSliderStatusUpdateEvent("Leveling Up"));
    while (CharacterManager.Current.Character.Level < characterSliderViewModel.Level)
      CharacterManager.Current.LevelUpMain();
    characterSliderViewModel.EventAggregator.Send<CharacterLoadingSliderStatusUpdateEvent>(new CharacterLoadingSliderStatusUpdateEvent(WebUtility.HtmlDecode("E10B")));
    AbilitiesGenerationOption generationOption = (AbilitiesGenerationOption) characterSliderViewModel.SelectedAbilityGenerateOption.Value;
    switch (generationOption)
    {
      case AbilitiesGenerationOption.Array:
        characterSliderViewModel.Manager.Character.Abilities.Strength.BaseScore = characterSliderViewModel.Abilities.Strength.BaseScore;
        characterSliderViewModel.Manager.Character.Abilities.Dexterity.BaseScore = characterSliderViewModel.Abilities.Dexterity.BaseScore;
        characterSliderViewModel.Manager.Character.Abilities.Constitution.BaseScore = characterSliderViewModel.Abilities.Constitution.BaseScore;
        characterSliderViewModel.Manager.Character.Abilities.Intelligence.BaseScore = characterSliderViewModel.Abilities.Intelligence.BaseScore;
        characterSliderViewModel.Manager.Character.Abilities.Wisdom.BaseScore = characterSliderViewModel.Abilities.Wisdom.BaseScore;
        characterSliderViewModel.Manager.Character.Abilities.Charisma.BaseScore = characterSliderViewModel.Abilities.Charisma.BaseScore;
        break;
    }
    characterSliderViewModel.Manager.Status.IsUserPortrait = true;
    characterSliderViewModel.Manager.Status.IsLoaded = true;
    characterSliderViewModel.Manager.Status.HasChanges = false;
    CharacterManager.Current.Status.HasChanges = false;
    characterSliderViewModel.EventAggregator.Send<CharacterLoadingCompletedEvent>(new CharacterLoadingCompletedEvent());
    characterSliderViewModel.CloseRequested?.Invoke(characterSliderViewModel, EventArgs.Empty);
    await Task.Delay(2000);
    characterSliderViewModel.EventAggregator.Send<CharacterLoadingSliderEventArgs>(new CharacterLoadingSliderEventArgs(false));
    AnalyticsEventHelper.CharacterCreate(characterSliderViewModel.Level.ToString(), generationOption.ToString());
  }

  private void RandomizeName()
  {
    try
    {
      List<Race> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Race")).Cast<Race>().ToList<Race>();
      Race race = list[this._random.Next(list.Count)];
      string str = this.Gender == "Male" || this.Gender == "Female" ? race.Names.GenerateRandomName(this.Gender.ToLower()) : race.Names.GenerateRandomName();
      if (!string.IsNullOrWhiteSpace(str))
      {
        if (!str.ToLower().Equals("n/a"))
        {
          if (!str.StartsWith("$"))
          {
            if (!str.StartsWith("{{"))
            {
              this.Name = str;
              return;
            }
          }
        }
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (RandomizeName));
    }
    this.RandomizeName();
  }

  private void RandomizePortrait()
  {
    try
    {
      if (this.PortraitPaths == null)
        this.PortraitPaths = new List<string>((IEnumerable<string>) Directory.GetFiles(DataManager.Current.UserDocumentsPortraitsDirectory));
      List<string> stringList = new List<string>();
      List<string> list;
      switch (this.Gender.ToLower())
      {
        case "female":
          list = this.PortraitPaths.Where<string>((Func<string, bool>) (x => x.ToLower().Contains("female"))).ToList<string>();
          break;
        case "male":
          list = this.PortraitPaths.Where<string>((Func<string, bool>) (x => x.ToLower().Contains("male") && !x.ToLower().Contains("female"))).ToList<string>();
          break;
        case "construct":
          list = this.PortraitPaths.Where<string>((Func<string, bool>) (x => x.ToLower().Contains("warforged"))).ToList<string>();
          break;
        default:
          list = this.PortraitPaths.ToList<string>();
          break;
      }
      if (!list.Any<string>())
        return;
      this.PortraitFilePath = list[this._random.Next(list.Count)];
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (RandomizePortrait));
      MessageDialogService.ShowException(ex);
    }
  }

  private void LoadCharacterOptions()
  {
    List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Option"))).OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)).ToList<ElementBase>();
    this.Options.Clear();
    foreach (ElementBase element in list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Source.Equals("Internal"))))
    {
      CharacterOption characterOption = new CharacterOption(element)
      {
        IsSelected = true
      };
      if (element is Option option)
        characterOption.IsSelected = option.IsDefault;
      this.Options.Add(characterOption);
    }
    foreach (ElementBase element in list.Where<ElementBase>((Func<ElementBase, bool>) (x => !x.Source.Equals("Internal"))))
    {
      CharacterOption characterOption = new CharacterOption(element)
      {
        IsSelected = false,
        IsEnabled = true,
        IsCustom = true
      };
      if (element is Option option)
        characterOption.IsSelected = option.IsDefault;
      this.Options.Add(characterOption);
    }
  }

  public ObservableCollection<CharacterOption> Options { get; } = new ObservableCollection<CharacterOption>();
}
