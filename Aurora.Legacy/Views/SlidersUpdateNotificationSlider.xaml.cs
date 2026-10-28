// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.UpdateNotificationSlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.Views.Sliders;

public partial class UpdateNotificationSlider : Flyout
{
  public UpdateNotificationSlider()
  {
    this.InitializeComponent();
    this.IsOpenChanged += new RoutedEventHandler(this.Page1_IsOpenChanged);
  }

  private async void Page1_IsOpenChanged(object sender, RoutedEventArgs e)
  {
    int num = this.IsOpen ? 1 : 0;
  }

  private void PageSaveClicked(object sender, RoutedEventArgs e)
  {
    Process.Start(Builder.Presentation.Properties.Resources.ChangelogUrl);
  }

  private void PageCloseClicked(object sender, RoutedEventArgs e) => this.IsOpen = false;





}
