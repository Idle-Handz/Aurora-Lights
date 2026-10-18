// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CompanionStatistics
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using System.Collections.Generic;
using System.Linq;
using Builder.Data.Strings;
using Builder.Presentation.Services.Calculator;

#nullable disable
namespace Builder.Presentation.Models;

public class CompanionStatistics : ObservableObject
{
  private readonly Companion _companion;
  private int _initiative;
  private int _armorClass;
  private int _proficiency;
  private int _maxHp;
  private int _speed;
  private int _speedFly;
  private int _speedClimb;
  private int _speedSwim;
  private int _speedBurrow;
  private int _attackBonus;
  private int _damageBonus;

  public CompanionStatistics(Companion companion) => this._companion = companion;

  public IReadOnlyDictionary<string, int> CalculatedValues { get; private set; } = new Dictionary<string, int>();

  public int Initiative
  {
    get => this._initiative;
    set => this.SetProperty<int>(ref this._initiative, value, nameof (Initiative));
  }

  public int ArmorClass
  {
    get => this._armorClass;
    set => this.SetProperty<int>(ref this._armorClass, value, nameof (ArmorClass));
  }

  public int Proficiency
  {
    get => this._proficiency;
    set => this.SetProperty<int>(ref this._proficiency, value, nameof (Proficiency));
  }

  public int MaxHp
  {
    get => this._maxHp;
    set => this.SetProperty<int>(ref this._maxHp, value, nameof (MaxHp));
  }

  public int Speed
  {
    get => this._speed;
    set => this.SetProperty<int>(ref this._speed, value, nameof (Speed));
  }

  public int SpeedFly
  {
    get => this._speedFly;
    set => this.SetProperty<int>(ref this._speedFly, value, nameof (SpeedFly));
  }

  public int SpeedClimb
  {
    get => this._speedClimb;
    set => this.SetProperty<int>(ref this._speedClimb, value, nameof (SpeedClimb));
  }

  public int SpeedSwim
  {
    get => this._speedSwim;
    set => this.SetProperty<int>(ref this._speedSwim, value, nameof (SpeedSwim));
  }

  public int SpeedBurrow
  {
    get => this._speedBurrow;
    set => this.SetProperty<int>(ref this._speedBurrow, value, nameof (SpeedBurrow));
  }

  public int AttackBonus
  {
    get => this._attackBonus;
    set => this.SetProperty<int>(ref this._attackBonus, value, nameof (AttackBonus));
  }

  public int DamageBonus
  {
    get => this._damageBonus;
    set => this.SetProperty<int>(ref this._damageBonus, value, nameof (DamageBonus));
  }

  public void Reset()
  {
    CalculatedValues = new Dictionary<string, int>();
    this.Initiative = 0;
    this.ArmorClass = 0;
    this.Proficiency = 0;
    this.MaxHp = 0;
    this.Speed = 0;
    this.SpeedFly = 0;
    this.SpeedClimb = 0;
    this.SpeedSwim = 0;
    this.SpeedBurrow = 0;
  }

  public void Update(StatisticValuesGroupCollection statistics)
  {
    CalculatedValues = statistics.ToDictionary(g => g.GroupName, g => g.Sum());
    this.Initiative = this._companion.Abilities.Dexterity.Modifier;
    this.Proficiency = statistics.ContainsGroup("companion:proficiency")
      ? statistics.GetValue("companion:proficiency") : this._companion.Element.Proficiency;
    this.Initiative += statistics.GetValue("companion:initiative");
    this.Initiative += statistics.GetValue("companion:initiative:misc");
    this.ArmorClass = statistics.GetValue("companion:ac");
    this.MaxHp = statistics.GetValue("companion:hp:max");
    this.Speed = statistics.GetValue("companion:speed");
    this.SpeedFly = statistics.GetValue("companion:speed:fly");
    this.SpeedClimb = statistics.GetValue("companion:speed:climb");
    this.SpeedSwim = statistics.GetValue("companion:speed:swim");
    this.SpeedBurrow = statistics.GetValue("companion:speed:burrow");
    AuroraStatisticStrings statisticStrings = new AuroraStatisticStrings();
    this._companion.Skills.Acrobatics.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.AcrobaticsProficiency);
    this._companion.Skills.AnimalHandling.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.AnimalHandlingProficiency);
    this._companion.Skills.Arcana.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.ArcanaProficiency);
    this._companion.Skills.Athletics.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.AthleticsProficiency);
    this._companion.Skills.Deception.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.DeceptionProficiency);
    this._companion.Skills.History.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.HistoryProficiency);
    this._companion.Skills.Insight.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.InsightProficiency);
    this._companion.Skills.Intimidation.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.IntimidationProficiency);
    this._companion.Skills.Investigation.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.InvestigationProficiency);
    this._companion.Skills.Medicine.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.MedicineProficiency);
    this._companion.Skills.Nature.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.NatureProficiency);
    this._companion.Skills.Perception.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.PerceptionProficiency);
    this._companion.Skills.Performance.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.PerformanceProficiency);
    this._companion.Skills.Persuasion.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.PersuasionProficiency);
    this._companion.Skills.Religion.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.ReligionProficiency);
    this._companion.Skills.SleightOfHand.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.SleightOfHandProficiency);
    this._companion.Skills.Stealth.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.StealthProficiency);
    this._companion.Skills.Survival.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.SurvivalProficiency);
    this._companion.Skills.Acrobatics.MiscBonus = statistics.GetValue("companion:" + statisticStrings.AcrobaticsMisc);
    this._companion.Skills.AnimalHandling.MiscBonus = statistics.GetValue("companion:" + statisticStrings.AnimalHandlingMisc);
    this._companion.Skills.Arcana.MiscBonus = statistics.GetValue("companion:" + statisticStrings.ArcanaMisc);
    this._companion.Skills.Athletics.MiscBonus = statistics.GetValue("companion:" + statisticStrings.AthleticsMisc);
    this._companion.Skills.Deception.MiscBonus = statistics.GetValue("companion:" + statisticStrings.DeceptionMisc);
    this._companion.Skills.History.MiscBonus = statistics.GetValue("companion:" + statisticStrings.HistoryMisc);
    this._companion.Skills.Insight.MiscBonus = statistics.GetValue("companion:" + statisticStrings.InsightMisc);
    this._companion.Skills.Intimidation.MiscBonus = statistics.GetValue("companion:" + statisticStrings.IntimidationMisc);
    this._companion.Skills.Investigation.MiscBonus = statistics.GetValue("companion:" + statisticStrings.InvestigationMisc);
    this._companion.Skills.Medicine.MiscBonus = statistics.GetValue("companion:" + statisticStrings.MedicineMisc);
    this._companion.Skills.Nature.MiscBonus = statistics.GetValue("companion:" + statisticStrings.NatureMisc);
    this._companion.Skills.Perception.MiscBonus = statistics.GetValue("companion:" + statisticStrings.PerceptionMisc);
    this._companion.Skills.Performance.MiscBonus = statistics.GetValue("companion:" + statisticStrings.PerformanceMisc);
    this._companion.Skills.Persuasion.MiscBonus = statistics.GetValue("companion:" + statisticStrings.PersuasionMisc);
    this._companion.Skills.Religion.MiscBonus = statistics.GetValue("companion:" + statisticStrings.ReligionMisc);
    this._companion.Skills.SleightOfHand.MiscBonus = statistics.GetValue("companion:" + statisticStrings.SleightOfHandMisc);
    this._companion.Skills.Stealth.MiscBonus = statistics.GetValue("companion:" + statisticStrings.StealthMisc);
    this._companion.Skills.Survival.MiscBonus = statistics.GetValue("companion:" + statisticStrings.SurvivalMisc);
    this._companion.SavingThrows.Strength.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.StrengthSaveProficiency);
    this._companion.SavingThrows.Dexterity.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.DexteritySaveProficiency);
    this._companion.SavingThrows.Constitution.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.ConstitutionSaveProficiency);
    this._companion.SavingThrows.Intelligence.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.IntelligenceSaveProficiency);
    this._companion.SavingThrows.Wisdom.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.WisdomSaveProficiency);
    this._companion.SavingThrows.Charisma.ProficiencyBonus = statistics.GetValue("companion:" + statisticStrings.CharismaSaveProficiency);
    this._companion.SavingThrows.Strength.MiscBonus = statistics.GetValue("companion:" + statisticStrings.StrengthSaveMisc);
    this._companion.SavingThrows.Dexterity.MiscBonus = statistics.GetValue("companion:" + statisticStrings.DexteritySaveMisc);
    this._companion.SavingThrows.Constitution.MiscBonus = statistics.GetValue("companion:" + statisticStrings.ConstitutionSaveMisc);
    this._companion.SavingThrows.Intelligence.MiscBonus = statistics.GetValue("companion:" + statisticStrings.IntelligenceSaveMisc);
    this._companion.SavingThrows.Wisdom.MiscBonus = statistics.GetValue("companion:" + statisticStrings.WisdomSaveMisc);
    this._companion.SavingThrows.Charisma.MiscBonus = statistics.GetValue("companion:" + statisticStrings.CharismaSaveMisc);
    this.AttackBonus = statistics.GetValue("companion:attack");
    this.DamageBonus = statistics.GetValue("companion:damage");
  }
}
