// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Equipment.CharacterInventory
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Strings;
using Builder.Presentation.Models.Helpers;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Models.Equipment;

public class CharacterInventory : ObservableObject
{
  private readonly ExpressionInterpreter _expressionInterpreter;
  private RefactoredEquipmentItem _equippedArmor;
  private RefactoredEquipmentItem _equippedPrimary;
  private RefactoredEquipmentItem _equippedSecondary;
  private Decimal _equipmentWeight;
  private int _attunedItemCount;
  private int _maxAttunedItemCount;
  private string _equipment;
  private string _treasure;
  private string _questItems;
  private int _weightCalculationDepth;
  private bool _weightCalculationPending;

  public CharacterInventory()
  {
    this._expressionInterpreter = new ExpressionInterpreter();
    this._maxAttunedItemCount = 3;
    this.Coins = new Coinage();
    this.Items = new ObservableCollection<RefactoredEquipmentItem>();
    this.Items.CollectionChanged += new NotifyCollectionChangedEventHandler(this.Items_CollectionChanged);
    this.StoredItems1 = new InventoryStorage();
    this.StoredItems2 = new InventoryStorage();
  }

  public Character Character => CharacterManager.Current.Character;

  public Coinage Coins { get; }

  public ObservableCollection<RefactoredEquipmentItem> Items { get; }

  public RefactoredEquipmentItem EquippedArmor
  {
    get => this._equippedArmor;
    set
    {
      this.SetProperty<RefactoredEquipmentItem>(ref this._equippedArmor, value, nameof (EquippedArmor));
      this.CalculateAttunedItemCount();
    }
  }

  public RefactoredEquipmentItem EquippedPrimary
  {
    get => this._equippedPrimary;
    set
    {
      this.SetProperty<RefactoredEquipmentItem>(ref this._equippedPrimary, value, nameof (EquippedPrimary));
      this.OnPropertyChanged("IsEquippedTwoHanded");
      this.OnPropertyChanged("IsEquippedVersatile");
      this.CalculateAttunedItemCount();
    }
  }

  public RefactoredEquipmentItem EquippedSecondary
  {
    get => this._equippedSecondary;
    set
    {
      this.SetProperty<RefactoredEquipmentItem>(ref this._equippedSecondary, value, nameof (EquippedSecondary));
      this.OnPropertyChanged("IsEquippedTwoHanded");
      this.OnPropertyChanged("IsEquippedVersatile");
      this.OnPropertyChanged("IsEquippedShield");
      this.CalculateAttunedItemCount();
    }
  }

  public Decimal EquipmentWeight
  {
    get => this._equipmentWeight;
    set => this.SetProperty<Decimal>(ref this._equipmentWeight, value, nameof (EquipmentWeight));
  }

  public int AttunedItemCount
  {
    get => this._attunedItemCount;
    set => this.SetProperty<int>(ref this._attunedItemCount, value, nameof (AttunedItemCount));
  }

  public int MaxAttunedItemCount
  {
    get => this._maxAttunedItemCount;
    set
    {
      this.SetProperty<int>(ref this._maxAttunedItemCount, value, nameof (MaxAttunedItemCount));
    }
  }

  public string Equipment
  {
    get => this._equipment;
    set => this.SetProperty<string>(ref this._equipment, value, nameof (Equipment));
  }

  public string Treasure
  {
    get => this._treasure;
    set => this.SetProperty<string>(ref this._treasure, value, nameof (Treasure));
  }

  public string QuestItems
  {
    get => this._questItems;
    set => this.SetProperty<string>(ref this._questItems, value, nameof (QuestItems));
  }

  public void CalculateAttunedItemCount()
  {
    this.AttunedItemCount = this.Items.Count<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>) (equipment => equipment.IsAttunable && equipment.IsAttuned));
  }

  public Decimal CalculateWeight()
  {
    if (this._weightCalculationDepth > 0)
    {
      this._weightCalculationPending = true;
      return this.EquipmentWeight;
    }
    return this.CalculateWeight((IEnumerable<RefactoredEquipmentItem>) this.Items);
  }

  /// <summary>Defers aggregate weight updates until the outermost bulk edit completes.</summary>
  public IDisposable DeferWeightCalculation()
  {
    ++this._weightCalculationDepth;
    return new WeightCalculationScope(this);
  }

  private sealed class WeightCalculationScope : IDisposable
  {
    private CharacterInventory _inventory;

    public WeightCalculationScope(CharacterInventory inventory) => this._inventory = inventory;

    public void Dispose()
    {
      CharacterInventory inventory = this._inventory;
      if (inventory == null)
        return;
      this._inventory = null;
      if (--inventory._weightCalculationDepth == 0 && inventory._weightCalculationPending)
      {
        inventory._weightCalculationPending = false;
        inventory.CalculateWeight();
      }
    }
  }

  public Decimal CalculateWeight(
    IEnumerable<RefactoredEquipmentItem> equipmentItems)
  {
    Decimal num = 0.0M;
    foreach (RefactoredEquipmentItem equipmentItem in equipmentItems)
    {
      if (!equipmentItem.IsStored)
        num += equipmentItem.GetWeight();
      else
        Logger.Warning($"not includeing stored item: {equipmentItem}");
    }
    Decimal weight = num + this.CalculateCoinsWeight();
    this.EquipmentWeight = weight;
    return weight;
  }

  public Decimal CalculateCoinsWeight()
  {
    return (Decimal) (0L + this.Coins.Copper + this.Coins.Silver + this.Coins.Electrum + this.Coins.Gold + this.Coins.Platinum) / 50M;
  }

  public bool IsEquippedPrimary(EquipmentItem equipment)
  {
    return this.EquippedPrimary != null && this.EquippedPrimary.Identifier.Equals(equipment.Identifier);
  }

  public bool IsEquippedSecondary(EquipmentItem equipment)
  {
    return this.EquippedSecondary != null && this.EquippedSecondary.Identifier.Equals(equipment.Identifier);
  }

  public bool IsEquippedTwoHanded()
  {
    return this.EquippedPrimary != null && this.EquippedSecondary != null && !this.EquippedPrimary.Item.HasVersatile && this.EquippedPrimary.Identifier.Equals(this.EquippedSecondary.Identifier);
  }

  public bool IsEquippedVersatile()
  {
    return this.EquippedPrimary != null && this.EquippedSecondary != null && this.EquippedPrimary.Item.HasVersatile && this.EquippedPrimary.Identifier.Equals(this.EquippedSecondary.Identifier);
  }

  public bool IsEquippedShield()
  {
    if (this.EquippedSecondary == null)
      return false;
    if (this.EquippedSecondary.Item.Supports.Contains(InternalElementID.ArmorGroup.Shield))
      return true;
    ElementSetters.Setter setter;
    if (this.EquippedSecondary.Item.AttemptGetSetterValue("armor", out setter))
      return setter.Value.ToLower().Equals("shield");
    return this.EquippedSecondary.Item.ElementSetters.ContainsSetter("armor") && this.EquippedSecondary.Item.ElementSetters.GetSetter("armor").Value.Equals("shield", StringComparison.OrdinalIgnoreCase);
  }

  public bool AllowMoreAttunement()
  {
    return this.AttunedItemCount < this.Character.Inventory.MaxAttunedItemCount;
  }

  public void ClearInventory()
  {
    using var weightUpdate = this.DeferWeightCalculation();
    while (this.Items.Any<RefactoredEquipmentItem>())
    {
      this.Items.FirstOrDefault<RefactoredEquipmentItem>()?.Retrieve();
      this.Items.FirstOrDefault<RefactoredEquipmentItem>()?.Deactivate();
      this.Items.RemoveAt(0);
    }
    this.Coins.Clear();
    this.Equipment = string.Empty;
    this.Treasure = string.Empty;
    this.QuestItems = string.Empty;
    this.EquipmentWeight = 0M;
    this.AttunedItemCount = 0;
    this.MaxAttunedItemCount = 3;
    this.EquippedArmor = (RefactoredEquipmentItem) null;
    this.EquippedPrimary = (RefactoredEquipmentItem) null;
    this.EquippedSecondary = (RefactoredEquipmentItem) null;
    this.StoredItems1.Name = "#1";
    this.StoredItems1.StoredItems.Clear();
    this.StoredItems2.Name = "#2";
    this.StoredItems2.StoredItems.Clear();
  }

  private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
  {
    this.CalculateWeight();
  }

  public void EquipArmor(RefactoredEquipmentItem equipment, bool? attune = null)
  {
    this.EquippedArmor = equipment;
    this.EquippedArmor.EquippedLocation = "Armor";
    this.EquippedArmor.Activate(true, attune ?? equipment.IsAttunable);
    this.CalculateAttunedItemCount();
    ApplicationContext.Current.EventAggregator.Send<ReprocessCharacterEvent>(new ReprocessCharacterEvent());
  }

  public void EquipPrimary(RefactoredEquipmentItem equipment, bool twohanded = false, bool? attune = null)
  {
    this.EquippedPrimary = equipment;
    this.EquippedPrimary.EquippedLocation = "Primary Hand";
    if (twohanded)
    {
      this.EquippedSecondary = equipment;
      this.EquippedSecondary.EquippedLocation = "Two-Handed";
      if (!equipment.IsTwoHandTarget() && equipment.Item.HasVersatile)
        this.EquippedSecondary.EquippedLocation = "Two-Handed (Versatile)";
    }
    this.EquippedPrimary.Activate(true, attune ?? equipment.IsAttunable);
    this.CalculateAttunedItemCount();
    this.PromtAddToAttacksIfEnabled(equipment);
    ApplicationContext.Current.EventAggregator.Send<ReprocessCharacterEvent>(new ReprocessCharacterEvent());
  }

  public void EquipSecondary(RefactoredEquipmentItem equipment, bool? attune = null)
  {
    this.EquippedSecondary = equipment;
    this.EquippedSecondary.EquippedLocation = "Secondary Hand";
    this.EquippedSecondary.Activate(true, attune ?? equipment.IsAttunable);
    this.CalculateAttunedItemCount();
    if (!this.IsEquippedShield())
      this.PromtAddToAttacksIfEnabled(equipment);
    ApplicationContext.Current.EventAggregator.Send<ReprocessCharacterEvent>(new ReprocessCharacterEvent());
  }

  public void UnequipArmor()
  {
    if (this.EquippedArmor == null)
      return;
    RefactoredEquipmentItem equippedArmor = this.EquippedArmor;
    this.EquippedArmor = (RefactoredEquipmentItem) null;
    equippedArmor.Deactivate();
    this.CalculateAttunedItemCount();
  }

  public void UnequipPrimary()
  {
    if (this.EquippedPrimary == null)
      return;
    RefactoredEquipmentItem equippedPrimary = this.EquippedPrimary;
    if (this.IsEquippedVersatile())
      this.EquippedSecondary = (RefactoredEquipmentItem) null;
    else if (this.IsEquippedTwoHanded())
      this.EquippedSecondary = (RefactoredEquipmentItem) null;
    this.EquippedPrimary = (RefactoredEquipmentItem) null;
    equippedPrimary.Deactivate();
    this.CalculateAttunedItemCount();
  }

  public void UnequipSecondary()
  {
    if (this.EquippedSecondary == null)
      return;
    RefactoredEquipmentItem equippedSecondary = this.EquippedSecondary;
    if (this.IsEquippedVersatile())
      this.EquippedSecondary = (RefactoredEquipmentItem) null;
    else if (this.IsEquippedTwoHanded())
    {
      this.EquippedSecondary = (RefactoredEquipmentItem) null;
      this.UnequipPrimary();
    }
    else
    {
      this.EquippedSecondary = (RefactoredEquipmentItem) null;
      equippedSecondary.Deactivate();
    }
    this.CalculateAttunedItemCount();
  }

  public bool AddFromStatistics(ElementHeader parent, string id, int amount = 1)
  {
    if (!(DataManager.Current.ElementsCollection.GetElement(id) is Item element) || this.Items.Any<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>) (x =>
    {
      if (!x.HasAquisitionParent || !x.AquisitionParent.Name.Equals(parent.Name))
        return false;
      if (x.Item.Id.Equals(id))
        return true;
      return x.IsAdorned && x.AdornerItem.Id.Equals(id);
    })))
      return false;
    RefactoredEquipmentItem refactoredEquipmentItem;
    if (element.Type.Equals("Magic Item"))
    {
      switch (element.ItemType)
      {
        case "Weapon":
          ArmorElement original1 = this.GetSupportedArmorElements(element.ElementSetters.GetSetter("weapon").Value).FirstOrDefault<ArmorElement>();
          refactoredEquipmentItem = original1 != null ? new RefactoredEquipmentItem((Item) original1.Copy<ArmorElement>(), element.Copy<Item>()) : new RefactoredEquipmentItem(element.Copy<Item>());
          break;
        case "Armor":
          ArmorElement original2 = this.GetSupportedArmorElements(element.ElementSetters.GetSetter("armor").Value).FirstOrDefault<ArmorElement>();
          refactoredEquipmentItem = original2 != null ? new RefactoredEquipmentItem((Item) original2.Copy<ArmorElement>(), element.Copy<Item>()) : new RefactoredEquipmentItem(element.Copy<Item>());
          break;
        default:
          refactoredEquipmentItem = new RefactoredEquipmentItem(element.Copy<Item>());
          break;
      }
    }
    else
    {
      if (!element.Type.Equals("Item") && !element.Type.Equals("Weapon") && !element.Type.Equals("Armor"))
        return false;
      refactoredEquipmentItem = new RefactoredEquipmentItem(element.Copy<Item>());
    }
    if (element.IsStackable)
      refactoredEquipmentItem.Amount = amount;
    refactoredEquipmentItem.Notes = $"This item added by '{parent.Name}'.";
    refactoredEquipmentItem.AquisitionParent = parent;
    refactoredEquipmentItem.HasAquisitionParent = refactoredEquipmentItem.AquisitionParent != null;
    this.Items.Add(refactoredEquipmentItem);
    return true;
  }

  public IEnumerable<WeaponElement> GetSupportedWeaponElements(string supportExpression)
  {
    IEnumerable<WeaponElement> elements = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Weapon")).Cast<WeaponElement>();
    return (IEnumerable<WeaponElement>) this._expressionInterpreter.EvaluateSupportsExpression<WeaponElement>(supportExpression, elements).ToList<WeaponElement>();
  }

  public IEnumerable<ArmorElement> GetSupportedArmorElements(string supportExpression)
  {
    IEnumerable<ArmorElement> elements = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Armor")).Cast<ArmorElement>();
    return (IEnumerable<ArmorElement>) this._expressionInterpreter.EvaluateSupportsExpression<ArmorElement>(supportExpression, elements).ToList<ArmorElement>();
  }

  private void PromtAddToAttacksIfEnabled(RefactoredEquipmentItem equipment)
  {
    if (!ApplicationContext.Current.Settings.RequestAddAttackWhenEquippingWeapon || !CharacterManager.Current.Status.IsLoaded || !this.Character.AttacksSection.Items.All<AttackSectionItem>((Func<AttackSectionItem, bool>) (x => x.EquipmentItem != equipment)))
      return;
    if (MessageDialogContext.Current?.Confirm($"You have equipped '{equipment}' which is not yet added to your attacks. Do you want to add it now?", "Aurora - Attacks Section") == true)
    {
      this.Character.AttacksSection.Items.Add(new AttackSectionItem(equipment));
      ApplicationContext.Current.SendStatusMessage($"{equipment} added to your attacks.");
    }
    else
      ApplicationContext.Current.SendStatusMessage("");
  }

  public InventoryStorage StoredItems1 { get; set; }

  public InventoryStorage StoredItems2 { get; set; }

  public InventoryStorage GetStorage(string storageName)
  {
    if (this.StoredItems1.Name.Equals(storageName))
      return this.StoredItems1;
    return this.StoredItems2.Name.Equals(storageName) ? this.StoredItems2 : (InventoryStorage) null;
  }
}
