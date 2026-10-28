// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.Manage.ManageSourcesSection
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using Builder.Presentation.ViewModels;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls.Manage;

public partial class ManageSourcesSection : UserControl
{
  public ManageSourcesSection() => this.InitializeComponent();

  private void SourceItems_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    this.GetViewModel<CampaignManagerViewModel>().SelectedSourceItems = (IEnumerable<Builder.Presentation.Models.Sources.SourceItem>) this.SourceItems.SelectedItems.Cast<Builder.Presentation.Models.Sources.SourceItem>().ToList<Builder.Presentation.Models.Sources.SourceItem>();
  }




}
