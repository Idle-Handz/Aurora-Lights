// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.DeveloperWindow
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Presentation.Views.Development;
using Builder.Presentation.Views.Dialogs;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using TheArtOfDev.HtmlRenderer.WPF;

#nullable disable
namespace Builder.Presentation.Views;

public partial class DeveloperWindow : MetroWindow
{
  public DeveloperWindow() => this.InitializeComponent();

  private void BrowseHomepageClick(object sender, RoutedEventArgs e)
  {
    Process.Start("http://www.dnd-builder.com/development");
  }

  private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
  {
  }

  private void OpenInformationWindow(object sender, RoutedEventArgs e)
  {
    new ElementInformationWindow().Show();
  }

  private void OpenSettingsWindow(object sender, RoutedEventArgs e) => new SettingsWindow().Show();

  private void OpenIntroductionWindow(object sender, RoutedEventArgs e)
  {
  }

  private void OpenSplash(object sender, RoutedEventArgs e) => new Splash().Show();

  private void Browser_MouseWheel(object sender, MouseWheelEventArgs e)
  {
  }

  private void BrowseTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
  {
  }

  private async void OpenEntry(object sender, RoutedEventArgs e)
  {
    Logger.Info(await new SingleEntryWindow().Show(new EntryInitializationArguments("Character Builder", "Personality Trait", "")));
  }

  private void OpenSupportTestWindow(object sender, RoutedEventArgs e)
  {
    new SupportStringTester().Show();
  }

  private void OpenCompareWindow(object sender, RoutedEventArgs e)
  {
    new ElementCloneWindow().Show();
  }

  private void OpenLauncherWindow(object sender, RoutedEventArgs e) => new Launcher().Show();

  private void MenuItem_OnClick(object sender, RoutedEventArgs e) => new DeveloperTools().Show();




}
