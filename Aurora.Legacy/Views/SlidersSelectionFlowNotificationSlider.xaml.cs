// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.SelectionFlowNotificationSlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Services;
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

public partial class SelectionFlowNotificationSlider : Flyout
{
  public SelectionFlowNotificationSlider()
  {
    this.InitializeComponent();
    this.IsOpenChanged += new RoutedEventHandler(this.Page1_IsOpenChanged);
  }

  public SelectionRuleNavigationService SelectionRuleNavigationService { get; set; }

  private async void Page1_IsOpenChanged(object sender, RoutedEventArgs e)
  {
    int num = this.IsOpen ? 1 : 0;
  }

  private void NextSelectionClick(object sender, RoutedEventArgs e)
  {
    this.SelectionRuleNavigationService.NavigateNextCommand.Execute((object) null);
    this.IsOpen = false;
  }

  private void PageCloseClicked(object sender, RoutedEventArgs e)
  {
  }




}
