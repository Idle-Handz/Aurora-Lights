// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.DeveloperToolsViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data.Files.Updater;
using Builder.Presentation.Events.Developer;

using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.Views.Development;

public class DeveloperToolsViewModel : ViewModelBase, ISubscriber<DeveloperWindowStatusUpdateEvent>
{
  private string _statusMessage;
  private int _progressPercentage;

  public ElementsViewerViewModel ElementsViewerViewModel { get; } = new ElementsViewerViewModel();

  public CustomFilesViewerViewModel CustomFilesViewerViewModel { get; } = new CustomFilesViewerViewModel();

  public DeveloperToolsViewModel()
  {
    if (this.IsInDesignMode)
    {
      this._statusMessage = "hello design time status";
      this._progressPercentage = 67;
    }
    else
      this.SubscribeWithEventAggregator();
  }

  public string StatusMessage
  {
    get => this._statusMessage;
    set => this.SetProperty<string>(ref this._statusMessage, value, nameof (StatusMessage));
  }

  public int ProgressPercentage
  {
    get => this._progressPercentage;
    set => this.SetProperty<int>(ref this._progressPercentage, value, nameof (ProgressPercentage));
  }

  public RelayCommand UpdateIndexFilesCommand
  {
    get => new RelayCommand(new Action(this.UpdateIndexFiles));
  }

  private void UpdateIndexFiles()
  {
    new IndicesUpdateService(new Version(Resources.AppVersionCheck)).UpdateIndexFiles(DataManager.Current.UserDocumentsCustomElementsDirectory);
  }

  public void OnHandleEvent(DeveloperWindowStatusUpdateEvent args)
  {
    this.StatusMessage = args.StatusMessage;
  }

  public void OnHandleEvent(IndicesUpdateStatusChangedEventArgs args)
  {
    this.StatusMessage = args.StatusMessage;
    this.ProgressPercentage = args.ProgressPercentage;
    Logger.Info(args.StatusMessage);
  }
}
