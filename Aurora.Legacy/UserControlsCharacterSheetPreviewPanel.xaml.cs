// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.CharacterSheetPreviewPanel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Services;
using Builder.Presentation.Views;
using MoonPdfLib;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls;

public partial class CharacterSheetPreviewPanel : 
  UserControl,
  ISubscriber<CharacterSheetPreviewEvent>,
  ISubscriber<CharacterSheetSavedEvent>,
  ISubscriber<CharacterSheetToggleViewEvent>
{
  public CharacterSheetPreviewPanel()
  {
    this.InitializeComponent();
    ApplicationManager.Current.EventAggregator.Subscribe((object) this);
  }

  private void UpdateCharacterSheetViewer(string path)
  {
    try
    {
      if (!File.Exists(path))
      {
        Logger.Warning($"The pdf at {path} does not exist, not updating the character sheet viewer.");
      }
      else
      {
        this.Viewer.OpenFile(path);
        this.Viewer.ZoomToWidth();
        this.Viewer.ZoomToWidth();
      }
    }
    catch (NullReferenceException ex)
    {
      Logger.Exception((Exception) ex, nameof (UpdateCharacterSheetViewer));
      if (ex.Source == "MoonPdfLib")
        ApplicationManager.Current.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"Unable to generate a preview of your character sheet. ({ex.Message})"));
      else
        MessageDialogService.ShowException((Exception) ex);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (UpdateCharacterSheetViewer));
      MessageDialogService.ShowException(ex);
    }
  }

  public void OnHandleEvent(CharacterSheetPreviewEvent args)
  {
    this.UpdateCharacterSheetViewer(args.File.FullName);
  }

  public void OnHandleEvent(CharacterSheetSavedEvent args)
  {
    this.UpdateCharacterSheetViewer(args.File);
  }

  public void OnHandleEvent(CharacterSheetToggleViewEvent args) => this.Viewer.TogglePageDisplay();




}
