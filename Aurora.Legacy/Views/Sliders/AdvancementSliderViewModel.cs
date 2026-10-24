// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.AdvancementSliderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;

using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.Views.Sliders;

public class AdvancementSliderViewModel : ViewModelBase
{
  private string _mainclassPrerequisites;
  private ElementBase _mainClass;

  public AdvancementSliderViewModel()
  {
    if (this.IsInDesignMode)
    {
      ObservableCollection<AdvancementSliderViewModel.AdvancementContainer> containers1 = this.Containers;
      ClassProgressionManager manager1 = new ClassProgressionManager(true, false, 1, (ElementBase) null);
      manager1.ProgressionLevel = 2;
      containers1.Add(new AdvancementSliderViewModel.AdvancementContainer(manager1)
      {
        Header = "Rogue",
        Description = "Some guy with a dagger and tieves' tools."
      });
      ObservableCollection<AdvancementSliderViewModel.AdvancementContainer> containers2 = this.Containers;
      ClassProgressionManager manager2 = new ClassProgressionManager(false, true, 3, (ElementBase) null);
      manager2.ProgressionLevel = 3;
      containers2.Add(new AdvancementSliderViewModel.AdvancementContainer(manager2)
      {
        Header = "Fighter",
        Description = "Some guy with a sword and shield."
      });
      this.Containers.First<AdvancementSliderViewModel.AdvancementContainer>().IsAverageHitPoints = false;
      this.Containers.First<AdvancementSliderViewModel.AdvancementContainer>().HitPoints.Add(new AdvancementSliderViewModel.HitPointsEntry(12));
      this.Containers.First<AdvancementSliderViewModel.AdvancementContainer>().HitPoints.Add(new AdvancementSliderViewModel.HitPointsEntry(8));
      this.Containers.First<AdvancementSliderViewModel.AdvancementContainer>().HitPoints.Add(new AdvancementSliderViewModel.HitPointsEntry(4));
      this.Containers.First<AdvancementSliderViewModel.AdvancementContainer>().Subheader = "Arcane Trickster";
      ObservableCollection<Multiclass> multiclassOptions1 = this.AvailableMulticlassOptions;
      Multiclass multiclass1 = new Multiclass();
      multiclass1.ElementHeader = new ElementHeader("Fighter", "", "", "");
      multiclass1.MulticlassPrerequisites = "Strength 13 or Dexterity 13";
      multiclassOptions1.Add(multiclass1);
      ObservableCollection<Multiclass> multiclassOptions2 = this.AvailableMulticlassOptions;
      Multiclass multiclass2 = new Multiclass();
      multiclass2.ElementHeader = new ElementHeader("Rogue", "", "", "");
      multiclass2.MulticlassPrerequisites = "Dexterity 13";
      multiclassOptions2.Add(multiclass2);
      ObservableCollection<Multiclass> multiclassOptions3 = this.AvailableMulticlassOptions;
      Multiclass multiclass3 = new Multiclass();
      multiclass3.ElementHeader = new ElementHeader("Sorcerer", "", "", "");
      multiclass3.MulticlassPrerequisites = "Charisma 13";
      multiclassOptions3.Add(multiclass3);
      this.MainclassPrerequisites = "Dexterity 13";
    }
    else
      this.MainclassPrerequisites = "N/A";
  }

  public event EventHandler CloseRequest;

  public CharacterManager Manager => CharacterManager.Current;

  public string MainclassPrerequisites
  {
    get => this._mainclassPrerequisites;
    set
    {
      this.SetProperty<string>(ref this._mainclassPrerequisites, value, nameof (MainclassPrerequisites));
    }
  }

  public ElementBase MainClass
  {
    get => this._mainClass;
    set => this.SetProperty<ElementBase>(ref this._mainClass, value, nameof (MainClass));
  }

  public ICommand AdvanceMainClassCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.AdvanceMainClass));
  }

  public ICommand AdvanceMulticlassCommand
  {
    get => (ICommand) new RelayCommand<object>(new Action<object>(this.AdvanceMulticlass));
  }

  public ICommand AddMulticlassCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.AddMulticlass));
  }

  public ICommand RemoveLastLevelCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.RemoveLastLevel));
  }

  public ICommand ManageHitPointsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ManageHitPoints));
  }

  private void ManageHitPoints()
  {
  }

  private void AdvanceMainClass()
  {
    this.Manager.LevelUpMain();
    this.PopulateContainers();
  }

  private void AdvanceMulticlass(object parameter)
  {
    this.Manager.LevelUpMulti(parameter as Multiclass);
    this.PopulateContainers();
  }

  private void AddMulticlass()
  {
    this.Manager.NewMulticlass();
    this.OnCloseRequest();
  }

  private async void RemoveLastLevel()
  {
    AdvancementSliderViewModel advancementSliderViewModel = this;
    LevelElement levelElement = CharacterManager.Current.Elements.Last<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level"))).AsElement<LevelElement>();
    ElementBase elementBase = (ElementBase) null;
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) CharacterManager.Current.ClassProgressionManagers)
    {
      if (progressionManager.LevelElements.Contains((ElementBase) levelElement))
        elementBase = progressionManager.ClassElement;
    }
    if (advancementSliderViewModel.Settings.DisplayRemoveLevelConfirmation)
    {
      string messageBoxText = $"Are you sure you want to remove the last level from the {elementBase?.Name.ToLower()} class?";
      if (elementBase == null)
        messageBoxText = $"Are you sure you want to remove the multiclass selection option at level {levelElement.Level}? \r\n\r\nYou have not yet selected a class there, maybe you did not meet the prerequisites yet.";
      if (MessageBox.Show(messageBoxText, Resources.ApplicationName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        advancementSliderViewModel.Manager.LevelDown();
    }
    else
      advancementSliderViewModel.Manager.LevelDown();
    await Task.Delay(500);
    advancementSliderViewModel.PopulateContainers();
  }

  public ObservableCollection<AdvancementSliderViewModel.AdvancementContainer> Containers { get; } = new ObservableCollection<AdvancementSliderViewModel.AdvancementContainer>();

  public ObservableCollection<Multiclass> AvailableMulticlassOptions { get; } = new ObservableCollection<Multiclass>();

  public override Task InitializeAsync()
  {
    IEnumerable<ElementBase> elementBases = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Multiclass")));
    this.AvailableMulticlassOptions.Clear();
    foreach (ElementBase elementBase in elementBases)
      this.AvailableMulticlassOptions.Add(elementBase as Multiclass);
    this.PopulateContainers();
    return base.InitializeAsync();
  }

  private void PopulateContainers()
  {
    List<AdvancementSliderViewModel.AdvancementContainer> list = this.Containers.ToList<AdvancementSliderViewModel.AdvancementContainer>();
    this.Containers.Clear();
    this.MainClass = (ElementBase) null;
    this.MainclassPrerequisites = "N/A";
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.Manager.ClassProgressionManagers)
    {
      ClassProgressionManager manager = progressionManager;
      try
      {
        ElementBase classElement1 = manager.ClassElement;
        AdvancementSliderViewModel.AdvancementContainer advancementContainer1 = new AdvancementSliderViewModel.AdvancementContainer(manager);
        if (manager.IsMainClass)
        {
          this.MainClass = manager.ClassElement;
          advancementContainer1.AdvanceCommand = this.AdvanceMainClassCommand;
          Class classElement = manager.ClassElement.AsElement<Class>();
          if (classElement.CanMulticlass)
          {
            ElementBase element = DataManager.Current.ElementsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id == classElement.MulticlassId));
            if (element != null && element.Type.Equals("Multiclass"))
              this.MainclassPrerequisites = "Prerequisites: " + element.AsElement<Multiclass>().MulticlassPrerequisites;
          }
          else
            this.MainclassPrerequisites = "N/A";
        }
        if (manager.IsMulticlass)
        {
          if (manager.ClassElement == null)
          {
            advancementContainer1.Header = $"EMPTY ({manager.ProgressionLevel})";
            advancementContainer1.Description = $"Select a multiclass from the {manager.SelectRule.Attributes.Name} selection picker.";
          }
          advancementContainer1.AdvanceCommand = this.AdvanceMulticlassCommand;
        }
        if (manager.ClassElement != null && manager.HasArchetype())
        {
          ElementBase elementBase = manager.Elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Archetype")));
          advancementContainer1.Header = $"{manager.ClassElement.Name} ({manager.ProgressionLevel}), {elementBase.Name}";
        }
        advancementContainer1.IsAverageHitPoints = CharacterManager.Current.ContainsAverageHitPointsOption();
        if (!advancementContainer1.IsAverageHitPoints)
          advancementContainer1.InitializeHitPoints();
        AdvancementSliderViewModel.AdvancementContainer advancementContainer2 = list.FirstOrDefault<AdvancementSliderViewModel.AdvancementContainer>((Func<AdvancementSliderViewModel.AdvancementContainer, bool>) (x => x.ProgressionManager.ClassElement == manager.ClassElement));
        advancementContainer1.IsExpanded = advancementContainer2 != null && advancementContainer2.IsExpanded;
        this.Containers.Add(advancementContainer1);
      }
      catch (Exception ex)
      {
        Logger.Exception(ex, nameof (PopulateContainers));
        MessageDialogService.ShowException(ex);
      }
    }
  }

  protected virtual void OnCloseRequest()
  {
    EventHandler closeRequest = this.CloseRequest;
    if (closeRequest == null)
      return;
    closeRequest((object) this, EventArgs.Empty);
  }

  public class HitPointsEntry : ObservableObject
  {
    private int _value;

    public HitPointsEntry(int initialHitPoints)
    {
      this.Min = 1;
      this.Max = 12;
      this._value = initialHitPoints;
    }

    public int Value
    {
      get => this._value;
      set => this.SetProperty<int>(ref this._value, value, nameof (Value));
    }

    public int Min { get; set; }

    public int Max { get; set; }

    public string Level { get; set; }

    public bool IsEnabled { get; set; } = true;
  }

  public class AdvancementContainer
  {
    public AdvancementContainer(ClassProgressionManager manager)
    {
      this.ProgressionManager = manager;
      if (manager.ClassElement == null)
      {
        Logger.Warning($"creating container without class element in manager {manager}");
      }
      else
      {
        this.Header = $"{manager.ClassElement.Name} ({manager.ProgressionLevel})";
        if (manager.ClassElement.AsElement<Class>().HasShort)
          this.Description = manager.ClassElement.AsElement<Class>().Short;
        else
          this.Description = manager.ClassElement.SheetDescription.Count > 0 ? manager.ClassElement.SheetDescription[0].Description : "No short description available.";
      }
    }

    public void InitializeHitPoints()
    {
      this.HitPoints.Clear();
      int[] pointsArrayAsync = this.ProgressionManager.GetRandomHitPointsArrayAsync();
      int hitDieValue = this.ProgressionManager.GetHitDieValue();
      ElementBaseCollection levelElements = this.ProgressionManager.LevelElements;
      for (int index = 0; index < this.ProgressionManager.ProgressionLevel; ++index)
      {
        AdvancementSliderViewModel.HitPointsEntry hitPointsEntry = new AdvancementSliderViewModel.HitPointsEntry(pointsArrayAsync[index]);
        hitPointsEntry.Max = hitDieValue;
        hitPointsEntry.PropertyChanged += new PropertyChangedEventHandler(this.Entry_PropertyChanged);
        hitPointsEntry.Level = this.ProgressionManager.LevelElements[index].Name;
        this.HitPoints.Add(hitPointsEntry);
      }
      if (!this.ProgressionManager.IsMainClass)
        return;
      this.HitPoints[0].IsEnabled = false;
    }

    private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      this.SaveHitPoints();
    }

    public void SaveHitPoints()
    {
      int[] pointsArrayAsync = this.ProgressionManager.GetRandomHitPointsArrayAsync();
      for (int index = 0; index < this.HitPoints.Count; ++index)
        pointsArrayAsync[index] = this.HitPoints[index].Value;
      this.ProgressionManager.SetRandomHitPointsArray(pointsArrayAsync);
      CharacterManager.Current.ReprocessCharacter();
    }

    public ClassProgressionManager ProgressionManager { get; }

    public string Header { get; set; }

    public string Subheader { get; set; }

    public string Description { get; set; }

    public ICommand AdvanceCommand { get; set; }

    public ObservableCollection<AdvancementSliderViewModel.HitPointsEntry> HitPoints { get; } = new ObservableCollection<AdvancementSliderViewModel.HitPointsEntry>();

    public bool IsAverageHitPoints { get; set; }

    public bool IsExpanded { get; set; }
  }
}
