// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Content.Manage.CreateAttackWindow
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
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Content.Manage;

public partial class CreateAttackWindow : MetroWindow
{
  public CreateAttackWindow()
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.CreateAttackWindow_Loaded);
  }

  private void CreateAttackWindow_Loaded(object sender, RoutedEventArgs e) => this.ApplyTheme();

  private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
  {
    this.DialogResult = new bool?(true);
    this.Close();
  }





}
