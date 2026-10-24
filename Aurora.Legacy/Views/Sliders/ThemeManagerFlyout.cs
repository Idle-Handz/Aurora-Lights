// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.ThemeManagerFlyout
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public partial class ThemeManagerFlyout : Flyout
{
  public ThemeManagerFlyout()
  {
    this.InitializeComponent();
    this.IsOpenChanged += new RoutedEventHandler(this.ThemeManagerFlyoutIsOpenChanged);
  }

  private void ThemeManagerFlyoutIsOpenChanged(object sender, RoutedEventArgs e)
  {
    Logger.Debug($"ThemeManagerFlyout_IsOpenChanged\t{this.IsOpen}");
    int num = this.IsOpen ? 1 : 0;
  }

  private void ThemeManagerFlyoutCloseClicked(object sender, RoutedEventArgs e)
  {
    this.IsOpen = false;
  }





}
