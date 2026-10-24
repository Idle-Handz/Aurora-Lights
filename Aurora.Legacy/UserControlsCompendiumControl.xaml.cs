// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.CompendiumControl
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
using System.Windows.Input;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls;

public partial class CompendiumControl : UserControl
{
  public CompendiumControl() => this.InitializeComponent();

  private void SearchResultsDataGrid_OnKeyDown(object sender, KeyEventArgs e)
  {
    if (e.Key != Key.F1)
      return;
    this.GetViewModel<CompendiumControlViewModel>().DeveloperCopyDetailsCommand.Execute((object) null);
  }





}
