// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Controls.CharacterProfileButton
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Controls;

public class CharacterProfileButton : Control
{
  public static readonly DependencyProperty CharacterNameProperty = DependencyProperty.Register(nameof (CharacterName), typeof (string), typeof (CharacterProfileButton), new PropertyMetadata((object) null));
  public static readonly DependencyProperty CharacterBuildProperty = DependencyProperty.Register(nameof (CharacterBuild), typeof (string), typeof (CharacterProfileButton), new PropertyMetadata((object) null));
  public static readonly DependencyProperty CharacterImagePathProperty = DependencyProperty.Register(nameof (CharacterImagePath), typeof (string), typeof (CharacterProfileButton), new PropertyMetadata((object) null));
  public static readonly DependencyProperty PortraitScaleProperty = DependencyProperty.Register(nameof (PortraitScale), typeof (double), typeof (CharacterProfileButton), new PropertyMetadata((object) 0.0));
  public static readonly DependencyProperty NewCharacterCommandButtonVisibilityProperty = DependencyProperty.Register(nameof (NewCharacterCommandButtonVisibility), typeof (Visibility), typeof (CharacterProfileButton), new PropertyMetadata((object) Visibility.Visible));
  public static readonly DependencyProperty PortraitVisibilityProperty = DependencyProperty.Register(nameof (PortraitVisibility), typeof (Visibility), typeof (CharacterProfileButton), new PropertyMetadata((object) Visibility.Visible));
  public static readonly DependencyProperty NewCharacterCommandProperty = DependencyProperty.Register(nameof (NewCharacterCommand), typeof (ICommand), typeof (CharacterProfileButton), new PropertyMetadata((object) null));
  public static readonly DependencyProperty ImageSizeProperty = DependencyProperty.Register(nameof (ImageSize), typeof (double), typeof (CharacterProfileButton), new PropertyMetadata((object) 0.0));

  static CharacterProfileButton()
  {
    FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof (CharacterProfileButton), (PropertyMetadata) new FrameworkPropertyMetadata((object) typeof (CharacterProfileButton)));
  }

  protected override void OnMouseWheel(MouseWheelEventArgs e)
  {
    base.OnMouseWheel(e);
    if (e.Delta > 0)
    {
      Logger.Warning($"scrolling up? {this.PortraitScale}");
      if (this.PortraitScale >= 1.25)
        return;
      this.PortraitScale += 0.05;
    }
    else
    {
      Logger.Warning($"scrolling down? {this.PortraitScale}");
      if (this.PortraitScale <= 0.15)
        return;
      this.PortraitScale -= 0.05;
    }
  }

  public event EventHandler<RoutedEventArgs> Click;

  public event EventHandler<RoutedEventArgs> NewClick;

  public override void OnApplyTemplate()
  {
    (this.GetTemplateChild("ProfileButton") as Button).Click += (RoutedEventHandler) ((s, e) =>
    {
      EventHandler<RoutedEventArgs> click = this.Click;
      if (click == null)
        return;
      click(s, e);
    });
    (this.GetTemplateChild("NewCharacterButton") as Button).Click += (RoutedEventHandler) ((s, e) =>
    {
      EventHandler<RoutedEventArgs> newClick = this.NewClick;
      if (newClick == null)
        return;
      newClick(s, e);
    });
    this.PortraitScale = 0.25;
  }

  public string CharacterName
  {
    get => (string) this.GetValue(CharacterProfileButton.CharacterNameProperty);
    set => this.SetValue(CharacterProfileButton.CharacterNameProperty, (object) value);
  }

  public string CharacterBuild
  {
    get => (string) this.GetValue(CharacterProfileButton.CharacterBuildProperty);
    set => this.SetValue(CharacterProfileButton.CharacterBuildProperty, (object) value);
  }

  public string CharacterImagePath
  {
    get => (string) this.GetValue(CharacterProfileButton.CharacterImagePathProperty);
    set => this.SetValue(CharacterProfileButton.CharacterImagePathProperty, (object) value);
  }

  public double PortraitScale
  {
    get => (double) this.GetValue(CharacterProfileButton.PortraitScaleProperty);
    set => this.SetValue(CharacterProfileButton.PortraitScaleProperty, (object) value);
  }

  public Visibility NewCharacterCommandButtonVisibility
  {
    get
    {
      return (Visibility) this.GetValue(CharacterProfileButton.NewCharacterCommandButtonVisibilityProperty);
    }
    set
    {
      this.SetValue(CharacterProfileButton.NewCharacterCommandButtonVisibilityProperty, (object) value);
    }
  }

  public Visibility PortraitVisibility
  {
    get => (Visibility) this.GetValue(CharacterProfileButton.PortraitVisibilityProperty);
    set => this.SetValue(CharacterProfileButton.PortraitVisibilityProperty, (object) value);
  }

  public ICommand NewCharacterCommand
  {
    get => (ICommand) this.GetValue(CharacterProfileButton.NewCharacterCommandProperty);
    set => this.SetValue(CharacterProfileButton.NewCharacterCommandProperty, (object) value);
  }

  public double ImageSize
  {
    get => (double) this.GetValue(CharacterProfileButton.ImageSizeProperty);
    set => this.SetValue(CharacterProfileButton.ImageSizeProperty, (object) value);
  }
}
