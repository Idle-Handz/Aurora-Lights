// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.RoundButton
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

public partial class RoundButton : UserControl
{
  public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof (Size), typeof (int), typeof (RoundButton), new PropertyMetadata((object) 50));
  public static readonly DependencyProperty OverlaySymbolProperty = DependencyProperty.Register(nameof (OverlaySymbol), typeof (string), typeof (RoundButton), new PropertyMetadata((object) "E109"));
  public static readonly DependencyProperty OverlaySymbolSizeProperty = DependencyProperty.Register(nameof (OverlaySymbolSize), typeof (int), typeof (RoundButton), new PropertyMetadata((object) 12));
  public static readonly DependencyProperty ImagePathProperty = DependencyProperty.Register(nameof (ImagePath), typeof (string), typeof (RoundButton), new PropertyMetadata((object) null));
  public static readonly DependencyProperty ButtonCommandProperty = DependencyProperty.Register(nameof (ButtonCommand), typeof (ICommand), typeof (RoundButton), new PropertyMetadata((object) null));
  public static readonly DependencyProperty InnerSizeProperty = DependencyProperty.Register(nameof (InnerSize), typeof (int), typeof (RoundButton), new PropertyMetadata((object) 48 /*0x30*/));
  public static readonly DependencyProperty BorderSizeProperty = DependencyProperty.Register(nameof (BorderSize), typeof (int), typeof (RoundButton), new PropertyMetadata((object) 2));
  public RoundButton() => this.InitializeComponent();

  public int Size
  {
    get => (int) this.GetValue(RoundButton.SizeProperty);
    set
    {
      this.SetValue(RoundButton.SizeProperty, (object) value);
      this.InnerSize = this.Size - 2;
    }
  }

  public int InnerSize
  {
    get => (int) this.GetValue(RoundButton.InnerSizeProperty);
    set => this.SetValue(RoundButton.InnerSizeProperty, (object) value);
  }

  public int BorderSize
  {
    get => (int) this.GetValue(RoundButton.BorderSizeProperty);
    set => this.SetValue(RoundButton.BorderSizeProperty, (object) value);
  }

  public string OverlaySymbol
  {
    get => (string) this.GetValue(RoundButton.OverlaySymbolProperty);
    set => this.SetValue(RoundButton.OverlaySymbolProperty, (object) value);
  }

  public int OverlaySymbolSize
  {
    get => (int) this.GetValue(RoundButton.OverlaySymbolSizeProperty);
    set => this.SetValue(RoundButton.OverlaySymbolSizeProperty, (object) value);
  }

  public string ImagePath
  {
    get => (string) this.GetValue(RoundButton.ImagePathProperty);
    set => this.SetValue(RoundButton.ImagePathProperty, (object) value);
  }

  public ICommand ButtonCommand
  {
    get => (ICommand) this.GetValue(RoundButton.ButtonCommandProperty);
    set => this.SetValue(RoundButton.ButtonCommandProperty, (object) value);
  }




}
