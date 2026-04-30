// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.BundleManagerSlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public partial class BundleManagerSlider : Flyout
{
  public BundleManagerSlider() => this.InitializeComponent();

  private void CloseButton_OnClick(object sender, RoutedEventArgs e) => this.IsOpen = false;

  private void ExpandersControlPreviewMouseWheel(object sender, MouseWheelEventArgs e)
  {
    if (e.Handled)
      return;
    e.Handled = true;
    MouseWheelEventArgs mouseWheelEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
    mouseWheelEventArgs.RoutedEvent = UIElement.MouseWheelEvent;
    mouseWheelEventArgs.Source = sender;
    MouseWheelEventArgs e1 = mouseWheelEventArgs;
    if (!(((FrameworkElement) sender).Parent is UIElement parent))
      return;
    // ISSUE: explicit non-virtual call
    parent.RaiseEvent(e1);
  }
}
