using System.IO;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Viewer
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
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views;

public partial class Viewer : 
  MetroWindow,
  ISubscriber<CharacterSheetPreviewEvent>
{
  public Viewer()
  {
    this.InitializeComponent();
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




}
