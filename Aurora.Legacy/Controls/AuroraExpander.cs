// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Controls.AuroraExpander
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System.Windows;
using System.Windows.Controls;

#nullable disable
namespace Builder.Presentation.Controls;

public class AuroraExpander : Expander
{
  public static readonly DependencyProperty AdditionalHeaderContentProperty = DependencyProperty.Register(nameof (AdditionalHeaderContent), typeof (FrameworkElement), typeof (AuroraExpander), new PropertyMetadata((object) null));
  public static readonly DependencyProperty FooterContentProperty = DependencyProperty.Register(nameof (FooterContent), typeof (object), typeof (AuroraExpander), new PropertyMetadata((object) null));
  public static readonly DependencyProperty FooterVisibilityProperty = DependencyProperty.Register(nameof (FooterVisibility), typeof (Visibility), typeof (AuroraExpander), new PropertyMetadata((object) Visibility.Collapsed));
  public static readonly DependencyProperty AdditionalHeaderVisibilityProperty = DependencyProperty.Register(nameof (AdditionalHeaderVisibility), typeof (Visibility), typeof (AuroraExpander), new PropertyMetadata((object) Visibility.Visible));
  public static readonly DependencyProperty AdditionalHeaderContentTemplateProperty = DependencyProperty.Register(nameof (AdditionalHeaderContentTemplate), typeof (DataTemplate), typeof (AuroraExpander), new PropertyMetadata((object) null));

  static AuroraExpander()
  {
    FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof (AuroraExpander), (PropertyMetadata) new FrameworkPropertyMetadata((object) typeof (AuroraExpander)));
  }

  public FrameworkElement AdditionalHeaderContent
  {
    get => (FrameworkElement) this.GetValue(AuroraExpander.AdditionalHeaderContentProperty);
    set => this.SetValue(AuroraExpander.AdditionalHeaderContentProperty, (object) value);
  }

  public object FooterContent
  {
    get => this.GetValue(AuroraExpander.FooterContentProperty);
    set => this.SetValue(AuroraExpander.FooterContentProperty, value);
  }

  public Visibility FooterVisibility
  {
    get => (Visibility) this.GetValue(AuroraExpander.FooterVisibilityProperty);
    set => this.SetValue(AuroraExpander.FooterVisibilityProperty, (object) value);
  }

  public Visibility AdditionalHeaderVisibility
  {
    get => (Visibility) this.GetValue(AuroraExpander.AdditionalHeaderVisibilityProperty);
    set => this.SetValue(AuroraExpander.AdditionalHeaderVisibilityProperty, (object) value);
  }

  public DataTemplate AdditionalHeaderContentTemplate
  {
    get => (DataTemplate) this.GetValue(AuroraExpander.AdditionalHeaderContentTemplateProperty);
    set => this.SetValue(AuroraExpander.AdditionalHeaderContentTemplateProperty, (object) value);
  }
}
