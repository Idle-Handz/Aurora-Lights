// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Content.SkillsContentViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Models;
using Builder.Presentation.Models.Collections;
using Builder.Presentation.Services.Calculator;
using Builder.Presentation.ViewModels.Base;
using System.Collections.Generic;

#nullable disable
namespace Builder.Presentation.ViewModels.Content;

public class SkillsContentViewModel : 
  ViewModelBase,
  ISubscriber<ReprocessCharacterEvent>,
  ISubscriber<CharacterManagerElementRegistered>,
  ISubscriber<CharacterManagerElementUnregistered>
{
  private int _passivePerception;
  private int _passiveInsight;

  public SkillsContentViewModel()
  {
    if (this.IsInDesignMode)
      this.InitializeDesignData();
    else
      this.EventAggregator.Subscribe((object) this);
  }

  public Builder.Presentation.Models.Character Character => CharacterManager.Current.Character;

  public SkillsCollection Skills => CharacterManager.Current.Character.Skills;

  public IEnumerable<SkillItem> SkillItems => this.Skills.GetCollection();

  public int PassivePerception
  {
    get => this._passivePerception;
    set => this.SetProperty<int>(ref this._passivePerception, value, nameof (PassivePerception));
  }

  public int PassiveInsight
  {
    get => this._passiveInsight;
    set => this.SetProperty<int>(ref this._passiveInsight, value, nameof (PassiveInsight));
  }

  protected override void InitializeDesignData()
  {
    base.InitializeDesignData();
    this.Skills.Acrobatics.ProficiencyBonus = 2;
    this.Skills.History.ProficiencyBonus = 2;
    this.Skills.Arcana.ProficiencyBonus = 2;
    this.Skills.Perception.ProficiencyBonus = 2;
    this.Skills.Perception.MiscBonus = 2;
    this.PassivePerception = this.Skills.Perception.FinalBonus + 10;
    this.PassiveInsight = 10;
  }

  public virtual void OnHandleEvent(ReprocessCharacterEvent args)
  {
    StatisticValuesGroupCollection statisticValues = CharacterManager.Current.StatisticsCalculator.StatisticValues;
    if (statisticValues == null)
      return;
    StatisticValuesGroup group1 = statisticValues.GetGroup("perception:passive", false);
    StatisticValuesGroup group2 = statisticValues.GetGroup("insight:passive", false);
    this.PassivePerception = group1.Sum() + this.Skills.Perception.FinalBonus;
    this.PassiveInsight = group2.Sum() + this.Skills.Insight.FinalBonus;
  }

  public virtual void OnHandleEvent(CharacterManagerElementRegistered args)
  {
    StatisticValuesGroupCollection statisticValues = CharacterManager.Current.StatisticsCalculator.StatisticValues;
    if (statisticValues == null)
      return;
    StatisticValuesGroup group1 = statisticValues.GetGroup("perception:passive", false);
    StatisticValuesGroup group2 = statisticValues.GetGroup("insight:passive", false);
    this.PassivePerception = group1.Sum() + this.Skills.Perception.FinalBonus;
    this.PassiveInsight = group2.Sum() + this.Skills.Insight.FinalBonus;
  }

  public virtual void OnHandleEvent(CharacterManagerElementUnregistered args)
  {
    StatisticValuesGroupCollection statisticValues = CharacterManager.Current.StatisticsCalculator.StatisticValues;
    if (statisticValues == null)
      return;
    StatisticValuesGroup group1 = statisticValues.GetGroup("perception:passive", false);
    StatisticValuesGroup group2 = statisticValues.GetGroup("insight:passive", false);
    this.PassivePerception = group1.Sum() + this.Skills.Perception.FinalBonus;
    this.PassiveInsight = group2.Sum() + this.Skills.Insight.FinalBonus;
  }
}
