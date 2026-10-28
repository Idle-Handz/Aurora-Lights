// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.SpellcastingExpanderContent
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Extensions;
using Builder.Presentation.Services;
using Builder.Presentation.ViewModels;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls;

public partial class SpellcastingExpanderContent : 
  UserControl,
  ISubscriber<SettingsChangedEvent>
{
  public SpellcastingExpanderContent()
  {
    this.InitializeComponent();
    this.SetMinRowHeights(ApplicationManager.Current.Settings.GetSelectionExpanderGridRowHeight());
    ApplicationManager.Current.EventAggregator.Subscribe((object) this);
  }

  private void SelectionsGridKeyDown(object sender, KeyEventArgs e)
  {
  }

  private void SelectionsGridPreviewKeyDown(object sender, KeyEventArgs e)
  {
    SpellcastingContentViewModel viewModel = this.GetViewModel<SpellcastingContentViewModel>();
    if (e.Key != Key.Delete)
      return;
    try
    {
      SelectionRuleExpanderViewModel expanderViewModel = viewModel?.GetSelectedExpanderViewModel();
      if (expanderViewModel == null || !expanderViewModel.ElementRegistered)
        return;
      expanderViewModel.UnregisterElementCommand.Execute((object) null);
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  private void ElementsGridPreviewKeyDown(object sender, KeyEventArgs e)
  {
    SpellcastingContentViewModel viewModel = this.GetViewModel<SpellcastingContentViewModel>();
    if (e.Key != Key.Delete)
      return;
    viewModel.GetSelectedExpanderViewModel().UnregisterElementCommand.Execute((object) null);
  }

  private void ElementsGridKeyDown(object sender, KeyEventArgs e)
  {
    SpellcastingContentViewModel viewModel = this.GetViewModel<SpellcastingContentViewModel>();
    Key key = e.Key;
    if (key <= Key.Space)
    {
      if (key == Key.Tab || key == Key.Escape || key != Key.Space)
        return;
      this.RegisteredSelectedElement();
    }
    else if (key != Key.Delete)
    {
      switch (key - 74)
      {
        case Key.None:
          viewModel.SpellcastingFilter.IncludeCantrips = !viewModel.SpellcastingFilter.IncludeCantrips;
          (sender as FrameworkElement).Focus();
          break;
        case Key.Cancel:
          viewModel.SpellcastingFilter.Include1 = !viewModel.SpellcastingFilter.Include1;
          (sender as FrameworkElement).Focus();
          break;
        case Key.Back:
          viewModel.SpellcastingFilter.Include2 = !viewModel.SpellcastingFilter.Include2;
          (sender as FrameworkElement).Focus();
          break;
        case Key.Tab:
          viewModel.SpellcastingFilter.Include3 = !viewModel.SpellcastingFilter.Include3;
          (sender as FrameworkElement).Focus();
          break;
        case Key.LineFeed:
          viewModel.SpellcastingFilter.Include4 = !viewModel.SpellcastingFilter.Include4;
          (sender as FrameworkElement).Focus();
          break;
        case Key.Clear:
          viewModel.SpellcastingFilter.Include5 = !viewModel.SpellcastingFilter.Include5;
          (sender as FrameworkElement).Focus();
          break;
        case Key.Return:
          viewModel.SpellcastingFilter.Include6 = !viewModel.SpellcastingFilter.Include6;
          (sender as FrameworkElement).Focus();
          break;
        case Key.Pause:
          viewModel.SpellcastingFilter.Include7 = !viewModel.SpellcastingFilter.Include7;
          (sender as FrameworkElement).Focus();
          break;
        case Key.Capital:
          viewModel.SpellcastingFilter.Include8 = !viewModel.SpellcastingFilter.Include8;
          (sender as FrameworkElement).Focus();
          break;
        case Key.KanaMode:
          viewModel.SpellcastingFilter.Include9 = !viewModel.SpellcastingFilter.Include9;
          (sender as FrameworkElement).Focus();
          break;
      }
    }
    else
      viewModel.GetSelectedExpanderViewModel().UnregisterElementCommand.Execute((object) null);
  }

  private void ElementsGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
  {
    try
    {
      if (e.OriginalSource.GetType() == typeof (ScrollViewer))
        return;
      this.RegisteredSelectedElement();
    }
    catch (NullReferenceException ex)
    {
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  private void RegisteredSelectedElement()
  {
    SpellcastingContentViewModel viewModel = this.GetViewModel<SpellcastingContentViewModel>();
    viewModel.GetSelectedExpanderViewModel().RegisterElementCommand.Execute((object) null);
    SelectionElement selectionElement = viewModel.SelectionElementsCollection.FirstOrDefault<SelectionElement>((Func<SelectionElement, bool>) (x => x.IsChosen));
    if (selectionElement != null)
      selectionElement.IsChosen = false;
    viewModel.SelectedSelectionElement.IsChosen = true;
  }

  private void SetMinRowHeights(int height)
  {
    this.SelectionElementsDataGrid.MinRowHeight = (double) height;
    this.SelectionPickerGrid.MinRowHeight = (double) height;
  }

  private void ScrollSelectedExpanderIntoView()
  {
    try
    {
      SpellcastingContentViewModel viewModel = this.GetViewModel<SpellcastingContentViewModel>();
      if (viewModel.SelectedExpander == null)
        return;
      this.SelectionPickerGrid.ScrollIntoView((object) viewModel.SelectedExpander);
      string id = viewModel.GetSelectedExpanderViewModel().RegisteredElementId;
      if (id == null)
        return;
      SelectionElement selectionElement = viewModel.SelectionElementsCollection.FirstOrDefault<SelectionElement>((Func<SelectionElement, bool>) (x => x.Element.Id == id));
      if (selectionElement == null)
        return;
      this.SelectionElementsDataGrid.ScrollIntoView((object) selectionElement);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (ScrollSelectedExpanderIntoView));
    }
  }

  void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
  {
    this.SetMinRowHeights(ApplicationManager.Current.Settings.GetSelectionExpanderGridRowHeight());
  }

  private void SelectionPickerGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    this.ScrollSelectedExpanderIntoView();
  }




}
