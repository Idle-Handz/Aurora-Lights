// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.SelectionRuleExpanderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Rules;
using Builder.Presentation.Elements;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Events.Global;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Calculator;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Sources;
using Builder.Presentation.UserControls;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.ViewModels;

public class SelectionRuleExpanderViewModel :
  ViewModelBase,
  ISubscriber<CharacterManagerElementRegistered>,
  ISubscriber<CharacterManagerElementUnregistered>,
  ISubscriber<CharacterManagerSelectionRuleDeleted>,
  ISubscriber<ReprocessCharacterEvent>,
  IDisposable
{
  private SelectionCollectionService _service;
  private bool _disposed;
  private bool _initialized;
  private string _header;
  private bool _isEnabled;
  private bool _isExpanded;
  private SelectRule _selectionRule;
  private ElementBaseCollection _selectionElements;
  private ElementBase _selectedElement;
  private string _registeredElementId;
  private int _initialLevel = 1;
  private ElementBaseCollection _baseCollection;
  private ElementBaseCollection _baseSupportsCollection;
  private ExpressionInterpreter _interpreter = new ExpressionInterpreter();
  private int _aquisitionLevel;
  private SpellcastingInformation _information;
  private int _retrainLevel;
  private bool _isOptional;
  private SelectionElement _selectedSelectionElement;

  public bool RegisterOnSelection { get; set; }

  public bool IsComboBoxExpander => this.RegisterOnSelection;

  private void Dispose(bool disposing)
  {
    if (this._disposed)
      return;
    if (disposing)
    {
      this.Dispose();
      this._baseCollection.Clear();
      this._baseSupportsCollection.Clear();
      this._interpreter = (ExpressionInterpreter)null;
    }
    this._disposed = true;
  }

  public void Dispose()
  {
    this.Dispose(true);
    GC.SuppressFinalize((object)this);
  }

  public SelectionRuleExpanderViewModel()
  {
    if (!this.IsInDesignMode)
      return;
    this.InitializeDesignData();
  }

  public SelectionRuleExpanderViewModel(SelectRule selectionRule)
  {
    this.SubscribeWithEventAggregator();
    this.SelectionRule = selectionRule;
    this.SelectionElements = new ElementBaseCollection();
    this.IsOptional = selectionRule.Attributes.Optional;
    this.Header = string.IsNullOrWhiteSpace(this._selectionRule.Attributes.Name) ? this._selectionRule.ElementHeader.Name : this._selectionRule.Attributes.Name;
    if (this.IsOptional)
      this.Header += " (optional)";
    this.IsExpanded = true;
    this.IsEnabled = true;
    Logger.Debug("Expander Created: [{0}]", (object)this._selectionRule.Attributes.Name);
  }

  public string Header
  {
    get => this._header;
    set => this.SetProperty<string>(ref this._header, value, nameof(Header));
  }

  public bool IsEnabled
  {
    get => this._isEnabled;
    set => this.SetProperty<bool>(ref this._isEnabled, value, nameof(IsEnabled));
  }

  public bool IsExpanded
  {
    get => this._isExpanded;
    set
    {
      this.SetProperty<bool>(ref this._isExpanded, value, nameof(IsExpanded));
      if (!this.IsExpanded)
        return;
      if (!this.ElementRegistered)
        return;
      try
      {
        this.SelectedElement = this.SelectionElements.Single<ElementBase>((Func<ElementBase, bool>)(e => e.Id == this.RegisteredElementId));
      }
      catch (Exception ex)
      {
        string title = ex.GetType().ToString();
        string introMessage = $"Unable to set selected element '{this.RegisteredElementId}'";
        MessageDialogService.ShowException(ex, title, introMessage);
      }
    }
  }

  public SelectRule SelectionRule
  {
    get => this._selectionRule;
    set => this.SetProperty<SelectRule>(ref this._selectionRule, value, nameof(SelectionRule));
  }

  public ElementBaseCollection SelectionElements
  {
    get => this._selectionElements;
    set
    {
      this.SetProperty<ElementBaseCollection>(ref this._selectionElements, value, nameof(SelectionElements));
    }
  }

  public ElementBase SelectedElement
  {
    get => this._selectedElement;
    set
    {
      this.SetProperty<ElementBase>(ref this._selectedElement, value, nameof(SelectedElement));
      this.EventAggregator.Send<ElementDescriptionDisplayRequestEvent>(new ElementDescriptionDisplayRequestEvent(this.SelectedElement));
      if (!this.RegisterOnSelection || this._selectedElement == null)
        return;
      this.RegisterSelection();
    }
  }

  public string RegisteredElementId
  {
    get => this._registeredElementId;
    set
    {
      this.SetProperty<string>(ref this._registeredElementId, value, nameof(RegisteredElementId));
      this.OnPropertyChanged("ElementRegistered", "RegisteredElement");
    }
  }

  public bool ElementRegistered => !string.IsNullOrWhiteSpace(this._registeredElementId);

  public ElementBase RegisteredElement
  {
    get
    {
      return string.IsNullOrWhiteSpace(this._registeredElementId) ? (ElementBase)null : this.SelectionElements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(e => e.Id == this._registeredElementId));
    }
  }

  public override async Task InitializeAsync(InitializationArguments args)
  {
    try
    {
      if (this.SelectionRule.Attributes.Type == "Spell")
        this.IsExpanded = false;
      Stopwatch.StartNew();
      this.SetSupportedElements(true, this.SelectionRule);
      this._initialized = true;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof(InitializeAsync));
      MessageDialogService.ShowException(ex);
    }
    await base.InitializeAsync(args);
  }

  public RelayCommand RegisterElementCommand
  {
    get => new RelayCommand(new Action(this.RegisterSelection));
  }

  public RelayCommand UnregisterElementCommand
  {
    get => new RelayCommand(new Action(this.UnregisterRegisteredElement));
  }

  public ICommand ManageSourcesCommand
  {
    get => (ICommand)new RelayCommand(new Action(this.ManageSources));
  }

  private void ManageSources()
  {
    this.EventAggregator.Send<SelectionRuleNavigationArgs>(new SelectionRuleNavigationArgs(NavigationLocation.StartSources));
  }

  private async Task<ElementBaseCollection> GetSupported(SelectRule rule)
  {
    await Task.Run(() =>
    {
      this._baseCollection = new ElementBaseCollection(
        DataManager.Current.ElementsCollection.Where<ElementBase>(
          element => element.Type.Equals(rule.Attributes.Type)));
    });
    return this._baseCollection;
  }

  private void SetSupportedElements(bool initial, SelectRule rule)
  {
    if (rule.Attributes.Type.Equals("Spell"))
    {
      this.GenerateBaseCollections(initial, rule);
    }
    else
    {
      this._interpreter.InitializeWithSelectionRule(rule);
      if (initial)
      {
        this._baseCollection = new ElementBaseCollection(DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>)(element => element.Type.Equals(rule.Attributes.Type))));
        this._initialLevel = rule.Attributes.RequiredLevel;
      }
      if (initial && rule.Attributes.ContainsSupports())
      {
        string expressionString = rule.Attributes.Supports;
        if (rule.Attributes.ContainsDynamicSupports() && rule.Attributes.Supports.Contains("$$SPELLSLOT"))
        {
          List<string> stringList = new List<string>();
          for (int index = 1; index <= 9; ++index)
          {
            if (CharacterManager.Current.StatisticsCalculator.StatisticValues.ContainsGroup("spellcasting:slot:" + index.ToString()) && CharacterManager.Current.StatisticsCalculator.StatisticValues.GetValue("spellcasting:slot:" + index.ToString()) > 0)
              stringList.Add(index.ToString());
          }
          if (stringList.Any<string>())
            expressionString = expressionString.Replace("$$SPELLSLOT", string.Join("||", (IEnumerable<string>)stringList));
        }
        this._baseSupportsCollection = new ElementBaseCollection(this._interpreter.EvaluateSupportsExpression<ElementBase>(expressionString, (IEnumerable<ElementBase>)this._baseCollection, rule.Attributes.SupportsElementIdRange()));
      }
      else if (initial)
        this._baseSupportsCollection = new ElementBaseCollection((IEnumerable<ElementBase>)this._baseCollection);
    }
    ElementBaseCollection elementBaseCollection1 = new ElementBaseCollection((IEnumerable<ElementBase>)this._baseSupportsCollection);
    SourcesManager sourcesManager = CharacterManager.Current.SourcesManager;
    List<string> list1 = sourcesManager.GetUndefinedRestrictedSourceNames().ToList<string>();
    List<string> list2 = sourcesManager.GetRestrictedElementIds().ToList<string>();
    ElementBaseCollection elementBaseCollection2 = new ElementBaseCollection();
    foreach (ElementBase elementBase in (Collection<ElementBase>)elementBaseCollection1)
    {
      if (list2.Contains(elementBase.Id))
        elementBaseCollection2.Add(elementBase);
      else if (list1.Contains(elementBase.Source))
        elementBaseCollection2.Add(elementBase);
    }
    foreach (ElementBase elementBase in (Collection<ElementBase>)elementBaseCollection2)
      elementBaseCollection1.Remove(elementBase);
    ElementBaseCollection elements = CharacterManager.Current.GetElements();
    foreach (ElementBase elementBase in elements.Where<ElementBase>((Func<ElementBase, bool>)(e => e.Type.Equals(rule.Attributes.Type))))
    {
      if (elementBaseCollection1.Contains(elementBase) && !elementBase.AllowDuplicate && !elementBase.Id.Equals(this.RegisteredElementId))
        elementBaseCollection1.RemoveElement(elementBase.Id);
    }
    ElementBaseCollection elementBaseCollection3 = new ElementBaseCollection();
    switch (rule.Attributes.Type)
    {
      case "Spell":
        elementBaseCollection3.AddRange((IEnumerable<ElementBase>)elementBaseCollection1.Cast<Spell>().OrderBy<Spell, int>((Func<Spell, int>)(x => x.Level)).ThenBy<Spell, string>((Func<Spell, string>)(x => x.Name)));
        break;
      case "Alignment":
        elementBaseCollection3.AddRange((IEnumerable<ElementBase>)elementBaseCollection1);
        break;
      default:
        if (!string.IsNullOrWhiteSpace(rule.Attributes.Name) && rule.Attributes.Name.Contains("Ability Score"))
        {
          if (rule.Attributes.Type == "Racial Trait" || rule.Attributes.Type == "Class Feature" || rule.Attributes.Type == "Ability Score Improvement")
          {
            elementBaseCollection3.AddRange((IEnumerable<ElementBase>)elementBaseCollection1);
            break;
          }
          elementBaseCollection3.AddRange((IEnumerable<ElementBase>)elementBaseCollection1.OrderBy<ElementBase, string>((Func<ElementBase, string>)(x => x.Name)));
          break;
        }
        elementBaseCollection3.AddRange((IEnumerable<ElementBase>)elementBaseCollection1.OrderBy<ElementBase, string>((Func<ElementBase, string>)(x => x.Name)));
        break;
    }
    ElementBaseCollection elementBaseCollection4 = elementBaseCollection3;
    List<ElementBase> elementBaseList = new List<ElementBase>();
    if (this.EnableFilter)
      elementBaseList = this.Filter.Filter((IEnumerable<ElementBase>)elementBaseCollection4.ToList<ElementBase>());
    this.SelectionElementsCollection.Clear();
    if (this._baseSupportsCollection.Any<ElementBase>((Func<ElementBase, bool>)(x => x.HasRequirements)))
    {
      ElementBaseCollection elementBaseCollection5 = new ElementBaseCollection();
      List<string> list3 = elements.Select<ElementBase, string>((Func<ElementBase, string>)(e => e.Id)).ToList<string>();
      foreach (ElementBase element in (Collection<ElementBase>)elementBaseCollection4)
      {
        if (element.HasRequirements)
        {
          if (this._interpreter.EvaluateElementRequirementsExpression(element.Requirements, (IEnumerable<string>)list3))
          {
            elementBaseCollection5.Add(element);
            this.SelectionElementsCollection.Add(new SelectionElement(element)
            {
              IsHighlighted = elementBaseList.Contains(element) && this.EnableFilter
            });
          }
          else if (!this.IsInDebugMode)
            ;
        }
        else
        {
          elementBaseCollection5.Add(element);
          this.SelectionElementsCollection.Add(new SelectionElement(element)
          {
            IsHighlighted = elementBaseList.Contains(element) && this.EnableFilter
          });
        }
      }
      elementBaseCollection4 = elementBaseCollection5;
    }
    else
    {
      foreach (ElementBase element in (Collection<ElementBase>)elementBaseCollection4)
        this.SelectionElementsCollection.Add(new SelectionElement(element)
        {
          IsHighlighted = elementBaseList.Contains(element) && this.EnableFilter
        });
    }
    if (this.IsComboBoxExpander)
    {
      this._selectedSelectionElement = this.SelectionElementsCollection.FirstOrDefault<SelectionElement>((Func<SelectionElement, bool>)(x => x.Element == this._selectedElement));
      this.OnPropertyChanged("SelectedSelectionElement");
    }
    if (!this.IsComboBoxExpander)
      this.SelectedElement = (ElementBase)null;
    this.SelectionElements.Clear();
    this.SelectionElements.AddRange((IEnumerable<ElementBase>)elementBaseCollection4);
    if (this._registeredElementId != null)
    {
      ElementBase elementBase = this.SelectionElements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(element => element.Id == this._registeredElementId));
      if (elementBase != null)
      {
        this._selectedElement = elementBase;
        SelectionElement selectionElement = this.SelectionElementsCollection.FirstOrDefault<SelectionElement>((Func<SelectionElement, bool>)(x => x.Element.Id.Equals(this._selectedElement.Id)));
        if (selectionElement != null)
          selectionElement.IsChosen = true;
      }
      else
        this.UnregisterRegisteredElement(true);
    }
    try
    {
      this.IsEnabled = true;
      if (initial && rule.Attributes.ContainsDefaultSelection())
      {
        ElementBase elementBase = this.SelectionElements.SingleOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id == rule.Attributes.Default));
        if (elementBase != null)
        {
          this._selectedElement = elementBase;
          this.RegisterSelection();
          if (this.SelectionElements.Count<ElementBase>() == 1)
            this.IsEnabled = false;
        }
      }
      if (rule.Attributes.ContainsDefaultSelection())
      {
        if (this.SelectionElements.Count<ElementBase>() == 1)
        {
          if (this.RegisteredElement != null)
            this.IsEnabled = false;
        }
      }
    }
    catch (Exception ex)
    {
      Logger.Warning($"{ex.GetType()} while trying to set the [default:{rule.Attributes.Default}] element on {rule} ");
      Logger.Exception(ex, nameof(SetSupportedElements));
    }
    if (!this.IsExpanded && !this.ElementRegistered && rule.Attributes.Type != "Spell")
      this.IsExpanded = true;
    rule.Attributes.ContainsDefaultSelection();
    if (rule.Attributes.Optional && this.SelectionElements.Count == 0)
    {
      this.IsEnabled = false;
      this.IsExpanded = false;
    }
    foreach (SelectionElement selectionElements in (Collection<SelectionElement>)this.SelectionElementsCollection)
    {
      if (selectionElements.Element.HasPrerequisites)
        selectionElements.DisplayShortDescription = selectionElements.Element.Prerequisite;
    }
    this.OnPropertyChanged("SelectedSelectionElement");
  }

  private CharacterManager Manager { get; set; }

  protected bool SupportContainsDynamicExpression(string supports)
  {
    if (string.IsNullOrWhiteSpace(supports))
      return false;
    foreach (string str in DynamicSupportExpressions.All)
    {
      if (supports.Contains(str))
        return true;
    }
    return false;
  }

  protected void GenerateBaseCollections(bool initial, SelectRule rule)
  {
    if (initial)
    {
      if (this.Manager == null)
        this.Manager = CharacterManager.Current;
      if (this._interpreter == null)
        this._interpreter = new ExpressionInterpreter();
      this._interpreter.InitializeWithSelectionRule(rule);
      this._baseCollection = new ElementBaseCollection(DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>)(element => element.Type.Equals(rule.Attributes.Type))));
      this._aquisitionLevel = rule.Attributes.RequiredLevel;
      this._baseSupportsCollection = new ElementBaseCollection();
    }
    if (this.SupportContainsDynamicExpression(rule.Attributes.Supports) || this.RetrainLevel > 0)
    {
      this._baseSupportsCollection.Clear();
      List<string> source = new List<string>();
      string str1 = rule.Attributes.Supports;
      SpellcastingInformation spellcastingInformation1 = this.Manager.GetSpellcastingInformations().FirstOrDefault<SpellcastingInformation>((Func<SpellcastingInformation, bool>)(x => x.Name.Equals(rule.Attributes.SpellcastingName) && !x.IsExtension));
      if (spellcastingInformation1 != null)
        this._information = spellcastingInformation1;
      if (spellcastingInformation1 == null)
        spellcastingInformation1 = this._information != null ? this._information : throw new Exception($"{rule} (in {rule.ElementHeader.Name}) does not have a spellcasting attribute with the name of the spellcasting class (e.g. Wizard or Arcane Trickster), this is required when using the dynamic support expressions");
      if (rule.Attributes.Supports.Contains("$(spellcasting:list)"))
      {
        List<SpellcastingInformation> list = this.Manager.GetSpellcastingInformations().Where<SpellcastingInformation>((Func<SpellcastingInformation, bool>)(x => (x.Name.Equals(rule.Attributes.SpellcastingName) || x.AssignToAllSpellcastingClasses) && x.IsExtension)).ToList<SpellcastingInformation>();
        if (list.Any<SpellcastingInformation>())
        {
          StringBuilder stringBuilder = new StringBuilder();
          stringBuilder.Append($"({spellcastingInformation1.InitialSupportedSpellsExpression.Supports})");
          foreach (SpellcastingInformation spellcastingInformation2 in list)
          {
            foreach (SpellcastingInformation.SpellcastingList spellsExpression in spellcastingInformation2.ExtendedSupportedSpellsExpressions)
            {
              if (spellsExpression.IsId)
              {
                if (spellsExpression.Supports.Contains(","))
                  source.AddRange(((IEnumerable<string>)spellsExpression.Supports.Split(',')).Select<string, string>((Func<string, string>)(x => x.Trim())));
                else
                  source.Add(spellsExpression.Supports);
              }
              else
                stringBuilder.Append($"||({spellsExpression.Supports})");
            }
          }
          str1 = str1.Replace("$(spellcasting:list)", $"({stringBuilder})");
        }
        else
          str1 = str1.Replace("$(spellcasting:list)", spellcastingInformation1.InitialSupportedSpellsExpression.Supports);
      }
      if (rule.Attributes.Supports.Contains("$(spellcasting:slots)") || this.RetrainLevel > 0)
      {
        StatisticValuesGroupCollection valuesAtLevel = this.Manager.StatisticsCalculator.CalculateValuesAtLevel(this.RetrainLevel > 0 ? this.RetrainLevel : this._aquisitionLevel, this.Manager.GetElements());
        List<int> activeSpellSlots = new List<int>();
        for (int slot = 9; slot >= 1; --slot)
        {
          if (valuesAtLevel.ContainsGroup(spellcastingInformation1.GetSlotStatisticName(slot)) || activeSpellSlots.Any<int>())
          {
            if (valuesAtLevel.GetValue(spellcastingInformation1.GetSlotStatisticName(slot)) > 0)
              activeSpellSlots.Add(slot);
            else
              activeSpellSlots.Add(slot);
          }
        }
        if (!activeSpellSlots.Any<int>())
          activeSpellSlots.Add(99);
        if (source.Any<string>())
        {
          foreach (string str2 in source)
          {
            string id = str2;
            ElementBase elementBase = this._baseCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id.Equals(id) && activeSpellSlots.Contains(x.AsElement<Spell>().Level)));
            if (elementBase != null)
              this._baseSupportsCollection.Add(elementBase);
          }
          source.Clear();
        }
        if (!rule.Attributes.Supports.Contains("$(spellcasting:slots)"))
        {
          string str3 = string.Join<int>("||", (IEnumerable<int>)activeSpellSlots);
          for (int index = 1; index <= 9; ++index)
          {
            if (str1.Contains(index.ToString()))
            {
              str1 = str1.Replace(index.ToString(), $"({str3})");
              break;
            }
          }
        }
        str1 = str1.Replace("$(spellcasting:slots)", $"({string.Join<int>("||", (IEnumerable<int>)activeSpellSlots)})");
      }
      else if (!str1.Contains("0") && source.Any<string>())
      {
        int num = -1;
        if (this._baseSupportsCollection.Any<ElementBase>())
          num = this._baseSupportsCollection.Cast<Spell>().Select<Spell, int>((Func<Spell, int>)(x => x.Level)).Max();
        foreach (string str4 in source)
        {
          string id = str4;
          ElementBase elementBase = this._baseCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id.Equals(id)));
          if (elementBase != null && num > 0)
          {
            int level = (elementBase as Spell).Level;
          }
        }
      }
      if (this.SupportContainsDynamicExpression(str1) && Debugger.IsAttached)
        Debugger.Break();
      IEnumerable<ElementBase> supportsExpression = this._interpreter.EvaluateSupportsExpression<ElementBase>(str1, (IEnumerable<ElementBase>)this._baseCollection, rule.Attributes.SupportsElementIdRange());
      if (str1.Contains("0"))
        supportsExpression.Any<ElementBase>((Func<ElementBase, bool>)(x => x.AsElement<Spell>().Level > 0));
      if (this._baseSupportsCollection.Any<ElementBase>())
      {
        foreach (ElementBase elementBase1 in supportsExpression)
        {
          ElementBase elementBase = elementBase1;
          if (this._baseSupportsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id.Equals(elementBase.Id))) == null)
            this._baseSupportsCollection.Add(elementBase);
        }
      }
      else
        this._baseSupportsCollection.AddRange(supportsExpression);
      str1.Contains("0");
      if (source.Any<string>())
      {
        int num1 = 0;
        int num2 = -1;
        if (this._baseSupportsCollection.Any<ElementBase>())
          num2 = this._baseSupportsCollection.Cast<Spell>().Select<Spell, int>((Func<Spell, int>)(x => x.Level)).Max();
        List<string> list = this._baseSupportsCollection.Select<ElementBase, string>((Func<ElementBase, string>)(x => x.Id)).ToList<string>();
        foreach (string str5 in source)
        {
          string id = str5;
          ElementBase elementBase = this._baseCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id.Equals(id)));
          if (elementBase != null && !list.Contains(elementBase.Id))
          {
            if (num2 > 0)
            {
              Spell spell = elementBase as Spell;
              if (spell.Level > num2 || spell.Level == 0)
                continue;
            }
            if (num2 != 0 || (elementBase as Spell).Level <= num2)
            {
              this._baseSupportsCollection.Add(elementBase);
              ++num1;
            }
          }
        }
        Logger.Info($"added {num1} from extended range to {str1} with max level set to {num2}");
        source.Clear();
      }
    }
    else if (rule.Attributes.ContainsSupports())
    {
      if (initial)
        this._baseSupportsCollection = new ElementBaseCollection(this._interpreter.EvaluateSupportsExpression<ElementBase>(rule.Attributes.Supports, (IEnumerable<ElementBase>)this._baseCollection, rule.Attributes.SupportsElementIdRange()));
    }
    else if (initial)
      this._baseSupportsCollection = new ElementBaseCollection((IEnumerable<ElementBase>)this._baseCollection);
    this.ContainsCantrips = this._baseSupportsCollection.Any<ElementBase>((Func<ElementBase, bool>)(x => x.Type.Equals("Spell") && x.Supports.Contains("0")));
  }

  public bool ContainsCantrips { get; set; }

  public ElementFilter Filter { get; set; }

  public bool EnableFilter { get; set; }

  public int GetAquisitionLevel() => this._aquisitionLevel;

  public void ActivateFilter() => this.SetSupportedElements(false, this.SelectionRule);

  private void GetAvailableElements(bool initial)
  {
    SelectRule selectionRule = this.SelectionRule;
  }

  private void PopulateAvailableElements(
    IEnumerable<ElementBase> availableElements,
    bool initial,
    SelectRule rule)
  {
    SelectionElementCollection elementCollection = new SelectionElementCollection();
    foreach (ElementBase availableElement in availableElements)
      elementCollection.Add(new SelectionElement(availableElement));
  }

  public ElementBase GetRegisteredElement() => this.RegisteredElement;

  private void RegisterSelection()
  {
    if (this.SelectedElement == null || this.ElementRegistered && this.RegisteredElementId == this.SelectedElement.Id)
      return;
    if (this.ElementRegistered)
      this.UnregisterRegisteredElement(false);
    this.RegisteredElementId = this.SelectedElement.Id;
    ElementBase element = this.SelectionElements.First<ElementBase>((Func<ElementBase, bool>)(e => e.Id == this.SelectedElement.Id));
    element.Aquisition.SelectedBy(this.SelectionRule);
    this.RegisteredElementId = CharacterManager.Current.RegisterElement(element).Id;
    this.IsExpanded = !this.ElementRegistered;
  }

  private void UnregisterRegisteredElement() => this.UnregisterRegisteredElement(false);

  private void UnregisterRegisteredElement(bool fromReevaluation)
  {
    if (!this.ElementRegistered)
      throw new ArgumentException("unable to unregister when nothing has been registered");
    ElementBase element = DataManager.Current.ElementsCollection.GetElement(this.RegisteredElementId) ?? DataManager.Current.ElementsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id.Equals(this.RegisteredElementId)));
    if (fromReevaluation)
      this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"Your selection ({element.Name}) in {this.Header} was removed due to loss of requirement."));
    this.RegisteredElementId = (string)null;
    CharacterManager.Current.UnregisterElement(element);
    this.IsExpanded = !this.ElementRegistered;
  }

  public async void SetSelectionAndRegister(string id)
  {
    try
    {
      ElementBase elementBase = this.SelectionElements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id == id));
      if (elementBase == null)
      {
        int count;
        for (count = 0; count < 25; ++count)
        {
          await Task.Delay(100);
          elementBase = this.SelectionElements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id == id));
          if (elementBase != null)
            break;
        }
        Logger.Warning($"it took {count * 100}ms to register {id}");
      }
      this.SelectedElement = elementBase != null ? elementBase : throw new InvalidOperationException($"Unable to find the element with id: {id} maybe it has been renamed since the character was saved. ");
      this.RegisterSelection();
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof(SetSelectionAndRegister));
    }
  }

  public void OnHandleEvent(CharacterManagerElementRegistered args)
  {
    if (this.RegisteredElement != null && this.RegisteredElement == args.Element)
      Logger.Debug("not populating the {0} expander after this selection", (object)this.SelectionRule);
    else
      this.SetSupportedElements(false, this.SelectionRule);
  }

  public void OnHandleEvent(CharacterManagerElementUnregistered args)
  {
    if (args.Element.HasSpellcastingInformation)
      Logger.Warning("//here it might fail from getting the proper list");
    this.SetSupportedElements(false, this.SelectionRule);
  }

  public void OnHandleEvent(CharacterManagerSelectionRuleDeleted args)
  {
    if (this.SelectionRule == null)
    {
      Logger.Warning("selection rule empty on '{0}' expander", (object)this.Header);
      if (Debugger.IsAttached)
        Debugger.Break();
      this.Header += " - RULE MISSING";
      this.IsEnabled = false;
    }
    else
    {
      this.IsEnabled = true;
      if (!(args.SelectionRule.UniqueIdentifier == this.SelectionRule.UniqueIdentifier) || !this.ElementRegistered)
        return;
      this.UnregisterRegisteredElement(false);
    }
  }

  public void Repopulate() => this.SetSupportedElements(false, this.SelectionRule);

  protected override void InitializeDesignData()
  {
    base.InitializeDesignData();
    this.IsEnabled = true;
    this.Header = "Language (Optional)";
    this.IsExpanded = true;
    ElementBaseCollection elementBaseCollection = new ElementBaseCollection();
    elementBaseCollection.Add(new ElementBase()
    {
      ElementHeader = new ElementHeader("Common", "Language", "Player’s Handbook", "1")
    });
    elementBaseCollection.Add(new ElementBase()
    {
      ElementHeader = new ElementHeader("Gnomish", "Language", "Dungeon Master's Guide", "2")
    });
    elementBaseCollection.Add(new ElementBase()
    {
      ElementHeader = new ElementHeader("Elvish", "Language", "Player’s Handbook", "3")
    });
    elementBaseCollection.Add(new ElementBase()
    {
      ElementHeader = new ElementHeader("Fireball", "Spell", "Sword Coast Adventurer's Guide", "4")
    });
    elementBaseCollection.Add(new ElementBase()
    {
      ElementHeader = new ElementHeader("Skilled", "Feat", "PHB", "5")
    });
    this._selectionElements = elementBaseCollection;
    this.SelectedElement = this._selectionElements[1];
    this.RegisteredElementId = this._selectedElement.Id;
    foreach (ElementBase selectionElement in (Collection<ElementBase>)this._selectionElements)
      this.SelectionElementsCollection.Add(new SelectionElement(selectionElement));
    this.SelectedSelectionElement = this.SelectionElementsCollection.FirstOrDefault<SelectionElement>();
    this.SelectedSelectionElement.IsEnabled = true;
  }

  private void DepricatedPopulateSelectionElements(bool isInitialPopulating)
  {
    try
    {
      SelectRule selectionRule = this.SelectionRule;
      string supportString = this.SelectionRule.Attributes.Supports;
      this.SelectionRule.ElementHeader.Name.Contains("Expertise");
      List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>)(e => e.Type == this.SelectionRule.Attributes.Type)).OrderBy<ElementBase, string>((Func<ElementBase, string>)(x => x.Source)).ThenBy<ElementBase, string>((Func<ElementBase, string>)(x => x.Name)).ToList<ElementBase>();
      ElementBaseCollection elementBaseCollection1 = new ElementBaseCollection();
      if (this.SelectionRule.Attributes.ContainsSupports())
      {
        if (supportString.Contains<char>(',') && supportString.Contains<char>('|'))
          throw new NotSupportedException();
        if (supportString.Contains<char>(',') && !supportString.Contains<char>('|'))
        {
          elementBaseCollection1.AddRange((IEnumerable<ElementBase>)list);
          foreach (string str1 in ((IEnumerable<string>)supportString.Split(',')).Select<string, string>((Func<string, string>)(s => s.Trim())))
          {
            string str = str1;
            elementBaseCollection1 = new ElementBaseCollection(elementBaseCollection1.Where<ElementBase>((Func<ElementBase, bool>)(e => e.Supports.Contains(str))));
          }
        }
        else if (!supportString.Contains<char>(',') && supportString.Contains<char>('|'))
        {
          string str2 = supportString;
          char[] chArray = new char[1] { '|' };
          foreach (string str3 in str2.Split(chArray))
          {
            string id = str3;
            ElementBase elementBase = list.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(e => e.Id == id.Trim()));
            if (elementBase == null)
              Logger.Warning($"unable to find id:{id.Trim()} for populating {this.SelectionRule}, maybe a typo in the ID");
            else
              elementBaseCollection1.Add(elementBase);
          }
        }
        else
          elementBaseCollection1.AddRange(this.SelectionRule.Attributes.ContainsSupports() ? list.Where<ElementBase>((Func<ElementBase, bool>)(e => e.Supports.Contains(supportString))) : (IEnumerable<ElementBase>)list);
      }
      else
        elementBaseCollection1.AddRange(this.SelectionRule.Attributes.ContainsSupports() ? list.Where<ElementBase>((Func<ElementBase, bool>)(e => e.Supports.Contains(supportString))) : (IEnumerable<ElementBase>)list);
      foreach (ElementBase elementBase in CharacterManager.Current.GetElements().ToList<ElementBase>())
      {
        if (elementBaseCollection1.Contains(elementBase) && !elementBase.AllowDuplicate && elementBase.Id != this._registeredElementId)
          elementBaseCollection1.RemoveElement(elementBase.Id);
      }
      try
      {
        if (this.SelectedElement != null)
        {
          string registeredElementId = this._registeredElementId;
          this.SelectedElement = (ElementBase)null;
        }
        foreach (string id in this.SelectionElements.Select<ElementBase, string>((Func<ElementBase, string>)(x => x.Id)).ToList<string>())
          this.SelectionElements.RemoveElement(id);
        this.SelectionElements.AddRange((IEnumerable<ElementBase>)elementBaseCollection1);
        if (!string.IsNullOrWhiteSpace(this._registeredElementId))
          this._selectedElement = this.SelectionElements.Single<ElementBase>((Func<ElementBase, bool>)(x => x.Id == this._registeredElementId));
      }
      catch (IndexOutOfRangeException ex)
      {
        ElementBaseCollection elementBaseCollection2 = new ElementBaseCollection();
        foreach (ElementBase selectionElement in (Collection<ElementBase>)this.SelectionElements)
        {
          if (!elementBaseCollection1.Contains(selectionElement))
            elementBaseCollection2.Add(selectionElement);
        }
        Logger.Exception((Exception)ex, nameof(DepricatedPopulateSelectionElements));
      }
      if (!this._initialized && !string.IsNullOrWhiteSpace(this.SelectionRule.Attributes.Default))
      {
        ElementBase elementBase = this.SelectionElements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(e => e.Id == this.SelectionRule.Attributes.Default));
        if (elementBase != null)
        {
          try
          {
            this.SelectedElement = elementBase;
            this.RegisterSelection();
          }
          catch (Exception ex)
          {
            Logger.Exception(ex, nameof(DepricatedPopulateSelectionElements));
            MessageDialogService.ShowException(ex);
          }
        }
      }
      if (!string.IsNullOrWhiteSpace(this._registeredElementId) || this.IsExpanded)
        return;
      this.IsExpanded = true;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof(DepricatedPopulateSelectionElements));
      MessageDialogService.ShowException(ex);
    }
  }

  public int RetrainLevel
  {
    get => this._retrainLevel;
    set => this.SetProperty<int>(ref this._retrainLevel, value, nameof(RetrainLevel));
  }

  public bool IsOptional
  {
    get => this._isOptional;
    set => this.SetProperty<bool>(ref this._isOptional, value, nameof(IsOptional));
  }

  public SelectionElementCollection SelectionElementsCollection { get; } = new SelectionElementCollection();

  public SelectionElement SelectedSelectionElement
  {
    get => this._selectedSelectionElement;
    set
    {
      this.SetProperty<SelectionElement>(ref this._selectedSelectionElement, value, nameof(SelectedSelectionElement));
      this.SelectedElement = this._selectedSelectionElement?.Element;
    }
  }

  public void OnHandleEvent(ReprocessCharacterEvent args)
  {
    if (this.SelectionRule.Attributes.Type == "Spell")
      return;
    this.SetSupportedElements(false, this.SelectionRule);
  }
}
