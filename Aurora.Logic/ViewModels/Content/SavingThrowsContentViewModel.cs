// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Content.SavingThrowsContentViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models;
using Builder.Presentation.Models.Collections;
using Builder.Presentation.ViewModels.Base;
using System.Collections.Generic;

#nullable disable
namespace Builder.Presentation.ViewModels.Content;

public sealed class SavingThrowsContentViewModel : ViewModelBase
{
  public SavingThrowsContentViewModel()
  {
    if (this.IsInDesignMode)
      this.InitializeDesignData();
    else
      this.EventAggregator.Subscribe((object) this);
  }

  public SavingThrowCollection SavingThrows => CharacterManager.Current.Character.SavingThrows;

  public IEnumerable<SavingThrowItem> SavingThrowItems => this.SavingThrows.GetCollection();

  protected override void InitializeDesignData()
  {
    base.InitializeDesignData();
    this.SavingThrows.Dexterity.MiscBonus = 2;
    this.SavingThrows.Dexterity.ProficiencyBonus = 2;
    this.SavingThrows.Charisma.ProficiencyBonus = 2;
  }
}
