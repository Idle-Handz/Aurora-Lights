using System.IO;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.SaveCharacterSlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using Builder.Presentation.Models;
using Builder.Presentation.ViewModels.Base;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public partial class SaveCharacterSlider : Flyout
{
  private CharacterFile _file;
  public SaveCharacterSlider()
  {
    this.InitializeComponent();
    this.IsOpenChanged += new RoutedEventHandler(this.Page1_IsOpenChanged);
    this.GetViewModel<SaveSliderViewModel>().CloseRequested += this.ViewModel_CloseRequested;
  }

  public void SetFile(CharacterFile file) => this._file = file;

  private async void Page1_IsOpenChanged(object sender, RoutedEventArgs e)
  {
    SaveCharacterSlider window = this;
    if (window.IsOpen)
    {
      if (window._file != null)
        await window.GetViewModel().InitializeAsync(new InitializationArguments((object) window._file));
      else
        await window.GetViewModel().InitializeAsync(new InitializationArguments((object) new CharacterFile("")
        {
          IsNew = true
        }));
    }
    else
      window._file = (CharacterFile) null;
  }

  private void PageSaveClicked(object sender, RoutedEventArgs e) => this.IsOpen = false;

  private void PageCloseClicked(object sender, RoutedEventArgs e) => this.IsOpen = false;

  private void ViewModel_CloseRequested(object? sender, EventArgs e) => this.IsOpen = false;





}
