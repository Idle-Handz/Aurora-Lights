// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Dialogs.ExceptionWindow
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
using Builder.Presentation.Properties;
namespace Builder.Presentation.Views.Dialogs;

public partial class ExceptionWindow : MetroWindow
{
  private readonly string _title;
  private readonly string _error;
  private readonly string _hint;
  private readonly string _message;
  private readonly string _version;
  public ExceptionWindow(string title, string error, string hint, string message)
  {
    this.InitializeComponent();
    this._title = title;
    this._error = error;
    this._hint = hint;
    this._message = message;
    this._version = Builder.Presentation.Properties.Resources.ApplicationVersion;
    this.Populate();
  }

  private void Populate()
  {
    this.Title = this._title;
    this.PrimaryTextBlock.Text = this._error;
    this.SecondaryTextBlock.Text = this._hint;
    this.MessageTextBox.Text = this._message;
    this.VersionTextBlock.Text = "Version " + this._version;
  }

  private void ExceptionWindow_OnLoaded(object sender, RoutedEventArgs e) => this.ApplyTheme();

  private void OnCancelClicked(object sender, RoutedEventArgs e) => this.Close();





}
