// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.ManageCoinageSliderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.ViewModels.Base;
using System;
using System.ComponentModel;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public class ManageCoinageSliderViewModel : ViewModelBase
{
  private int _currencyChangeInterval;
  private long _copper;
  private long _silver;
  private long _electrum;
  private long _gold;
  private long _platinum;

  public ManageCoinageSliderViewModel()
  {
    this._currencyChangeInterval = 1;
    if (this.IsInDesignMode)
    {
      this.Coinage.Deposit(Coinage.CurrencyCoin.Copper, 124L);
      this.Coinage.Deposit(Coinage.CurrencyCoin.Silver, 3L);
      this.Coinage.Deposit(Coinage.CurrencyCoin.Electrum, 0L);
      this.Coinage.Deposit(Coinage.CurrencyCoin.Gold, 570L);
      this.Coinage.Deposit(Coinage.CurrencyCoin.Platinum);
    }
    else
      this.Coinage.PropertyChanged += new PropertyChangedEventHandler(this.Coinage_PropertyChanged);
  }

  private void Coinage_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    if (!e.PropertyName.Equals("Copper") && !e.PropertyName.Equals("Silver") && !e.PropertyName.Equals("Electrum") && !e.PropertyName.Equals("Gold") && !e.PropertyName.Equals("Platinum"))
      return;
    this.OnPropertyChanged(e.PropertyName);
  }

  public int CurrencyChangeInterval
  {
    get => this._currencyChangeInterval;
    set
    {
      this.SetProperty<int>(ref this._currencyChangeInterval, value, nameof (CurrencyChangeInterval));
    }
  }

  public long Copper
  {
    get => this.Coinage.Copper;
    set
    {
      this.Coinage.Copper = value;
      this.Coinage.UpdateCalculationBaseFromOutside(nameof (Copper));
    }
  }

  public long Silver
  {
    get => this.Coinage.Silver;
    set
    {
      this.Coinage.Silver = value;
      this.Coinage.UpdateCalculationBaseFromOutside(nameof (Silver));
    }
  }

  public long Electrum
  {
    get => this.Coinage.Electrum;
    set
    {
      this.Coinage.Electrum = value;
      this.Coinage.UpdateCalculationBaseFromOutside(nameof (Electrum));
    }
  }

  public long Gold
  {
    get => this.Coinage.Gold;
    set
    {
      this.Coinage.Gold = value;
      this.Coinage.UpdateCalculationBaseFromOutside(nameof (Gold));
    }
  }

  public long Platinum
  {
    get => this.Coinage.Platinum;
    set
    {
      this.Coinage.Platinum = value;
      this.Coinage.UpdateCalculationBaseFromOutside(nameof (Platinum));
    }
  }

  public Coinage Coinage => CharacterManager.Current.Character.Inventory.Coins;

  public ICommand IncreaseCopperCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() =>
      {
        this.Coinage.Deposit(Coinage.CurrencyCoin.Copper, (long) this.CurrencyChangeInterval);
        this.OnPropertyChanged("Copper");
      }));
    }
  }

  public ICommand IncreaseSilverCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Deposit(Coinage.CurrencyCoin.Silver, (long) this.CurrencyChangeInterval)));
    }
  }

  public ICommand IncreaseElectrumCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Deposit(Coinage.CurrencyCoin.Electrum, (long) this.CurrencyChangeInterval)));
    }
  }

  public ICommand IncreaseGoldCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Deposit(Coinage.CurrencyCoin.Gold, (long) this.CurrencyChangeInterval)));
    }
  }

  public ICommand IncreasePlatinumCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Deposit(Coinage.CurrencyCoin.Platinum, (long) this.CurrencyChangeInterval)));
    }
  }

  public ICommand DecreaseCopperCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Withdraw(Coinage.CurrencyCoin.Copper, Math.Min(this.Coinage.Copper, (long) this.CurrencyChangeInterval), false)));
    }
  }

  public ICommand DecreaseSilverCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Withdraw(Coinage.CurrencyCoin.Silver, Math.Min(this.Coinage.Silver, (long) this.CurrencyChangeInterval), false)));
    }
  }

  public ICommand DecreaseElectrumCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Withdraw(Coinage.CurrencyCoin.Electrum, Math.Min(this.Coinage.Electrum, (long) this.CurrencyChangeInterval), false)));
    }
  }

  public ICommand DecreaseGoldCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Withdraw(Coinage.CurrencyCoin.Gold, Math.Min(this.Coinage.Gold, (long) this.CurrencyChangeInterval), false)));
    }
  }

  public ICommand DecreasePlatinumCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.Withdraw(Coinage.CurrencyCoin.Platinum, Math.Min(this.Coinage.Platinum, (long) this.CurrencyChangeInterval), false)));
    }
  }

  public ICommand ConvertCopperCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.ConvertTo(Coinage.CurrencyCoin.Copper)));
    }
  }

  public ICommand ConvertSilverCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.ConvertTo(Coinage.CurrencyCoin.Silver)));
    }
  }

  public ICommand ConvertElectrumCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.ConvertTo(Coinage.CurrencyCoin.Electrum)));
    }
  }

  public ICommand ConvertGoldCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.ConvertTo(Coinage.CurrencyCoin.Gold)));
    }
  }

  public ICommand ConvertPlatinumCommand
  {
    get
    {
      return (ICommand) new RelayCommand((Action) (() => this.Coinage.ConvertTo(Coinage.CurrencyCoin.Platinum)));
    }
  }
}
