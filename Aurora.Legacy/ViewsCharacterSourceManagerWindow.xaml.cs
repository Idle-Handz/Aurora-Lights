// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.CharacterSourceManagerWindow
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
using System.Windows.Navigation;

#nullable disable
namespace Builder.Presentation.Views;

public partial class CharacterSourceManagerWindow : MetroWindow
{
  public CharacterSourceManagerWindow()
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.CharacterSourceManagerWindow_Loaded);
  }

  private void CharacterSourceManagerWindow_Loaded(object sender, RoutedEventArgs e)
  {
    this.ApplyTheme();
  }

  private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
  {
    Process.Start(e.Uri.ToString());
  }




}
