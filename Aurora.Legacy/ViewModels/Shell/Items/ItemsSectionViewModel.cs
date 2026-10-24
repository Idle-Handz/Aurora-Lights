// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Shell.Items.ItemsSectionViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.ViewModels.Base;

#nullable disable
namespace Builder.Presentation.ViewModels.Shell.Items;

public sealed class ItemsSectionViewModel : ViewModelBase
{
  public Character Character => CharacterManager.Current.Character;

  public CharacterInventory Inventory => this.Character.Inventory;

  public ItemsSectionViewModel()
  {
    if (this.IsInDesignMode)
      this.InitializeDesignData();
    else
      this.SubscribeWithEventAggregator();
  }

  public RefactoredEquipmentSectionViewModel RefactoredEquipmentSectionViewModel { get; } = new RefactoredEquipmentSectionViewModel();

  protected override void InitializeDesignData() => this.Inventory.Coins.Set(13L, 49L, 11L, 8L, 4L);
}
