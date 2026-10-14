// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Shell.Items.RefactoredEquipmentItem
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

#nullable disable
namespace Builder.Presentation.ViewModels.Shell.Items;

public class RefactoredEquipmentItem : EquipmentItem
{
  private bool _isAttuned;
  private bool _showCard = true;
  private string _alternativeName;
  private string _notes;
  private string _notesWatermark;
  private ElementHeader _aquisitionParent;
  private bool _hasAquisitionParent;
  private bool _includeInEquipmentPageInventory;
  private bool _includeInEquipmentPageDescriptionSidebar;
  private bool _includeIndividualPriceOnEquipmentPage;
  private bool _includeInTreasure;
  private bool _isStored;
  private InventoryStorage _storage;
  private string _displayPrice;
  private string _displayWeight;

  public RefactoredEquipmentItem(Item item, Item adorner = null)
    : base(item, adorner)
  {
    if (this.Item.ContainsSelectRules)
    {
      foreach (SelectRule selectRule in this.Item.GetSelectRules())
        selectRule.RenewIdentifier();
    }
    if (this.AdornerItem != null && this.AdornerItem.ContainsSelectRules)
    {
      foreach (SelectRule selectRule in this.AdornerItem.GetSelectRules())
        selectRule.RenewIdentifier();
    }
    this.AlternativeName = "";
    this.Notes = "";
    this.NotesWatermark = ElementDescriptionGenerator.GeneratePlainDescription(item.Description);
    if (this.AdornerItem != null)
      this.NotesWatermark = ElementDescriptionGenerator.GeneratePlainDescription(this.AdornerItem.Description);
    this.IncludeInEquipmentPageInventory = true;
    if (this.Item.HideFromInventory)
    {
      this.IncludeInEquipmentPageInventory = false;
      this.ShowCard = false;
    }
    else if (this.AdornerItem != null && this.AdornerItem.HideFromInventory)
    {
      this.IncludeInEquipmentPageInventory = false;
      this.ShowCard = false;
    }
    if (!this.IncludeInEquipmentPageInventory)
      this.IncludeInEquipmentPageDescriptionSidebar = false;
    this.IncludeInEquipmentPageDescriptionSidebar = this.RaiseEquippedConditionForSidebar();
    this.IncludeIndividualPriceOnEquipmentPage = this.Item.IsValuable;
    this.IncludeInTreasure = this.IncludeIndividualPriceOnEquipmentPage || this.Item.Category.Equals("Valuables");
    this.DisplayPrice = item.DisplayPrice;
    if (this.IsAdorned && this.AdornerItem is MagicItemElement adornerItem && adornerItem.OverrideCost)
      this.DisplayPrice = this.AdornerItem.DisplayPrice;
    this.OnPropertyChanged(nameof (Underline));
  }

  public void SetIdentifier(string identifier) => this.Identifier = identifier;

  public override string DisplayName
  {
    get
    {
      if (!string.IsNullOrWhiteSpace(this.AlternativeName))
        return this.AlternativeName + (this.IsStackable ? $" ({this.Amount})" : "");
      return this.AdornerItem == null ? base.DisplayName : this.GetAdornedName() + (!this.IsStackable || this.Amount <= 1 ? "" : $" ({this.Amount})");
    }
  }

  public bool IsAttunable
  {
    get
    {
      if (this.Item.RequiresAttunement)
        return true;
      return this.IsAdorned && this.AdornerItem.RequiresAttunement;
    }
  }

  public bool IsAttuned
  {
    get => this._isAttuned;
    set
    {
      this.SetProperty<bool>(ref this._isAttuned, value, nameof (IsAttuned));
      this.OnPropertyChanged("IsActivated");
      this.OnPropertyChanged("Underline");
    }
  }

  [Obsolete]
  public void AttuneItem() => this.Register();

  [Obsolete]
  public void UnAttuneItem() => this.Unregister();

  private string GetAdornedName()
  {
    if (string.IsNullOrWhiteSpace(this.AdornerItem.NameFormat))
      return this.AdornerItem.Name;
    string adornedName = this.AdornerItem.NameFormat;
    foreach (Match match in Regex.Matches(this.AdornerItem.NameFormat, "\\$\\((.*?)\\)"))
    {
      switch (match.Value.Substring(2, match.Value.Length - 3))
      {
        case "parent":
          adornedName = adornedName.Replace(match.Value, this.Item.Name);
          continue;
        case "enhancement":
          adornedName = adornedName.Replace(match.Value, this.AdornerItem.Enhancement);
          continue;
        default:
          continue;
      }
    }
    foreach (Match match in Regex.Matches(this.AdornerItem.NameFormat, "{{(.*?)}}"))
    {
      switch (match.Value.Substring(2, match.Value.Length - 4).Trim())
      {
        case "parent":
          adornedName = adornedName.Replace(match.Value, this.Item.Name);
          continue;
        case "enhancement":
          adornedName = adornedName.Replace(match.Value, this.AdornerItem.Enhancement);
          continue;
        default:
          continue;
      }
    }
    return adornedName;
  }

  [Obsolete]
  private void Register()
  {
    if (this.Item != null)
      CharacterManager.Current.RegisterElement((ElementBase) this.Item);
    if (this.AdornerItem != null)
      CharacterManager.Current.RegisterElement((ElementBase) this.AdornerItem);
    this.IsAttuned = true;
  }

  [Obsolete]
  private void Unregister()
  {
    if (this.Item != null)
      CharacterManager.Current.UnregisterElement((ElementBase) this.Item);
    if (this.AdornerItem != null)
      CharacterManager.Current.UnregisterElement((ElementBase) this.AdornerItem);
    this.IsAttuned = false;
  }

  public bool IsActivated => this.IsAttuned || this.IsEquipped;

  private bool IsItemRegistered { get; set; }

  private bool IsAdornedItemRegisted { get; set; }

  private void RegisterItem()
  {
    if (this.IsItemRegistered)
      return;
    Logger.Info($"registering item: {this.Item}");
    CharacterManager.Current.RegisterElement((ElementBase) this.Item);
    this.IsItemRegistered = true;
  }

  private void UnRegisterItem()
  {
    if (!this.IsItemRegistered)
      return;
    Logger.Info($"unregistering item: {this.Item}");
    CharacterManager.Current.UnregisterElement((ElementBase) this.Item);
    this.IsItemRegistered = false;
  }

  private void RegisterAdorner()
  {
    if (!this.IsAdorned || this.IsAdornedItemRegisted)
      return;
    Logger.Info($"registering adorner item: {this.AdornerItem}");
    CharacterManager.Current.RegisterElement((ElementBase) this.AdornerItem);
    this.IsAdornedItemRegisted = true;
  }

  private void UnRegisterAdorner()
  {
    if (!this.IsAdorned || !this.IsAdornedItemRegisted)
      return;
    Logger.Info($"unregistering adorner item: {this.AdornerItem}");
    CharacterManager.Current.UnregisterElement((ElementBase) this.AdornerItem);
    this.IsAdornedItemRegisted = false;
  }

  public void Activate(bool equip, bool attune)
  {
    if (equip && !this.IsEquippable)
      Logger.Warning($"request equip item {this} while not equippable");
    if (attune && !this.IsAttunable)
    {
      Logger.Warning($"request attune item {this} while not attunable");
      attune = false;
    }
    if (equip && this.Item.IsEquippable)
    {
      if (!this.Item.RequiresAttunement)
        this.RegisterItem();
      if (this.IsAdorned && !this.AdornerItem.RequiresAttunement)
        this.RegisterAdorner();
      this.IsEquipped = true;
    }
    if (attune)
    {
      if (this.Item.RequiresAttunement)
        this.RegisterItem();
      if (this.IsAdorned && this.AdornerItem.RequiresAttunement)
        this.RegisterAdorner();
      this.IsAttuned = true;
    }
    if (this.RaiseEquippedConditionForSidebar())
      this.IncludeInEquipmentPageDescriptionSidebar = true;
    this.OnPropertyChanged("Underline");
  }

  public void Deactivate()
  {
    this.UnRegisterAdorner();
    this.UnRegisterItem();
    this.IsEquipped = false;
    this.IsAttuned = false;
    this.EquippedLocation = string.Empty;
    this.IncludeInEquipmentPageDescriptionSidebar = false;
  }

  public void DeactivateAttunement()
  {
    if (!this.IsAttuned)
      Logger.Warning($"trying to unattune {this} which was not attuned.");
    if (this.IsAdorned && this.AdornerItem.RequiresAttunement)
      this.UnRegisterAdorner();
    if (this.Item.RequiresAttunement)
      this.UnRegisterItem();
    this.IsAttuned = false;
  }

  public bool IsArmorTarget()
  {
    return this.Item.Slots.Contains("armor") || this.Item.Slots.Contains("body");
  }

  public bool IsTwoHandTarget()
  {
    return this.Item.Slot.Equals("twohand") || this.Item.Slots.Contains("twohand");
  }

  public bool IsOneHandTarget()
  {
    return this.Item.Slot.Equals("onehand") || this.Item.Slots.Contains("onehand");
  }

  public bool IsPrimaryTarget() => this.Item.Slots.Contains("primary");

  public bool IsSecondaryTarget() => this.Item.Slots.Contains("secondary");

  public bool ShowCard
  {
    get => this._showCard;
    set => this.SetProperty<bool>(ref this._showCard, value, nameof (ShowCard));
  }

  public string AlternativeName
  {
    get => this._alternativeName;
    set
    {
      this.SetProperty<string>(ref this._alternativeName, value, nameof (AlternativeName));
      this.OnPropertyChanged("DisplayName");
    }
  }

  public string Notes
  {
    get => this._notes;
    set
    {
      this.SetProperty<string>(ref this._notes, value, nameof (Notes));
      this.OnPropertyChanged("HasAdditionalNotes");
      this.IncludeInEquipmentPageDescriptionSidebar = true;
    }
  }

  public string NotesWatermark
  {
    get => this._notesWatermark;
    set => this.SetProperty<string>(ref this._notesWatermark, value, nameof (NotesWatermark));
  }

  public bool HasAdditionalNotes => !string.IsNullOrWhiteSpace(this.Notes);

  public ElementHeader AquisitionParent
  {
    get => this._aquisitionParent;
    set
    {
      this.SetProperty<ElementHeader>(ref this._aquisitionParent, value, nameof (AquisitionParent));
    }
  }

  public bool HasAquisitionParent
  {
    get => this._hasAquisitionParent;
    set
    {
      this.SetProperty<bool>(ref this._hasAquisitionParent, value, nameof (HasAquisitionParent));
    }
  }

  public string GetUnderline()
  {
    List<string> values = new List<string>();
    if (this.Item is WeaponElement weaponElement)
    {
      foreach (ElementBase elementBase in DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Weapon Category"))).OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)).ToList<ElementBase>())
      {
        if (weaponElement.Supports.Contains(elementBase.Id))
        {
          if (elementBase.Name.Equals("firearm", StringComparison.InvariantCultureIgnoreCase))
            values.Add(elementBase.Name ?? "");
          else
            values.Add(elementBase.Name + " Weapon");
        }
      }
    }
    return string.Join(", ", (IEnumerable<string>) values);
  }

  public override string ToString() => this.IsAdorned ? this.GetAdornedName() : base.DisplayName;

  public string Underline
  {
    get
    {
      List<string> stringList = new List<string>();
      string underline = this.GetUnderline();
      if (!string.IsNullOrWhiteSpace(underline))
        stringList.Add(underline);
      else if (this.Item is ArmorElement armorElement)
        stringList.Add(armorElement.Name);
      if (!string.IsNullOrWhiteSpace(this.EquippedLocation))
        stringList.Add(this.EquippedLocation);
      if (!stringList.Any<string>() && !string.IsNullOrWhiteSpace(this.Item.ItemType))
        stringList.Add(this.Item.ItemType);
      if (!stringList.Any<string>() && !string.IsNullOrWhiteSpace(this.Item.Category))
        stringList.Add(this.Item.Category);
      return string.Join(" • ", (IEnumerable<string>) stringList);
    }
  }

  public bool IncludeInEquipmentPageInventory
  {
    get => this._includeInEquipmentPageInventory;
    set
    {
      this.SetProperty<bool>(ref this._includeInEquipmentPageInventory, value, nameof (IncludeInEquipmentPageInventory));
    }
  }

  public bool IncludeInEquipmentPageDescriptionSidebar
  {
    get => this._includeInEquipmentPageDescriptionSidebar;
    set
    {
      this.SetProperty<bool>(ref this._includeInEquipmentPageDescriptionSidebar, value, nameof (IncludeInEquipmentPageDescriptionSidebar));
    }
  }

  public bool IncludeIndividualPriceOnEquipmentPage
  {
    get => this._includeIndividualPriceOnEquipmentPage;
    set
    {
      this.SetProperty<bool>(ref this._includeIndividualPriceOnEquipmentPage, value, nameof (IncludeIndividualPriceOnEquipmentPage));
    }
  }

  public bool IncludeInTreasure
  {
    get => this._includeInTreasure;
    set => this.SetProperty<bool>(ref this._includeInTreasure, value, nameof (IncludeInTreasure));
  }

  public bool RaiseEquippedConditionForSidebar()
  {
    return !this.Item.HideFromInventory && (!this.IsAdorned || !this.AdornerItem.HideFromInventory) && (this.IsEquipped || this.IsAttuned) && (this.IsAdorned || !this.Item.Type.Equals("Weapon") && !this.Item.Type.Equals("Armor"));
  }

  public string GetEquipmentPageName()
  {
    string str = this.Item.Name;
    if (!string.IsNullOrWhiteSpace(this.AlternativeName))
      str = this.AlternativeName;
    if (this.IsAdorned)
      str = this.GetAdornedName();
    return this.IncludeIndividualPriceOnEquipmentPage ? $"{str} ({this.Item.DisplayPrice})" : str ?? "";
  }

  public void Store(InventoryStorage storage, bool deactivate = true)
  {
    if (this.Storage != null)
      this.Retrieve();
    if (deactivate)
      this.Deactivate();
    this.Storage = storage;
    this.Storage.PropertyChanged += new PropertyChangedEventHandler(this.Storage_PropertyChanged);
    this.Storage.StoredItems.Add(this);
    this.IsStored = true;
    this.OnPropertyChanged("StorageDisplayName");
  }

  private void Storage_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    this.OnPropertyChanged("StorageDisplayName");
  }

  public void Retrieve()
  {
    if (!this.IsStored)
      return;
    this.Storage.StoredItems.Remove(this);
    this.IsStored = false;
    this.Storage.PropertyChanged -= new PropertyChangedEventHandler(this.Storage_PropertyChanged);
    this.Storage = (InventoryStorage) null;
    this.OnPropertyChanged("StorageDisplayName");
  }

  public bool IsStored
  {
    get => this._isStored;
    set => this.SetProperty<bool>(ref this._isStored, value, nameof (IsStored));
  }

  public InventoryStorage Storage
  {
    get => this._storage;
    set => this.SetProperty<InventoryStorage>(ref this._storage, value, nameof (Storage));
  }

  public string StorageDisplayName
  {
    get
    {
      if (!this.IsStored)
        return "";
      return this.Storage?.Name;
    }
  }

  public string DisplayPrice
  {
    get => this._displayPrice;
    set => this.SetProperty<string>(ref this._displayPrice, value, nameof (DisplayPrice));
  }

  public string DisplayWeight
  {
    get => this._displayWeight;
    set => this.SetProperty<string>(ref this._displayWeight, value, nameof (DisplayWeight));
  }

  private bool ExcludeEncumbrance()
  {
    return this.IsAdorned && this.AdornerItem.ExcludeFromEncumbrance || this.Item.ExcludeFromEncumbrance;
  }

  public Decimal GetWeight()
  {
    if (this.ExcludeEncumbrance())
    {
      Logger.Warning($"ExcludeEncumbrance: {this}");
      this.DisplayWeight = "—";
      return 0M;
    }
    Decimal calculableWeight = this.Item.CalculableWeight;
    this.DisplayWeight = this.Item.DisplayWeight;
    if (this.IsAdorned && this.AdornerItem is MagicItemElement adornerItem && adornerItem.OverrideWeight)
    {
      calculableWeight = adornerItem.CalculableWeight;
      this.DisplayWeight = adornerItem.DisplayWeight;
    }
    if (this.Item.ItemType == "Vehicle")
      Logger.Warning($"{this.Item} needs a excludeEncumbrance=\"true\" setter attribute");
    Decimal num = 0M;
    return !this.IsStackable || this.Amount <= 1 ? num + calculableWeight : num + calculableWeight * (Decimal) this.Amount;
  }
}
