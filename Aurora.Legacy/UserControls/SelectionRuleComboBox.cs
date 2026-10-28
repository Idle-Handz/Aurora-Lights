// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.SelectionRuleComboBox
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data.Rules;
using Builder.Presentation.Extensions;
using Builder.Presentation.Interfaces;

using Builder.Presentation.ViewModels;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls;

public partial class SelectionRuleComboBox : UserControl, ISelectionRuleExpander
{
  private bool _toggleHeaders;
  public void FocusExpander()
  {
    this.VisualFocusQue.BringIntoView();
    this.Focus();
  }

  public bool IsSelectionMade()
  {
    return this.GetViewModel<SelectionRuleComboBoxViewModel>().ElementRegistered;
  }

  private SelectionRuleComboBoxViewModel ViewModel
  {
    get => this.GetViewModel<SelectionRuleComboBoxViewModel>();
  }

  public SelectionRuleComboBox(SelectRule selectionRule, int number = 1)
  {
    this.SelectionRule = selectionRule != null ? selectionRule : throw new ArgumentNullException(nameof (selectionRule));
    this.Number = number;
    this.InitializeComponent();
    ApplicationManager.Current.EventAggregator.Subscribe((object) this);
  }

  public SelectRule SelectionRule { get; }

  public int Number { get; }

  public string UniqueIdentifier => this.SelectionRule.UniqueIdentifier;

  public void SetSelection(string elementId)
  {
    this.GetViewModel<SelectionRuleComboBoxViewModel>().SetSelectionAndRegister(elementId);
  }

  protected override async void OnInitialized(EventArgs e)
  {
    SelectionRuleComboBox control = this;
    // ISSUE: reference to a compiler-generated method
    base.OnInitialized(e);
    // ISSUE: explicit non-virtual call
    control.DataContext = (object) new SelectionRuleComboBoxViewModel(control.SelectionRule)
    {
      RegisterOnSelection = true
    };
    await control.GetViewModel().InitializeAsync();
    try
    {
      control._toggleHeaders = !ApplicationManager.Current.Settings.Settings.DisplaySelectionExpanderColumnHeaders;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (OnInitialized));
    }
    // ISSUE: explicit non-virtual call
    string type = control.SelectionRule.Attributes.Type;
    switch (type)
    {
      case null:
        break;
      case "Class":
        break;
      case "Companion":
        break;
      case "Spell":
        break;
      case "Race":
        break;
      case "Feat":
        break;
      default:
        int num = type == "Alignment" ? 1 : 0;
        break;
    }
  }

  private void Register()
  {
    this.GetViewModel<SelectionRuleComboBoxViewModel>().RegisterElementCommand.Execute((object) null);
  }

  private void Unregister()
  {
    this.GetViewModel<SelectionRuleComboBoxViewModel>().UnregisterElementCommand.Execute((object) null);
  }

  private void ToggleColumnHeaders(object sender, RoutedEventArgs e)
  {
  }

  private void ElementsGridKeyDown(object sender, KeyEventArgs e)
  {
    if (e.Key != Key.Return && e.Key != Key.Space)
      return;
    this.Register();
  }

  private void ElementsGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
  {
    this.Register();
  }

  private void UnregisterElement(object sender, RoutedEventArgs e) => this.Unregister();

  public override string ToString()
  {
    return $"{"SelectionRuleExpander"} ({this.Number}): ({this.SelectionRule})";
  }

  public void Hide()
  {
    if (this.GetViewModel().IsInDebugMode)
      this.IsEnabled = false;
    else
      this.Visibility = Visibility.Collapsed;
  }

  public void Unhide()
  {
    if (this.GetViewModel().IsInDebugMode)
      this.IsEnabled = true;
    else
      this.Visibility = Visibility.Visible;
  }

  private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
  }


}
