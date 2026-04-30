// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.NewCharacterSlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Presentation.Controls;
using Builder.Presentation.Extensions;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.ViewModels.Base;
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

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public partial class NewCharacterSlider : Flyout
{
  private bool _isDragging;
  public NewCharacterSlider()
  {
    this.InitializeComponent();
    this.IsOpenChanged += new RoutedEventHandler(this.Page1_IsOpenChanged);
    this.GetViewModel<NewCharacterSliderViewModel>().CloseRequested += this.ViewModel_CloseRequested;
  }

  private async void Page1_IsOpenChanged(object sender, RoutedEventArgs e)
  {
    NewCharacterSlider window = this;
    if (!window.IsOpen)
      return;
    await window.GetViewModel().InitializeAsync((InitializationArguments) null);
  }

  private void PageSaveClicked(object sender, RoutedEventArgs e) => this.IsOpen = false;

  private void PageCloseClicked(object sender, RoutedEventArgs e) => this.IsOpen = false;

  private void ViewModel_CloseRequested(object? sender, EventArgs e) => this.IsOpen = false;

  private void UIElement_OnMouseMove(object sender, MouseEventArgs e)
  {
    if (e.LeftButton == MouseButtonState.Pressed)
    {
      Logger.Info("entered UIElement_OnMouseMove");
      try
      {
        if (this._isDragging)
          return;
        this._isDragging = true;
        AuroraPanel auroraPanel = (AuroraPanel) sender;
        if (auroraPanel == null)
          return;
        int num = (int) DragDrop.DoDragDrop((DependencyObject) auroraPanel, (object) auroraPanel, DragDropEffects.Move);
      }
      catch (Exception ex)
      {
        MessageDialogService.ShowException(ex);
      }
    }
    else
      this._isDragging = false;
  }

  private void UIElement_OnDrop(object sender, DragEventArgs e)
  {
    try
    {
      Logger.Info("entered UIElement_OnDrop");
      if (!(((FrameworkElement) sender)?.DataContext is AbilityItem dataContext1) || !((e.Data.GetData(typeof (AuroraPanel)) is AuroraPanel data ? data.DataContext : (object) null) is AbilityItem dataContext2))
        return;
      int baseScore1 = dataContext1.BaseScore;
      int baseScore2 = dataContext2.BaseScore;
      dataContext1.BaseScore = baseScore2;
      dataContext2.BaseScore = baseScore1;
      this._isDragging = false;
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }





}
