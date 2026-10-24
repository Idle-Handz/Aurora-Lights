// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.PreferencesWindow
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using Builder.Presentation.ViewModels;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views;

public partial class PreferencesWindow : MetroWindow
{
  private bool _userSaved;
  public PreferencesWindow()
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.SettingsWindow_Loaded);
    this.Closing += new CancelEventHandler(this.SettingsWindow_Closing);
  }

  private void SettingsWindow_Loaded(object sender, RoutedEventArgs e) => this.ApplyTheme();

  private void SettingsWindow_Closing(object sender, CancelEventArgs e)
  {
    if (this._userSaved)
      return;
    this.GetViewModel<SettingsWindowViewModel>().CancelSettingsCommand.Execute((object) null);
  }

  private void OnSaveClicked(object sender, RoutedEventArgs e)
  {
    this._userSaved = true;
    this.Close();
  }

  private void OnCancelClicked(object sender, RoutedEventArgs e) => this.Close();




}
