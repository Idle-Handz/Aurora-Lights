// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.QuickBar.Commands.QuickBarFetchCommand
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data.Files;
using Builder.Data.Files.Updater;
using Builder.Presentation.Events.Shell;

using Builder.Presentation.Services.Data;
using Builder.Presentation.Properties;
using Builder.Presentation.Services.QuickBar.Commands.Base;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

#nullable disable
namespace Builder.Presentation.Services.QuickBar.Commands;

public class QuickBarFetchCommand : QuickBarCommand
{
  private readonly IEventAggregator _eventAggregator;
  private readonly IndicesUpdateService _updater;

  public QuickBarFetchCommand()
    : base("fetch")
  {
    this._eventAggregator = ApplicationContext.Current.EventAggregator;
    this._updater = new IndicesUpdateService(new Version(Resources.AppVersionCheck));
    this._updater.StatusChanged += new EventHandler<IndicesUpdateStatusChangedEventArgs>(this._updater_StatusChanged);
  }

  private void _updater_StatusChanged(object sender, IndicesUpdateStatusChangedEventArgs e)
  {
    this._eventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(e.StatusMessage)
    {
      ProgressPercentage = e.ProgressPercentage
    });
  }

  public override void Execute(string parameter)
  {
    if (parameter == "?" || parameter == "help" || !parameter.StartsWith("http") || !parameter.EndsWith(".index"))
    {
      MessageDialogContext.Current?.Show($"@{this.CommandName} accepts a valid (starting with http:// or https://) url for an index file.", "@" + this.CommandName);
    }
    else
    {
      MainWindowStatusUpdateEvent args = new MainWindowStatusUpdateEvent($"executing @{this.CommandName} {parameter}");
      try
      {
        this.Fetch(parameter);
      }
      catch (Exception ex)
      {
        args.IsDanger = true;
        args.StatusMessage = ex.Message;
      }
      this._eventAggregator.Send<MainWindowStatusUpdateEvent>(args);
    }
  }

  private async void Fetch(string url)
  {
    try
    {
      IndexFile file = await IndexFile.FromUrl(url);
      file.SaveContent(new FileInfo(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, file.Info.UpdateFilename)));
      this._eventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"The index file '{file.Info.UpdateFilename}' has successfully been written to the custom folder. Run the 'update custom files' command to pull in the content."));
      await Task.Delay(1000);
      if (await this._updater.UpdateIndexFiles(DataManager.Current.UserDocumentsCustomElementsDirectory, file.FileInfo.FullName) && MessageBox.Show(Application.Current.MainWindow, "Your custom files have been updated, do you want to restart the applicaton to reload the content?", Resources.ApplicationName, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
      {
        ApplicationManager.Current.RestartApplication();
      }
      this._eventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"The '{file.Info.DisplayName}' bundle has been added."));
      file = (IndexFile) null;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (Fetch));
      this._eventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(ex.Message)
      {
        IsDanger = true
      });
      MessageDialogContext.Current?.ShowException(ex);
    }
  }
}
