// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Dialogs.ExceptionMessageWindow
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
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Dialogs;

public partial class ExceptionMessageWindow : MetroWindow
{
  public ExceptionMessageWindow(string title, string intro, string message)
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.ExceptionMessageWindow_Loaded);
    this.txtTitle.Text = title;
    this.txtIntroduction.Text = intro;
    this.txtMessage.Text = message;
  }

  private void ExceptionMessageWindow_Loaded(object sender, RoutedEventArgs e) => this.ApplyTheme();

  private void Button_Click(object sender, RoutedEventArgs e) => this.Close();




}
