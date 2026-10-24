// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.CharacterProfile
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls;

public partial class CharacterProfile : UserControl
{
  public CharacterProfile() => this.InitializeComponent();

  public event EventHandler<RoutedEventArgs> CharacterNameClick;

  public event EventHandler<RoutedEventArgs> NewCharacterClick;

  public event EventHandler<RoutedEventArgs> PortraitClick;

  public override void OnApplyTemplate()
  {
    base.OnApplyTemplate();
    this.CharacterNameButton.Click += (RoutedEventHandler) ((s, e) =>
    {
      EventHandler<RoutedEventArgs> characterNameClick = this.CharacterNameClick;
      if (characterNameClick == null)
        return;
      characterNameClick(s, e);
    });
    this.NewCharacterButton.Click += (RoutedEventHandler) ((s, e) =>
    {
      EventHandler<RoutedEventArgs> newCharacterClick = this.NewCharacterClick;
      if (newCharacterClick == null)
        return;
      newCharacterClick(s, e);
    });
    this.ProfileButton.Click += (RoutedEventHandler) ((s, e) =>
    {
      EventHandler<RoutedEventArgs> portraitClick = this.PortraitClick;
      if (portraitClick == null)
        return;
      portraitClick(s, e);
    });
  }
}



