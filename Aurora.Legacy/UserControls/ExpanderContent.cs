// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.ExpanderContent
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

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

public partial class ExpanderContent : UserControl
{
  public ExpanderContent() => this.InitializeComponent();

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
    parent.RaiseEvent((RoutedEventArgs) e1);
  }
}
