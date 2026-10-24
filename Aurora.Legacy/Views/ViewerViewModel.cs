using System.IO;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.ViewerViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Events.Character;
using Builder.Presentation.ViewModels.Base;

#nullable disable
namespace Builder.Presentation.Views;

public class ViewerViewModel : 
  ViewModelBase,
  ISubscriber<CharacterSheetPreviewEvent>,
  ISubscriber<CharacterBuildChangedEvent>
{
  private string _title;
  private string _statusMessage;

  public ViewerViewModel()
  {
    this._title = "Viewer";
    this._statusMessage = "READY";
    if (this.IsInDesignMode)
      return;
    this.SubscribeWithEventAggregator();
  }

  public string Title
  {
    get => this._title;
    set => this.SetProperty<string>(ref this._title, value, nameof (Title));
  }

  public string StatusMessage
  {
    get => this._statusMessage;
    set => this.SetProperty<string>(ref this._statusMessage, value, nameof (StatusMessage));
  }

  public void OnHandleEvent(CharacterSheetPreviewEvent args)
  {
    this.StatusMessage = args.File.FullName ?? "";
  }

  public void OnHandleEvent(CharacterBuildChangedEvent args)
  {
    this.Title = args.Character.ToString();
  }
}
