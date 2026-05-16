// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.Calculator.StatisticsHandler2
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Data.Rules.Parsers;
using Builder.Data.Strings;
using Builder.Presentation.Models;
using Builder.Presentation.Models.Collections;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

#nullable disable
namespace Builder.Presentation.Services.Calculator;

public class StatisticsHandler2
{
  private ExpressionInterpreter _interpreter;
  private readonly CharacterManager _manager;

  public StatisticsHandler2(CharacterManager manager)
  {
    this._manager = manager;
    this.Names = new AuroraStatisticStrings();
    this.InlineValues = new Dictionary<string, string>();
  }

  public AuroraStatisticStrings Names { get; }

  public Dictionary<string, string> InlineValues { get; }

  public StatisticValuesGroupCollection StatisticValues { get; private set; }

  public StatisticValuesGroupCollection CalculateValuesAtLevel(
    int level,
    ElementBaseCollection elements,
    StatisticValuesGroupCollection seed = null)
  {
    return level <= 0 ? this.CalculateValues(elements, seed) : this.CalculateValues(elements, seed, level, false);
  }

  public StatisticValuesGroupCollection CalculateValues(
    ElementBaseCollection elements,
    StatisticValuesGroupCollection seed = null)
  {
    return this.CalculateValues(elements, seed, -1, true);
  }

  public string ReplaceInline(string input)
  {
    string input1 = input;
    foreach (Match match in Regex.Matches(input1, "\\$\\((.*?)\\)"))
    {
      string oldValue = match.Value;
      string str = StatisticRuleParser.MapLegacyName(match.Value.Substring(2, match.Value.Length - 3));
      string newValue = "==UNKNOWN_VALUE==";
      if (this.StatisticValues.ContainsGroup(str))
        newValue = this.StatisticValues.GetValue(str).ToString();
      else if (this.InlineValues.ContainsKey(str))
        newValue = this.InlineValues[str];
      else
        Logger.Warning("UNKNOWN REPLACE STRING: " + oldValue);
      input1 = input1.Replace(oldValue, newValue);
    }
    if (input1.Contains("{{"))
      input1 = this.ReplaceDoubleBrackets(input1);
    return input1;
  }

  public StatisticValuesGroupCollection CreateSeed(int level, CharacterManager characterManager)
  {
    AuroraStatisticStrings statisticStrings = new AuroraStatisticStrings();
    StatisticValuesGroupCollection seed = new StatisticValuesGroupCollection();
    StatisticValuesGroup statisticValuesGroup1 = new StatisticValuesGroup(statisticStrings.Level);
    statisticValuesGroup1.AddValue("Character", level);
    seed.AddGroup(statisticValuesGroup1);
    seed.AddGroup(this.CreateHalf(statisticValuesGroup1));
    seed.AddGroup(this.CreateHalfUp(statisticValuesGroup1));
    foreach (ClassProgressionManager progressionManager in characterManager.ClassProgressionManagers.Where<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.ClassElement != null)))
    {
      StatisticValuesGroup statisticValuesGroup2 = new StatisticValuesGroup(progressionManager.GetClassLevelStatisticsName());
      statisticValuesGroup2.AddValue(progressionManager.ClassElement.Name, progressionManager.ProgressionLevel);
      seed.AddGroup(statisticValuesGroup2);
      seed.AddGroup(this.CreateHalf(statisticValuesGroup2));
      seed.AddGroup(this.CreateHalfUp(statisticValuesGroup2));
      StatisticValuesGroup group = new StatisticValuesGroup("hp:" + progressionManager.ClassElement.Name.ToLowerInvariant());
      group.AddValue(progressionManager.ClassElement.Name, progressionManager.GetHitPoints());
      seed.AddGroup(group);
      seed.GetGroup(statisticStrings.HitPoints).AddValue(progressionManager.ClassElement.Name, group.Sum());
    }
    if (characterManager.Status.HasCompanion)
    {
      Companion companion = characterManager.Character.Companion;
      if (companion?.Element != null)
      {
        StatisticValuesGroup group = new StatisticValuesGroup("companion:proficiency");
        group.AddValue(companion.Element.Name, companion.Element.Proficiency);
        seed.AddGroup(group);
      }
    }
    foreach (SpellcastingInformation spellcastingInformation in characterManager.GetSpellcastingInformations())
    {
      StatisticValuesGroup group1 = new StatisticValuesGroup(spellcastingInformation.GetSpellcasterSpellAttackStatisticName());
      StatisticValuesGroup group2 = new StatisticValuesGroup(spellcastingInformation.GetSpellcasterSpellAttackStatisticName());
      seed.AddGroup(group1);
      seed.AddGroup(group2);
    }
    seed.GetGroup("attunement:current").AddValue("Internal", characterManager.Character.Inventory.AttunedItemCount);
    return seed;
  }

  public string ReplaceDoubleBrackets(string input)
  {
    string input1 = input;
    foreach (Match match in Regex.Matches(input1, "{{(.*?)}}"))
    {
      string oldValue = match.Value;
      string str = StatisticRuleParser.MapLegacyName(match.Value.Substring(2, match.Value.Length - 4).Trim());
      string newValue = "==UNKNOWN_VALUE==";
      if (this.StatisticValues.ContainsGroup(str))
        newValue = this.StatisticValues.GetValue(str).ToString();
      else if (this.InlineValues.ContainsKey(str))
        newValue = this.InlineValues[str];
      else
        Logger.Warning("UNKNOWN REPLACE STRING: " + oldValue);
      input1 = input1.Replace(oldValue, newValue);
    }
    return input1;
  }

  private StatisticValuesGroupCollection CalculateValues(
    ElementBaseCollection elements,
    StatisticValuesGroupCollection seed = null,
    int atLevel = -1,
    bool setProperties = true)
  {
    if (this._interpreter == null)
      this._interpreter = new ExpressionInterpreter();
    List<StatisticRule> list1 = CharacterManager.Current.GetStatisticRules2().ToList<StatisticRule>();
    if (atLevel > 0)
      list1 = CharacterManager.Current.GetStatisticRulesAtLevel(atLevel).ToList<StatisticRule>();
    foreach (StatisticRule statisticRule in list1.ToList<StatisticRule>())
    {
      if (statisticRule.Attributes.HasEquipmentConditions && !this._interpreter.EvaluateEquippedExpression(statisticRule.Attributes.Equipped))
        list1.Remove(statisticRule);
    }
    foreach (StatisticRule statisticRule in list1.ToList<StatisticRule>())
    {
      if (statisticRule.Attributes.HasRequirementsConditions && !this._interpreter.EvaluateElementRequirementsExpression(statisticRule.Attributes.Requirements, (IEnumerable<string>) elements.Select<ElementBase, string>((Func<ElementBase, string>) (e => e.Id)).ToList<string>()))
        list1.Remove(statisticRule);
    }
    Queue<StatisticRule> source1 = new Queue<StatisticRule>();
    Queue<StatisticRule> source2 = new Queue<StatisticRule>();
    foreach (StatisticRule statisticRule in list1)
    {
      if (statisticRule.Attributes.IsNumberValue() && !statisticRule.Attributes.HasBonus && !statisticRule.Attributes.HasCap)
        source1.Enqueue(statisticRule);
      else
        source2.Enqueue(statisticRule);
    }
    AuroraStatisticStrings strings = new AuroraStatisticStrings();
    StatisticValuesGroupCollection groups1 = seed ?? new StatisticValuesGroupCollection();
    foreach (KeyValuePair<string, int> keyValuePair in new StatisticsCalculatedResult(true).GetValues())
    {
      StatisticValuesGroup group = groups1.GetGroup(keyValuePair.Key);
      if (keyValuePair.Value != 0)
        group.AddValue("Initial", keyValuePair.Value);
    }
    while (source1.Any<StatisticRule>())
    {
      StatisticRule statisticRule = source1.Dequeue();
      groups1.GetGroup(statisticRule.Attributes.Name).AddValue(statisticRule.Attributes.HasAlt ? statisticRule.Attributes.Alt : statisticRule.ElementHeader.Name, statisticRule.Attributes.GetValue());
    }
    if (Debugger.IsAttached)
    {
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.Strength, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.Dexterity, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.Constitution, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.Intelligence, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.Wisdom, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.Charisma, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.StrengthMaximum, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.DexterityMaximum, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.ConstitutionMaximum, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.IntelligenceMaximum, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.WisdomMaximum, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
      if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Equals(strings.CharismaMaximum, StringComparison.OrdinalIgnoreCase))))
        Debugger.Break();
    }
    AbilitiesCollection abilities1 = this._manager.Character.Abilities;
    abilities1.Strength.AdditionalScore = groups1.GetValue(strings.Strength);
    abilities1.Dexterity.AdditionalScore = groups1.GetValue(strings.Dexterity);
    abilities1.Constitution.AdditionalScore = groups1.GetValue(strings.Constitution);
    abilities1.Intelligence.AdditionalScore = groups1.GetValue(strings.Intelligence);
    abilities1.Wisdom.AdditionalScore = groups1.GetValue(strings.Wisdom);
    abilities1.Charisma.AdditionalScore = groups1.GetValue(strings.Charisma);
    AbilitiesCollection abilities2 = this._manager.Character.Companion.Abilities;
    abilities2.Strength.AdditionalScore = groups1.GetValue("companion:strength");
    abilities2.Dexterity.AdditionalScore = groups1.GetValue("companion:dexterity");
    abilities2.Constitution.AdditionalScore = groups1.GetValue("companion:constitution");
    abilities2.Intelligence.AdditionalScore = groups1.GetValue("companion:intelligence");
    abilities2.Wisdom.AdditionalScore = groups1.GetValue("companion:wisdom");
    abilities2.Charisma.AdditionalScore = groups1.GetValue("companion:charisma");
    if (source2.Any<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Contains(":score:set"))))
    {
      List<StatisticRule> list2 = source2.Where<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.Name.Contains(":score:set"))).ToList<StatisticRule>();
      StatisticValuesGroupCollection groups2 = new StatisticValuesGroupCollection();
      this.CalculateGroups((IEnumerable<StatisticRule>) list2, groups2, true);
      abilities1.Strength.OverrideScore = groups2.GetValue(strings.StrengthSet);
      abilities1.Dexterity.OverrideScore = groups2.GetValue(strings.DexteritySet);
      abilities1.Constitution.OverrideScore = groups2.GetValue(strings.ConstitutionSet);
      abilities1.Intelligence.OverrideScore = groups2.GetValue(strings.IntelligenceSet);
      abilities1.Wisdom.OverrideScore = groups2.GetValue(strings.WisdomSet);
      abilities1.Charisma.OverrideScore = groups2.GetValue(strings.CharismaSet);
      abilities2.Strength.OverrideScore = groups2.GetValue("companion:" + strings.StrengthSet);
      abilities2.Dexterity.OverrideScore = groups2.GetValue("companion:" + strings.DexteritySet);
      abilities2.Constitution.OverrideScore = groups2.GetValue("companion:" + strings.ConstitutionSet);
      abilities2.Intelligence.OverrideScore = groups2.GetValue("companion:" + strings.IntelligenceSet);
      abilities2.Wisdom.OverrideScore = groups2.GetValue("companion:" + strings.WisdomSet);
      abilities2.Charisma.OverrideScore = groups2.GetValue("companion:" + strings.CharismaSet);
    }
    else
    {
      abilities1.Strength.OverrideScore = groups1.GetValue(strings.StrengthSet);
      abilities1.Dexterity.OverrideScore = groups1.GetValue(strings.DexteritySet);
      abilities1.Constitution.OverrideScore = groups1.GetValue(strings.ConstitutionSet);
      abilities1.Intelligence.OverrideScore = groups1.GetValue(strings.IntelligenceSet);
      abilities1.Wisdom.OverrideScore = groups1.GetValue(strings.WisdomSet);
      abilities1.Charisma.OverrideScore = groups1.GetValue(strings.CharismaSet);
      abilities2.Strength.OverrideScore = groups1.GetValue("companion:" + strings.StrengthSet);
      abilities2.Dexterity.OverrideScore = groups1.GetValue("companion:" + strings.DexteritySet);
      abilities2.Constitution.OverrideScore = groups1.GetValue("companion:" + strings.ConstitutionSet);
      abilities2.Intelligence.OverrideScore = groups1.GetValue("companion:" + strings.IntelligenceSet);
      abilities2.Wisdom.OverrideScore = groups1.GetValue("companion:" + strings.WisdomSet);
      abilities2.Charisma.OverrideScore = groups1.GetValue("companion:" + strings.CharismaSet);
    }
    if (ApplicationContext.Current.Settings.UseDefaultAbilityScoreMaximum)
    {
      abilities1.Strength.MaximumScore = groups1.GetValue(strings.StrengthMaximum);
      abilities1.Dexterity.MaximumScore = groups1.GetValue(strings.DexterityMaximum);
      abilities1.Constitution.MaximumScore = groups1.GetValue(strings.ConstitutionMaximum);
      abilities1.Intelligence.MaximumScore = groups1.GetValue(strings.IntelligenceMaximum);
      abilities1.Wisdom.MaximumScore = groups1.GetValue(strings.WisdomMaximum);
      abilities1.Charisma.MaximumScore = groups1.GetValue(strings.CharismaMaximum);
    }
    else
    {
      abilities1.Strength.MaximumScore = 100;
      abilities1.Dexterity.MaximumScore = 100;
      abilities1.Constitution.MaximumScore = 100;
      abilities1.Intelligence.MaximumScore = 100;
      abilities1.Wisdom.MaximumScore = 100;
      abilities1.Charisma.MaximumScore = 100;
    }
    groups1.GetGroup(strings.StrengthScore).AddValue("Strength", abilities1.Strength.FinalScore);
    groups1.GetGroup(strings.DexterityScore).AddValue("Dexterity", abilities1.Dexterity.FinalScore);
    groups1.GetGroup(strings.ConstitutionScore).AddValue("Constitution", abilities1.Constitution.FinalScore);
    groups1.GetGroup(strings.IntelligenceScore).AddValue("Intelligence", abilities1.Intelligence.FinalScore);
    groups1.GetGroup(strings.WisdomScore).AddValue("Wisdom", abilities1.Wisdom.FinalScore);
    groups1.GetGroup(strings.CharismaScore).AddValue("Charisma", abilities1.Charisma.FinalScore);
    groups1.GetGroup(strings.StrengthModifier).AddValue("Strength Modifier", abilities1.Strength.Modifier);
    groups1.GetGroup(strings.DexterityModifier).AddValue("Dexterity Modifier", abilities1.Dexterity.Modifier);
    groups1.GetGroup(strings.ConstitutionModifier).AddValue("Constitution Modifier", abilities1.Constitution.Modifier);
    groups1.GetGroup(strings.IntelligenceModifier).AddValue("Intelligence Modifier", abilities1.Intelligence.Modifier);
    groups1.GetGroup(strings.WisdomModifier).AddValue("Wisdom Modifier", abilities1.Wisdom.Modifier);
    groups1.GetGroup(strings.CharismaModifier).AddValue("Charisma Modifier", abilities1.Charisma.Modifier);
    StatisticValuesGroup half1 = this.CreateHalf(groups1.GetGroup(strings.StrengthModifier), "Strength Modifier");
    StatisticValuesGroup half2 = this.CreateHalf(groups1.GetGroup(strings.DexterityModifier), "Dexterity Modifier");
    StatisticValuesGroup half3 = this.CreateHalf(groups1.GetGroup(strings.ConstitutionModifier), "Constitution Modifier");
    StatisticValuesGroup half4 = this.CreateHalf(groups1.GetGroup(strings.IntelligenceModifier), "Intelligence Modifier");
    StatisticValuesGroup half5 = this.CreateHalf(groups1.GetGroup(strings.WisdomModifier), "Wisdom Modifier");
    StatisticValuesGroup half6 = this.CreateHalf(groups1.GetGroup(strings.CharismaModifier), "Charisma Modifier");
    groups1.AddGroup(half1);
    groups1.AddGroup(half2);
    groups1.AddGroup(half3);
    groups1.AddGroup(half4);
    groups1.AddGroup(half5);
    groups1.AddGroup(half6);
    StatisticValuesGroup halfUp1 = this.CreateHalfUp(groups1.GetGroup(strings.StrengthModifier), "Strength Modifier");
    StatisticValuesGroup halfUp2 = this.CreateHalfUp(groups1.GetGroup(strings.DexterityModifier), "Dexterity Modifier");
    StatisticValuesGroup halfUp3 = this.CreateHalfUp(groups1.GetGroup(strings.ConstitutionModifier), "Constitution Modifier");
    StatisticValuesGroup halfUp4 = this.CreateHalfUp(groups1.GetGroup(strings.IntelligenceModifier), "Intelligence Modifier");
    StatisticValuesGroup halfUp5 = this.CreateHalfUp(groups1.GetGroup(strings.WisdomModifier), "Wisdom Modifier");
    StatisticValuesGroup halfUp6 = this.CreateHalfUp(groups1.GetGroup(strings.CharismaModifier), "Charisma Modifier");
    groups1.AddGroup(halfUp1);
    groups1.AddGroup(halfUp2);
    groups1.AddGroup(halfUp3);
    groups1.AddGroup(halfUp4);
    groups1.AddGroup(halfUp5);
    groups1.AddGroup(halfUp6);
    groups1.GetGroup(strings.ProficiencyHalf).AddValue("INTERNAL", groups1.GetValue(strings.Proficiency) / 2);
    groups1.GetGroup(strings.ProficiencyHalfUp).AddValue("INTERNAL", groups1.GetValue(strings.Proficiency) / 2 + groups1.GetValue(strings.Proficiency) % 2);
    groups1.GetGroup("companion:" + strings.StrengthScore).AddValue("Companion Strength", abilities2.Strength.FinalScore);
    groups1.GetGroup("companion:" + strings.DexterityScore).AddValue("Companion Dexterity", abilities2.Dexterity.FinalScore);
    groups1.GetGroup("companion:" + strings.ConstitutionScore).AddValue("Companion Constitution", abilities2.Constitution.FinalScore);
    groups1.GetGroup("companion:" + strings.IntelligenceScore).AddValue("Companion Intelligence", abilities2.Intelligence.FinalScore);
    groups1.GetGroup("companion:" + strings.WisdomScore).AddValue("Companion Wisdom", abilities2.Wisdom.FinalScore);
    groups1.GetGroup("companion:" + strings.CharismaScore).AddValue("Companion Charisma", abilities2.Charisma.FinalScore);
    groups1.GetGroup("companion:" + strings.StrengthModifier).AddValue("Companion Strength Modifier", abilities2.Strength.Modifier);
    groups1.GetGroup("companion:" + strings.DexterityModifier).AddValue("Companion Dexterity Modifier", abilities2.Dexterity.Modifier);
    groups1.GetGroup("companion:" + strings.ConstitutionModifier).AddValue("Companion Constitution Modifier", abilities2.Constitution.Modifier);
    groups1.GetGroup("companion:" + strings.IntelligenceModifier).AddValue("Companion Intelligence Modifier", abilities2.Intelligence.Modifier);
    groups1.GetGroup("companion:" + strings.WisdomModifier).AddValue("Companion Wisdom Modifier", abilities2.Wisdom.Modifier);
    groups1.GetGroup("companion:" + strings.CharismaModifier).AddValue("Companion Charisma Modifier", abilities2.Charisma.Modifier);
    groups1.GetGroup("companion:" + strings.ProficiencyHalf).AddValue("INTERNAL", groups1.GetValue("companion:" + strings.Proficiency) / 2);
    groups1.GetGroup("companion:" + strings.ProficiencyHalfUp).AddValue("INTERNAL", groups1.GetValue("companion:" + strings.Proficiency) / 2 + groups1.GetValue("companion:" + strings.Proficiency) % 2);
    List<StatisticRule> list3 = this.CalculateGroups((IEnumerable<StatisticRule>) source2, groups1).ToList<StatisticRule>();
    if (list3.Any<StatisticRule>())
    {
      foreach (StatisticRule group in this.CalculateGroups((IEnumerable<StatisticRule>) list3, groups1, true))
      {
        Logger.Warning($"invalid rule that was not calculated: {group}");
        try
        {
          if (group.Attributes.HasBonus)
            Logger.Warning($"rule with bonus getting added without bonus type in mind! {group}");
          if (group.Attributes.Value.StartsWith("+") || group.Attributes.Value.StartsWith("-"))
          {
            Logger.Warning($"{group} has a named value with + or - in front, todo: check and fix in proper method, not here!");
            if (Debugger.IsAttached)
              Debugger.Break();
          }
          else
          {
            StatisticValuesGroup statisticValuesGroup = new StatisticValuesGroup(group.Attributes.Name);
            if (groups1.ContainsGroup(group.Attributes.Name))
              statisticValuesGroup = groups1.GetGroup(group.Attributes.Name);
            if (this.HandleSingleRule(group, statisticValuesGroup, groups1))
              groups1.AddGroup(statisticValuesGroup);
            else
              Logger.Warning($"unable to add '{group.Attributes.Name}' from todoRules ({group})");
          }
        }
        catch (Exception ex)
        {
          Logger.Warning($"unable to add '{group.Attributes.Name}' from todoRules ({group})");
          Logger.Exception(ex, nameof (CalculateValues));
        }
      }
    }
    StatisticValuesGroup group1 = groups1.GetGroup("ac");
    group1.Merge(groups1.GetGroup("ac:calculation", false));
    group1.Merge(groups1.GetGroup("ac:shield", false));
    group1.Merge(groups1.GetGroup("ac:misc", false));
    foreach (SpellcastingInformation spellcastingInformation in this._manager.GetSpellcastingInformations())
    {
      StatisticValuesGroup group2 = groups1.GetGroup(spellcastingInformation.GetSpellcasterSpellAttackStatisticName());
      StatisticValuesGroup group3 = groups1.GetGroup(spellcastingInformation.GetSpellcasterSpellSaveStatisticName());
      StatisticValuesGroup group4 = groups1.GetGroup(spellcastingInformation.GetSpellAttackStatisticName(), false);
      group2.Merge(group4);
      group3.Merge(groups1.GetGroup(spellcastingInformation.GetSpellSaveStatisticName(), false));
    }
    foreach (StatisticRule rule in this._manager.GetStatisticRules().Where<StatisticRule>((Func<StatisticRule, bool>) (rule => rule.Attributes.Level <= this._manager.Character.Level && rule.Attributes.Inline)).ToList<StatisticRule>())
      this.SetInlineValue(rule);
    if (setProperties)
      this.StatisticValues = groups1;
    return groups1;
  }

  private IEnumerable<StatisticRule> CalculateGroups(
    IEnumerable<StatisticRule> statisticRules,
    StatisticValuesGroupCollection groups,
    bool forceUnhandledQueue = false)
  {
    List<StatisticRule> groups1 = new List<StatisticRule>();
    Queue<IGrouping<string, StatisticRule>> source1 = new Queue<IGrouping<string, StatisticRule>>(statisticRules.GroupBy<StatisticRule, string>((Func<StatisticRule, string>) (x => x.Attributes.Name)));
    List<string> stringList = new List<string>();
    while (source1.Any<IGrouping<string, StatisticRule>>())
    {
      IGrouping<string, StatisticRule> grouping = source1.Dequeue();
      List<string> list = source1.Select<IGrouping<string, StatisticRule>, string>((Func<IGrouping<string, StatisticRule>, string>) (x => x.Key)).ToList<string>();
      StatisticValuesGroup statisticValuesGroup = new StatisticValuesGroup(grouping.Key);
      if (grouping.Count<StatisticRule>() == 1)
      {
        StatisticRule rule = grouping.First<StatisticRule>();
        if (this.HandleSingleRule(rule, statisticValuesGroup, groups, list))
          groups.AddGroup(statisticValuesGroup);
        else
          groups1.Add(rule);
      }
      else
      {
        Queue<StatisticRule> statisticRuleQueue = new Queue<StatisticRule>();
        foreach (StatisticRule rule in grouping.Where<StatisticRule>((Func<StatisticRule, bool>) (x => !x.Attributes.HasBonus)))
        {
          if (!this.HandleSingleRule(rule, statisticValuesGroup, groups, list))
            statisticRuleQueue.Enqueue(rule);
        }
        foreach (IGrouping<string, StatisticRule> source2 in grouping.Where<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.HasBonus)).GroupBy<StatisticRule, string>((Func<StatisticRule, string>) (x => x.Attributes.Bonus)))
        {
          if (source2.Count<StatisticRule>() == 1)
          {
            StatisticRule rule = source2.First<StatisticRule>();
            if (!this.HandleSingleRule(rule, statisticValuesGroup, groups, list))
              statisticRuleQueue.Enqueue(rule);
          }
          else
          {
            int num = 0;
            string source3 = "";
            foreach (StatisticRule rule in (IEnumerable<StatisticRule>) source2)
            {
              int val1;
              if (!this.TryGetValue(rule, out val1, groups, list))
              {
                statisticRuleQueue.Enqueue(rule);
              }
              else
              {
                if (rule.Attributes.HasCap)
                {
                  int capValue;
                  if (!this.TryGetCappedValue(rule, out capValue, groups, list))
                  {
                    statisticRuleQueue.Enqueue(rule);
                    continue;
                  }
                  val1 = Math.Min(val1, capValue);
                }
                if (val1 > num)
                {
                  num = val1;
                  source3 = rule.Attributes.HasAlt ? rule.Attributes.Alt : rule.ElementHeader.Name;
                }
                else if (val1 == num)
                  source3 = $"{source3} | {(rule.Attributes.HasAlt ? rule.Attributes.Alt : rule.ElementHeader.Name)}";
              }
            }
            statisticValuesGroup.AddValue(source3, num);
          }
        }
        if (!statisticRuleQueue.Any<StatisticRule>())
          groups.AddGroup(statisticValuesGroup);
        else if (stringList.Contains(grouping.Key))
        {
          if (forceUnhandledQueue)
          {
            groups.AddGroup(statisticValuesGroup);
            groups1.AddRange((IEnumerable<StatisticRule>) statisticRuleQueue);
          }
          else
            groups1.AddRange((IEnumerable<StatisticRule>) grouping);
        }
        else
        {
          source1.Enqueue(grouping);
          stringList.Add(grouping.Key);
        }
      }
    }
    return (IEnumerable<StatisticRule>) groups1;
  }

  private bool TryGetValue(
    StatisticRule rule,
    out int value,
    StatisticValuesGroupCollection groups,
    List<string> pendingRules = null)
  {
    if (rule.Attributes.IsNumberValue())
      value = rule.Attributes.GetValue();
    else if (groups.ContainsGroup(rule.Attributes.Value))
    {
      int num = rule.Attributes.Merge ? 1 : 0;
      if (pendingRules != null && this.HasPendingStatistics(rule.Attributes.Value, (IEnumerable<string>) pendingRules))
      {
        value = 0;
        return false;
      }
      value = !rule.Attributes.Value.StartsWith("-") ? groups.GetGroup(rule.Attributes.Value).Sum() : -groups.GetGroup(rule.Attributes.Value).Sum();
    }
    else
    {
      value = 0;
      return false;
    }
    return true;
  }

  private bool TryGetCappedValue(
    StatisticRule rule,
    out int capValue,
    StatisticValuesGroupCollection groups,
    List<string> pendingRules = null)
  {
    if (rule.Attributes.IsNumberCap())
      capValue = rule.Attributes.GetCapValue();
    else if (groups.ContainsGroup(rule.Attributes.Cap))
    {
      if (rule.Attributes.Merge && Debugger.IsAttached)
        Debugger.Break();
      if (pendingRules != null && this.HasPendingStatistics(rule.Attributes.Value, (IEnumerable<string>) pendingRules))
      {
        if (Debugger.IsAttached)
          Debugger.Break();
        Logger.Warning($"TryGetCappedValue: there are still pending rules to be added to '{rule.Attributes.Value}' before this value should be recovered");
      }
      capValue = groups.GetGroup(rule.Attributes.Cap).Sum();
    }
    else
    {
      capValue = 0;
      return false;
    }
    return true;
  }

  private bool HasPendingStatistics(string statisticName, IEnumerable<string> pendingStatisticRules)
  {
    return pendingStatisticRules.Any<string>((Func<string, bool>) (x => x.Equals(statisticName, StringComparison.OrdinalIgnoreCase)));
  }

  private bool HandleSingleRule(
    StatisticRule rule,
    StatisticValuesGroup statGroup,
    StatisticValuesGroupCollection groups,
    List<string> pendingRules = null)
  {
    int num = rule.Attributes.HasAlt ? 1 : 0;
    int val1;
    if (!this.TryGetValue(rule, out val1, groups, pendingRules))
      return false;
    if (rule.Attributes.HasCap)
    {
      int capValue;
      if (!this.TryGetCappedValue(rule, out capValue, groups, pendingRules))
        return false;
      val1 = Math.Min(val1, capValue);
    }
    if (pendingRules != null)
      this.HasPendingStatistics(rule.Attributes.Value, (IEnumerable<string>) pendingRules);
    statGroup.AddValue(rule.Attributes.HasAlt ? rule.Attributes.Alt : rule.ElementHeader.Name, val1);
    return true;
  }

  private StatisticValuesGroup CreateHalf(StatisticValuesGroup normalGroup, string sourceName = "Internal")
  {
    StatisticValuesGroup half = new StatisticValuesGroup(normalGroup.GroupName + ":half");
    half.AddValue(sourceName, normalGroup.Sum() / 2);
    return half;
  }

  private StatisticValuesGroup CreateHalfUp(StatisticValuesGroup normalGroup, string sourceName = "Internal")
  {
    StatisticValuesGroup halfUp = new StatisticValuesGroup(normalGroup.GroupName + ":half:up");
    halfUp.AddValue(sourceName, normalGroup.Sum() / 2 + normalGroup.Sum() % 2);
    return halfUp;
  }

  private void SetInlineValue(StatisticRule rule)
  {
    if (this.InlineValues.ContainsKey(rule.Attributes.Name))
      this.InlineValues[rule.Attributes.Name] = rule.Attributes.Value;
    else
      this.InlineValues.Add(rule.Attributes.Name, rule.Attributes.Value);
  }

  [Obsolete]
  private Queue<IGrouping<string, StatisticRule>> CalculateGroupsPreRefactorHelpers(
    IEnumerable<StatisticRule> statisticRules,
    StatisticValuesGroupCollection groups)
  {
    Queue<IGrouping<string, StatisticRule>> source1 = new Queue<IGrouping<string, StatisticRule>>(statisticRules.GroupBy<StatisticRule, string>((Func<StatisticRule, string>) (x => x.Attributes.Name)));
    Queue<IGrouping<string, StatisticRule>> preRefactorHelpers = new Queue<IGrouping<string, StatisticRule>>();
    List<string> stringList = new List<string>();
    while (source1.Any<IGrouping<string, StatisticRule>>())
    {
      IGrouping<string, StatisticRule> source2 = source1.Dequeue();
      StatisticValuesGroup statisticValuesGroup = new StatisticValuesGroup(source2.Key);
      if (source2.Count<StatisticRule>() == 1)
      {
        StatisticRule rule = source2.First<StatisticRule>();
        this.HandleSingleRule(rule, statisticValuesGroup, groups);
        if (rule.Attributes.IsNumberValue())
        {
          statisticValuesGroup.AddValue(rule.ElementHeader.Name, rule.Attributes.GetValue());
          groups.AddGroup(statisticValuesGroup);
        }
        else if (groups.ContainsGroup(rule.Attributes.Value))
        {
          statisticValuesGroup.AddValue(rule.ElementHeader.Name, groups.GetGroup(rule.Attributes.Value).Sum());
          groups.AddGroup(statisticValuesGroup);
        }
        else
          preRefactorHelpers.Enqueue(source2);
      }
      else
      {
        Queue<StatisticRule> source3 = new Queue<StatisticRule>();
        foreach (StatisticRule statisticRule in source2.Where<StatisticRule>((Func<StatisticRule, bool>) (x => !x.Attributes.HasBonus)))
        {
          if (statisticRule.Attributes.IsNumberValue())
            statisticValuesGroup.AddValue(statisticRule.ElementHeader.Name, statisticRule.Attributes.GetValue());
          else if (groups.ContainsGroup(statisticRule.Attributes.Value))
            statisticValuesGroup.AddValue(statisticRule.ElementHeader.Name, groups.GetGroup(statisticRule.Attributes.Value).Sum());
          else
            source3.Enqueue(statisticRule);
        }
        foreach (IGrouping<string, StatisticRule> source4 in source2.Where<StatisticRule>((Func<StatisticRule, bool>) (x => x.Attributes.HasBonus)).GroupBy<StatisticRule, string>((Func<StatisticRule, string>) (x => x.Attributes.Bonus)))
        {
          if (source4.Count<StatisticRule>() == 1)
          {
            StatisticRule statisticRule = source4.First<StatisticRule>();
            if (statisticRule.Attributes.IsNumberValue())
            {
              int val1 = statisticRule.Attributes.GetValue();
              if (statisticRule.Attributes.HasCap)
              {
                int val2 = statisticRule.Attributes.IsNumberCap() ? statisticRule.Attributes.GetCapValue() : groups.GetGroup(statisticRule.Attributes.Cap).Sum();
                statisticValuesGroup.AddValue(statisticRule.ElementHeader.Name, Math.Min(val1, val2));
              }
              else
                statisticValuesGroup.AddValue(statisticRule.ElementHeader.Name, val1);
            }
            else if (groups.ContainsGroup(statisticRule.Attributes.Value))
            {
              if (statisticRule.Attributes.HasCap)
              {
                int val1 = groups.GetGroup(statisticRule.Attributes.Value).Sum();
                if (statisticRule.Attributes.IsNumberCap())
                {
                  int capValue = statisticRule.Attributes.GetCapValue();
                  statisticValuesGroup.AddValue(statisticRule.ElementHeader.Name, Math.Min(val1, capValue));
                }
                else if (groups.ContainsGroup(statisticRule.Attributes.Cap))
                {
                  int val2 = groups.GetGroup(statisticRule.Attributes.Cap).Sum();
                  statisticValuesGroup.AddValue(statisticRule.ElementHeader.Name, Math.Min(val1, val2));
                }
                else
                  source3.Enqueue(statisticRule);
              }
              else
                statisticValuesGroup.AddValue(statisticRule.ElementHeader.Name, groups.GetGroup(statisticRule.Attributes.Value).Sum());
            }
            else
              source3.Enqueue(statisticRule);
          }
          else
          {
            int num1 = 0;
            StatisticRule statisticRule1 = (StatisticRule) null;
            foreach (StatisticRule statisticRule2 in (IEnumerable<StatisticRule>) source4)
            {
              if (statisticRule2.Attributes.IsNumberValue())
              {
                int val1 = statisticRule2.Attributes.GetValue();
                if (statisticRule2.Attributes.HasCap)
                {
                  int val2 = statisticRule2.Attributes.IsNumberCap() ? statisticRule2.Attributes.GetCapValue() : groups.GetGroup(statisticRule2.Attributes.Cap).Sum();
                  val1 = Math.Min(val1, val2);
                }
                if (val1 > num1)
                {
                  num1 = val1;
                  statisticRule1 = statisticRule2;
                }
              }
              else if (groups.ContainsGroup(statisticRule2.Attributes.Value))
              {
                int val1 = groups.GetGroup(statisticRule2.Attributes.Value).Sum();
                if (statisticRule2.Attributes.HasCap)
                {
                  if (statisticRule2.Attributes.IsNumberCap())
                  {
                    int capValue = statisticRule2.Attributes.GetCapValue();
                    int num2 = Math.Min(val1, capValue);
                    if (num2 > num1)
                    {
                      num1 = num2;
                      statisticRule1 = statisticRule2;
                    }
                  }
                  else if (groups.ContainsGroup(statisticRule2.Attributes.Cap))
                  {
                    int val2 = groups.GetGroup(statisticRule2.Attributes.Cap).Sum();
                    int num3 = Math.Min(val1, val2);
                    if (num3 > num1)
                    {
                      num1 = num3;
                      statisticRule1 = statisticRule2;
                    }
                  }
                  else
                    source3.Enqueue(statisticRule2);
                }
                else if (val1 > num1)
                {
                  num1 = val1;
                  statisticRule1 = statisticRule2;
                }
              }
              else
                source3.Enqueue(statisticRule2);
            }
            if (statisticRule1 != null)
              statisticValuesGroup.AddValue(statisticRule1.ElementHeader.Name, num1);
            else
              Logger.Warning($"bonusRule is null and the value is {num1}");
          }
        }
        if (source3.Any<StatisticRule>())
        {
          if (stringList.Contains(source2.Key))
          {
            preRefactorHelpers.Enqueue(source2);
            Logger.Warning($"adding ({source2.Key}) to the remaining rule groups");
            Logger.Warning($"\tcurrent statgroup: {statisticValuesGroup}");
            Logger.Warning($"\tgroupQueue count: {source3.Count}");
          }
          else
          {
            source1.Enqueue(source2);
            stringList.Add(source2.Key);
          }
        }
        else if (statisticValuesGroup.GetValues().Any<KeyValuePair<string, int>>())
          groups.AddGroup(statisticValuesGroup);
      }
    }
    return preRefactorHelpers;
  }

  [Obsolete]
  public class StatisticNames
  {
    public string StrengthScore => "Strength:Score";

    public string DexterityScore => "Dexterity:Score";

    public string ConstitutionScore => "Constitution:Score";

    public string IntelligenceScore => "Intelligence:Score";

    public string WisdomScore => "Wisdom:Score";

    public string CharismaScore => "Charisma:Score";

    public string Strength => nameof (Strength);

    public string Dexterity => nameof (Dexterity);

    public string Constitution => nameof (Constitution);

    public string Intelligence => nameof (Intelligence);

    public string Wisdom => nameof (Wisdom);

    public string Charisma => nameof (Charisma);

    public string StrengthModifier => this.Strength + " Modifier";

    public string DexterityModifier => this.Dexterity + " Modifier";

    public string ConstitutionModifier => this.Constitution + " Modifier";

    public string IntelligenceModifier => this.Intelligence + " Modifier";

    public string WisdomModifier => this.Wisdom + " Modifier";

    public string CharismaModifier => this.Charisma + " Modifier";

    public string StrengthMaximum => this.Strength + " Maximum";

    public string DexterityMaximum => this.Dexterity + " Maximum";

    public string ConstitutionMaximum => this.Constitution + " Maximum";

    public string IntelligenceMaximum => this.Intelligence + " Maximum";

    public string WisdomMaximum => this.Wisdom + " Maximum";

    public string CharismaMaximum => this.Charisma + " Maximum";

    public string StrengthSavingThrowProficiency => this.Strength + " Saving Throw Proficiency";

    public string DexteritySavingThrowProficiency => this.Dexterity + " Saving Throw Proficiency";

    public string ConstitutionSavingThrowProficiency
    {
      get => this.Constitution + " Saving Throw Proficiency";
    }

    public string IntelligenceSavingThrowProficiency
    {
      get => this.Intelligence + " Saving Throw Proficiency";
    }

    public string WisdomSavingThrowProficiency => this.Wisdom + " Saving Throw Proficiency";

    public string CharismaSavingThrowProficiency => this.Charisma + " Saving Throw Proficiency";

    public string StrengthSavingThrowMisc => this.Strength + " Saving Throw Misc";

    public string DexteritySavingThrowMisc => this.Dexterity + " Saving Throw Misc";

    public string ConstitutionSavingThrowMisc => this.Constitution + " Saving Throw Misc";

    public string IntelligenceSavingThrowMisc => this.Intelligence + " Saving Throw Misc";

    public string WisdomSavingThrowMisc => this.Wisdom + " Saving Throw Misc";

    public string CharismaSavingThrowMisc => this.Charisma + " Saving Throw Misc";

    public string Acrobatics => nameof (Acrobatics);

    public string AnimalHandling => "Animal Handling";

    public string Arcana => nameof (Arcana);

    public string Athletics => nameof (Athletics);

    public string Deception => nameof (Deception);

    public string History => nameof (History);

    public string Insight => nameof (Insight);

    public string Intimidation => nameof (Intimidation);

    public string Investigation => nameof (Investigation);

    public string Medicine => nameof (Medicine);

    public string Nature => nameof (Nature);

    public string Perception => nameof (Perception);

    public string Performance => nameof (Performance);

    public string Persuasion => nameof (Persuasion);

    public string Religion => nameof (Religion);

    public string SleightOfHand => "Sleight of Hand";

    public string Stealth => nameof (Stealth);

    public string Survival => nameof (Survival);

    public string AcrobaticsProficiency => this.Acrobatics + " Proficiency";

    public string AnimalHandlingProficiency => this.AnimalHandling + " Proficiency";

    public string ArcanaProficiency => this.Arcana + " Proficiency";

    public string AthleticsProficiency => this.Athletics + " Proficiency";

    public string DeceptionProficiency => this.Deception + " Proficiency";

    public string HistoryProficiency => this.History + " Proficiency";

    public string InsightProficiency => this.Insight + " Proficiency";

    public string IntimidationProficiency => this.Intimidation + " Proficiency";

    public string InvestigationProficiency => this.Investigation + " Proficiency";

    public string MedicineProficiency => this.Medicine + " Proficiency";

    public string NatureProficiency => this.Nature + " Proficiency";

    public string PerceptionProficiency => this.Perception + " Proficiency";

    public string PerformanceProficiency => this.Performance + " Proficiency";

    public string PersuasionProficiency => this.Persuasion + " Proficiency";

    public string ReligionProficiency => this.Religion + " Proficiency";

    public string SleightOfHandProficiency => this.SleightOfHand + " Proficiency";

    public string StealthProficiency => this.Stealth + " Proficiency";

    public string SurvivalProficiency => this.Survival + " Proficiency";

    public string AcrobaticsMisc => this.Acrobatics + " Misc";

    public string AnimalHandlingMisc => this.AnimalHandling + " Misc";

    public string ArcanaMisc => this.Arcana + " Misc";

    public string AthleticsMisc => this.Athletics + " Misc";

    public string DeceptionMisc => this.Deception + " Misc";

    public string HistoryMisc => this.History + " Misc";

    public string InsightMisc => this.Insight + " Misc";

    public string IntimidationMisc => this.Intimidation + " Misc";

    public string InvestigationMisc => this.Investigation + " Misc";

    public string MedicineMisc => this.Medicine + " Misc";

    public string NatureMisc => this.Nature + " Misc";

    public string PerceptionMisc => this.Perception + " Misc";

    public string PerformanceMisc => this.Performance + " Misc";

    public string PersuasionMisc => this.Persuasion + " Misc";

    public string ReligionMisc => this.Religion + " Misc";

    public string SleightOfHandMisc => this.SleightOfHand + " Misc";

    public string StealthMisc => this.Stealth + " Misc";

    public string SurvivalMisc => this.Survival + " Misc";

    public string AcrobaticsPassive => this.Acrobatics + " Passive";

    public string AnimalHandlingPassive => this.AnimalHandling + " Passive";

    public string ArcanaPassive => this.Arcana + " Passive";

    public string AthleticsPassive => this.Athletics + " Passive";

    public string DeceptionPassive => this.Deception + " Passive";

    public string HistoryPassive => this.History + " Passive";

    public string InsightPassive => this.Insight + " Passive";

    public string IntimidationPassive => this.Intimidation + " Passive";

    public string InvestigationPassive => this.Investigation + " Passive";

    public string MedicinePassive => this.Medicine + " Passive";

    public string NaturePassive => this.Nature + " Passive";

    public string PerceptionPassive => this.Perception + " Passive";

    public string PerformancePassive => this.Performance + " Passive";

    public string PersuasionPassive => this.Persuasion + " Passive";

    public string ReligionPassive => this.Religion + " Passive";

    public string SleightOfHandPassive => this.SleightOfHand + " Passive";

    public string StealthPassive => this.Stealth + " Passive";

    public string SurvivalPassive => this.Survival + " Passive";

    public string HP => nameof (HP);

    public string StartingHP => "Starting HP";

    public string Level => nameof (Level);

    public string Proficiency => nameof (Proficiency);

    public string ArmorClass => "AC";

    public string Speed => nameof (Speed);

    public string Initiative => nameof (Initiative);

    public string ProficiencyHalf => "Proficiency Half";
  }
}
