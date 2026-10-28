// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.ManageCharacterOptionsSlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using Builder.Presentation.ViewModels.Base;
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

public partial class ManageCharacterOptionsSlider : Flyout
{
  public ManageCharacterOptionsSlider()
  {
    this.InitializeComponent();
    this.IsOpenChanged += new RoutedEventHandler(this.Page1_IsOpenChanged);
  }

  private async void Page1_IsOpenChanged(object sender, RoutedEventArgs e)
  {
    ManageCharacterOptionsSlider window = this;
    if (!window.IsOpen)
      return;
    await window.GetViewModel().InitializeAsync((InitializationArguments) null);
  }

  private void PageSaveClicked(object sender, RoutedEventArgs e) => this.IsOpen = false;

  private void PageCloseClicked(object sender, RoutedEventArgs e) => this.IsOpen = false;





}
