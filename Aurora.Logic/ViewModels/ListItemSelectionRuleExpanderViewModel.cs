// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.ListItemSelectionRuleExpanderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Events.Global;
using Builder.Presentation.Services;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

#nullable disable
namespace Builder.Presentation.ViewModels;

public sealed class ListItemSelectionRuleExpanderViewModel : 
  ViewModelBase,
  ISubscriber<CharacterManagerSelectionRuleDeleted>
{
  private SelectRule _selectionRule;
  private readonly int _number;
  private string _header;
  private bool _isExpanded;
  private bool _selectionMade;
  private SelectionRuleListItem _selectedItem;
  private SelectionRuleListItem _registeredItem;

  public ListItemSelectionRuleExpanderViewModel()
  {
    ApplicationContext.Current.EventAggregator.Subscribe((object) this);
    if (!this.IsInDesignMode)
      return;
    this.InitializeDesignData();
  }

  public ListItemSelectionRuleExpanderViewModel(SelectRule selectionRule, int number)
  {
    ApplicationContext.Current.EventAggregator.Subscribe((object) this);
    this._selectionRule = selectionRule;
    this._number = number;
    this.SelectionItems = new ObservableCollection<SelectionRuleListItem>();
  }

  public ObservableCollection<SelectionRuleListItem> SelectionItems { get; set; }

  public SelectRule SelectionRule
  {
    get => this._selectionRule;
    set => this.SetProperty<SelectRule>(ref this._selectionRule, value, nameof (SelectionRule));
  }

  public string Header
  {
    get => this._header;
    set => this.SetProperty<string>(ref this._header, value, nameof (Header));
  }

  public bool IsExpanded
  {
    get => this._isExpanded;
    set
    {
      if (!this.SetProperty<bool>(ref this._isExpanded, value, nameof (IsExpanded)) || !this._isExpanded)
        return;
      if (this._selectionMade)
        this.SelectedItem = this.RegisteredItem;
      else
        this.SelectedItem = (SelectionRuleListItem) null;
    }
  }

  public SelectionRuleListItem SelectedItem
  {
    get => this._selectedItem;
    set
    {
      this.SetProperty<SelectionRuleListItem>(ref this._selectedItem, value, nameof (SelectedItem));
    }
  }

  public bool SelectionMade
  {
    get => this._selectionMade;
    set => this.SetProperty<bool>(ref this._selectionMade, value, nameof (SelectionMade));
  }

  public SelectionRuleListItem RegisteredItem
  {
    get => this._registeredItem;
    set
    {
      this.SetProperty<SelectionRuleListItem>(ref this._registeredItem, value, nameof (RegisteredItem));
    }
  }

  public RelayCommand SetCommand => new RelayCommand(new Action(this.RegisterSelection));

  public RelayCommand UnsetCommand => new RelayCommand(new Action(this.UnregisteredSelection));

  public override async Task InitializeAsync(InitializationArguments args)
  {
    ListItemSelectionRuleExpanderViewModel expanderViewModel = this;
    try
    {
      expanderViewModel.IsExpanded = true;
      expanderViewModel.SelectedItem = (SelectionRuleListItem) null;
      expanderViewModel.Header = string.IsNullOrWhiteSpace(expanderViewModel.SelectionRule.Attributes.Name) ? expanderViewModel.SelectionRule.ElementHeader.Type.ToUpper() : string.Format("{0}", (object) expanderViewModel.SelectionRule.Attributes.Name, (object) expanderViewModel.SelectionRule.ElementHeader.Name).ToUpper();
      if (expanderViewModel._selectionRule.Attributes.Optional)
        expanderViewModel.Header += " (optional)";
      if (!expanderViewModel.SelectionRule.Attributes.IsList)
        throw new Exception("wrong expander chosen");
      foreach (SelectionRuleListItem listItem in expanderViewModel.SelectionRule.Attributes.ListItems)
        expanderViewModel.SelectionItems.Add(listItem);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (InitializeAsync));
      string title = expanderViewModel.ToString();
      MessageDialogContext.Current?.ShowException(ex, title);
    }
    await base.InitializeAsync(args);
  }

  public string GetKey() => $"{this.SelectionRule.Attributes.Name}:{this._number}";

  private void RegisterSelection()
  {
    if (this.RegisteredItem != null)
      Logger.Info("trying to register with registered element set");
    int id1 = this.SelectedItem.ID;
    int? id2 = this.RegisteredItem?.ID;
    int valueOrDefault = id2.GetValueOrDefault();
    if (id1 == valueOrDefault & id2.HasValue)
      return;
    if (this.SelectionMade)
      this.UnregisteredSelection();
    try
    {
      ElementBase elementBase = CharacterManager.Current.GetElements().First<ElementBase>((Func<ElementBase, bool>) (e => e.Id == this.SelectionRule.ElementHeader.Id));
      if (elementBase.SelectionRuleListItems.ContainsKey(this.GetKey()))
        Logger.Warning("'{0}' SelectionRuleListItems already contains the key '{1}'", (object) elementBase.Name, (object) this.GetKey());
      else
        elementBase.SelectionRuleListItems.Add(this.GetKey(), this.SelectedItem);
      this.RegisteredItem = new SelectionRuleListItem(this.SelectedItem.ID, this.SelectedItem.Text);
      this.SelectionMade = true;
      this.IsExpanded = false;
      this.EventAggregator.Send<ListSelectionRuleRegisteredEvent>(new ListSelectionRuleRegisteredEvent(this.SelectionRule));
      CharacterManager.Current.ReprocessCharacter();
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (RegisterSelection));
      MessageDialogContext.Current?.ShowException(ex);
    }
  }

  private void UnregisteredSelection()
  {
    if (this.RegisteredItem == null)
    {
      Logger.Info("trying to unregister NULL");
    }
    else
    {
      ElementBaseCollection elements = CharacterManager.Current.GetElements();
      if (!elements.Any<ElementBase>() && Debugger.IsAttached)
        Debugger.Break();
      ElementBase elementBase = elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (e => e.Id == this.SelectionRule.ElementHeader.Id));
      if (elementBase == null)
        Logger.Warning("old element removed and not able to unregister selection");
      else if (!elementBase.SelectionRuleListItems.Remove(this.GetKey()))
        Logger.Info("FAILED TO REMOVE " + this.GetKey());
      else
        Logger.Info("REMOVED " + this.GetKey());
      this.RegisteredItem = (SelectionRuleListItem) null;
      this.IsExpanded = true;
      this.SelectionMade = false;
      this.EventAggregator.Send<ListSelectionRuleUnregisteredEvent>(new ListSelectionRuleUnregisteredEvent(this.SelectionRule));
      CharacterManager.Current.ReprocessCharacter();
    }
  }

  protected override void InitializeDesignData()
  {
    base.InitializeDesignData();
    this._header = "Specialty";
    this.SelectionRule = new SelectRule(new ElementHeader("parent name", "parent type", "handbook", "id"));
    this.SelectionRule.Attributes.Type = "List";
    this.SelectionItems = new ObservableCollection<SelectionRuleListItem>();
    this.SelectionRule.Attributes.ListItems = new List<SelectionRuleListItem>();
    this.SelectionRule.Attributes.ListItems.Add(new SelectionRuleListItem(1, "item 1"));
    this.SelectionRule.Attributes.ListItems.Add(new SelectionRuleListItem(2, "item 2"));
    this.SelectionRule.Attributes.ListItems.Add(new SelectionRuleListItem(3, "item 3"));
    this.SelectionRule.Attributes.ListItems.Add(new SelectionRuleListItem(4, "item 4"));
    this.SelectionRule.Attributes.ListItems.Add(new SelectionRuleListItem(5, "item 5"));
    foreach (SelectionRuleListItem listItem in this.SelectionRule.Attributes.ListItems)
      this.SelectionItems.Add(listItem);
    this.SelectedItem = this.SelectionRule.Attributes.ListItems[2];
    this.RegisteredItem = this._selectedItem;
    this.SelectionMade = true;
    this.IsExpanded = true;
  }

  public void OnHandleEvent(CharacterManagerSelectionRuleDeleted args)
  {
    int num = this.SelectionRule.UniqueIdentifier == args.SelectionRule.UniqueIdentifier ? 1 : 0;
  }
}
