// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.ApplicationSettingsWindow
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Aurora.Presentation.Controls;
using Builder.Presentation.Extensions;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views;

public partial class ApplicationSettingsWindow : MetroWindow
{
  private readonly bool _jumptosheet;
  private bool _userSaved;
  public ApplicationSettingsWindow()
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.SettingsWindow_Loaded);
    this.Closing += new CancelEventHandler(this.SettingsWindow_Closing);
  }

  public ApplicationSettingsWindow(bool jumptosheet)
  {
    this._jumptosheet = jumptosheet;
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.SettingsWindow_Loaded);
    this.Closing += new CancelEventHandler(this.SettingsWindow_Closing);
  }

  private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
  {
    this.ApplyTheme();
    if (!this._jumptosheet)
      return;
    this.SettingsTabControl.SelectedIndex = 6;
  }

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

  private void OnBrowseGitHubClick(object sender, RoutedEventArgs e)
  {
    Process.Start("https://github.com/aurorabuilder");
  }

  private void BrowseRedditPageClick(object sender, RoutedEventArgs e)
  {
    Process.Start("https://www.reddit.com/r/aurorabuilder/");
  }

  private void BrowseDiscordChannelClick(object sender, RoutedEventArgs e)
  {
    Process.Start("https://discord.gg/MmWvNFV");
  }

  private void BrowsePatreonPageClick(object sender, RoutedEventArgs e)
  {
    Process.Start("https://www.patreon.com/aurorabuilder");
  }

  private void ClearNewsArticlesCache(object sender, RoutedEventArgs e)
  {
    string path = Path.Combine(Path.Combine(DataManager.Current.LocalAppDataRootDirectory, "syndication"), "posts.dat");
    if (File.Exists(path))
      File.Delete(path);
    this.ClearNewsCacheButton.IsEnabled = false;
  }





}
