// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.ExpressionInterpreter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Data.Strings;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.ViewModels.Shell.Items;
using DynamicExpresso;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

#nullable disable
namespace Builder.Presentation.Services;

public class ExpressionInterpreter
{
  private readonly IExpressionConverter _expressionConverter;
  private const string BracketsExpression = "(\\[([^\\]])+])";
  private const string IdentifierExpression = "ID_([^+])\\w+";
  private readonly Interpreter _interpreter;
  private CharacterManager _manager;
  private SelectRule _currentRule;

  public ExpressionInterpreter()
  {
    this._expressionConverter = (IExpressionConverter) new DynamicExpressionConverter();
    this._manager = CharacterManager.Current;
    this._interpreter = new Interpreter();
    this._interpreter.EnableAssignment(AssignmentOperators.None);
    this._interpreter.SetVariable("manager", (object) this._manager);
    this._interpreter.SetVariable("evaluate", (object) this);
  }

  public bool EvaluateElementRequirementsExpression(
    string expressionString,
    IEnumerable<string> elementsIDs)
  {
    expressionString = this._expressionConverter.SanitizeExpression(expressionString);
    foreach (string oldValue in Regex.Matches(expressionString, "(\\[([^\\]])+])").Cast<Match>().Select<Match, string>((Func<Match, string>) (x => x.Value)))
    {
      string[] source = oldValue.Substring(1, oldValue.Length - 2).Split(':');
      source[0].ToLowerInvariant();
      source[1].ToLowerInvariant();
      string str1 = oldValue.Substring(1, oldValue.Length - 2).Replace(":" + ((IEnumerable<string>) source).Last<string>(), "");
      string str2 = ((IEnumerable<string>) source).Last<string>();
      string lowerInvariant1 = str1.ToLowerInvariant();
      string lowerInvariant2 = str2.ToLowerInvariant();
      expressionString = expressionString.Replace(oldValue, $"evaluate.Require(\"{lowerInvariant1}\", \"{lowerInvariant2}\")");
    }
    foreach (string oldValue in Regex.Matches(expressionString, "ID_([^+])\\w+").Cast<Match>().Select<Match, string>((Func<Match, string>) (x => x.Value)))
      expressionString = expressionString.Replace(oldValue, $"evaluate.Contains(ids, \"{oldValue}\")");
    this._interpreter.SetVariable("ids", (object) elementsIDs);
    return this._interpreter.Eval<bool>(expressionString);
  }

  public bool EvaluateRuleRequirementsExpression(
    string expressionString,
    IEnumerable<string> elementsIDs)
  {
    this._interpreter.SetVariable("ids", (object) elementsIDs);
    expressionString = this._expressionConverter.SanitizeExpression(expressionString);
    expressionString = this._expressionConverter.ConvertRequirementsExpression(expressionString);
    Logger.Debug("evaluating: " + expressionString);
    return this._interpreter.Eval<bool>(expressionString);
  }

  public bool EvaluateEquippedExpression(string expressionString)
  {
    Logger.Debug("generating expression from: " + expressionString);
    expressionString = this._expressionConverter.SanitizeExpression(expressionString);
    if (!expressionString.Contains("[") && !expressionString.Contains("]"))
    {
      Logger.Warning("fix equipped expression: " + expressionString);
      expressionString = $"[{expressionString}]";
    }
    foreach (string oldValue in Regex.Matches(expressionString, "(\\[([^\\]])+])").Cast<Match>().Select<Match, string>((Func<Match, string>) (x => x.Value)))
    {
      string[] strArray = oldValue.Substring(1, oldValue.Length - 2).Split(':');
      string lowerInvariant1 = strArray[0].ToLowerInvariant();
      string lowerInvariant2 = strArray[1].ToLowerInvariant();
      expressionString = expressionString.Replace(oldValue, $"evaluate.Equipped(\"{lowerInvariant1}\", \"{lowerInvariant2}\")");
    }
    Logger.Debug("evaluating: " + expressionString);
    return this._interpreter.Eval<bool>(expressionString);
  }

  public IEnumerable<T> EvaluateSupportsExpression<T>(
    string expressionString,
    IEnumerable<T> elements,
    bool containsElementIDs = false)
  {
    if (string.IsNullOrWhiteSpace(expressionString))
      return elements;
    expressionString = this._expressionConverter.SanitizeExpression(expressionString);
    expressionString = !containsElementIDs ? this._expressionConverter.ConvertSupportsExpression(expressionString) : this._expressionConverter.ConvertSupportsExpression(expressionString, true);
    Expression<Func<T, bool>> asExpression = this._interpreter.ParseAsExpression<Func<T, bool>>(expressionString, "element");
    return (IEnumerable<T>) elements.AsQueryable<T>().Where<T>(asExpression);
  }

  public void InitializeWithSelectionRule(SelectRule rule) => this._currentRule = rule;

  public bool Equipped(string key, string value) => this.RefactoredEquipped(key, value);

  public bool Require(string key, string value)
  {
    if (this._manager == null)
      this._manager = CharacterManager.Current;
    switch (key)
    {
      case "cha":
      case "charisma":
        return this._manager.Character.Abilities.Charisma.FinalScore >= Convert.ToInt32(value);
      case "character":
        return this._manager.Character.Level >= Convert.ToInt32(value);
      case "con":
      case "constitution":
        return this._manager.Character.Abilities.Constitution.FinalScore >= Convert.ToInt32(value);
      case "dex":
      case "dexterity":
        return this._manager.Character.Abilities.Dexterity.FinalScore >= Convert.ToInt32(value);
      case "int":
      case "intelligence":
        return this._manager.Character.Abilities.Intelligence.FinalScore >= Convert.ToInt32(value);
      case "level":
        if (this._currentRule != null)
        {
          ClassProgressionManager progressionManager = this._manager.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.SelectionRules.Contains(this._currentRule)));
          Logger.Info($"comparing requirement [{key}:{value}] against {progressionManager}");
          if (progressionManager != null)
            return progressionManager.ProgressionLevel >= Convert.ToInt32(value);
        }
        return this._manager.Character.Level >= Convert.ToInt32(value);
      case "str":
      case "strength":
        return this._manager.Character.Abilities.Strength.FinalScore >= Convert.ToInt32(value);
      case "type":
        return this._manager.GetElements().Any<ElementBase>((Func<ElementBase, bool>) (x => x.Type.ToLowerInvariant().Equals(value)));
      case "wis":
      case "wisdom":
        return this._manager.Character.Abilities.Wisdom.FinalScore >= Convert.ToInt32(value);
      default:
        foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this._manager.ClassProgressionManagers)
        {
          if (progressionManager.ClassElement != null && key.Equals(progressionManager.ClassElement.Name.ToLowerInvariant()))
            return progressionManager.ProgressionLevel >= Convert.ToInt32(value);
        }
        return this.RequireStatisticsValue(key, value);
    }
  }

  public bool Contains(IEnumerable<string> elementIds, string id)
  {
    return elementIds.Contains<string>(id);
  }

  public bool RefactoredEquipped(string key, string value)
  {
    if (value == null)
      throw new ArgumentNullException(nameof (value));
    CharacterInventory inventory = this._manager.Character.Inventory;
    switch (key)
    {
      case "armor":
        Item obj1 = inventory.EquippedArmor?.Item;
        List<string> list = this.GetGrants().ToList<string>();
        bool flag1 = list.Contains(InternalGrants.CountsAsEquippedLightArmor);
        bool flag2 = list.Contains(InternalGrants.CountsAsEquippedMediumArmor);
        bool flag3 = list.Contains(InternalGrants.CountsAsEquippedHeavyArmor);
        bool flag4 = list.Contains(InternalGrants.CountsAsEquippedArmor) | flag1 | flag2 | flag3;
        if ((string.IsNullOrWhiteSpace(value) || value.Equals("any")) && obj1 != null | flag4 || value.Equals("none") && obj1 == null && !flag4)
          return true;
        if (value.Equals("none") && obj1 != null | flag4)
          return false;
        if (obj1 == null)
          return value.Equals("light") & flag1 || value.Equals("medium") & flag2 || value.Equals("heavy") & flag3;
        if (value.Equals("light") && obj1.ElementSetters.GetSetter("armor").Value.ToLowerInvariant() == "light" || value.Equals("medium") && obj1.ElementSetters.GetSetter("armor").Value.ToLowerInvariant() == "medium" || value.Equals("heavy") && obj1.ElementSetters.GetSetter("armor").Value.ToLowerInvariant() == "heavy" || value.Equals(obj1.Name.ToLowerInvariant()) || value.Equals(obj1.Id.ToLowerInvariant()))
          return true;
        break;
      case "shield":
        Item obj2 = inventory.EquippedSecondary?.Item;
        return obj2 != null && obj2.ElementSetters.ContainsSetter("armor") && obj2.ElementSetters.GetSetter("armor").Value.ToLowerInvariant().Equals("shield") ? string.IsNullOrWhiteSpace(value) || value.Equals("any") || !value.Equals("none") && (value.Equals(obj2.Name.ToLowerInvariant()) || value.Equals(obj2.Id.ToLowerInvariant())) : value.Equals("none");
      case "main":
      case "primary":
        Item obj3 = inventory.EquippedPrimary?.Item;
        if (obj3 != null && (string.IsNullOrWhiteSpace(value) || value.Equals("any")) || value.Equals("none") && obj3 == null)
          return true;
        if (obj3 == null)
          return false;
        if (value.Equals("weapon") && obj3.ElementSetters.GetSetter("category").Value.ToLowerInvariant() == "weapon" || value.Equals("versatile") && inventory.IsEquippedVersatile() || value.Equals(obj3.Name.ToLowerInvariant()) || value.Equals(obj3.Id.ToLowerInvariant()))
          return true;
        break;
      case "offhand":
      case "secondary":
        Item obj4 = inventory.EquippedSecondary?.Item;
        if (obj4 != null && obj4.ElementSetters.ContainsSetter("armor") && obj4.ElementSetters.GetSetter("armor").Value.Equals("shield"))
          return false;
        if (obj4 != null && (string.IsNullOrWhiteSpace(value) || value.Equals("any")) || value.Equals("none") && obj4 == null)
          return true;
        if (obj4 == null)
          return false;
        if (value.Equals("weapon") && obj4.ElementSetters.GetSetter("category").Value.ToLowerInvariant() == "weapon" || value.Equals(obj4.Name.ToLowerInvariant()) || value.Equals(obj4.Id.ToLowerInvariant()))
          return true;
        break;
      case "attunement":
      case "attuned":
        return inventory.Items.Any<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>) (x =>
        {
          if (!x.IsAttuned)
            return false;
          if (x.Item.Id.ToLowerInvariant().Equals(value))
            return true;
          return x.IsAdorned && x.AdornerItem.Id.ToLowerInvariant().Equals(value);
        }));
      case "item":
        return inventory.Items.Any<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>) (x =>
        {
          if (!x.IsEquipped)
            return false;
          if (x.Item.Id.ToLowerInvariant().Equals(value))
            return true;
          return x.IsAdorned && x.AdornerItem.Id.ToLowerInvariant().Equals(value);
        }));
      default:
        Logger.Warning($"Unknown key in Equipped Expression [{key}:{value}]");
        return false;
    }
    Logger.Info($"returning final false on Equipped [{key}:{value}]");
    return false;
  }

  private IEnumerable<string> GetGrants()
  {
    return this._manager.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Grants"))).Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id));
  }

  public bool RequireStatisticsValue(string key, string value)
  {
    if (this._manager == null)
      this._manager = CharacterManager.Current;
    if (this._manager.StatisticsCalculator.StatisticValues.ContainsGroup(key))
    {
      return this._manager.StatisticsCalculator.StatisticValues.GetValue(key) >= Convert.ToInt32(value);
    }
    return false;
  }

  [Obsolete("replace with DynamicExpressionConverter class")]
  private static string SanitizeExpressionString(string expression)
  {
    if (expression.Contains(","))
      expression = expression.Replace(",", "&&");
    if (expression.Contains("&amp;"))
      expression = expression.Replace("&amp;", "&");
    expression = expression.Replace("&", "&&");
    while (expression.Contains("&&&"))
      expression = expression.Replace("&&&", "&&");
    expression = expression.Replace("|", "||");
    while (expression.Contains("|||"))
      expression = expression.Replace("|||", "||");
    if (Debugger.IsAttached)
    {
      expression = expression.Replace("&&", " && ");
      expression = expression.Replace("||", " || ");
    }
    return expression;
  }
}
