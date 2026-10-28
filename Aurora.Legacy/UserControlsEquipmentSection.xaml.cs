// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.EquipmentSection
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Extensions;
using Builder.Presentation.ViewModels.Shell.Items;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls;

public partial class EquipmentSection : 
  System.Windows.Controls.UserControl,
  ISubscriber<SettingsChangedEvent>
{
  private RefactoredEquipmentSectionViewModel _viewModel;
  public EquipmentSection()
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.EquipmentSection_Loaded);
  }

  private void EquipmentSection_Loaded(object sender, RoutedEventArgs e)
  {
    int expanderGridRowHeight = ApplicationManager.Current.Settings.GetSelectionExpanderGridRowHeight();
    this.CategoriesDataGrid.MinRowHeight = (double) expanderGridRowHeight;
    this.ArmorCollectionDataGrid.MinRowHeight = (double) expanderGridRowHeight;
    this.WeaponsCollectionDataGrid.MinRowHeight = (double) expanderGridRowHeight;
    this.SelectionElementsDataGrid.MinRowHeight = (double) expanderGridRowHeight;
    this.SelectionElementsDataGrid2.MinRowHeight = (double) expanderGridRowHeight;
    ApplicationManager.Current.EventAggregator.Subscribe((object) this);
    this._viewModel = this.GetViewModel<RefactoredEquipmentSectionViewModel>();
    this._viewModel.Columns.PropertyChanged += new PropertyChangedEventHandler(this.Columns_PropertyChanged);
  }

  private void Columns_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    this.DisplayColumn((DataGridColumn) this.ItemPriceColumn, this._viewModel.Columns.IsPriceColumnVisible);
    this.DisplayColumn((DataGridColumn) this.ItemWeightColumn, this._viewModel.Columns.IsWeightColumnVisible);
    this.DisplayColumn((DataGridColumn) this.attunementColumn, this._viewModel.Columns.IsAttunementColumnVisible);
    this.DisplayColumn((DataGridColumn) this.itemsRarityColumn, this._viewModel.Columns.IsRarityColumnVisible);
    this.DisplayColumn((DataGridColumn) this.ArmorClassColumn, this._viewModel.Columns.IsArmorClassColumnVisible);
    this.DisplayColumn((DataGridColumn) this.ArmorStealthColumn, this._viewModel.Columns.IsArmorStrengthColumnVisible);
    this.DisplayColumn((DataGridColumn) this.ArmorStrengthColumn, this._viewModel.Columns.IsArmorStrengthColumnVisible);
    this.DisplayColumn((DataGridColumn) this.ArmorGroupColumn, this._viewModel.Columns.IsArmorGroupColumnVisible);
    this.DisplayColumn((DataGridColumn) this.WeaponRangeColumn, this._viewModel.Columns.IsWeaponRangeColumnVisible);
    this.DisplayColumn((DataGridColumn) this.WeaponDamageColumn, this._viewModel.Columns.IsWeaponDamageColumnVisible);
    this.DisplayColumn((DataGridColumn) this.WeaponDamageColumn, this._viewModel.Columns.IsWeaponCategoryVisible);
    this.DisplayColumn((DataGridColumn) this.WeaponGroupColumn, this._viewModel.Columns.IsWeaponGroupVisible);
    this.DisplayColumn((DataGridColumn) this.WeaponPropertiesColumn, this._viewModel.Columns.IsWeaponPropertiesVisible);
    this.DisplayColumn((DataGridColumn) this.SourceColumn, this._viewModel.Columns.IsSourceColumnVisible);
    this.DisplayColumn((DataGridColumn) this.ItemcardVisibleColumn, this._viewModel.Columns.IsItemcardsColumnVisible);
  }

  private void DisplayColumn(DataGridColumn column, bool visible)
  {
    column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
  }

  private void SelectionElementsDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
  {
    this._viewModel.AddSelectedItemCommand.Execute((object) null);
  }

  private void SelectionElementsDataGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
  {
  }

  private void SelectionEquipmentItem_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
  {
    if (!this._viewModel.AllowActivateEquipmentItem)
      return;
    this._viewModel.ActivateEquipmentItemCommand.Execute((object) null);
  }

  private void SelectionEquipmentItem_OnPreviewKeyDown(object sender, KeyEventArgs e)
  {
  }

  private void SelectionElementsDataGrid2_OnKeyDown(object sender, KeyEventArgs e)
  {
    this.SelectionElementsDataGrid2.Focus();
  }

  private void SelectionElementsDataGrid2_OnKeyUp(object sender, KeyEventArgs e)
  {
  }

  void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
  {
    int expanderGridRowHeight = ApplicationManager.Current.Settings.GetSelectionExpanderGridRowHeight();
    this.CategoriesDataGrid.MinRowHeight = (double) expanderGridRowHeight;
    this.ArmorCollectionDataGrid.MinRowHeight = (double) expanderGridRowHeight;
    this.WeaponsCollectionDataGrid.MinRowHeight = (double) expanderGridRowHeight;
    this.SelectionElementsDataGrid.MinRowHeight = (double) expanderGridRowHeight;
    this.SelectionElementsDataGrid2.MinRowHeight = (double) expanderGridRowHeight;
  }






}
