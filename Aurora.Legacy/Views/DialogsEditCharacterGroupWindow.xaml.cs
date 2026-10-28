// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Dialogs.EditCharacterGroupWindow
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Dialogs;

public partial class EditCharacterGroupWindow : MetroWindow
{
  private readonly List<string> _existingGroups;
  public EditCharacterGroupWindow(string currentGroup, List<string> existingGroups = null)
  {
    this.InitializeComponent();
    this.ApplyTheme();
    this._existingGroups = existingGroups ?? new List<string>();
    this.GroupComboBox.Text = currentGroup;
    this.GroupComboBox.ItemsSource = (IEnumerable) this._existingGroups;
    this.Loaded += new RoutedEventHandler(this.EditCharacterGroupWindow_Loaded);
  }

  private void EditCharacterGroupWindow_Loaded(object sender, RoutedEventArgs e)
  {
  }

  public string NewGroupName { get; set; }

  private void OnAcceptClicked(object sender, RoutedEventArgs e)
  {
    this.NewGroupName = this.GroupComboBox.Text.Trim();
    this.DialogResult = new bool?(true);
  }

  private void OnCancelClicked(object sender, RoutedEventArgs e)
  {
    this.DialogResult = new bool?(false);
  }




}
