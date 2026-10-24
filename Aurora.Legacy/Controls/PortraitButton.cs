// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Controls.PortraitButton
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation.Controls;

public class PortraitButton : Button
{
  public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(nameof (ImageSource), typeof (ImageSource), typeof (PortraitButton), new PropertyMetadata((object) null));
  public static readonly DependencyProperty PortraitScaleProperty = DependencyProperty.Register(nameof (PortraitScale), typeof (double), typeof (PortraitButton), new PropertyMetadata((object) 0.0));
  public static readonly DependencyProperty PortraitSizeProperty = DependencyProperty.Register(nameof (PortraitSize), typeof (double), typeof (PortraitButton), new PropertyMetadata((object) 0.0));
  public static readonly DependencyProperty PortaitSizeProperty = DependencyProperty.Register(nameof (PortaitSize), typeof (double), typeof (PortraitButton), new PropertyMetadata((object) 0.0));
  public static readonly DependencyProperty PortraitBorderThicknessProperty = DependencyProperty.Register(nameof (PortraitBorderThickness), typeof (double), typeof (PortraitButton), new PropertyMetadata((object) 0.0));
  public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(nameof (Symbol), typeof (string), typeof (PortraitButton), new PropertyMetadata((object) null));
  public static readonly DependencyProperty PortraitStretchProperty = DependencyProperty.Register(nameof (PortraitStretch), typeof (Stretch), typeof (PortraitButton), new PropertyMetadata((object) Stretch.None));

  static PortraitButton()
  {
    FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof (PortraitButton), (PropertyMetadata) new FrameworkPropertyMetadata((object) typeof (PortraitButton)));
  }

  public override void OnApplyTemplate() => base.OnApplyTemplate();

  public ImageSource ImageSource
  {
    get => (ImageSource) this.GetValue(PortraitButton.ImageSourceProperty);
    set => this.SetValue(PortraitButton.ImageSourceProperty, (object) value);
  }

  public double PortraitScale
  {
    get => (double) this.GetValue(PortraitButton.PortraitScaleProperty);
    set => this.SetValue(PortraitButton.PortraitScaleProperty, (object) value);
  }

  public double PortraitSize
  {
    get => (double) this.GetValue(PortraitButton.PortraitSizeProperty);
    set => this.SetValue(PortraitButton.PortraitSizeProperty, (object) value);
  }

  public double PortaitSize
  {
    get => (double) this.GetValue(PortraitButton.PortaitSizeProperty);
    set => this.SetValue(PortraitButton.PortaitSizeProperty, (object) value);
  }

  public double PortraitBorderThickness
  {
    get => (double) this.GetValue(PortraitButton.PortraitBorderThicknessProperty);
    set => this.SetValue(PortraitButton.PortraitBorderThicknessProperty, (object) value);
  }

  public string Symbol
  {
    get => (string) this.GetValue(PortraitButton.SymbolProperty);
    set => this.SetValue(PortraitButton.SymbolProperty, (object) value);
  }

  public Stretch PortraitStretch
  {
    get => (Stretch) this.GetValue(PortraitButton.PortraitStretchProperty);
    set => this.SetValue(PortraitButton.PortraitStretchProperty, (object) value);
  }
}
