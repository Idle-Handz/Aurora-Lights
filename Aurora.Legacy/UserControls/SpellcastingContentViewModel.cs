// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.SpellcastingContentViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Extensions;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Content;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

#nullable disable
namespace Builder.Presentation.UserControls;

public class SpellcastingContentViewModel : 
  SpellsContentViewModel,
  ISubscriber<ExpanderFocusedEvent>,
  ISubscriber<CharacterManagerElementRegistered>
{
  private SpellcastingInformation _spellcastingInformation;
  private int _filteredElementsCount;
  private int _selectionElementsCount;
  private int _availableSelections;
  private int _totalAvailableSelection;
  private SelectionElement _selectedSelectionElement;
  private bool _hasSelectionRules;
  private bool _allowRetrainSelectedSpell;

  public SpellcastingContentViewModel()
  {
    this.InformationHeader = new SpellcastingInformationHeader();
    this.Notification = new SelectionNotification("", "");
    List<string> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Spell"))).Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Source)).Distinct<string>().OrderBy<string, string>((Func<string, string>) (x => x)).ToList<string>();
    List<string> schoolCollection = new List<string>((IEnumerable<string>) new string[8]
    {
      "Abjuration",
      "Conjuration",
      "Divination",
      "Enchantment",
      "Evocation",
      "Illusion",
      "Necromancy",
      "Transmutation"
    });
    foreach (ElementBase elementBase in DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Magic School"))).Distinct<ElementBase>().ToList<ElementBase>())
    {
      if (!schoolCollection.Contains(elementBase.Name))
        schoolCollection.Add(elementBase.Name);
    }
    list.Insert(0, "--");
    schoolCollection.Insert(0, "--");
    SpellcastingFilter spellcastingFilter = new SpellcastingFilter((IEnumerable<string>) list, (IEnumerable<string>) schoolCollection);
    spellcastingFilter.IncludeCantrips = true;
    spellcastingFilter.Include1 = true;
    spellcastingFilter.Include2 = true;
    spellcastingFilter.Include3 = true;
    spellcastingFilter.Include4 = true;
    spellcastingFilter.Include5 = true;
    spellcastingFilter.Include6 = true;
    spellcastingFilter.Include7 = true;
    spellcastingFilter.Include8 = true;
    spellcastingFilter.Include9 = true;
    spellcastingFilter.IsSchoolFilterAvailable = false;
    spellcastingFilter.IsSourceFilterAvailable = true;
    this.SpellcastingFilter = spellcastingFilter;
    this.SpellcastingFilter.IsLocked = false;
    this.SpellcastingFilter.IsSchoolFilterAvailable = true;
    this.SpellcastingFilter.IsSourceFilterAvailable = true;
    this.SpellcastingFilter.School = "--";
    this.SpellcastingFilter.Source = "--";
    if (this.IsInDesignMode)
    {
      this.Notification.IsActive = true;
      this.InformationHeader.Header = "a nice header";
      this.InformationHeader.SpellcastingAbilityName = "Intelligence";
      this.InformationHeader.SpellAttackModifier = 6;
      this.InformationHeader.SpellSaveDc = 14;
      this.InformationHeader.Slot1 = 4;
      this.InformationHeader.Slot2 = 2;
      this.InformationHeader.Slot3 = 1;
      this.FilterNotification.IsActive = true;
      this.Notification.IsActive = true;
    }
    else
    {
      this.RetrainSelectionRuleCommand = new RelayCommand(new Action(this.RetrainSelectionRule), new Func<bool>(this.CanRetrainSelectionRule));
      this.PropertyChanged += new PropertyChangedEventHandler(this.NewSpellsContentViewModel_PropertyChanged);
      this.SpellcastingFilter.PropertyChanged += new PropertyChangedEventHandler(this.SpellcastingFilter_PropertyChanged);
      this.Expanders.CollectionChanged += new NotifyCollectionChangedEventHandler(this.Expanders_CollectionChanged);
      CharacterManager.Current.SourcesManager.SourceRestrictionsApplied += new EventHandler(this.SourcesManager_SourceRestrictionsApplied);
      this.SubscribeWithEventAggregator();
    }
  }

  private void SourcesManager_SourceRestrictionsApplied(object sender, EventArgs e)
  {
    if (!CharacterManager.Current.Status.IsLoaded)
      return;
    if (this.SelectedExpander == null)
      return;
    try
    {
      this.UpdateSelectionCount();
      this.PopulateFromSelectedExpander(this.SpellcastingFilter.IsLocked);
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  private void Expanders_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
  {
    this.UpdateSelectionCount();
  }

  private void UpdateSelectionCount()
  {
    this.RequiredSelectionCount = this.Expanders.Count<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => !x.IsSelectionMade() && !x.SelectionRule.Attributes.Optional));
    this.SelectionCount = this.Expanders.Count;
    this.HasSelectionRules = this.SelectionCount != 0;
  }

  private void SpellcastingFilter_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    this.PopulateFromFilterActivation();
  }

  private void NewSpellsContentViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    if (!e.PropertyName.Equals("SelectedExpander"))
      return;
    this.RetrainSelectionRuleCommand.RaiseCanExecuteChanged();
    this.AllowRetrainSelectedSpell = this.CanRetrainSelectionRule();
    if (this.SelectedExpander == null)
    {
      this.InformationHeader.Header = "N/A";
    }
    else
    {
      this.UpdateSelectionCount();
      this.PopulateFromSelectedExpander(this.SpellcastingFilter.IsLocked);
    }
  }

  private void PopulateFromSelectedExpander(bool applyFilter)
  {
    Logger.Warning(nameof (PopulateFromSelectedExpander));
    this.InformationHeader.Header = this.SelectedExpander.SelectionRule.Attributes.Name;
    if (this.SelectedExpander.SelectionRule.Attributes.ContainsSpellcastingName())
    {
      CharacterManager current = CharacterManager.Current;
      SpellcastingInformation spellcastingInformation = current.GetSpellcastingInformations().FirstOrDefault<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => x.Name.Equals(this.SelectedExpander.SelectionRule.Attributes.SpellcastingName)));
      if (spellcastingInformation != null)
      {
        this.InformationHeader.SpellcastingAbilityName = spellcastingInformation.AbilityName;
        this.InformationHeader.SpellAttackModifier = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSpellcasterSpellAttackStatisticName());
        this.InformationHeader.SpellSaveDc = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSpellcasterSpellSaveStatisticName());
        this.InformationHeader.Slot1 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(1));
        this.InformationHeader.Slot2 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(2));
        this.InformationHeader.Slot3 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(3));
        this.InformationHeader.Slot4 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(4));
        this.InformationHeader.Slot5 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(5));
        this.InformationHeader.Slot6 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(6));
        this.InformationHeader.Slot7 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(7));
        this.InformationHeader.Slot8 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(8));
        this.InformationHeader.Slot9 = current.StatisticsCalculator.StatisticValues.GetValue(spellcastingInformation.GetSlotStatisticName(9));
      }
      else
      {
        this.InformationHeader.SpellcastingAbilityName = "unknown";
        this.InformationHeader.SpellAttackModifier = 0;
        this.InformationHeader.SpellSaveDc = 0;
        this.InformationHeader.Slot1 = 0;
        this.InformationHeader.Slot2 = 0;
        this.InformationHeader.Slot3 = 0;
        this.InformationHeader.Slot4 = 0;
        this.InformationHeader.Slot5 = 0;
        this.InformationHeader.Slot6 = 0;
        this.InformationHeader.Slot7 = 0;
        this.InformationHeader.Slot8 = 0;
        this.InformationHeader.Slot9 = 0;
      }
    }
    else
    {
      this.InformationHeader.SpellcastingAbilityName = "unknown";
      this.InformationHeader.SpellAttackModifier = 0;
      this.InformationHeader.SpellSaveDc = 0;
      this.InformationHeader.Slot1 = 0;
      this.InformationHeader.Slot2 = 0;
      this.InformationHeader.Slot3 = 0;
      this.InformationHeader.Slot4 = 0;
      this.InformationHeader.Slot5 = 0;
      this.InformationHeader.Slot6 = 0;
      this.InformationHeader.Slot7 = 0;
      this.InformationHeader.Slot8 = 0;
      this.InformationHeader.Slot9 = 0;
    }
    SelectionRuleExpander selectedExpander = this.GetSelectedExpander();
    SelectionRuleExpanderViewModel expanderViewModel = this.GetSelectedExpanderViewModel();
    if (expanderViewModel.ElementRegistered)
      this.EventAggregator.Send<ElementDescriptionDisplayRequestEvent>(new ElementDescriptionDisplayRequestEvent(expanderViewModel.RegisteredElement));
    expanderViewModel.Filter = (ElementFilter) this.SpellcastingFilter;
    expanderViewModel.EnableFilter = applyFilter;
    if (!this.SpellcastingFilter.IsLocked)
    {
      this.SpellcastingFilter.Include1 = this.InformationHeader.Slot1 > 0;
      this.SpellcastingFilter.Include2 = this.InformationHeader.Slot2 > 0;
      this.SpellcastingFilter.Include3 = this.InformationHeader.Slot3 > 0;
      this.SpellcastingFilter.Include4 = this.InformationHeader.Slot4 > 0;
      this.SpellcastingFilter.Include5 = this.InformationHeader.Slot5 > 0;
      this.SpellcastingFilter.Include6 = this.InformationHeader.Slot6 > 0;
      this.SpellcastingFilter.Include7 = this.InformationHeader.Slot7 > 0;
      this.SpellcastingFilter.Include8 = this.InformationHeader.Slot8 > 0;
      this.SpellcastingFilter.Include9 = this.InformationHeader.Slot9 > 0;
    }
    this.Notification.IsActive = this.SelectedExpander.SelectionRule.Attributes.Optional;
    this.Notification.Title = this.SelectedExpander.SelectionRule.Attributes.Name;
    this.Notification.Message = "This selection is optional.";
    if (applyFilter)
      expanderViewModel.ActivateFilter();
    this.SelectionElementsCollection.Clear();
    if (applyFilter)
      this.SelectionElementsCollection.Initialize(selectedExpander.GetViewModel<SelectionRuleExpanderViewModel>().SelectionElementsCollection.Where<SelectionElement>((Func<SelectionElement, bool>) (x => x.IsHighlighted)));
    else
      this.SelectionElementsCollection.Initialize((IEnumerable<SelectionElement>) selectedExpander.GetViewModel<SelectionRuleExpanderViewModel>().SelectionElementsCollection);
    this.FilteredElementsCount = this.SelectionElementsCollection.Count;
    this.SelectionElementsCount = selectedExpander.GetViewModel<SelectionRuleExpanderViewModel>().SelectionElementsCollection.Count;
    this.FilterNotification.IsActive = applyFilter;
    this.FilterNotification.Title = "Filter";
    this.FilterNotification.Message = $"{this.FilteredElementsCount}/{this.SelectionElementsCount}";
  }

  private void PopulateFromFilterActivation()
  {
    Logger.Warning(nameof (PopulateFromFilterActivation));
    SelectionRuleExpander selectedExpander = this.GetSelectedExpander();
    SelectionRuleExpanderViewModel expanderViewModel = this.GetSelectedExpanderViewModel();
    if (expanderViewModel != null)
    {
      expanderViewModel.Filter = (ElementFilter) this.SpellcastingFilter;
      expanderViewModel.EnableFilter = true;
      expanderViewModel.ActivateFilter();
      SelectionElement previousSelection = this.SelectedSelectionElement;
      this.SelectionElementsCollection.Clear();
      this.SelectionElementsCollection.Initialize(selectedExpander.GetViewModel<SelectionRuleExpanderViewModel>().SelectionElementsCollection.Where<SelectionElement>((Func<SelectionElement, bool>) (x => x.IsHighlighted)));
      this.FilteredElementsCount = this.SelectionElementsCollection.Count;
      this.SelectionElementsCount = selectedExpander.GetViewModel<SelectionRuleExpanderViewModel>().SelectionElementsCollection.Count;
      this.FilterNotification.IsActive = true;
      this.FilterNotification.Title = "Filter";
      this.FilterNotification.Message = $"{this.FilteredElementsCount}/{this.SelectionElementsCount}";
      if (previousSelection == null)
        return;
      this.SelectedSelectionElement = this.SelectionElementsCollection.FirstOrDefault<SelectionElement>((Func<SelectionElement, bool>) (x => x.Element.Id == previousSelection.Element.Id));
    }
    else
      Logger.Warning($"{this} PopulateFromFilterActivation,  vm is null");
  }

  public SpellcastingInformationHeader InformationHeader { get; }

  public SelectionNotification Notification { get; } = new SelectionNotification(nameof (Notification), "Notification Message");

  public SpellcastingFilter SpellcastingFilter { get; }

  public SelectionNotification FilterNotification { get; } = new SelectionNotification("Filter", "Notification Message");

  public int FilteredElementsCount
  {
    get => this._filteredElementsCount;
    set
    {
      this.SetProperty<int>(ref this._filteredElementsCount, value, nameof (FilteredElementsCount));
    }
  }

  public int SelectionElementsCount
  {
    get => this._selectionElementsCount;
    set
    {
      this.SetProperty<int>(ref this._selectionElementsCount, value, nameof (SelectionElementsCount));
    }
  }

  public int RequiredSelectionCount
  {
    get => this._availableSelections;
    set
    {
      this.SetProperty<int>(ref this._availableSelections, value, nameof (RequiredSelectionCount));
    }
  }

  public int SelectionCount
  {
    get => this._totalAvailableSelection;
    set => this.SetProperty<int>(ref this._totalAvailableSelection, value, nameof (SelectionCount));
  }

  public SpellcastingInformation SpellcastingInformation
  {
    get => this._spellcastingInformation;
    set
    {
      this.SetProperty<SpellcastingInformation>(ref this._spellcastingInformation, value, nameof (SpellcastingInformation));
      if (this._spellcastingInformation != null)
        this.InformationHeader.Header = this._spellcastingInformation.Name;
      else
        this.InformationHeader.Header = "NO INFORMATION AVAILABLE";
    }
  }

  public SelectionElementCollection SelectionElementsCollection { get; set; } = new SelectionElementCollection();

  public SelectionElement SelectedSelectionElement
  {
    get => this._selectedSelectionElement;
    set
    {
      this.SetProperty<SelectionElement>(ref this._selectedSelectionElement, value, nameof (SelectedSelectionElement));
      this.GetSelectedExpander();
      SelectionRuleExpanderViewModel expanderViewModel = this.GetSelectedExpanderViewModel();
      if (expanderViewModel == null)
        return;
      expanderViewModel.SelectedSelectionElement = this._selectedSelectionElement;
      this.EventAggregator.Send<SpellcastingElementDescriptionDisplayRequestEvent>(new SpellcastingElementDescriptionDisplayRequestEvent(this._selectedSelectionElement?.Element));
    }
  }

  public SelectionRuleExpander GetSelectedExpander()
  {
    return this.SelectedExpander as SelectionRuleExpander;
  }

  public SelectionRuleExpanderViewModel GetSelectedExpanderViewModel()
  {
    return this.GetSelectedExpander().GetViewModel<SelectionRuleExpanderViewModel>();
  }

  public void OnHandleEvent(ExpanderFocusedEvent args)
  {
    if (!args.Expander.SelectionRule.Attributes.Type.Equals("Spell"))
      return;
    string id = args.Expander.SelectionRule.UniqueIdentifier;
    int number = args.Expander.Number;
    ISelectionRuleExpander selectionRuleExpander = this.Expanders.FirstOrDefault<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier.Equals(id) && x.Number.Equals(number)));
    this.SpellcastingFilter.IsLocked = false;
    this.SelectedExpander = selectionRuleExpander;
  }

  public void OnHandleEvent(CharacterManagerElementRegistered args) => this.UpdateSelectionCount();

  public bool HasSelectionRules
  {
    get => this._hasSelectionRules;
    set => this.SetProperty<bool>(ref this._hasSelectionRules, value, nameof (HasSelectionRules));
  }

  public RelayCommand RetrainSelectionRuleCommand { get; }

  private void RetrainSelectionRule()
  {
    if (this.SelectedExpander == null)
      return;
    try
    {
      ProgressionManager progressManager = CharacterManager.Current.GetProgressManager(this.SelectedExpander.SelectionRule);
      if (progressManager == null)
        return;
      SpellcastingInformation spellcastingInformation = CharacterManager.Current.GetSpellcastingInformations().FirstOrDefault<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => !x.IsExtension && x.Name.Equals(this.SelectedExpander.SelectionRule.Attributes.SpellcastingName)));
      if (spellcastingInformation == null || !spellcastingInformation.AllowSpellSwap)
        return;
      SelectionRuleExpanderViewModel expanderViewModel = this.GetSelectedExpanderViewModel();
      expanderViewModel.RetrainLevel = progressManager.ProgressionLevel;
      if (expanderViewModel.ElementRegistered)
        expanderViewModel.UnregisterElementCommand.Execute((object) null);
      this.PopulateFromSelectedExpander(true);
      this.UpdateSelectionCount();
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (RetrainSelectionRule));
      MessageDialogService.ShowException(ex);
    }
  }

  private bool CanRetrainSelectionRule()
  {
    if (this.SelectedExpander == null)
      return false;
    try
    {
      if (this.GetSelectedExpanderViewModel().ContainsCantrips || this.SelectedExpander.SelectionRule.ContainsSetters() && this.SelectedExpander.SelectionRule.Setters.ContainsSetter("allowReplace") && !this.SelectedExpander.SelectionRule.Setters.GetSetter("allowReplace").ValueAsBool())
        return false;
      SpellcastingInformation spellcastingInformation = CharacterManager.Current.GetSpellcastingInformations().FirstOrDefault<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => !x.IsExtension && x.Name.Equals(this.SelectedExpander.SelectionRule.Attributes.SpellcastingName)));
      return spellcastingInformation != null && spellcastingInformation.AllowSpellSwap;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (CanRetrainSelectionRule));
    }
    return false;
  }

  public bool AllowRetrainSelectedSpell
  {
    get => this._allowRetrainSelectedSpell;
    set
    {
      this.SetProperty<bool>(ref this._allowRetrainSelectedSpell, value, nameof (AllowRetrainSelectedSpell));
    }
  }
}
