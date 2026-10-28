// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Dialogs.ExtractItemWindow
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Extensions;
using Builder.Presentation.ViewModels.Base;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Dialogs;

public partial class ExtractItemWindow : MetroWindow
{
  private readonly Item _extractableItem;
  private readonly ElementBaseCollection _items;
  public ExtractItemWindow(Item extractableItem, ElementBaseCollection items)
  {
    this._extractableItem = extractableItem;
    this._items = items;
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.ExtractItemWindow_Loaded);
  }

  private async void ExtractItemWindow_Loaded(object sender, RoutedEventArgs e)
  {
    ExtractItemWindow window = this;
    window.ApplyTheme();
    object[] objArray = new object[2]
    {
      (object) window._extractableItem,
      (object) window._items
    };
    await window.GetViewModel().InitializeAsync(new InitializationArguments((object) objArray));
  }

  private void OnSaveClicked(object sender, RoutedEventArgs e)
  {
    this.DialogResult = new bool?(true);
  }

  private void OnCancelClicked(object sender, RoutedEventArgs e)
  {
    this.DialogResult = new bool?(false);
  }




}
