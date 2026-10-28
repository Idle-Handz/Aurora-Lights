// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.DeveloperTools
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
using System.Windows.Media;
using TheArtOfDev.HtmlRenderer.WPF;

#nullable disable
namespace Builder.Presentation.Views.Development;

public partial class DeveloperTools : MetroWindow
{
  public DeveloperTools()
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.DeveloperTools_Loaded);
  }

  private void DeveloperTools_Loaded(object sender, RoutedEventArgs e) => this.ApplyTheme();

  private void ExitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

  private void UpdateNotificationButtonClicked(object sender, RoutedEventArgs e)
  {
    this.GetViewModel<DeveloperToolsViewModel>().StatusMessage = "Update Notification Clicked";
  }

  private void OpenSupportTesterWindow(object sender, RoutedEventArgs e)
  {
    new SupportStringTester().Show();
  }




}
