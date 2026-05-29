// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.ProgressionManager
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation.Services.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Services;

public class ProgressionManager
{
  private readonly ExpressionInterpreter _interpreter;

  public ProgressionManager() => this._interpreter = new ExpressionInterpreter();

  public event EventHandler<SelectRule> SelectionRuleCreated;

  public event EventHandler<SelectRule> SelectionRuleRemoved;

  public event EventHandler<SpellcastingInformation> SpellcastingInformationCreated;

  public event EventHandler<SpellcastingInformation> SpellcastingInformationRemoved;

  public ElementBaseCollection Elements { get; } = new ElementBaseCollection();

  public ObservableCollection<SelectRule> SelectionRules { get; } = new ObservableCollection<SelectRule>();

  public ObservableCollection<SpellcastingInformation> SpellcastingInformations { get; } = new ObservableCollection<SpellcastingInformation>();

  public int ProgressionLevel { get; set; }

  public ElementBaseCollection GetElements()
  {
    ElementBaseCollection elements = new ElementBaseCollection();
    foreach (ElementBase element in (Collection<ElementBase>) this.Elements)
      elements.AddRange(ProgressionManager.GetChildElements(element));
    return elements;
  }

  private static IEnumerable<ElementBase> GetChildElements(ElementBase element)
  {
    ElementBaseCollection childElements = new ElementBaseCollection();
    childElements.Add(element);
    foreach (ElementBase ruleElement1 in (Collection<ElementBase>) element.RuleElements)
    {
      childElements.Add(ruleElement1);
      foreach (ElementBase ruleElement2 in (Collection<ElementBase>) ruleElement1.RuleElements)
        childElements.AddRange(ProgressionManager.GetChildElements(ruleElement2));
    }
    return (IEnumerable<ElementBase>) childElements;
  }

  public IEnumerable<StatisticRule> GetStatisticRules(bool applyLevelRequirement = true)
  {
    List<StatisticRule> list = this.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (e => e.ContainsStatisticRules)).SelectMany<ElementBase, StatisticRule>((Func<ElementBase, IEnumerable<StatisticRule>>) (e => e.GetStatisticRules())).Where<StatisticRule>((Func<StatisticRule, bool>) (x => !x.Attributes.Inline)).Select<StatisticRule, StatisticRule>((Func<StatisticRule, StatisticRule>) (x => x.Copy<StatisticRule>())).ToList<StatisticRule>();
    if (applyLevelRequirement)
      list = list.Where<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Level <= this.ProgressionLevel)).ToList<StatisticRule>();
    return (IEnumerable<StatisticRule>) list;
  }

  public IEnumerable<StatisticRule> GetStatisticRulesAtLevel(int level)
  {
    List<StatisticRule> list = this.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (e => e.ContainsStatisticRules)).SelectMany<ElementBase, StatisticRule>((Func<ElementBase, IEnumerable<StatisticRule>>) (e => e.GetStatisticRules())).Where<StatisticRule>((Func<StatisticRule, bool>) (x => !x.Attributes.Inline)).Select<StatisticRule, StatisticRule>((Func<StatisticRule, StatisticRule>) (x => x.Copy<StatisticRule>())).ToList<StatisticRule>();
    int num = level;
    int progressionLevel = this.ProgressionLevel;
    Func<StatisticRule, bool> predicate = (Func<StatisticRule, bool>) (x => x.Attributes.Level <= level);
    return (IEnumerable<StatisticRule>) list.Where<StatisticRule>(predicate).ToList<StatisticRule>();
  }

  public IEnumerable<StatisticRule> GetInlineStatisticRules(bool applyLevelRequirement = true)
  {
    List<StatisticRule> list = this.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (e => e.ContainsStatisticRules)).SelectMany<ElementBase, StatisticRule>((Func<ElementBase, IEnumerable<StatisticRule>>) (e => e.GetStatisticRules())).Where<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Inline)).Select<StatisticRule, StatisticRule>((Func<StatisticRule, StatisticRule>) (x => x.Copy<StatisticRule>())).ToList<StatisticRule>();
    if (applyLevelRequirement)
      list = list.Where<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Level <= this.ProgressionLevel)).ToList<StatisticRule>();
    return (IEnumerable<StatisticRule>) list;
  }

  public void Process(ElementBase element) => this.ProcessElement(element, this.ProgressionLevel);

  public void Clean(ElementBase element) => this.CleanElement(element);

  public void ProcessExistingElements()
  {
    foreach (ElementBase element in (Collection<ElementBase>) this.Elements)
      this.ProcessElement(element, this.ProgressionLevel);
  }

  private void ProcessElement(ElementBase element, int currentLevel)
  {
    foreach (ElementBase ruleElement in (Collection<ElementBase>) element.RuleElements)
      this.ProcessElement(ruleElement, currentLevel);
    this.ProcessSelectionRules(element, currentLevel);
    this.ProcessGrantRules(element, currentLevel);
    if (element.HasSpellcastingInformation && !this.SpellcastingInformations.Any<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => x.UniqueIdentifier.Equals(element.SpellcastingInformation.UniqueIdentifier))))
    {
      this.SpellcastingInformations.Add(element.SpellcastingInformation);
      this.OnSpellcastingSectionCreated(element.SpellcastingInformation);
    }
    foreach (ElementBase ruleElement in (Collection<ElementBase>) element.RuleElements)
      this.ProcessElement(ruleElement, currentLevel);
  }

  private bool ProcessSelectionRules(ElementBase element, int currentLevel)
  {
    bool flag = false;
    foreach (SelectRule selectRule in element.GetSelectRules())
    {
      if (!selectRule.Attributes.MeetsLevelRequirement(currentLevel))
      {
        if (this.SelectionRules.Contains(selectRule))
        {
          this.SelectionRules.Remove(selectRule);
          this.OnSelectionRuleRemoved(selectRule);
        }
      }
      else
      {
        if (!string.IsNullOrWhiteSpace(selectRule.Attributes.Requirements))
        {
          ElementBaseCollection elements = CharacterManager.Current.GetElements();
          if (!this._interpreter.EvaluateRuleRequirementsExpression(selectRule.Attributes.Requirements, elements.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id))))
          {
            if (this.SelectionRules.Contains(selectRule))
            {
              this.SelectionRules.Remove(selectRule);
              this.OnSelectionRuleRemoved(selectRule);
              continue;
            }
            continue;
          }
        }
        if (!this.SelectionRules.Contains(selectRule))
        {
          flag = true;
          this.SelectionRules.Add(selectRule);
          this.OnSelectionRuleCreated(selectRule);
        }
      }
    }
    return flag;
  }

  private bool ProcessGrantRules(ElementBase element, int currentLevel)
  {
    bool flag = false;
    element.Name.Contains("Black Dragon Mask");
    foreach (ElementBase element1 in element.RuleElements.ToList<ElementBase>())
    {
      if (element1.Aquisition.WasGranted)
      {
        GrantRule grantRule = element1.Aquisition.GrantRule;
        if (!grantRule.Attributes.MeetsLevelRequirement(currentLevel))
        {
          this.CleanSelectionRules(element1);
          this.CleanGrantRules(element1);
          Logger.Info("\tungranting: {0} after losing level requirements", (object) element1);
          element.RuleElements.Remove(element1);
        }
        else
        {
          if (grantRule.HasRequirements())
          {
            ElementBaseCollection elements = CharacterManager.Current.GetElements();
            bool requirementsExpression1 = this._interpreter.EvaluateRuleRequirementsExpression(grantRule.Attributes.Requirements, elements.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id)));
            ElementBaseCollection source = elements.WithoutRuleParent(grantRule);
            int num1 = requirementsExpression1 ? 1 : 0;
            bool requirementsExpression2 = this._interpreter.EvaluateRuleRequirementsExpression(grantRule.Attributes.Requirements, source.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id)));
            int num2 = requirementsExpression2 ? 1 : 0;
            if (!requirementsExpression2)
            {
              this.CleanSelectionRules(element1);
              this.CleanGrantRules(element1);
              Logger.Info("\tungranting: {0} after losing requirements", (object) element1);
              element.RuleElements.Remove(element1);
            }
          }
          if (element1.HasRequirements)
          {
            ElementBaseCollection elements = CharacterManager.Current.GetElements();
            if (!this._interpreter.EvaluateElementRequirementsExpression(element1.Requirements, elements.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id))))
            {
              Logger.Warning("\tungranting: {0} after losing element requirements", (object) element1);
              this.CleanElement(element1);
              element.RuleElements.Remove(element1);
            }
          }
        }
      }
    }
    foreach (GrantRule grantRule in element.GetGrantRules())
    {
      GrantRule rule = grantRule;
      if (rule.Attributes.MeetsLevelRequirement(currentLevel))
      {
        if (rule.HasRequirements())
        {
          ElementBaseCollection elements = CharacterManager.Current.GetElements();
          bool requirementsExpression3 = this._interpreter.EvaluateRuleRequirementsExpression(rule.Attributes.Requirements, elements.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id)));
          ElementBaseCollection source = elements.WithoutRuleParent(rule);
          int num3 = requirementsExpression3 ? 1 : 0;
          bool requirementsExpression4 = this._interpreter.EvaluateRuleRequirementsExpression(rule.Attributes.Requirements, source.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id)));
          int num4 = requirementsExpression4 ? 1 : 0;
          if (num3 != num4)
            Logger.Warning($"without parent rule the before/after requirements don't match with {rule} on {rule.ElementHeader}");
          if (!requirementsExpression4)
            continue;
        }
        ElementBase element2 = DataManager.Current.ElementsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(rule.Attributes.Name)));
        if (element2 == null)
        {
          string message = $"Unable to find {rule.Attributes.Name} of type {rule.Attributes.Type} that was set as grant in {element}.";
          Logger.Warning(message);
          MessageDialogContext.Current?.Show(message);
        }
        else
        {
          if (element2.HasRequirements)
          {
            List<string> list = CharacterManager.Current.GetElements().Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id)).ToList<string>();
            if (!this._interpreter.EvaluateElementRequirementsExpression(element2.Requirements, (IEnumerable<string>) list))
            {
              if (!element2.Id.Equals("ID_RACIAL_TRAIT_DWARVEN_TOOL_PROFICIENCY"))
              {
                Logger.Debug("\tnot granting: {0} due to not meeting element requirements", (object) element2);
                continue;
              }
              continue;
            }
          }
          element2.RuleElements.Any<ElementBase>();
          if (element.RuleElements.ContainsRuleElement(element2, rule))
          {
            Logger.Debug("\tgrant exists accoding to new contains rule element check: {0}", (object) element2);
          }
          else
          {
            element.RuleElements.Add(element2);
            element2.Aquisition.GrantedBy(rule);
            flag = true;
            Logger.Debug("\tgranted: {0}", (object) element2);
            this.ProcessElement(element2, currentLevel);
          }
        }
      }
    }
    return flag;
  }

  private void CleanElement(ElementBase element)
  {
    this.CleanSelectionRules(element);
    this.CleanSpellcastingInformation(element);
    this.CleanGrantRules(element);
    element.Aquisition.Clear();
  }

  private void CleanSelectionRules(ElementBase element)
  {
    if (element.ContainsSelectRules)
    {
      if (element.Type.Equals("Item") || element.Type.Equals("Weapon") || element.Type.Equals("Armor") || element.Type.Equals("Magic Item"))
      {
        foreach (SelectRule selectRule in element.GetSelectRules())
        {
          SelectRule rule = selectRule;
          if (!this.SelectionRules.Remove(this.SelectionRules.FirstOrDefault<SelectRule>((Func<SelectRule, bool>) (x => x.UniqueIdentifier == rule.UniqueIdentifier))))
            CharacterManager.Current.SelectionRules.Remove(rule);
          this.OnSelectionRuleRemoved(rule);
        }
      }
      else
      {
        foreach (SelectRule selectRule1 in this.SelectionRules.Where<SelectRule>((Func<SelectRule, bool>) (rule => rule.ElementHeader.Id == element.Id)).ToList<SelectRule>())
        {
          SelectRule rule = selectRule1;
          SelectRule selectRule2 = this.SelectionRules.FirstOrDefault<SelectRule>((Func<SelectRule, bool>) (x => x.UniqueIdentifier == rule.UniqueIdentifier));
          if (rule != selectRule2 && Debugger.IsAttached)
            Debugger.Break();
          if (!this.SelectionRules.Remove(rule))
          {
            if (Debugger.IsAttached)
              Debugger.Break();
            CharacterManager.Current.SelectionRules.Remove(rule);
          }
          this.OnSelectionRuleRemoved(rule);
        }
      }
    }
    if (element.SelectionRuleListItems.Any<KeyValuePair<string, SelectionRuleListItem>>())
      element.SelectionRuleListItems.Clear();
    foreach (ElementBase ruleElement in (Collection<ElementBase>) element.RuleElements)
      this.CleanSelectionRules(ruleElement);
  }

  private void CleanGrantRules(ElementBase element)
  {
    foreach (ElementBase ruleElement1 in (Collection<ElementBase>) element.RuleElements)
    {
      if (ruleElement1.HasSpellcastingInformation)
        this.OnSpellcastingSectionRemoved(ruleElement1.SpellcastingInformation);
      foreach (ElementBase ruleElement2 in (Collection<ElementBase>) ruleElement1.RuleElements)
      {
        this.CleanGrantRules(ruleElement2);
        ruleElement2.Aquisition.Clear();
        Logger.Info("\tungranting: {0}", (object) ruleElement2);
      }
      ruleElement1.RuleElements.Clear();
      Logger.Info("\tungranting: {0}", (object) ruleElement1);
    }
    element.RuleElements.Clear();
  }

  private void CleanSpellcastingInformation(ElementBase element)
  {
    if (element.HasSpellcastingInformation && this.SpellcastingInformations.Any<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => x.UniqueIdentifier.Equals(element.SpellcastingInformation.UniqueIdentifier))) && this.SpellcastingInformations.Remove(element.SpellcastingInformation))
      this.OnSpellcastingSectionRemoved(element.SpellcastingInformation);
    foreach (ElementBase ruleElement in (Collection<ElementBase>) element.RuleElements)
      this.CleanSpellcastingInformation(ruleElement);
  }

  protected virtual void OnSelectionRuleCreated(SelectRule e)
  {
    EventHandler<SelectRule> selectionRuleCreated = this.SelectionRuleCreated;
    if (selectionRuleCreated == null)
      return;
    selectionRuleCreated((object) this, e);
  }

  protected virtual void OnSelectionRuleRemoved(SelectRule e)
  {
    EventHandler<SelectRule> selectionRuleRemoved = this.SelectionRuleRemoved;
    if (selectionRuleRemoved == null)
      return;
    selectionRuleRemoved((object) this, e);
  }

  protected virtual void OnSpellcastingSectionCreated(SpellcastingInformation e)
  {
    EventHandler<SpellcastingInformation> informationCreated = this.SpellcastingInformationCreated;
    if (informationCreated == null)
      return;
    informationCreated((object) this, e);
  }

  protected virtual void OnSpellcastingSectionRemoved(SpellcastingInformation e)
  {
    EventHandler<SpellcastingInformation> informationRemoved = this.SpellcastingInformationRemoved;
    if (informationRemoved == null)
      return;
    informationRemoved((object) this, e);
  }
}
