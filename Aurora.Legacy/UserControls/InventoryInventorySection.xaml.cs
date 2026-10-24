// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.Inventory.InventorySection
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using Builder.Presentation.ViewModels.Shell.Items;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls.Inventory;

public partial class InventorySection : UserControl
{
  private RefactoredEquipmentSectionViewModel _viewModel;
  public InventorySection()
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.InventorySection_Loaded);
  }

  private void InventorySection_Loaded(object sender, RoutedEventArgs e)
  {
    this._viewModel = this.GetViewModel<RefactoredEquipmentSectionViewModel>();
    this._viewModel.PropertyChanged += new PropertyChangedEventHandler(this.InventorySection_PropertyChanged);
    this.ItemcardVisibleColumn.Visibility = this.GetVisibility(this._viewModel.ShowInventoryItemCardColumn);
  }

  private void InventorySection_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    string str = "ShowInventoryItemCardColumn";
    if (e.PropertyName.Equals(str))
      this.ItemcardVisibleColumn.Visibility = this.GetVisibility(this._viewModel.ShowInventoryItemCardColumn);
    e.PropertyName.Equals("ShowInventoryItemCardColumn");
  }

  private Visibility GetVisibility(bool visible)
  {
    return !visible ? Visibility.Collapsed : Visibility.Visible;
  }





}
