using System.IO;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.CharacterViewer
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Extensions;
using Builder.Presentation.Services;
using MahApps.Metro.Controls;
using MoonPdfLib;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views;

public partial class CharacterViewer : 
  MetroWindow,
  ISubscriber<CharacterSheetPreviewEvent>
{
  public CharacterViewer()
  {
    this.InitializeComponent();
    this.Loaded += new RoutedEventHandler(this.CharacterViewer_Loaded);
  }

  private void CharacterViewer_Loaded(object sender, RoutedEventArgs e)
  {
    this.ApplyTheme();
    ApplicationManager.Current.EventAggregator.Subscribe((object) this);
  }

  public void OnHandleEvent(CharacterSheetPreviewEvent args)
  {
    try
    {
      this.ViewerPanel.OpenFile(args.File.FullName);
      this.ViewerPanel.ZoomToWidth();
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  private void OpenDocumentsFolder(object sender, RoutedEventArgs e)
  {
  }

  private void BrowseWebsiteClick(object sender, RoutedEventArgs e)
  {
  }




}
