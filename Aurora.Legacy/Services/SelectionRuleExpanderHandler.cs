// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.SelectionRuleExpanderHandler
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data.Rules;
using Builder.Presentation.Events.Global;
using Builder.Presentation.Extensions;
using Builder.Presentation.Interfaces;
using Builder.Presentation.UserControls;
using Builder.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Services;

public class SelectionRuleExpanderHandler :
  ISelectionRuleExpanderHandler,
  ISubscriber<CharacterManagerSelectionRuleCreated>,
  ISubscriber<CharacterManagerSelectionRuleDeleted>
{
  private object _expanderLock = new object();
  private readonly List<ISupportExpanders> _supports;
  private readonly ObservableCollection<ISelectionRuleExpander> _expanders;

  private SelectionRuleExpanderHandler()
  {
    Logger.Initializing((object) nameof (SelectionRuleExpanderHandler));
    this._supports = new List<ISupportExpanders>();
    this._expanders = new ObservableCollection<ISelectionRuleExpander>();
    ApplicationContext.Current.EventAggregator.Subscribe((object) this);
    SelectionRuleExpanderContext.Current = this;
  }

  public static SelectionRuleExpanderHandler Current { get; } = new SelectionRuleExpanderHandler();

  public void RegisterSupport(ISupportExpanders support)
  {
    if (this._supports.Contains(support))
      throw new ArgumentException("already registered " + support.Name);
    if (support.Expanders == null)
      support.Expanders = new ObservableCollection<ISelectionRuleExpander>();
    foreach (ISelectionRuleExpander selectionRuleExpander in this._expanders.Where<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (expander => support.Listings.Contains<string>(expander.SelectionRule.Attributes.Type))))
      support.Expanders.Add(selectionRuleExpander);
    this._supports.Add(support);
    Logger.Debug("registered {0} with handler", (object) support.Name);
  }

  [Obsolete]
  public void UnregisterSupport(ISupportExpanders support)
  {
    if (this._supports.Contains(support))
      this._supports.Remove(support);
    Logger.Debug("unregistered {0} with handler", (object) support.Name);
  }

  public bool HasExpander(string uniqueIdentifier)
  {
    return this._expanders.Any<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier == uniqueIdentifier));
  }

  public bool HasExpander(string uniqueIdentifier, int number)
  {
    return this._expanders.Any<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier == uniqueIdentifier && x.Number == number));
  }

  public void OnHandleEvent(CharacterManagerSelectionRuleCreated args)
  {
    for (int number = 1; number <= args.SelectionRule.Attributes.Number; ++number)
    {
      ISelectionRuleExpander expander;
      if (args.SelectionRule.Attributes.IsList)
      {
        expander = (ISelectionRuleExpander) new ListItemsSelectionRuleExpander(args.SelectionRule, number);
        foreach (ISupportExpanders supportExpanders in this._supports.Where<ISupportExpanders>((Func<ISupportExpanders, bool>) (s => s.Listings.Contains<string>(args.SelectionRule.ElementHeader.Type))))
        {
          supportExpanders.AddExpander(expander);
          Logger.Debug("expander '{0}' assigned to support '{1}'", (object) expander.SelectionRule, (object) supportExpanders.Name);
        }
      }
      else
      {
        string type = args.SelectionRule.Attributes.Type;
        expander = type == "Deity" || type == "Alignment" || type == "Ability Score Improvement" ? (ISelectionRuleExpander) new SelectionRuleComboBox(args.SelectionRule, number) : (ISelectionRuleExpander) new SelectionRuleExpander(args.SelectionRule, number);
        foreach (ISupportExpanders supportExpanders in this._supports.Where<ISupportExpanders>((Func<ISupportExpanders, bool>) (s => s.Listings.Contains<string>(args.SelectionRule.Attributes.Type))))
        {
          supportExpanders.AddExpander(expander);
          Logger.Info("expander '{0}' assigned to support '{1}'", (object) expander.SelectionRule, (object) supportExpanders.Name);
        }
      }
      this._expanders.Add(expander);
    }
  }

  public void OnHandleEvent(CharacterManagerSelectionRuleDeleted args)
  {
    for (int index = 1; index <= args.SelectionRule.Attributes.Number; ++index)
    {
      ISelectionRuleExpander control = this._expanders.FirstOrDefault<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (ex => ex.UniqueIdentifier == args.SelectionRule.UniqueIdentifier));
      if (control == null)
      {
        Logger.Warning($"expander you want to delete doesn't exist ({args.SelectionRule}) | parent:: {args.SelectionRule.ElementHeader.Id}");
        break;
      }
      if (args.SelectionRule.Attributes.IsList)
      {
        foreach (ISupportExpanders supportExpanders in this._supports.Where<ISupportExpanders>((Func<ISupportExpanders, bool>) (s => s.Listings.Contains<string>(args.SelectionRule.ElementHeader.Type))))
        {
          supportExpanders.Expanders.Remove(control);
          Logger.Info("list expander '{0}' removed from support '{1}'", (object) control.SelectionRule.ElementHeader.Name, (object) supportExpanders.Name);
        }
      }
      else
      {
        foreach (ISupportExpanders supportExpanders in this._supports.Where<ISupportExpanders>((Func<ISupportExpanders, bool>) (s => s.Listings.Contains<string>(args.SelectionRule.Attributes.Type))))
        {
          if (supportExpanders.Expanders.Remove(control))
            Logger.Info("selection rule expander '{0}' removed from support '{1}' ", (object) control.SelectionRule.Attributes.Name, (object) supportExpanders.Name);
          else
            Logger.Warning("!selection rule expander '{0}' WAS NOT removed from support '{1}' ", (object) control.SelectionRule.Attributes.Name, (object) supportExpanders.Name);
        }
      }
      this._expanders.Remove(control);
      Logger.Info("expander '{0}' removed from handler", (object) control.SelectionRule.ElementHeader.Name);
      if (control is SelectionRuleExpander)
        (control as SelectionRuleExpander).GetViewModel<SelectionRuleExpanderViewModel>().IsEnabled = false;
    }
  }

  public bool HasRegisteredElement(SelectRule selectionRule)
  {
    if (!this.HasExpander(selectionRule.UniqueIdentifier))
      return false;
    ISelectionRuleExpander control = this._expanders.Single<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier == selectionRule.UniqueIdentifier));
    if (control.SelectionRule.Attributes.IsList)
      return (control as ListItemsSelectionRuleExpander).GetViewModel<ListItemSelectionRuleExpanderViewModel>().SelectionMade;
    return control is SelectionRuleComboBox ? (control as SelectionRuleComboBox).GetViewModel<SelectionRuleComboBoxViewModel>().ElementRegistered : (control as SelectionRuleExpander).GetViewModel<SelectionRuleExpanderViewModel>().ElementRegistered;
  }

  public int GetExpanderCount(SelectRule selectionRule)
  {
    return this._expanders.Count<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier == selectionRule.UniqueIdentifier));
  }

  public object GetRegisteredElement(SelectRule selectionRule, int number = 1)
  {
    ISelectionRuleExpander control = this._expanders.Single<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier == selectionRule.UniqueIdentifier && x.Number == number));
    if (selectionRule.Attributes.IsList)
      return (object) (control as ListItemsSelectionRuleExpander).GetViewModel<ListItemSelectionRuleExpanderViewModel>().RegisteredItem;
    switch (control)
    {
      case SelectionRuleExpander _:
        return (object) (control as SelectionRuleExpander).GetViewModel<SelectionRuleExpanderViewModel>().RegisteredElement;
      case SelectionRuleComboBox _:
        return (object) (control as SelectionRuleComboBox).GetViewModel<SelectionRuleComboBoxViewModel>().RegisteredElement;
      default:
        throw new ArgumentException($"GetRegisteredElement unknown expander: {control}");
    }
  }

  private IEnumerable<ISelectionRuleExpander> GetExpanders(string uniqueIdentifier)
  {
    return this._expanders.Where<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier == uniqueIdentifier));
  }

  public void SetRegisteredElement(SelectRule selectionRule, string id, int number = 1)
  {
    this._expanders.Single<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier == selectionRule.UniqueIdentifier && x.Number == number)).SetSelection(id);
  }

  [Obsolete]
  private void SetExpander(SelectRule rule, string id)
  {
    ISelectionRuleExpander selectionRuleExpander = this._expanders.Single<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (e => e.UniqueIdentifier == rule.UniqueIdentifier));
    if (selectionRuleExpander == null)
    {
      Logger.Warning("unable to set the expander for the selection rule: {0} ", (object) rule.Attributes.Name);
      throw new ArgumentException("unable to set the expander for the selection rule", rule.Attributes.Name);
    }
    selectionRuleExpander.SetSelection(id);
  }

  public int GetExpandersCount() => this._expanders.Count;

  public void FocusExpander(SelectRule rule, int number = 1)
  {
    ISelectionRuleExpander selectionRuleExpander = this._expanders.FirstOrDefault<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier.Equals(rule.UniqueIdentifier) && x.Number == number));
    if (selectionRuleExpander == null)
      return;
    NavigationLocation location = NavigationLocation.None;
    switch (rule.Attributes.IsList ? rule.ElementHeader.Type : rule.Attributes.Type)
    {
      case "Ability Score Improvement":
        location = NavigationLocation.BuildAbilities;
        break;
      case "Alignment":
      case "Deity":
        location = NavigationLocation.ManageCharacter;
        break;
      case "Archetype":
      case "Archetype Feature":
      case "Class":
      case "Class Feature":
      case "Multiclass":
        location = NavigationLocation.BuildClass;
        break;
      case "Background":
      case "Background Characteristics":
      case "Background Feature":
      case "Background Variant":
        location = NavigationLocation.BuildBackground;
        break;
      case "Companion":
      case "Companion Feature":
        location = NavigationLocation.BuildCompanion;
        break;
      case "Feat":
      case "Feat Feature":
        location = NavigationLocation.BuildFeats;
        break;
      case "Language":
        location = NavigationLocation.BuildLanguages;
        break;
      case "Proficiency":
        location = NavigationLocation.BuildProficiencies;
        break;
      case "Race":
      case "Race Variant":
      case "Racial Trait":
      case "Sub Race":
        location = NavigationLocation.BuildRace;
        break;
      case "Spell":
        location = NavigationLocation.MagicSpells;
        break;
    }
    ApplicationContext.Current.EventAggregator.Send<SelectionRuleNavigationArgs>(new SelectionRuleNavigationArgs(location));
    selectionRuleExpander.FocusExpander();
    Logger.Info($"FocusExpander: {selectionRuleExpander}");
  }

  public void RetrainSpellExpander(SelectRule rule, int number, int retrainLevel)
  {
    ISelectionRuleExpander control = this._expanders.FirstOrDefault<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier.Equals(rule.UniqueIdentifier) && x.Number == number));
    if (control == null)
      return;
    SelectionRuleExpanderViewModel viewModel = (control as SelectionRuleExpander).GetViewModel<SelectionRuleExpanderViewModel>();
    viewModel.RetrainLevel = retrainLevel;
    viewModel.Repopulate();
  }

  public void RemoveAllExpanders()
  {
    foreach (ISupportExpanders support in this._supports)
    {
      if (support.Expanders.Count > 0)
        Logger.Warning($"removing left over expanders from {support}");
      support.Expanders.Clear();
    }
    if (this._expanders.Count > 0)
      Logger.Warning("removing left over expanders from handler");
    this._expanders.Clear();
  }

  public bool RequiresSelection(SelectRule rule, int number = 1)
  {
    if (rule.Attributes.Optional)
      return false;
    foreach (ISelectionRuleExpander expander in this.GetExpanders(rule.UniqueIdentifier))
    {
      if (expander.Number == number && !expander.IsSelectionMade())
        return true;
    }
    return false;
  }

  public int GetRetrainLevel(SelectRule rule, int number)
  {
    try
    {
      ISelectionRuleExpander control = this._expanders.FirstOrDefault<ISelectionRuleExpander>((Func<ISelectionRuleExpander, bool>) (x => x.SelectionRule.UniqueIdentifier.Equals(rule.UniqueIdentifier) && x.Number == number));
      switch (control)
      {
        case null:
          return 0;
        case SelectionRuleExpander _:
          return (control as SelectionRuleExpander).GetViewModel<SelectionRuleExpanderViewModel>().RetrainLevel;
        case ListItemsSelectionRuleExpander _:
          return 0;
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (GetRetrainLevel));
    }
    return 0;
  }
}
