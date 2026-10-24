// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.SupportStringTester
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Development;

public partial class SupportStringTester : Window
{
  public SupportStringTester() => this.InitializeComponent();

  private void OnSupportsContentChanged(object sender, TextChangedEventArgs e)
  {
    this.GetViewModel<SupportStringViewModel>().RunSupports();
  }

  private void OnRegexContentChanged(object sender, TextChangedEventArgs e)
  {
    this.GetViewModel<SupportStringViewModel>().RunSupports();
  }

  private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    this.GetViewModel<SupportStringViewModel>().RunSupports();
  }




}
