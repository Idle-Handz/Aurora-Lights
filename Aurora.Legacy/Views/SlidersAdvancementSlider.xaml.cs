// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.AdvancementSlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public partial class AdvancementSlider : Flyout
{
  public AdvancementSlider()
  {
    this.InitializeComponent();
    this.IsOpen = this.GetViewModel().IsInDesignMode;
    this.IsOpenChanged += new RoutedEventHandler(this.AdvancementSlider_IsOpenChanged);
    this.GetViewModel<AdvancementSliderViewModel>().CloseRequest += new EventHandler(this.AdvancementSlider_CloseRequest);
  }

  private void AdvancementSlider_CloseRequest(object sender, EventArgs e) => this.IsOpen = false;

  private void AdvancementSlider_IsOpenChanged(object sender, RoutedEventArgs e)
  {
    if (!this.IsOpen)
      return;
    this.GetViewModel().InitializeAsync();
  }

  private void ButtonBase_OnClick(object sender, RoutedEventArgs e) => this.IsOpen = false;





}
