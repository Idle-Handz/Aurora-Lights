// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Collections.AbilitiesCollection
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using RelayCommand = Builder.Presentation.Utilities.RelayCommand;

#nullable disable
namespace Builder.Presentation.Models.Collections;

public class AbilitiesCollection : ObservableObject
{
  private const int StartingPoints = 27;
  private int _minimumAbilityBaseScore;
  private int _maximumAbilityBaseScore;
  private readonly List<AbilityItem> _collection;
  private readonly Dictionary<int, int> _pointCost;
  private int _availablePoints;

  public AbilitiesCollection()
  {
    this._minimumAbilityBaseScore = 3;
    this._maximumAbilityBaseScore = 30;
    this._pointCost = new Dictionary<int, int>()
    {
      {
        1,
        0
      },
      {
        2,
        0
      },
      {
        3,
        0
      },
      {
        4,
        0
      },
      {
        5,
        0
      },
      {
        6,
        0
      },
      {
        7,
        0
      },
      {
        8,
        0
      },
      {
        9,
        1
      },
      {
        10,
        2
      },
      {
        11,
        3
      },
      {
        12,
        4
      },
      {
        13,
        5
      },
      {
        14,
        7
      },
      {
        15,
        9
      },
      {
        16 /*0x10*/,
        11
      },
      {
        17,
        13
      },
      {
        18,
        15
      },
      {
        19,
        17
      },
      {
        20,
        19
      },
      {
        21,
        21
      },
      {
        22,
        23
      },
      {
        23,
        25
      },
      {
        24,
        27
      },
      {
        25,
        29
      },
      {
        26,
        31 /*0x1F*/
      },
      {
        27,
        33
      },
      {
        28,
        35
      },
      {
        29,
        37
      },
      {
        30,
        39
      },
      {
        31 /*0x1F*/,
        41
      },
      {
        32 /*0x20*/,
        43
      },
      {
        33,
        45
      },
      {
        34,
        47
      },
      {
        35,
        49
      },
      {
        36,
        51
      },
      {
        37,
        53
      },
      {
        38,
        55
      },
      {
        39,
        57
      },
      {
        40,
        59
      },
      {
        41,
        61
      }
    };
    while (!this._pointCost.ContainsKey(20))
      this._pointCost.Add(this._pointCost.Last<KeyValuePair<int, int>>().Key + 1, this._pointCost.Last<KeyValuePair<int, int>>().Value + 2);
    this._availablePoints = 27;
    this.Strength = new AbilityItem(nameof (Strength), 10);
    this.Dexterity = new AbilityItem(nameof (Dexterity), 10);
    this.Constitution = new AbilityItem(nameof (Constitution), 10);
    this.Intelligence = new AbilityItem(nameof (Intelligence), 10);
    this.Wisdom = new AbilityItem(nameof (Wisdom), 10);
    this.Charisma = new AbilityItem(nameof (Charisma), 10);
    this._collection = new List<AbilityItem>()
    {
      this.Strength,
      this.Dexterity,
      this.Constitution,
      this.Intelligence,
      this.Wisdom,
      this.Charisma
    };
    this.CalculateAvailablePoints();
  }

  public int MinimumAbilityBaseScore
  {
    get => this._minimumAbilityBaseScore;
    set
    {
      this.SetProperty<int>(ref this._minimumAbilityBaseScore, value, nameof (MinimumAbilityBaseScore));
    }
  }

  public int MaximumAbilityBaseScore
  {
    get => this._maximumAbilityBaseScore;
    set
    {
      this.SetProperty<int>(ref this._maximumAbilityBaseScore, value, nameof (MaximumAbilityBaseScore));
    }
  }

  public int AvailablePoints
  {
    get => this._availablePoints;
    set => this.SetProperty<int>(ref this._availablePoints, value, nameof (AvailablePoints));
  }

  public AbilityItem Strength { get; }

  public AbilityItem Dexterity { get; }

  public AbilityItem Constitution { get; }

  public AbilityItem Intelligence { get; }

  public AbilityItem Wisdom { get; }

  public AbilityItem Charisma { get; }

#pragma warning disable CS8632
  private RelayCommand? _increaseAbilityCommand;
#pragma warning restore CS8632
  public RelayCommand IncreaseAbilityCommand =>
      _increaseAbilityCommand ??= new RelayCommand(this.IncreaseAbility, this.CanIncreaseAbility);

#pragma warning disable CS8632
  private RelayCommand? _decreaseAbilityCommand;
#pragma warning restore CS8632
  public RelayCommand DecreaseAbilityCommand =>
      _decreaseAbilityCommand ??= new RelayCommand(this.DecreaseAbility, this.CanDecreaseAbility);

#pragma warning disable CS8632
  private RelayCommand? _resetScoresCommand;
#pragma warning restore CS8632
  public RelayCommand ResetScoresCommand =>
      _resetScoresCommand ??= new RelayCommand(_ => this.ResetScores());

  private bool CanIncreaseAbility(object parameter)
  {
    return parameter != null && ((AbilityItem) parameter).BaseScore < this.MaximumAbilityBaseScore;
  }

  private bool CanDecreaseAbility(object parameter)
  {
    return parameter != null && ((AbilityItem) parameter).BaseScore > this.MinimumAbilityBaseScore;
  }

  private void IncreaseAbility(object parameter)
  {
    ++((AbilityItem) parameter).BaseScore;
    this.CalculateAvailablePoints();
    this.IncreaseAbilityCommand.OnCanExecuteChanged();
  }

  private void DecreaseAbility(object parameter)
  {
    --((AbilityItem) parameter).BaseScore;
    this.CalculateAvailablePoints();
    this.DecreaseAbilityCommand.OnCanExecuteChanged();
  }

  private void ResetScores()
  {
    foreach (AbilityItem abilityItem in this._collection)
      abilityItem.BaseScore = 10;
    this.CalculateAvailablePoints();
    this.IncreaseAbilityCommand.OnCanExecuteChanged();
    this.DecreaseAbilityCommand.OnCanExecuteChanged();
  }

  public int CalculateAvailablePoints()
  {
    if (this.DisablePointsCalculation)
    {
      if (CharacterManager.Current != null && CharacterManager.Current.Status.IsLoaded)
        CharacterManager.Current.ReprocessCharacter();
      return 0;
    }
    try
    {
      int num1 = this._pointCost[this.Strength.BaseScore];
      int num2 = this._pointCost[this.Dexterity.BaseScore];
      int num3 = this._pointCost[this.Constitution.BaseScore];
      int num4 = this._pointCost[this.Intelligence.BaseScore];
      int num5 = this._pointCost[this.Wisdom.BaseScore];
      int num6 = this._pointCost[this.Charisma.BaseScore];
      int num7 = num2;
      this.AvailablePoints = 27 - (num1 + num7 + num3 + num4 + num5 + num6);
      this.IncreaseAbilityCommand.OnCanExecuteChanged();
      this.DecreaseAbilityCommand.OnCanExecuteChanged();
      if (CharacterManager.Current != null && CharacterManager.Current.Status.IsLoaded)
        CharacterManager.Current.ReprocessCharacter();
      return this.AvailablePoints;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (CalculateAvailablePoints));
    }
    finally
    {
      this.IncreaseAbilityCommand.OnCanExecuteChanged();
      this.DecreaseAbilityCommand.OnCanExecuteChanged();
    }
    return this.AvailablePoints;
  }

  public void Reset() => this.ResetScores();

  public IEnumerable<AbilityItem> GetCollection() => (IEnumerable<AbilityItem>) this._collection;

  public bool DisablePointsCalculation { get; set; }
}
