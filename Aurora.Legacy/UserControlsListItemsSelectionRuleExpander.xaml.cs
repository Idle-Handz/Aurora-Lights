// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.ListItemsSelectionRuleExpander
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data.Rules;
using Builder.Presentation.Controls;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Extensions;
using Builder.Presentation.Interfaces;

using Builder.Presentation.Services;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Base;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.UserControls;

public partial class ListItemsSelectionRuleExpander : 
  UserControl,
  ISelectionRuleExpander,
  ISubscriber<SettingsChangedEvent>
{
  public void FocusExpander()
  {
    this.VisualFocusQue.BringIntoView();
    this.Expander.Focus();
    this.Expander.IsExpanded = true;
  }

  public bool IsSelectionMade()
  {
    return this.GetViewModel<ListItemSelectionRuleExpanderViewModel>().SelectionMade;
  }

  public ListItemsSelectionRuleExpander(SelectRule selectionRule, int number = 1)
  {
    this.SelectionRule = selectionRule;
    this.Number = number;
    this.DataContext = (object) new ListItemSelectionRuleExpanderViewModel(selectionRule, number);
    this.InitializeComponent();
    ApplicationManager.Current.EventAggregator.Subscribe((object) this);
  }

  public SelectRule SelectionRule { get; }

  public int Number { get; }

  public string UniqueIdentifier => this.SelectionRule.UniqueIdentifier;

  public void SetSelection(string elementId)
  {
    try
    {
      SelectionRuleListItem selectionRuleListItem = this.GetViewModel<ListItemSelectionRuleExpanderViewModel>().SelectionItems.Single<SelectionRuleListItem>((Func<SelectionRuleListItem, bool>) (x => x.ID == int.Parse(elementId)));
      this.GetViewModel<ListItemSelectionRuleExpanderViewModel>().SelectedItem = selectionRuleListItem;
      this.GetViewModel<ListItemSelectionRuleExpanderViewModel>().SetCommand.Execute((object) null);
    }
    catch (Exception ex)
    {
      Logger.Warning($"unable to set '{elementId}' on '{this.SelectionRule}'");
      MessageDialogService.ShowException(ex, Builder.Presentation.Properties.Resources.ApplicationName, $"unable to set '{elementId}' on '{this.SelectionRule}'");
    }
  }

  protected override async void OnInitialized(EventArgs e)
  {
    ListItemsSelectionRuleExpander control = this;
    // ISSUE: reference to a compiler-generated method
    base.OnInitialized(e);
    await control.GetViewModel().InitializeAsync((InitializationArguments) null);
    switch (ApplicationManager.Current.Settings.Settings.SelectionExpanderGridRowSize)
    {
      case 1:
        control.DataGrid.MinRowHeight = 17.0;
        break;
      case 2:
        control.DataGrid.MinRowHeight = 21.0;
        break;
      case 3:
        control.DataGrid.MinRowHeight = 25.0;
        break;
      default:
        control.DataGrid.MinRowHeight = 21.0;
        ApplicationManager.Current.Settings.Settings.SelectionExpanderGridRowSize = 2;
        ApplicationManager.Current.Settings.Settings.Save();
        break;
    }
  }

  private void ElementsGridSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
  }

  private void ElementsGridKeyDown(object sender, KeyEventArgs e)
  {
    if (e.Key != Key.Return && e.Key != Key.Space)
      return;
    this.GetViewModel<ListItemSelectionRuleExpanderViewModel>().SetCommand.Execute((object) null);
  }

  private void UnsetElement(object sender, RoutedEventArgs e)
  {
    this.GetViewModel<ListItemSelectionRuleExpanderViewModel>().UnsetCommand.Execute((object) null);
  }

  private void ___No_Name__MouseDoubleClick(object sender, MouseButtonEventArgs e)
  {
    this.GetViewModel<ListItemSelectionRuleExpanderViewModel>().SetCommand.Execute((object) null);
  }

  private void RandomizeClick(object sender, RoutedEventArgs e)
  {
  }

  private void SelectionRuleInfoClick(object sender, RoutedEventArgs e)
  {
    if (this.GetViewModel().IsInDebugMode)
      MessageDialogService.Show(this.SelectionRule.UniqueIdentifier);
    else
      MessageDialogService.Show("Selection-rule details are available while the application is running in debug mode.");
  }

  void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
  {
    switch (args.Settings.SelectionExpanderGridRowSize)
    {
      case 1:
        this.DataGrid.MinRowHeight = 17.0;
        break;
      case 2:
        this.DataGrid.MinRowHeight = 21.0;
        break;
      case 3:
        this.DataGrid.MinRowHeight = 25.0;
        break;
      default:
        this.DataGrid.MinRowHeight = 21.0;
        ApplicationManager.Current.Settings.Settings.SelectionExpanderGridRowSize = 2;
        ApplicationManager.Current.Settings.Settings.Save();
        break;
    }
  }




}
