// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Shell.Items.RefactoredEquipmentSectionViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Strings;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.Models.Helpers;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Sources;
using Builder.Presentation.Telemetry;
using Builder.Presentation.UserControls;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.Views;
using Builder.Presentation.Views.Dialogs;
using Builder.Presentation.Views.Sliders;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.ViewModels.Shell.Items;

public class RefactoredEquipmentSectionViewModel :
  ViewModelBase,
  ISubscriber<CharacterManagerElementRegistered>,
  ISubscriber<CharacterManagerElementUnregistered>,
  ISubscriber<SettingsChangedEvent>,
  ISubscriber<CharacterLoadingCompletedEvent>
{
    private readonly ExpressionInterpreter _expressionInterpreter;
    private readonly ElementBaseCollection _items = new ElementBaseCollection();
    private readonly ElementBaseCollection _a = new ElementBaseCollection();
    private readonly ElementBaseCollection _w = new ElementBaseCollection();
    private bool _isArmorSelection;
    private bool _isWeaponSelection;
    private EquipmentCategory _selectedEquipmentCategory;
    private ArmorElement _selectedArmorElement;
    private WeaponElement _selectedWeaponElement;
    private ElementBase _selectedItem;
    private int _buyAmount = 1;
    private string _equipmentWeight;
    private RefactoredEquipmentItem _selectedEquipmentItem;
    private int _attunedItemCount;
    private bool _allowActivateEquipmentItem;
    private bool _allowEquipEquipmentItem;
    private bool _allowEquipPrimaryEquipmentItem;
    private bool _allowEquipSecondaryEquipmentItem;
    private bool _allowEquipVersatileEquipmentItem;
    private bool _allowToggleAttunementEquipmentItem;
    private bool _allowEquipmentItemAddToAttacks;
    private bool _allowExtractEquipmentItem;
    private bool _allowSwitchWhenEquippingWeapons;
    private int _addAmount;
    private bool _isQuickEditEnabled;
    private bool _showInventoryItemCardColumn;
    private List<WeaponElement> _baseWeapons = new List<WeaponElement>();
    private List<ArmorElement> _baseArmors = new List<ArmorElement>();

    private CharacterManager Manager => CharacterManager.Current;

    public Builder.Presentation.Models.Character Character => this.Manager.Character;

    public CharacterInventory Inventory => this.Character.Inventory;

    public RelayCommand AddSelectedItemCommand { get; set; }

    public RelayCommand BuySelectedItemCommand { get; set; }

    public RelayCommand DeleteSelectedEquipmentCommand { get; set; }

    public RelayCommand DeleteAllSelectedEquipmentCommand { get; set; }

    public RelayCommand MoveSelectedEquipmentItemUpCommand { get; set; }

    public RelayCommand MoveSelectedEquipmentItemDownCommand { get; set; }

    public RelayCommand EquipSelectedEquipmentItemCommand { get; set; }

    public RelayCommand AttuneSelectedEquipmentItemCommand { get; set; }

    public RelayCommand ManageEquipmentItemCommand { get; }

    public RelayCommand AddToAttacksCommand { get; }

    public RelayCommand EquipmentItemSellCommand { get; set; }

    public RelayCommand EquipmentItemRemoveCommand { get; set; }

    public RelayCommand EquipmentItemRemoveAllCommand { get; set; }

    public RelayCommand ExtractEquipmentItemCommand { get; }

    public RelayCommand ShowManageStorageCommand
    {
        get
        {
            return new RelayCommand((Action)(() =>
            {
                new InventoryStorageWindow()
                {
                    DataContext = ((object)this)
                }.Show();
            }));
        }
    }

    public RelayCommand StoreSelectedEquipmentAsPrimaryCommand { get; }

    public RelayCommand StoreSelectedEquipmentAsSecondaryCommand { get; }

    public RelayCommand RetrieveStoredSelectedEquipmentCommand { get; }

    private void StoreSelectedEquipmentInPrimary()
    {
        RefactoredEquipmentItem selectedEquipmentItem = this.SelectedEquipmentItem;
        this.SelectedEquipmentItem.Store(this.Inventory.StoredItems1);
        this.SelectedEquipmentItem = selectedEquipmentItem;
        this.Inventory.CalculateWeight();
    }

    private bool CanStoreSelectedEquipmentInPrimary()
    {
        return this.SelectedEquipmentItem != null && (!this.SelectedEquipmentItem.IsStored || this.SelectedEquipmentItem.Storage != this.Inventory.StoredItems1);
    }

    private void StoreSelectedEquipmentInSecondary()
    {
        RefactoredEquipmentItem selectedEquipmentItem = this.SelectedEquipmentItem;
        this.SelectedEquipmentItem.Store(this.Inventory.StoredItems2);
        this.SelectedEquipmentItem = selectedEquipmentItem;
        this.Inventory.CalculateWeight();
    }

    private bool CanStoreSelectedEquipmentInSecondary()
    {
        return this.SelectedEquipmentItem != null && (!this.SelectedEquipmentItem.IsStored || this.SelectedEquipmentItem.Storage != this.Inventory.StoredItems2);
    }

    private void RetrieveStoredSelectedEquipment()
    {
        this.SelectedEquipmentItem.Retrieve();
        this.OnPropertyChanged("SelectedEquipmentItem");
        this.Inventory.CalculateWeight();
    }

    public RelayCommand ActivateEquipmentItemCommand { get; set; }

    public RelayCommand EquipEquipmentItemCommand { get; set; }

    public RelayCommand EquipPrimaryEquipmentItemCommand { get; set; }

    public RelayCommand EquipSecondaryEquipmentItemCommand { get; set; }

    public RelayCommand EquipVersatileEquipmentItemCommand { get; set; }

    public RelayCommand ToggleAttunementEquipmentItemCommand { get; set; }

    public bool AllowActivateEquipmentItem
    {
        get => this._allowActivateEquipmentItem;
        set
        {
            this.SetProperty<bool>(ref this._allowActivateEquipmentItem, value, nameof(AllowActivateEquipmentItem));
        }
    }

    public bool AllowEquipEquipmentItem
    {
        get => this._allowEquipEquipmentItem;
        set
        {
            this.SetProperty<bool>(ref this._allowEquipEquipmentItem, value, nameof(AllowEquipEquipmentItem));
        }
    }

    public bool AllowEquipPrimaryEquipmentItem
    {
        get => this._allowEquipPrimaryEquipmentItem;
        set
        {
            this.SetProperty<bool>(ref this._allowEquipPrimaryEquipmentItem, value, nameof(AllowEquipPrimaryEquipmentItem));
        }
    }

    public bool AllowEquipSecondaryEquipmentItem
    {
        get => this._allowEquipSecondaryEquipmentItem;
        set
        {
            this.SetProperty<bool>(ref this._allowEquipSecondaryEquipmentItem, value, nameof(AllowEquipSecondaryEquipmentItem));
        }
    }

    public bool AllowEquipVersatileEquipmentItem
    {
        get => this._allowEquipVersatileEquipmentItem;
        set
        {
            this.SetProperty<bool>(ref this._allowEquipVersatileEquipmentItem, value, nameof(AllowEquipVersatileEquipmentItem));
        }
    }

    public bool AllowToggleAttunementEquipmentItem
    {
        get => this._allowToggleAttunementEquipmentItem;
        set
        {
            this.SetProperty<bool>(ref this._allowToggleAttunementEquipmentItem, value, nameof(AllowToggleAttunementEquipmentItem));
        }
    }

    public bool AllowEquipmentItemAddToAttacks
    {
        get => this._allowEquipmentItemAddToAttacks;
        set
        {
            this.SetProperty<bool>(ref this._allowEquipmentItemAddToAttacks, value, nameof(AllowEquipmentItemAddToAttacks));
        }
    }

    public ICommand ShowManageCoinageSliderCommand
    {
        get
        {
            return (ICommand)new RelayCommand((Action)(() => this.EventAggregator.Send<ShowSliderEvent>(new ShowSliderEvent(Slider.ManageCoinage))));
        }
    }

    public RelayCommand IncrementSelectedEquipmentItemAmountCommand { get; set; }

    public RelayCommand DecrementSelectedEquipmentItemAmountCommand { get; set; }

    private void AddSelectedItem()
    {
        Item item = this.SelectedItem as Item;
        if (item == null)
            return;
        try
        {
            int num = Math.Max(1, this.AddAmount);
            AnalyticsEventHelper.EquipmentAdd(this.SelectedEquipmentCategory.DisplayName, item.Name, item.Source);
            if (item.IsStackable)
            {
                RefactoredEquipmentItem refactoredEquipmentItem = this.Inventory.Items.FirstOrDefault<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>)(x => x.Item.Id.Equals(item.Id)));
                if (refactoredEquipmentItem != null)
                {
                    refactoredEquipmentItem.Amount += num;
                    this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"You have added {(num > 1 ? num.ToString() + " x" : "another")} '{item.Name}' to your inventory."));
                    return;
                }
            }
            string str = "";
            for (int index = 0; index < num; ++index)
            {
                RefactoredEquipmentItem refactoredEquipmentItem1 = (RefactoredEquipmentItem)null;
                if (this.IsArmorSelection)
                {
                    this.SelectedArmorElement.Copy<ArmorElement>();
                    refactoredEquipmentItem1 = new RefactoredEquipmentItem((Item)this.SelectedArmorElement, item);
                }
                if (this.IsWeaponSelection)
                    refactoredEquipmentItem1 = new RefactoredEquipmentItem((Item)this.SelectedWeaponElement, item);
                if (item.Type.Equals("Magic Item") || item.Type.Equals("Item"))
                {
                    if (item.ElementSetters.ContainsSetter("weapon"))
                    {
                        List<WeaponElement> list = this.Inventory.GetSupportedWeaponElements(item.ElementSetters.GetSetter("weapon").Value).ToList<WeaponElement>();
                        if (list.Count<WeaponElement>() == 1)
                            refactoredEquipmentItem1 = new RefactoredEquipmentItem((Item)list.FirstOrDefault<WeaponElement>(), item);
                    }
                    else if (item.ElementSetters.ContainsSetter("armor"))
                    {
                        List<ArmorElement> list = this.Inventory.GetSupportedArmorElements(item.ElementSetters.GetSetter("armor").Value).ToList<ArmorElement>();
                        if (list.Count<ArmorElement>() == 1)
                            refactoredEquipmentItem1 = new RefactoredEquipmentItem((Item)list.FirstOrDefault<ArmorElement>(), item);
                    }
                }
                if (refactoredEquipmentItem1 == null)
                    refactoredEquipmentItem1 = new RefactoredEquipmentItem(item);
                if (item.IsStackable)
                {
                    RefactoredEquipmentItem refactoredEquipmentItem2 = this.Inventory.Items.FirstOrDefault<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>)(x => x.Item.Id.Equals(item.Id)));
                    if (refactoredEquipmentItem2 != null)
                    {
                        ++refactoredEquipmentItem2.Amount;
                        str = item.Name;
                        continue;
                    }
                }
                this.Inventory.Items.Add(refactoredEquipmentItem1);
                str = refactoredEquipmentItem1.DisplayName;
            }
            this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"You have added {num} x '{str}' to your inventory."));
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(AddSelectedItem));
            Dictionary<string, string> properties = AnalyticsErrorHelper.CreateProperties("item", item.ToString());
            string elementNodeString = item.ElementNodeString;
            AnalyticsErrorHelper.Exception(ex, properties, elementNodeString, nameof(AddSelectedItem), 332);
            MessageDialogService.ShowException(ex);
        }
    }

    private bool CanAddSelectedItem() => this._selectedItem != null;

    private void BuySelectedItem()
    {
        if (!(this.SelectedItem is Item selectedItem1))
            return;
        try
        {
            AnalyticsEventHelper.EquipmentBuy(this.SelectedEquipmentCategory.DisplayName, selectedItem1.Name, selectedItem1.Source);
            Coinage coinage = new Coinage();
            bool flag = false;
            if (this.SelectedItem is MagicItemElement selectedItem2)
                flag = selectedItem2.OverrideCost;
            if (this.IsArmorSelection)
            {
                if (!flag)
                {
                    for (int index = 0; index < this.BuyAmount; ++index)
                        coinage.Deposit(Coinage.GetCurrencyCoinFromAbbreviation(this.SelectedArmorElement.CurrencyAbbreviation), (long)this.SelectedArmorElement.Cost);
                }
            }
            else if (this.IsWeaponSelection && !flag)
            {
                for (int index = 0; index < this.BuyAmount; ++index)
                    coinage.Deposit(Coinage.GetCurrencyCoinFromAbbreviation(this.SelectedWeaponElement.CurrencyAbbreviation), (long)this.SelectedWeaponElement.Cost);
            }
            for (int index = 0; index < this.BuyAmount; ++index)
                coinage.Deposit(Coinage.GetCurrencyCoinFromAbbreviation(selectedItem1.CurrencyAbbreviation), (long)selectedItem1.Cost);
            long calculationBase = coinage.CalculationBase;
            if (this.Inventory.Coins.HasSufficienctFunds(Coinage.CurrencyCoin.Copper, calculationBase))
            {
                if (!this.IsArmorSelection && !this.IsWeaponSelection)
                {
                    if (coinage.Platinum > 0L)
                        this.Inventory.Coins.Withdraw(Coinage.CurrencyCoin.Platinum, coinage.Platinum);
                    else if (coinage.Gold > 0L)
                        this.Inventory.Coins.Withdraw(Coinage.CurrencyCoin.Gold, coinage.Gold);
                    else if (coinage.Electrum > 0L)
                        this.Inventory.Coins.Withdraw(Coinage.CurrencyCoin.Electrum, coinage.Electrum);
                    else if (coinage.Silver > 0L)
                        this.Inventory.Coins.Withdraw(Coinage.CurrencyCoin.Silver, coinage.Silver);
                    else if (coinage.Copper > 0L)
                        this.Inventory.Coins.Withdraw(Coinage.CurrencyCoin.Copper, coinage.Copper);
                }
                else
                    this.Inventory.Coins.Withdraw(Coinage.CurrencyCoin.Copper, calculationBase);
                int addAmount = this.AddAmount;
                this.AddAmount = 1;
                for (int index = 0; index < this.BuyAmount; ++index)
                    this.AddSelectedItem();
                this.AddAmount = addAmount;
            }
            else
            {
                if (MessageBox.Show($"This purchase costs {coinage.DisplayCoinage}gp, you only have {this.Inventory.Coins.DisplayCoinage}gp." + " Do you want to add the goods instead of buying?", "Insufficienct Coin", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;
                int addAmount = this.AddAmount;
                this.AddAmount = 1;
                for (int index = 0; index < this.BuyAmount; ++index)
                    this.AddSelectedItem();
                this.AddAmount = addAmount;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(BuySelectedItem));
            Dictionary<string, string> properties = AnalyticsErrorHelper.CreateProperties("item", selectedItem1.ToString());
            string elementNodeString = selectedItem1.ElementNodeString;
            AnalyticsErrorHelper.Exception(ex, properties, elementNodeString, nameof(BuySelectedItem), 480);
            MessageDialogService.ShowException(ex);
        }
    }

    private bool BuyAddSelectedItem() => this.CanAddSelectedItem();

    private void MoveSelectedEquipmentItemUp()
    {
        if (!this.CanMoveSelectedEquipmentItemUp())
            return;
        int oldIndex = this.Inventory.Items.IndexOf(this._selectedEquipmentItem);
        this.Inventory.Items.Move(oldIndex, oldIndex - 1);
        this.MoveSelectedEquipmentItemUpCommand.RaiseCanExecuteChanged();
        this.MoveSelectedEquipmentItemDownCommand.RaiseCanExecuteChanged();
    }

    private bool CanMoveSelectedEquipmentItemUp()
    {
        return this._selectedEquipmentItem != null && this.Inventory.Items.IndexOf(this._selectedEquipmentItem) != 0;
    }

    private void MoveSelectedEquipmentItemDown()
    {
        if (!this.CanMoveSelectedEquipmentItemDown())
            return;
        int oldIndex = this.Inventory.Items.IndexOf(this._selectedEquipmentItem);
        this.Inventory.Items.Move(oldIndex, oldIndex + 1);
        this.MoveSelectedEquipmentItemUpCommand.RaiseCanExecuteChanged();
        this.MoveSelectedEquipmentItemDownCommand.RaiseCanExecuteChanged();
    }

    private bool CanMoveSelectedEquipmentItemDown()
    {
        return this._selectedEquipmentItem != null && this.Inventory.Items.IndexOf(this._selectedEquipmentItem) != this.Inventory.Items.Count - 1;
    }

    private void DeleteSelectedEquipment()
    {
        if (!this.CanDeleteSelectedEquipment())
            return;
        int num1 = this.Inventory.Items.IndexOf(this._selectedEquipmentItem);
        if (this.SelectedEquipmentItem.IsEquipped)
        {
            int num2 = this.Inventory.IsEquippedPrimary((EquipmentItem)this.SelectedEquipmentItem) ? 1 : 0;
            bool flag = this.Inventory.IsEquippedSecondary((EquipmentItem)this.SelectedEquipmentItem);
            if (num2 != 0)
                this.Inventory.UnequipPrimary();
            else if (flag)
                this.Inventory.UnequipSecondary();
            else if (this.SelectedEquipmentItem.IsArmorTarget())
                this.Inventory.UnequipArmor();
            else
                this.SelectedEquipmentItem.Deactivate();
        }
        string statusMessage = $"You have removed{(this.SelectedEquipmentItem.Amount > 1 ? (object)" 1" : (object)"")} '{this.SelectedEquipmentItem}' from your inventory.";
        if (this.SelectedEquipmentItem.IsStackable)
        {
            if (this.SelectedEquipmentItem.Amount > 1)
                --this.SelectedEquipmentItem.Amount;
            else
                this.Inventory.Items.Remove(this.SelectedEquipmentItem);
        }
        else
            this.Inventory.Items.Remove(this.SelectedEquipmentItem);
        this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(statusMessage));
        if (this.Inventory.Items.Any<RefactoredEquipmentItem>() && this.SelectedEquipmentItem == null)
            this.SelectedEquipmentItem = num1 != 0 ? this.Inventory.Items[num1 - 1] : this.Inventory.Items[0];
        this.CalculateWeight();
    }

    private bool CanDeleteSelectedEquipment() => this._selectedEquipmentItem != null;

    private void DeleteAllSelectedEquipment()
    {
        if (!this.CanDeleteSelectedEquipment())
            return;
        int num = this.Inventory.Items.IndexOf(this._selectedEquipmentItem);
        if (this.SelectedEquipmentItem.IsEquipped)
        {
            this.SelectedEquipmentItem.Deactivate();
            this.SelectedEquipmentItem.EquippedLocation = "";
        }
        if (this.SelectedEquipmentItem.IsStored)
            this.SelectedEquipmentItem.Retrieve();
        string statusMessage = $"You have removed '{this.SelectedEquipmentItem}' from your inventory.";
        this.Inventory.Items.Remove(this.SelectedEquipmentItem);
        this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(statusMessage));
        if (this.Inventory.Items.Any<RefactoredEquipmentItem>() && this.SelectedEquipmentItem == null)
            this.SelectedEquipmentItem = num != 0 ? this.Inventory.Items[num - 1] : this.Inventory.Items[0];
        this.CalculateWeight();
    }

    private bool CanDeleteAllSelectedEquipment()
    {
        return this._selectedEquipmentItem != null && this._selectedEquipmentItem.IsStackable;
    }

    private void ManageEquipmentItem()
    {
        if (this._selectedEquipmentItem == null)
            return;
        ManageEquipmentItemWindow equipmentItemWindow = new ManageEquipmentItemWindow();
        equipmentItemWindow.DataContext = (object)this;
        equipmentItemWindow.ShowDialog();
    }

    private bool CanManageEquipmentItem() => this._selectedEquipmentItem != null;

    private void ExtractEquipmentItem()
    {
        if (this._selectedEquipmentItem == null || !this._selectedEquipmentItem.Item.IsExtractable)
            return;
        bool? nullable = new ExtractItemWindow(this._selectedEquipmentItem.Item, this._items).ShowDialog();
        bool flag = true;
        if (!(nullable.GetValueOrDefault() == flag & nullable.HasValue))
            return;
        int count = this._selectedEquipmentItem.Item.Extractables.Count;
        int num1 = 0;
        foreach (KeyValuePair<string, int> extractable1 in this._selectedEquipmentItem.Item.Extractables)
        {
            string id = extractable1.Key;
            int num2 = extractable1.Value;
            Item extractable = this._items.FirstOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id.Equals(id))) as Item;
            if (extractable != null)
            {
                RefactoredEquipmentItem refactoredEquipmentItem1 = new RefactoredEquipmentItem(extractable);
                if (extractable.IsStackable)
                {
                    RefactoredEquipmentItem refactoredEquipmentItem2 = this.Inventory.Items.FirstOrDefault<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>)(x => x.Item.Id.Equals(extractable.Id)));
                    if (refactoredEquipmentItem2 != null)
                    {
                        refactoredEquipmentItem2.Amount += num2;
                        ++num1;
                        continue;
                    }
                }
                refactoredEquipmentItem1.Amount = num2;
                this.Inventory.Items.Add(refactoredEquipmentItem1);
                ++num1;
            }
            else
            {
                string str = $"unable to extract item with id: {id} from {this._selectedEquipmentItem.Item} with id {this._selectedEquipmentItem.Item.Id}";
                Logger.Warning(str);
                ApplicationManager.Current.SendStatusMessage(str);
            }
        }
        string displayName = this._selectedEquipmentItem.DisplayName;
        this.DeleteSelectedEquipment();
        if (count != num1)
            return;
        ApplicationManager.Current.SendStatusMessage("You extracted " + displayName);
    }

    private bool CanExtractEquipmentItem()
    {
        return this._selectedEquipmentItem != null && this._selectedEquipmentItem.Item.IsExtractable;
    }

    public bool AllowExtractEquipmentItem
    {
        get => this._allowExtractEquipmentItem;
        set
        {
            this.SetProperty<bool>(ref this._allowExtractEquipmentItem, value, nameof(AllowExtractEquipmentItem));
        }
    }

    private void EquipSelectedEquipmentItem()
    {
        if (this._selectedEquipmentItem == null || !this.CanEquipSelectedEquipmentItem())
            return;
        this.EquipEquipmentItem();
    }

    private bool CanEquipSelectedEquipmentItem()
    {
        return this._selectedEquipmentItem != null && this._selectedEquipmentItem.Item.IsEquippable;
    }

    private void AttuneSelectedEquipmentItemUp()
    {
        if (this._selectedEquipmentItem == null || !this.CanAttuneSelectedEquipmentItem())
            return;
        if (!this._selectedEquipmentItem.IsAttuned)
            this._selectedEquipmentItem.AttuneItem();
        else
            this._selectedEquipmentItem.UnAttuneItem();
        this.CalculateAttunementCount();
    }

    private bool CanAttuneSelectedEquipmentItem()
    {
        return this._selectedEquipmentItem != null && this._selectedEquipmentItem.IsAttunable;
    }

    private void ActivateEquipmentItem()
    {
        if (this._selectedEquipmentItem == null)
            return;
        int num = this._selectedEquipmentItem.IsActivated ? 1 : 0;
        if (this.CanEquipEquipmentItem())
        {
            this.EquipEquipmentItem();
        }
        else
        {
            this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(this._selectedEquipmentItem.DisplayName + " activated clicked."));
            this.EventAggregator.Send<EquipmentElementDescriptionDisplayRequestEvent>(this._selectedEquipmentItem.IsAdorned ? new EquipmentElementDescriptionDisplayRequestEvent((ElementBase)this._selectedEquipmentItem.AdornerItem, (ElementBase)this._selectedEquipmentItem.Item) : new EquipmentElementDescriptionDisplayRequestEvent((ElementBase)this._selectedEquipmentItem.Item));
            this.CalculateAttunementCount();
        }
    }

    private void EquipEquipmentItem()
    {
        if (this._selectedEquipmentItem.IsArmorTarget())
        {
            if (this.Inventory.EquippedArmor != null)
            {
                int num = this.Inventory.EquippedArmor.Identifier.Equals(this._selectedEquipmentItem.Identifier) ? 1 : 0;
                this.Inventory.UnequipArmor();
                if (num != 0)
                    return;
            }
            this.Inventory.EquipArmor(this._selectedEquipmentItem);
        }
        else if (this._selectedEquipmentItem.IsOneHandTarget() && this._selectedEquipmentItem.IsSecondaryTarget())
            this.EquipSecondaryEquipmentItem();
        else if (this._selectedEquipmentItem.IsTwoHandTarget())
        {
            if (this.Inventory.EquippedSecondary != null)
            {
                int num = this.Inventory.EquippedSecondary.Identifier.Equals(this._selectedEquipmentItem.Identifier) ? 1 : 0;
                this.Inventory.UnequipSecondary();
                if (num != 0)
                    return;
            }
            if (this.Inventory.EquippedPrimary != null)
            {
                int num = this.Inventory.EquippedPrimary.Identifier.Equals(this._selectedEquipmentItem.Identifier) ? 1 : 0;
                this.Inventory.UnequipPrimary();
                if (num != 0)
                    return;
            }
            this._selectedEquipmentItem.Activate(true, this._selectedEquipmentItem.IsAttunable);
            this.Inventory.EquipPrimary(this._selectedEquipmentItem, true);
        }
        else if (this._selectedEquipmentItem.IsOneHandTarget())
        {
            if (this.Inventory.IsEquippedTwoHanded())
            {
                this.Inventory.UnequipPrimary();
                this.Inventory.UnequipSecondary();
            }
            if (this.IsEquippedPrimary((EquipmentItem)this._selectedEquipmentItem))
            {
                this.Inventory.UnequipPrimary();
            }
            else
            {
                if (this.Inventory.EquippedPrimary != null && !this.IsEquippedSecondary((EquipmentItem)this.Inventory.EquippedPrimary))
                    this.Inventory.UnequipPrimary();
                if (this._selectedEquipmentItem.IsEquipped || this.Inventory.EquippedPrimary != null)
                    return;
                this.Inventory.EquipPrimary(this._selectedEquipmentItem);
            }
        }
        else if (this._selectedEquipmentItem.IsPrimaryTarget() || this._selectedEquipmentItem.IsSecondaryTarget())
        {
            if (this._selectedEquipmentItem.IsPrimaryTarget())
            {
                this.EquipPrimaryEquipmentItem();
            }
            else
            {
                if (!this._selectedEquipmentItem.IsSecondaryTarget())
                    return;
                this.EquipSecondaryEquipmentItem();
            }
        }
        else
        {
            if (this._selectedEquipmentItem.IsEquippable)
            {
                if (this._selectedEquipmentItem.IsEquipped)
                    this._selectedEquipmentItem.Deactivate();
                else
                    this._selectedEquipmentItem.Activate(true, this._selectedEquipmentItem.IsAttunable);
            }
            else if (this._selectedEquipmentItem.IsAttunable)
            {
                if (this._selectedEquipmentItem.IsAttuned)
                    this._selectedEquipmentItem.DeactivateAttunement();
                else
                    this._selectedEquipmentItem.Activate(false, this._selectedEquipmentItem.IsAttunable);
            }
            this.CalculateAttunementCount();
        }
    }

    private bool IsEquippedPrimary(EquipmentItem equipment)
    {
        return this.Inventory.IsEquippedPrimary(equipment);
    }

    private bool IsEquippedSecondary(EquipmentItem equipment)
    {
        return this.Inventory.IsEquippedSecondary(equipment);
    }

    public bool AllowSwitchWhenEquippingWeapons
    {
        get => this._allowSwitchWhenEquippingWeapons;
        set
        {
            this.SetProperty<bool>(ref this._allowSwitchWhenEquippingWeapons, value, nameof(AllowSwitchWhenEquippingWeapons));
        }
    }

    private void EquipPrimaryEquipmentItem()
    {
        if (this._selectedEquipmentItem == null)
            return;
        bool equippingWeapons = this.AllowSwitchWhenEquippingWeapons;
        bool flag1 = false;
        if (this._selectedEquipmentItem.IsEquipped)
        {
            bool flag2 = this.IsEquippedPrimary((EquipmentItem)this._selectedEquipmentItem);
            bool flag3 = this.IsEquippedSecondary((EquipmentItem)this._selectedEquipmentItem);
            if (flag2 & flag3)
            {
                this.Inventory.EquippedSecondary = (RefactoredEquipmentItem)null;
                this.Inventory.EquippedPrimary.EquippedLocation = "Primary Hand";
                return;
            }
            if (flag3)
            {
                this.Inventory.EquippedSecondary = (RefactoredEquipmentItem)null;
                flag1 = equippingWeapons;
            }
            else if (flag2)
            {
                this.Inventory.UnequipPrimary();
                return;
            }
        }
        if (this.Inventory.EquippedPrimary != null)
        {
            RefactoredEquipmentItem equippedPrimary = this.Inventory.EquippedPrimary;
            if (flag1)
            {
                this.Inventory.EquippedSecondary = equippedPrimary;
                equippedPrimary.EquippedLocation = "Secondary Hand";
            }
            else
            {
                if (this.Inventory.IsEquippedTwoHanded() || this.Inventory.IsEquippedVersatile())
                    this.Inventory.EquippedSecondary = (RefactoredEquipmentItem)null;
                this.Inventory.EquippedPrimary.Deactivate();
            }
            this.Inventory.EquippedPrimary = (RefactoredEquipmentItem)null;
        }
        this.Inventory.EquipPrimary(this._selectedEquipmentItem);
    }

    private void EquipSecondaryEquipmentItem()
    {
        if (this._selectedEquipmentItem == null)
            return;
        bool equippingWeapons = this.AllowSwitchWhenEquippingWeapons;
        bool flag1 = false;
        if (this._selectedEquipmentItem.IsEquipped)
        {
            bool flag2 = this.IsEquippedPrimary((EquipmentItem)this._selectedEquipmentItem);
            bool flag3 = this.IsEquippedSecondary((EquipmentItem)this._selectedEquipmentItem);
            if (flag2 & flag3)
            {
                if (this._selectedEquipmentItem.Item.HasVersatile)
                {
                    this.Inventory.UnequipPrimary();
                    this.Inventory.UnequipSecondary();
                }
            }
            else if (flag2)
            {
                this.Inventory.EquippedPrimary = (RefactoredEquipmentItem)null;
                flag1 = equippingWeapons;
            }
            else if (flag3)
            {
                this.Inventory.EquippedSecondary.Deactivate();
                this.Inventory.EquippedSecondary = (RefactoredEquipmentItem)null;
                return;
            }
        }
        if (this.Inventory.EquippedSecondary != null)
        {
            RefactoredEquipmentItem equippedSecondary = this.Inventory.EquippedSecondary;
            if (flag1 && !this.Inventory.EquippedSecondary.IsSecondaryTarget())
            {
                this.Inventory.EquippedPrimary = equippedSecondary;
                equippedSecondary.EquippedLocation = "Primary Hand";
            }
            else if (this.Inventory.IsEquippedTwoHanded())
            {
                this.Inventory.EquippedPrimary = (RefactoredEquipmentItem)null;
                this.Inventory.EquippedSecondary.Deactivate();
            }
            else if (this.Inventory.IsEquippedVersatile())
                this.Inventory.EquippedPrimary.EquippedLocation = "Primary Hand";
            else
                this.Inventory.UnequipSecondary();
            this.Inventory.EquippedSecondary = (RefactoredEquipmentItem)null;
        }
        this.Inventory.EquipSecondary(this._selectedEquipmentItem);
    }

    private void EquipVersatileEquipmentItem()
    {
        if (this._selectedEquipmentItem == null)
            return;
        if (this._selectedEquipmentItem.IsEquipped)
        {
            bool flag1 = this.IsEquippedPrimary((EquipmentItem)this._selectedEquipmentItem);
            bool flag2 = this.IsEquippedSecondary((EquipmentItem)this._selectedEquipmentItem);
            if (flag1 & flag2)
                this.Inventory.UnequipSecondary();
            else if (flag1)
            {
                if (this.Inventory.EquippedSecondary != null)
                    this.Inventory.UnequipSecondary();
            }
            else if (flag2 && this.Inventory.EquippedPrimary != null)
                this.Inventory.UnequipPrimary();
        }
        else
        {
            if (this.Inventory.EquippedPrimary != null)
                this.Inventory.UnequipPrimary();
            if (this.Inventory.EquippedSecondary != null)
                this.Inventory.UnequipSecondary();
        }
        this.Inventory.EquipPrimary(this._selectedEquipmentItem, true);
    }

    private void ToggleAttunementEquipmentItem()
    {
        if (this._selectedEquipmentItem.IsAttunable)
        {
            if (this._selectedEquipmentItem.IsAttuned)
                this._selectedEquipmentItem.DeactivateAttunement();
            else
                this._selectedEquipmentItem.Activate(false, true);
        }
        this.CalculateAttunementCount();
    }

    private void AddToAttacks()
    {
        this.Character.AttacksSection.Items.Add(new AttackSectionItem(this.SelectedEquipmentItem));
        ApplicationManager.Current.SendStatusMessage($"{this.SelectedEquipmentItem} added to your attacks.");
    }

    private bool CanActivateEquipmentItem()
    {
        return this._selectedEquipmentItem != null && (this._selectedEquipmentItem.IsEquippable || this._selectedEquipmentItem.IsAttunable);
    }

    private bool CanEquipEquipmentItem()
    {
        if (this._selectedEquipmentItem == null || !this._selectedEquipmentItem.IsEquippable)
            return false;
        if (this._selectedEquipmentItem.IsArmorTarget() || this._selectedEquipmentItem.IsTwoHandTarget())
            return true;
        if (this._selectedEquipmentItem.IsOneHandTarget())
        {
            if (this._selectedEquipmentItem.IsSecondaryTarget() || this.Inventory.IsEquippedTwoHanded())
                return false;
            RefactoredEquipmentItem equippedPrimary = this.Inventory.EquippedPrimary;
            return false;
        }
        return this._selectedEquipmentItem.IsEquippable;
    }

    private bool CanEquipPrimaryEquipmentItem()
    {
        return this._selectedEquipmentItem != null && this._selectedEquipmentItem.IsEquippable && this._selectedEquipmentItem.IsOneHandTarget() && !this._selectedEquipmentItem.IsSecondaryTarget();
    }

    private bool CanEquipSecondaryEquipmentItem()
    {
        return this._selectedEquipmentItem != null && this._selectedEquipmentItem.IsEquippable && this._selectedEquipmentItem.IsOneHandTarget() && !this._selectedEquipmentItem.IsPrimaryTarget();
    }

    private bool CanEquipVersatileEquipmentItem()
    {
        return this._selectedEquipmentItem != null && this._selectedEquipmentItem.IsEquippable && this._selectedEquipmentItem.Item.HasVersatile;
    }

    private bool CanToggleAttunementEquipmentItem()
    {
        return this._selectedEquipmentItem != null && this._selectedEquipmentItem.IsAttunable;
    }

    private bool CanAddToAttacks()
    {
        return this.SelectedEquipmentItem != null && this.SelectedEquipmentItem.Item.Type == "Weapon";
    }

    public RefactoredEquipmentSectionViewModel()
    {
        if (this.IsInDesignMode)
        {
            this.InitializeDesignData();
        }
        else
        {
            this._expressionInterpreter = new ExpressionInterpreter();
            this._items.AddRange(DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type.Equals("Item") || x.Type.Equals("Magic Item") || x.Type.Equals("Armor") || x.Type.Equals("Weapon"))));
            List<string> list = this._items.Select<ElementBase, string>((Func<ElementBase, string>)(x => x.Source)).Distinct<string>().OrderBy<string, string>((Func<string, string>)(x => x)).ToList<string>();
            list.Insert(0, "--");
            this.ItemFilter = new ElementFilter((IEnumerable<string>)list)
            {
                IncludeKeywords = true,
                IsSourceFilterAvailable = true
            };
            this.ItemFilter.PropertyChanged += new PropertyChangedEventHandler(this.ItemFilter_PropertyChanged);
            this.AddSelectedItemCommand = new RelayCommand(new Action(this.AddSelectedItem), new Func<bool>(this.CanAddSelectedItem));
            this.BuySelectedItemCommand = new RelayCommand(new Action(this.BuySelectedItem), new Func<bool>(this.BuyAddSelectedItem));
            this.DeleteSelectedEquipmentCommand = new RelayCommand(new Action(this.DeleteSelectedEquipment), new Func<bool>(this.CanDeleteSelectedEquipment));
            this.DeleteAllSelectedEquipmentCommand = new RelayCommand(new Action(this.DeleteAllSelectedEquipment), new Func<bool>(this.CanDeleteAllSelectedEquipment));
            this.MoveSelectedEquipmentItemUpCommand = new RelayCommand(new Action(this.MoveSelectedEquipmentItemUp), new Func<bool>(this.CanMoveSelectedEquipmentItemUp));
            this.MoveSelectedEquipmentItemDownCommand = new RelayCommand(new Action(this.MoveSelectedEquipmentItemDown), new Func<bool>(this.CanMoveSelectedEquipmentItemDown));
            this.EquipSelectedEquipmentItemCommand = new RelayCommand(new Action(this.EquipSelectedEquipmentItem), new Func<bool>(this.CanEquipSelectedEquipmentItem));
            this.ManageEquipmentItemCommand = new RelayCommand(new Action(this.ManageEquipmentItem), new Func<bool>(this.CanManageEquipmentItem));
            this.ActivateEquipmentItemCommand = new RelayCommand(new Action(this.ActivateEquipmentItem));
            this.EquipEquipmentItemCommand = new RelayCommand(new Action(this.EquipEquipmentItem), new Func<bool>(this.CanEquipEquipmentItem));
            this.EquipPrimaryEquipmentItemCommand = new RelayCommand(new Action(this.EquipPrimaryEquipmentItem), new Func<bool>(this.CanEquipPrimaryEquipmentItem));
            this.EquipSecondaryEquipmentItemCommand = new RelayCommand(new Action(this.EquipSecondaryEquipmentItem), new Func<bool>(this.CanEquipSecondaryEquipmentItem));
            this.EquipVersatileEquipmentItemCommand = new RelayCommand(new Action(this.EquipVersatileEquipmentItem), new Func<bool>(this.CanEquipVersatileEquipmentItem));
            this.ToggleAttunementEquipmentItemCommand = new RelayCommand(new Action(this.ToggleAttunementEquipmentItem), new Func<bool>(this.CanToggleAttunementEquipmentItem));
            this.AddToAttacksCommand = new RelayCommand(new Action(this.AddToAttacks), new Func<bool>(this.CanAddToAttacks));
            this.ExtractEquipmentItemCommand = new RelayCommand(new Action(this.ExtractEquipmentItem), new Func<bool>(this.CanExtractEquipmentItem));
            this.IncrementSelectedEquipmentItemAmountCommand = new RelayCommand((Action)(() =>
            {
                ++this.SelectedEquipmentItem.Amount;
                this.Inventory.CalculateWeight();
            }), (Func<bool>)(() => this.SelectedEquipmentItem != null && this.SelectedEquipmentItem.IsStackable));
            this.DecrementSelectedEquipmentItemAmountCommand = new RelayCommand((Action)(() =>
            {
                --this.SelectedEquipmentItem.Amount;
                this.Inventory.CalculateWeight();
            }), (Func<bool>)(() => this.SelectedEquipmentItem != null && this.SelectedEquipmentItem.IsStackable && this.SelectedEquipmentItem.Amount > 1));
            this.StoreSelectedEquipmentAsPrimaryCommand = new RelayCommand(new Action(this.StoreSelectedEquipmentInPrimary), new Func<bool>(this.CanStoreSelectedEquipmentInPrimary));
            this.StoreSelectedEquipmentAsSecondaryCommand = new RelayCommand(new Action(this.StoreSelectedEquipmentInSecondary), new Func<bool>(this.CanStoreSelectedEquipmentInSecondary));
            this.RetrieveStoredSelectedEquipmentCommand = new RelayCommand(new Action(this.RetrieveStoredSelectedEquipment), (Func<bool>)(() => this.SelectedEquipmentItem != null && this.SelectedEquipmentItem.IsStored));
            this.InitializeCollections();
            foreach (Item obj in (IEnumerable<Item>)this._items.Cast<Item>().OrderBy<Item, string>((Func<Item, string>)(x => x.Name)))
            {
                Item item = obj;
                EquipmentCategory equipmentCategory1 = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>();
                switch (item.Type)
                {
                    case "Item":
                        equipmentCategory1 = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals(item.Category))) ?? equipmentCategory1;
                        break;
                    case "Armor":
                    case "Weapon":
                        continue;
                    case "Magic Item":
                        EquipmentCategory equipmentCategory2 = this.EquipmentCategories.First<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals("Wondrous Items", StringComparison.OrdinalIgnoreCase)));
                        equipmentCategory1 = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals(item.Category))) ?? equipmentCategory2;
                        break;
                }
                if (equipmentCategory1 == null)
                    Logger.Warning($"unable to add {item} with category: {item.Category} to non-existing category");
                else
                    equipmentCategory1.Items.Add((ElementBase)item);
            }
            this.Inventory.Items.CollectionChanged += new NotifyCollectionChangedEventHandler(this.EquipmentItems_CollectionChanged);
            Logger.Warning($"{this._items.Count} items loaded");
            this.SelectedEquipmentCategory = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>();
            this.Inventory.Coins.PropertyChanged += new PropertyChangedEventHandler(this.Coinage_PropertyChanged);
            this.Columns.IsItemcardsColumnVisible = this.Settings.IncludeItemcards;
            this.ShowInventoryItemCardColumn = this.Settings.IncludeItemcards;
            this.Manager.SourcesManager.SourceRestrictionsApplied += new EventHandler(this.SourceRestrictionsApplied);
            this.SubscribeWithEventAggregator();
        }
    }

    private void ItemFilter_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (!e.PropertyName.Equals("Name") && !e.PropertyName.Equals("Source"))
            return;
        if (string.IsNullOrWhiteSpace(this.ItemFilter.Name) && this.ItemFilter.Source != null && this.ItemFilter.Source.Equals("--"))
            this.UpdateItemsCollections();
        else
            this.UpdateItemsCollections(true);
    }

    private void InitializeEquipmentCategories()
    {
        this.EquipmentCategories.Add(new EquipmentCategory("Adventuring Gear"));
        this.EquipmentCategories.Add(new EquipmentCategory("Treasure"));
        this.EquipmentCategories.Add(new EquipmentCategory("Equipment Packs"));
        this.EquipmentCategories.Add(new EquipmentCategory("Tools"));
        this.EquipmentCategories.Add(new EquipmentCategory("Musical Instruments"));
        this.EquipmentCategories.Add(new EquipmentCategory("Armor"));
        this.EquipmentCategories.Add(new EquipmentCategory("Magic Armor"));
        this.EquipmentCategories.Add(new EquipmentCategory("Weapons"));
        this.EquipmentCategories.Add(new EquipmentCategory("Magic Weapons"));
        this.EquipmentCategories.Add(new EquipmentCategory("Ammunition"));
        this.EquipmentCategories.Add(new EquipmentCategory("Spellcasting Focus"));
        this.EquipmentCategories.Add(new EquipmentCategory("Wondrous Items"));
        this.EquipmentCategories.Add(new EquipmentCategory("Artificer Infusions"));
        this.EquipmentCategories.Add(new EquipmentCategory("Supernatural Gifts"));
        this.EquipmentCategories.Add(new EquipmentCategory("Staffs"));
        this.EquipmentCategories.Add(new EquipmentCategory("Rods"));
        this.EquipmentCategories.Add(new EquipmentCategory("Wands"));
        this.EquipmentCategories.Add(new EquipmentCategory("Rings"));
        this.EquipmentCategories.Add(new EquipmentCategory("Potions"));
        this.EquipmentCategories.Add(new EquipmentCategory("Poison"));
        this.EquipmentCategories.Add(new EquipmentCategory("Scrolls"));
        this.EquipmentCategories.Add(new EquipmentCategory("Spell Scrolls"));
        foreach (string displayName in (IEnumerable<string>)this._items.Cast<Item>().Where<Item>((Func<Item, bool>)(x => !x.Category.StartsWith("Additional "))).Select<Item, string>((Func<Item, string>)(x => x.Category)).Distinct<string>().OrderBy<string, string>((Func<string, string>)(x => x)))
        {
            if (!string.IsNullOrWhiteSpace(displayName) && !this.EquipmentCategories.Select<EquipmentCategory, string>((Func<EquipmentCategory, string>)(x => x.DisplayName)).Contains<string>(displayName))
                this.EquipmentCategories.Add(new EquipmentCategory(displayName));
        }
        foreach (string displayName in this._items.Cast<Item>().Where<Item>((Func<Item, bool>)(x => x.Category.StartsWith("Additional "))).Select<Item, string>((Func<Item, string>)(x => x.Category)).Distinct<string>())
        {
            if (!string.IsNullOrWhiteSpace(displayName) && !this.EquipmentCategories.Select<EquipmentCategory, string>((Func<EquipmentCategory, string>)(x => x.DisplayName)).Contains<string>(displayName))
                this.EquipmentCategories.Add(new EquipmentCategory(displayName));
        }
    }

    public ObservableCollection<EquipmentCategory> EquipmentCategories { get; } = new ObservableCollection<EquipmentCategory>();

    public ObservableCollection<ArmorElement> ArmorCollection { get; } = new ObservableCollection<ArmorElement>();

    public ObservableCollection<WeaponElement> WeaponsCollection { get; } = new ObservableCollection<WeaponElement>();

    public ElementBaseCollection ItemsCollection { get; } = new ElementBaseCollection();

    public bool IsArmorSelection
    {
        get => this._isArmorSelection;
        set => this.SetProperty<bool>(ref this._isArmorSelection, value, nameof(IsArmorSelection));
    }

    public bool IsWeaponSelection
    {
        get => this._isWeaponSelection;
        set => this.SetProperty<bool>(ref this._isWeaponSelection, value, nameof(IsWeaponSelection));
    }

    public EquipmentCategory SelectedEquipmentCategory
    {
        get => this._selectedEquipmentCategory;
        set
        {
            this.SetProperty<EquipmentCategory>(ref this._selectedEquipmentCategory, value, nameof(SelectedEquipmentCategory));
            if (this._selectedEquipmentCategory == null)
                return;
            this.IsArmorSelection = this._selectedEquipmentCategory.DisplayName.Equals("Magic Armor");
            this.IsWeaponSelection = this._selectedEquipmentCategory.DisplayName.Equals("Magic Weapons");
            this.UpdateItemsCollections();
        }
    }

    public ArmorElement SelectedArmorElement
    {
        get => this._selectedArmorElement;
        set
        {
            this.SetProperty<ArmorElement>(ref this._selectedArmorElement, value, nameof(SelectedArmorElement));
            if (this._selectedArmorElement == null)
                return;
            this.EventAggregator.Send<EquipmentElementDescriptionDisplayRequestEvent>(new EquipmentElementDescriptionDisplayRequestEvent((ElementBase)this._selectedArmorElement));
            this.UpdateItemsCollections();
        }
    }

    public WeaponElement SelectedWeaponElement
    {
        get => this._selectedWeaponElement;
        set
        {
            this.SetProperty<WeaponElement>(ref this._selectedWeaponElement, value, nameof(SelectedWeaponElement));
            if (this._selectedWeaponElement == null)
                return;
            this.EventAggregator.Send<EquipmentElementDescriptionDisplayRequestEvent>(new EquipmentElementDescriptionDisplayRequestEvent((ElementBase)this._selectedWeaponElement));
            this.UpdateItemsCollections();
        }
    }

    public ElementBase SelectedItem
    {
        get => this._selectedItem;
        set
        {
            this.SetProperty<ElementBase>(ref this._selectedItem, value, nameof(SelectedItem));
            if (this.IsArmorSelection)
            {
                IEventAggregator eventAggregator = this.EventAggregator;
                EquipmentElementDescriptionDisplayRequestEvent args = new EquipmentElementDescriptionDisplayRequestEvent(this._selectedItem, (ElementBase)this._selectedArmorElement);
                args.IgnoreGeneratedDescription = true;
                eventAggregator.Send<EquipmentElementDescriptionDisplayRequestEvent>(args);
            }
            else if (this.IsWeaponSelection)
            {
                IEventAggregator eventAggregator = this.EventAggregator;
                EquipmentElementDescriptionDisplayRequestEvent args = new EquipmentElementDescriptionDisplayRequestEvent(this._selectedItem, (ElementBase)this._selectedWeaponElement);
                args.IgnoreGeneratedDescription = true;
                eventAggregator.Send<EquipmentElementDescriptionDisplayRequestEvent>(args);
            }
            else
                this.EventAggregator.Send<EquipmentElementDescriptionDisplayRequestEvent>(new EquipmentElementDescriptionDisplayRequestEvent(this._selectedItem));
            this.AddSelectedItemCommand.RaiseCanExecuteChanged();
            this.RaiseEquipmentItemCommands();
        }
    }

    public int BuyAmount
    {
        get => this._buyAmount;
        set => this.SetProperty<int>(ref this._buyAmount, value, nameof(BuyAmount));
    }

    public int AddAmount
    {
        get => this._addAmount;
        set => this.SetProperty<int>(ref this._addAmount, value, nameof(AddAmount));
    }

    public string EquipmentWeight
    {
        get => this._equipmentWeight;
        set => this.SetProperty<string>(ref this._equipmentWeight, value, nameof(EquipmentWeight));
    }

    public RefactoredEquipmentItem SelectedEquipmentItem
    {
        get => this._selectedEquipmentItem;
        set
        {
            this.SetProperty<RefactoredEquipmentItem>(ref this._selectedEquipmentItem, value, nameof(SelectedEquipmentItem));
            if (this._selectedEquipmentItem != null)
                this.EventAggregator.Send<EquipmentElementDescriptionDisplayRequestEvent>(this._selectedEquipmentItem.IsAdorned ? new EquipmentElementDescriptionDisplayRequestEvent((ElementBase)this._selectedEquipmentItem.AdornerItem, (ElementBase)this._selectedEquipmentItem.Item) : new EquipmentElementDescriptionDisplayRequestEvent((ElementBase)this._selectedEquipmentItem.Item));
            this.RaiseEquipmentItemCommands();
            this.OnPropertyChanged("HasSelectedEquipmentItem");
        }
    }

    public bool HasSelectedEquipmentItem => this.SelectedEquipmentItem != null;

    private void Coinage_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        this.CalculateWeight();
    }

    private void RaiseEquipmentItemCommands()
    {
        this.AddSelectedItemCommand.RaiseCanExecuteChanged();
        this.BuySelectedItemCommand.RaiseCanExecuteChanged();
        this.DeleteSelectedEquipmentCommand.RaiseCanExecuteChanged();
        this.DeleteAllSelectedEquipmentCommand.RaiseCanExecuteChanged();
        this.MoveSelectedEquipmentItemUpCommand.RaiseCanExecuteChanged();
        this.MoveSelectedEquipmentItemDownCommand.RaiseCanExecuteChanged();
        this.ManageEquipmentItemCommand.RaiseCanExecuteChanged();
        this.ActivateEquipmentItemCommand.RaiseCanExecuteChanged();
        this.EquipEquipmentItemCommand.RaiseCanExecuteChanged();
        this.EquipPrimaryEquipmentItemCommand.RaiseCanExecuteChanged();
        this.EquipSecondaryEquipmentItemCommand.RaiseCanExecuteChanged();
        this.EquipVersatileEquipmentItemCommand.RaiseCanExecuteChanged();
        this.ToggleAttunementEquipmentItemCommand.RaiseCanExecuteChanged();
        this.AddToAttacksCommand.RaiseCanExecuteChanged();
        this.ExtractEquipmentItemCommand.RaiseCanExecuteChanged();
        this.AllowExtractEquipmentItem = this.CanExtractEquipmentItem();
        this.IncrementSelectedEquipmentItemAmountCommand.RaiseCanExecuteChanged();
        this.DecrementSelectedEquipmentItemAmountCommand.RaiseCanExecuteChanged();
        this.StoreSelectedEquipmentAsPrimaryCommand.RaiseCanExecuteChanged();
        this.StoreSelectedEquipmentAsSecondaryCommand.RaiseCanExecuteChanged();
        this.RetrieveStoredSelectedEquipmentCommand.RaiseCanExecuteChanged();
        this.AllowActivateEquipmentItem = this.CanActivateEquipmentItem();
        this.AllowEquipEquipmentItem = this.CanEquipEquipmentItem();
        this.AllowEquipPrimaryEquipmentItem = this.CanEquipPrimaryEquipmentItem();
        this.AllowEquipSecondaryEquipmentItem = this.CanEquipSecondaryEquipmentItem();
        this.AllowEquipVersatileEquipmentItem = this.CanEquipVersatileEquipmentItem();
        this.AllowToggleAttunementEquipmentItem = this.CanToggleAttunementEquipmentItem();
        this.AllowEquipmentItemAddToAttacks = this.CanAddToAttacks();
    }

    private Decimal CalculateWeight()
    {
        return this.CalculateWeight((IEnumerable<RefactoredEquipmentItem>)this.Inventory.Items);
    }

    private Decimal CalculateWeight(
      IEnumerable<RefactoredEquipmentItem> equipmentItems)
    {
        Decimal weight = this.Inventory.CalculateWeight(equipmentItems);
        this.EquipmentWeight = $"{weight}";
        return weight;
    }

    private int CalculateAttunementCount()
    {
        this.Inventory.CalculateAttunedItemCount();
        return this.Inventory.AttunedItemCount;
    }

    private void EquipmentItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.Manager.Status.IsLoaded)
            this.CalculateWeight();
        RefactoredEquipmentItem selectedEquipmentItem = this._selectedEquipmentItem;
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            if (e.NewItems.Count == 0)
                return;
            RefactoredEquipmentItem newItem = (RefactoredEquipmentItem)e.NewItems[0];
            if (!newItem.Item.IsEquippable && !newItem.IsActivated)
                return;
            if (newItem.IsActivated)
            {
                int num = newItem.IsEquipped ? 1 : 0;
                bool isAttuned = newItem.IsAttuned;
                string equippedLocation = newItem.EquippedLocation;
                newItem.Deactivate();
                this.SelectedEquipmentItem = newItem;
                if (num != 0)
                {
                    if (!string.IsNullOrWhiteSpace(equippedLocation))
                    {
                        switch (equippedLocation)
                        {
                            case "Armor":
                            case "body":
                                this.EquipEquipmentItem();
                                break;
                            case "One-Hand":
                            case "Primary":
                            case "Primary Hand":
                            case "mainhand":
                                this.EquipPrimaryEquipmentItem();
                                break;
                            case "Secondary":
                            case "Secondary Hand":
                            case "offhand":
                                this.EquipSecondaryEquipmentItem();
                                break;
                            case "Two-Handed":
                                this.EquipEquipmentItem();
                                break;
                            case "Two-Handed (Versatile)":
                                this.EquipVersatileEquipmentItem();
                                break;
                        }
                    }
                    else
                        this.EquipEquipmentItem();
                    if (isAttuned && !this.SelectedEquipmentItem.IsAttuned)
                        this.ToggleAttunementEquipmentItem();
                }
                else if (isAttuned)
                    this.ToggleAttunementEquipmentItem();
                this.SelectedEquipmentItem = selectedEquipmentItem;
                return;
            }
            if (this.Manager.Status.IsLoaded)
            {
                if (!newItem.Item.HasMultipleSlots)
                {
                    string slot = newItem.Item.Slot;
                }
                else
                    newItem.Item.Slots.First<string>();
                if (newItem.IsArmorTarget() && this.Inventory.EquippedArmor == null)
                {
                    this.SelectedEquipmentItem = newItem;
                    this.EquipEquipmentItem();
                }
                else if (newItem.IsTwoHandTarget() && this.Inventory.EquippedPrimary == null && this.Inventory.EquippedSecondary == null)
                {
                    this.SelectedEquipmentItem = newItem;
                    this.EquipEquipmentItem();
                }
                else if (newItem.IsOneHandTarget() && newItem.IsSecondaryTarget() && this.Inventory.EquippedSecondary == null)
                {
                    this.SelectedEquipmentItem = newItem;
                    this.EquipEquipmentItem();
                }
                else if (newItem.IsOneHandTarget() && this.Inventory.EquippedPrimary == null && !newItem.IsSecondaryTarget())
                {
                    this.SelectedEquipmentItem = newItem;
                    this.EquipEquipmentItem();
                }
                else if (newItem.IsArmorTarget() || newItem.IsOneHandTarget() || newItem.IsTwoHandTarget() || newItem.IsPrimaryTarget() || newItem.IsSecondaryTarget() || !newItem.IsAttunable || !this.Inventory.AllowMoreAttunement())
                    ;
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            if (e.OldItems.Count == 0)
                return;
            RefactoredEquipmentItem oldItem = (RefactoredEquipmentItem)e.OldItems[0];
            if (!oldItem.Item.IsEquippable)
                return;
            oldItem.Deactivate();
        }
        if (this.Manager.Status.IsLoaded)
            this.CalculateWeight();
        this.SelectedEquipmentItem = selectedEquipmentItem;
    }

    private void UpdateItemsCollections(bool filter = false)
    {
        this.ItemsCollection.Clear();
        this.Columns.CollapseColumns();
        if (this.IsArmorSelection)
        {
            if (this._selectedArmorElement == null)
                this._selectedArmorElement = this.ArmorCollection.First<ArmorElement>();
            foreach (ElementBase elementBase in (IEnumerable<ElementBase>)this._a.OrderBy<ElementBase, string>((Func<ElementBase, string>)(x => x.Name)))
            {
                if (elementBase.ElementSetters.ContainsSetter("armor"))
                {
                    string expressionString = elementBase.ElementSetters.GetSetter("armor").Value;
                    if (!string.IsNullOrWhiteSpace(expressionString) && this._expressionInterpreter.EvaluateSupportsExpression<ArmorElement>(expressionString, (IEnumerable<ArmorElement>)this.ArmorCollection).Contains<ArmorElement>(this.SelectedArmorElement))
                        this.ItemsCollection.Add(elementBase);
                }
            }
        }
        else if (this.IsWeaponSelection)
        {
            if (this._selectedWeaponElement == null)
                this._selectedWeaponElement = this.WeaponsCollection.First<WeaponElement>();
            foreach (ElementBase elementBase in (IEnumerable<ElementBase>)this._w.OrderBy<ElementBase, string>((Func<ElementBase, string>)(x => x.Name)))
            {
                if (elementBase.ElementSetters.ContainsSetter("weapon"))
                {
                    string expressionString = elementBase.ElementSetters.GetSetter("weapon").Value;
                    if (!string.IsNullOrWhiteSpace(expressionString) && this._expressionInterpreter.EvaluateSupportsExpression<WeaponElement>(expressionString, (IEnumerable<WeaponElement>)this.WeaponsCollection).Contains<WeaponElement>(this.SelectedWeaponElement))
                        this.ItemsCollection.Add(elementBase);
                }
            }
        }
        else
            this.ItemsCollection.AddRange((IEnumerable<ElementBase>)this._selectedEquipmentCategory.Items);
        if (filter || this.ItemFilter.IsLocked)
        {
            List<ElementBase> elements = this.ItemFilter.Filter((IEnumerable<ElementBase>)this.ItemsCollection);
            this.ItemsCollection.Clear();
            this.ItemsCollection.AddRange((IEnumerable<ElementBase>)elements);
        }
        SourcesManager sourcesManager = CharacterManager.Current.SourcesManager;
        List<string> list1 = sourcesManager.GetUndefinedRestrictedSourceNames().ToList<string>();
        List<string> list2 = sourcesManager.GetRestrictedElementIds().ToList<string>();
        ElementBaseCollection elementBaseCollection = new ElementBaseCollection();
        foreach (ElementBase items in (Collection<ElementBase>)this.ItemsCollection)
        {
            if (list2.Contains(items.Id))
                elementBaseCollection.Add(items);
            else if (list1.Contains(items.Source))
                elementBaseCollection.Add(items);
        }
        foreach (ElementBase elementBase in (Collection<ElementBase>)elementBaseCollection)
            this.ItemsCollection.Remove(elementBase);
        this.Columns.IsPriceColumnVisible = true;
        this.Columns.IsWeightColumnVisible = true;
        this.Columns.IsSourceColumnVisible = true;
        string displayName = this.SelectedEquipmentCategory.DisplayName;
        if (displayName == null)
            return;
        // ISSUE: reference to a compiler-generated method
        switch (displayName)
        {
            case "Rings":
            case "Wondrous Items":
                this.Columns.IsRarityColumnVisible = true;
                this.Columns.IsAttunementColumnVisible = true;
                return;
            case "Magic Weapons":
            case "Staffs":
            case "Rods":
            case "Wands":
                this.Columns.IsRarityColumnVisible = true;
                this.Columns.IsAttunementColumnVisible = true;
                return;
            case "Weapons":
                this.Columns.DisplayWeaponColumns();
                return;
            case "Armor":
                this.Columns.DisplayArmorColumns();
                return;
            case "Potions":
            case "Potion":
            case "Poison":
            case "Scrolls":
            case "Spell Scrolls":
                this.Columns.IsRarityColumnVisible = true;
                return;
            default:
                return;
        }
        this.Columns.IsRarityColumnVisible = true;
        this.Columns.IsAttunementColumnVisible = true;
        return;
    label_79:
        this.Columns.IsRarityColumnVisible = true;
        this.Columns.IsAttunementColumnVisible = true;
        return;
    label_80:
        this.Columns.IsRarityColumnVisible = true;
    }

    public void UpdateSubCollections()
    {
        ArmorElement selectedArmorElement = this._selectedArmorElement;
        List<ArmorElement> list1 = this.ArmorCollection.ToList<ArmorElement>();
        this.ArmorCollection.Clear();
        foreach (ArmorElement armorElement in list1)
            this.ArmorCollection.Add(armorElement);
        this._selectedArmorElement = selectedArmorElement;
        WeaponElement selectedWeaponElement = this._selectedWeaponElement;
        List<WeaponElement> list2 = this.WeaponsCollection.ToList<WeaponElement>();
        this.WeaponsCollection.Clear();
        foreach (WeaponElement weaponElement in list2)
            this.WeaponsCollection.Add(weaponElement);
        this._selectedWeaponElement = selectedWeaponElement;
    }

    public ElementFilter ItemFilter { get; }

    public RefactoredEquipmentSectionViewModel.ItemColumns Columns { get; } = new RefactoredEquipmentSectionViewModel.ItemColumns();

    public void OnHandleEvent(CharacterManagerElementRegistered args) => this.UpdateCollections();

    public void OnHandleEvent(CharacterManagerElementUnregistered args) => this.UpdateCollections();

    void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
    {
        this.Columns.IsItemcardsColumnVisible = args.Settings.IncludeItemcards;
        this.OnSettingsChanged();
    }

    public void OnSettingsChanged()
    {
        this.ShowInventoryItemCardColumn = this.Settings.IncludeItemcards;
    }

    public bool IsQuickEditEnabled
    {
        get => this._isQuickEditEnabled;
        set => this.SetProperty<bool>(ref this._isQuickEditEnabled, value, nameof(IsQuickEditEnabled));
    }

    public bool ShowInventoryItemCardColumn
    {
        get => this._showInventoryItemCardColumn;
        set
        {
            this.SetProperty<bool>(ref this._showInventoryItemCardColumn, value, nameof(ShowInventoryItemCardColumn));
        }
    }

    protected override void InitializeDesignData()
    {
        base.InitializeDesignData();
        this.AllowActivateEquipmentItem = true;
        this.AllowEquipEquipmentItem = true;
        this.AllowEquipPrimaryEquipmentItem = true;
        this.AllowEquipSecondaryEquipmentItem = true;
        this.AllowEquipVersatileEquipmentItem = true;
        this.AllowToggleAttunementEquipmentItem = true;
        this.EquipmentWeight = "1349 lb.";
        this.EquipmentCategories.Add(new EquipmentCategory("Adventuring Gear"));
        this.EquipmentCategories.Add(new EquipmentCategory("Armor"));
        this.EquipmentCategories.Add(new EquipmentCategory("Weapons"));
        this.EquipmentCategories.Add(new EquipmentCategory("Shields"));
        this.EquipmentCategories.Add(new EquipmentCategory("Trinkets"));
    }

    public void OnHandleEvent(CharacterLoadingCompletedEvent args) => this.UpdateCollections();

    public ICollectionView CategoriesCollectionView { get; set; }

    public ICollectionView WeaponElementsCollectionView { get; set; }

    public ICollectionView ArmorElementsCollectionView { get; set; }

    public ICollectionView ItemsCollectionView { get; set; }

    private void SourceRestrictionsApplied(object sender, EventArgs e)
    {
        this.UpdateCollections(true);
    }

    public void InitializeCollections()
    {
        this.CategoriesCollectionView = CollectionViewSource.GetDefaultView((object)this.EquipmentCategories);
        this.CategoriesCollectionView.Filter = (Predicate<object>)(item => item is EquipmentCategory equipmentCategory && equipmentCategory.IsEnabled);
        this.WeaponElementsCollectionView = CollectionViewSource.GetDefaultView((object)this.WeaponsCollection);
        this.ArmorElementsCollectionView = CollectionViewSource.GetDefaultView((object)this.ArmorCollection);
        this.InitializeCategoryCollection();
        this.InitializeWeaponCollection();
        this.InitializeArmorCollection();
    }

    private void InitializeCategoryCollection()
    {
        string selectedCategoryName = this._selectedEquipmentCategory?.DisplayName;
        this.EquipmentCategories.Clear();
        List<string> orderedCategories = new List<string>()
    {
      "Adventuring Gear",
      "Treasure",
      "Trade Goods",
      "Equipment Packs",
      "Tools",
      "Musical Instruments",
      "Armor",
      "Magic Armor",
      "Weapons",
      "Magic Weapons",
      "Ammunition",
      "Spellcasting Focus",
      "Wondrous Items",
      "Supernatural Gifts",
      "Staffs",
      "Rods",
      "Wands",
      "Rings",
      "Potions",
      "Poison",
      "Scrolls",
      "Spell Scrolls"
    };
        List<string> list = this._items.Cast<Item>().Select<Item, string>((Func<Item, string>)(x => x.Category)).Distinct<string>().ToList<string>();
        list.RemoveAll((Predicate<string>)(cat => orderedCategories.Contains(cat)));
        foreach (string str in list.Where<string>((Func<string, bool>)(x => !x.StartsWith("Additional "))))
        {
            if (!string.IsNullOrWhiteSpace(str) && !orderedCategories.Contains(str))
                orderedCategories.Add(str);
        }
        list.RemoveAll((Predicate<string>)(cat => orderedCategories.Contains(cat)));
        foreach (string str in list.Where<string>((Func<string, bool>)(x => x.StartsWith("Additional "))))
        {
            if (!string.IsNullOrWhiteSpace(str) && !orderedCategories.Contains(str))
                orderedCategories.Add(str);
        }
        list.RemoveAll((Predicate<string>)(cat => orderedCategories.Contains(cat)));
        foreach (string str in list)
        {
            if (!string.IsNullOrWhiteSpace(str) && !orderedCategories.Contains(str))
                orderedCategories.Add(str);
        }
        foreach (string displayName in orderedCategories)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                this.EquipmentCategories.Add(new EquipmentCategory(displayName));
        }
        this.SelectedEquipmentCategory = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals(selectedCategoryName)));
    }

    private void InitializeWeaponCollection()
    {
        EquipmentCategory equipmentCategory = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals("Weapons")));
        this._baseWeapons = this._items.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Weapon")).Cast<WeaponElement>().OrderBy<WeaponElement, string>((Func<WeaponElement, string>)(x => x.Name)).ToList<WeaponElement>();
        foreach (WeaponElement baseWeapon in this._baseWeapons)
        {
            this.WeaponsCollection.Add(baseWeapon);
            equipmentCategory.Items.Add((ElementBase)baseWeapon);
        }
        this._w.AddRange((IEnumerable<ElementBase>)this._items.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Magic Item")).Where<ElementBase>((Func<ElementBase, bool>)(x => x.ElementSetters.ContainsSetter("type") && x.ElementSetters.GetSetter("type").Value.Equals("weapon", StringComparison.OrdinalIgnoreCase))).ToList<ElementBase>());
    }

    private void InitializeArmorCollection()
    {
        EquipmentCategory equipmentCategory = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals("Armor")));
        List<ArmorElement> list = this._items.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Armor")).Cast<ArmorElement>().OrderBy<ArmorElement, string>((Func<ArmorElement, string>)(x => x.Name)).ToList<ArmorElement>();
        IEnumerable<ArmorElement> collection1 = list.Where<ArmorElement>((Func<ArmorElement, bool>)(x => x.Supports.Contains(InternalArmorGroups.Light)));
        IEnumerable<ArmorElement> collection2 = list.Where<ArmorElement>((Func<ArmorElement, bool>)(x => x.Supports.Contains(InternalArmorGroups.Medium)));
        IEnumerable<ArmorElement> collection3 = list.Where<ArmorElement>((Func<ArmorElement, bool>)(x => x.Supports.Contains(InternalArmorGroups.Heavy)));
        IEnumerable<ArmorElement> collection4 = list.Where<ArmorElement>((Func<ArmorElement, bool>)(x => x.Supports.Contains(InternalArmorGroups.Shield)));
        this._baseArmors.AddRange(collection1);
        this._baseArmors.AddRange(collection2);
        this._baseArmors.AddRange(collection3);
        this._baseArmors.AddRange(collection4);
        foreach (ArmorElement baseArmor in this._baseArmors)
            this.ArmorCollection.Add(baseArmor);
        equipmentCategory.Items.AddRange((IEnumerable<ElementBase>)this.ArmorCollection);
        this._a.AddRange((IEnumerable<ElementBase>)this._items.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Magic Item")).Where<ElementBase>((Func<ElementBase, bool>)(x => x.ElementSetters.ContainsSetter("type") && x.ElementSetters.GetSetter("type").Value.Equals("armor", StringComparison.OrdinalIgnoreCase))).ToList<ElementBase>());
    }

    public async Task UpdateCollections(bool updateItemsCollection = false)
    {
        RefactoredEquipmentSectionViewModel sectionViewModel = this;
        await Task.Run(new Action(sectionViewModel.UpdateCategoryCollection));
        sectionViewModel.CategoriesCollectionView.Refresh();
        sectionViewModel.UpdateWeaponCollection();
        sectionViewModel.WeaponElementsCollectionView.Refresh();
        sectionViewModel.UpdateArmorCollection();
        sectionViewModel.ArmorElementsCollectionView.Refresh();
        sectionViewModel.UpdateItemFilterSources();
        if (!updateItemsCollection)
            return;
        sectionViewModel.UpdateItemsCollections();
    }

    public void UpdateCategoryCollection()
    {
        if (!this.Manager.Status.IsLoaded)
            return;
        List<string> list = this.Manager.GetSpellcastingInformations().Select<SpellcastingInformation, string>((Func<SpellcastingInformation, string>)(x => $"Additional {x.Name} Spell")).ToList<string>();
        foreach (EquipmentCategory equipmentCategory in (Collection<EquipmentCategory>)this.EquipmentCategories)
        {
            equipmentCategory.IsEnabled = true;
            if (equipmentCategory.DisplayName.Contains("Additional ") && equipmentCategory.DisplayName.Contains(" Spell") && !equipmentCategory.DisplayName.Equals("Additional Spell"))
                equipmentCategory.IsEnabled = list.Contains(equipmentCategory.DisplayName);
        }
        IEnumerable<string> restricted = this.Manager.SourcesManager.RestrictedSources.Select<Builder.Presentation.Models.Sources.SourceItem, string>((Func<Builder.Presentation.Models.Sources.SourceItem, string>)(x => x.Source.Name));
        foreach (EquipmentCategory equipmentCategory in (Collection<EquipmentCategory>)this.EquipmentCategories)
        {
            if (equipmentCategory.Items.Select<ElementBase, string>((Func<ElementBase, string>)(x => x.Source)).Distinct<string>().All<string>((Func<string, bool>)(x => restricted.Contains<string>(x))))
                equipmentCategory.IsEnabled = false;
            int num = equipmentCategory.Items.Count<ElementBase>((Func<ElementBase, bool>)(x => !restricted.Contains<string>(x.Source)));
            equipmentCategory.EnabledItemCount = num;
        }
    }

    public void UpdateWeaponCollection()
    {
        List<string> list = this.Manager.SourcesManager.RestrictedSources.Select<Builder.Presentation.Models.Sources.SourceItem, string>((Func<Builder.Presentation.Models.Sources.SourceItem, string>)(x => x.Source.Name)).ToList<string>();
        WeaponElement selectedWeaponElement = this._selectedWeaponElement;
        this.WeaponsCollection.Clear();
        foreach (WeaponElement baseWeapon in this._baseWeapons)
        {
            if (!list.Contains(baseWeapon.Source))
                this.WeaponsCollection.Add(baseWeapon);
        }
        this._selectedWeaponElement = selectedWeaponElement;
    }

    public void UpdateArmorCollection()
    {
        List<string> list = this.Manager.SourcesManager.RestrictedSources.Select<Builder.Presentation.Models.Sources.SourceItem, string>((Func<Builder.Presentation.Models.Sources.SourceItem, string>)(x => x.Source.Name)).ToList<string>();
        ArmorElement selectedArmorElement = this._selectedArmorElement;
        this.ArmorCollection.Clear();
        foreach (ArmorElement baseArmor in this._baseArmors)
        {
            if (!list.Contains(baseArmor.Source))
                this.ArmorCollection.Add(baseArmor);
        }
        this._selectedArmorElement = selectedArmorElement;
    }

    public void UpdateItemFilterSources()
    {
        List<string> list = this._items.Select<ElementBase, string>((Func<ElementBase, string>)(x => x.Source)).Distinct<string>().OrderBy<string, string>((Func<string, string>)(x => x)).ToList<string>();
        List<string> restricted = this.Manager.SourcesManager.RestrictedSources.Select<Builder.Presentation.Models.Sources.SourceItem, string>((Func<Builder.Presentation.Models.Sources.SourceItem, string>)(x => x.Source.Name)).ToList<string>();
        list.RemoveAll((Predicate<string>)(x => restricted.Contains(x)));
        list.Insert(0, "--");
        this.ItemFilter.SourceCollection.Clear();
        foreach (string str in list)
            this.ItemFilter.SourceCollection.Add(str);
        this.ItemFilter.Clear();
    }

    public void UpdateItemsCollection()
    {
        foreach (Item obj in (IEnumerable<Item>)this._items.Cast<Item>().OrderBy<Item, string>((Func<Item, string>)(x => x.Name)))
        {
            Item item = obj;
            EquipmentCategory equipmentCategory1 = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>();
            switch (item.Type)
            {
                case "Item":
                    equipmentCategory1 = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals(item.Category))) ?? equipmentCategory1;
                    break;
                case "Armor":
                case "Weapon":
                    continue;
                case "Magic Item":
                    EquipmentCategory equipmentCategory2 = this.EquipmentCategories.First<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals("Wondrous Items", StringComparison.OrdinalIgnoreCase)));
                    equipmentCategory1 = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals(item.Category))) ?? equipmentCategory2;
                    break;
            }
            if (equipmentCategory1 == null)
                Logger.Warning($"unable to add {item} with category: {item.Category} to non-existing category");
            else
                equipmentCategory1.Items.Add((ElementBase)item);
        }
        EquipmentCategory equipmentCategory3 = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals("Armor")));
        EquipmentCategory equipmentCategory4 = this.EquipmentCategories.FirstOrDefault<EquipmentCategory>((Func<EquipmentCategory, bool>)(x => x.DisplayName.Equals("Weapons")));
        List<ArmorElement> list = this._items.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Armor")).Cast<ArmorElement>().ToList<ArmorElement>();
        IEnumerable<ArmorElement> armorElements1 = list.Where<ArmorElement>((Func<ArmorElement, bool>)(x => x.Supports.Contains(InternalArmorGroups.Light)));
        IEnumerable<ArmorElement> armorElements2 = list.Where<ArmorElement>((Func<ArmorElement, bool>)(x => x.Supports.Contains(InternalArmorGroups.Medium)));
        IEnumerable<ArmorElement> armorElements3 = list.Where<ArmorElement>((Func<ArmorElement, bool>)(x => x.Supports.Contains(InternalArmorGroups.Heavy)));
        IEnumerable<ArmorElement> armorElements4 = list.Where<ArmorElement>((Func<ArmorElement, bool>)(x => x.Supports.Contains(InternalArmorGroups.Shield)));
        foreach (ArmorElement armorElement in armorElements1)
            this.ArmorCollection.Add(armorElement);
        foreach (ArmorElement armorElement in armorElements2)
            this.ArmorCollection.Add(armorElement);
        foreach (ArmorElement armorElement in armorElements3)
            this.ArmorCollection.Add(armorElement);
        foreach (ArmorElement armorElement in armorElements4)
            this.ArmorCollection.Add(armorElement);
        foreach (ArmorElement armorElement in list)
        {
            if (!this.ArmorCollection.Contains(armorElement))
                this.ArmorCollection.Add(armorElement);
        }
        equipmentCategory3.Items.AddRange((IEnumerable<ElementBase>)this.ArmorCollection);
        foreach (WeaponElement weaponElement in this._items.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Weapon")).Cast<WeaponElement>().OrderBy<WeaponElement, string>((Func<WeaponElement, string>)(x => x.Name)).ToList<WeaponElement>())
        {
            this.WeaponsCollection.Add(weaponElement);
            equipmentCategory4.Items.Add((ElementBase)weaponElement);
        }
        this._a.AddRange((IEnumerable<ElementBase>)this._items.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Magic Item")).Where<ElementBase>((Func<ElementBase, bool>)(x => x.ElementSetters.ContainsSetter("type") && x.ElementSetters.GetSetter("type").Value.Equals("armor", StringComparison.OrdinalIgnoreCase))).ToList<ElementBase>());
        this._w.AddRange((IEnumerable<ElementBase>)this._items.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Magic Item")).Where<ElementBase>((Func<ElementBase, bool>)(x => x.ElementSetters.ContainsSetter("type") && x.ElementSetters.GetSetter("type").Value.Equals("weapon", StringComparison.OrdinalIgnoreCase))).ToList<ElementBase>());
    }

    public class ItemColumns : ObservableObject
    {
        private bool _isPriceColumnVisible;
        private bool _isWeightColumnVisible;
        private bool _isWeaponDamageColumnVisible;
        private bool _isWeaponRangeColumnVisible;
        private bool _isWeaponCategoryVisible;
        private bool _isWeaponGroupVisible;
        private bool _isWeaponPropertiesVisible;
        private bool _isRarityColumnVisible;
        private bool _isAttunementColumnVisible;
        private bool _isSourceColumnVisible;
        private bool _isArmorClassColumnVisible;
        private bool _isArmorStrengthColumnVisible;
        private bool _isArmorStealthColumnVisible;
        private bool _isArmorGroupColumnVisible;
        private bool _isItemcardsColumnVisible;

        public ItemColumns() => this.Initialize();

        public bool IsPriceColumnVisible
        {
            get => this._isPriceColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isPriceColumnVisible, value, nameof(IsPriceColumnVisible));
            }
        }

        public bool IsWeightColumnVisible
        {
            get => this._isWeightColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isWeightColumnVisible, value, nameof(IsWeightColumnVisible));
            }
        }

        public bool IsRarityColumnVisible
        {
            get => this._isRarityColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isRarityColumnVisible, value, nameof(IsRarityColumnVisible));
            }
        }

        public bool IsAttunementColumnVisible
        {
            get => this._isAttunementColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isAttunementColumnVisible, value, nameof(IsAttunementColumnVisible));
            }
        }

        public bool IsArmorClassColumnVisible
        {
            get => this._isArmorClassColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isArmorClassColumnVisible, value, nameof(IsArmorClassColumnVisible));
            }
        }

        public bool IsArmorStrengthColumnVisible
        {
            get => this._isArmorStrengthColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isArmorStrengthColumnVisible, value, nameof(IsArmorStrengthColumnVisible));
            }
        }

        public bool IsArmorStealthColumnVisible
        {
            get => this._isArmorStealthColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isArmorStealthColumnVisible, value, nameof(IsArmorStealthColumnVisible));
            }
        }

        public bool IsArmorGroupColumnVisible
        {
            get => this._isArmorGroupColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isArmorGroupColumnVisible, value, nameof(IsArmorGroupColumnVisible));
            }
        }

        public bool IsWeaponDamageColumnVisible
        {
            get => this._isWeaponDamageColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isWeaponDamageColumnVisible, value, nameof(IsWeaponDamageColumnVisible));
            }
        }

        public bool IsWeaponRangeColumnVisible
        {
            get => this._isWeaponRangeColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isWeaponRangeColumnVisible, value, nameof(IsWeaponRangeColumnVisible));
            }
        }

        public bool IsWeaponCategoryVisible
        {
            get => this._isWeaponCategoryVisible;
            set
            {
                this.SetProperty<bool>(ref this._isWeaponCategoryVisible, value, nameof(IsWeaponCategoryVisible));
            }
        }

        public bool IsWeaponGroupVisible
        {
            get => this._isWeaponGroupVisible;
            set
            {
                this.SetProperty<bool>(ref this._isWeaponGroupVisible, value, nameof(IsWeaponGroupVisible));
            }
        }

        public bool IsWeaponPropertiesVisible
        {
            get => this._isWeaponPropertiesVisible;
            set
            {
                this.SetProperty<bool>(ref this._isWeaponPropertiesVisible, value, nameof(IsWeaponPropertiesVisible));
            }
        }

        public bool IsSourceColumnVisible
        {
            get => this._isSourceColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isSourceColumnVisible, value, nameof(IsSourceColumnVisible));
            }
        }

        public bool IsItemcardsColumnVisible
        {
            get => this._isItemcardsColumnVisible;
            set
            {
                this.SetProperty<bool>(ref this._isItemcardsColumnVisible, value, nameof(IsItemcardsColumnVisible));
            }
        }

        public void Initialize()
        {
            this.IsPriceColumnVisible = true;
            this.IsWeightColumnVisible = true;
            this.IsRarityColumnVisible = false;
            this.IsAttunementColumnVisible = false;
            this.DisplayWeaponColumns(false);
            this.DisplayArmorColumns(false);
            this.IsSourceColumnVisible = true;
        }

        public void DisplayWeaponColumns(bool display = true)
        {
            this.IsWeaponDamageColumnVisible = display;
            this.IsWeaponRangeColumnVisible = display;
            this.IsWeaponCategoryVisible = display;
            this.IsWeaponGroupVisible = display;
            this.IsWeaponPropertiesVisible = display;
        }

        public void DisplayArmorColumns(bool display = true)
        {
            this.IsArmorClassColumnVisible = display;
            this.IsArmorStrengthColumnVisible = display;
            this.IsArmorStealthColumnVisible = display;
            this.IsArmorGroupColumnVisible = display;
        }

        public void CollapseColumns()
        {
            this.IsPriceColumnVisible = false;
            this.IsWeightColumnVisible = false;
            this.IsRarityColumnVisible = false;
            this.IsAttunementColumnVisible = false;
            this.DisplayWeaponColumns(false);
            this.DisplayArmorColumns(false);
            this.IsSourceColumnVisible = false;
        }
    }
}
