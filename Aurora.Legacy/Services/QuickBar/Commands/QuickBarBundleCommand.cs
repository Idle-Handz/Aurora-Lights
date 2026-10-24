// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.QuickBar.Commands.QuickBarBundleCommand
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
using Builder.Presentation.ViewModels.Shell.Start;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

#nullable disable
namespace Builder.Presentation.Services.QuickBar.Commands;

public class QuickBarBundleCommand : QuickBarCommand
{
  private readonly IEventAggregator _eventAggregator;
  private readonly IndicesUpdateService _updater;
  private readonly string[] _parameters;

  public QuickBarBundleCommand()
    : base("bundle")
  {
    this._eventAggregator = ApplicationContext.Current.EventAggregator;
    this._updater = new IndicesUpdateService(new Version(Resources.AppVersionCheck));
    this._updater.StatusChanged += new EventHandler<IndicesUpdateStatusChangedEventArgs>(this._updater_StatusChanged);
    this._parameters = new string[6]
    {
      "core",
      "supplements",
      "unearthed-arcana",
      "third-party",
      "reddit",
      "clear"
    };
  }

  private void _updater_StatusChanged(object sender, IndicesUpdateStatusChangedEventArgs e)
  {
    this._eventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(e.StatusMessage)
    {
      IsSuccess = true,
      ProgressPercentage = e.ProgressPercentage
    });
  }

  public override void Execute(string parameter)
  {
    if (parameter == "?" || parameter == "help")
    {
      MessageDialogContext.Current?.Show($"@{this.CommandName} parameters are: {string.Join(", ", this._parameters)}", "@" + this.CommandName);
    }
    else
    {
      MainWindowStatusUpdateEvent args = new MainWindowStatusUpdateEvent($"executing @{this.CommandName} parameter: {parameter}");
      switch (parameter)
      {
        case "":
        case "?":
        case "help":
          string message = $"@{this.CommandName}parameters are:{Environment.NewLine}";
          foreach (string parameter1 in this._parameters)
            message = message + parameter1 + Environment.NewLine;
          MessageDialogContext.Current?.Show(message, "@" + this.CommandName);
          return;
        case "clear":
          if (MessageBox.Show("This will remove all content from folders created by index files. Proceed?", "Clear Bundles", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
          {
            foreach (string indexFile1 in this._updater.GetIndexFiles(DataManager.Current.UserDocumentsCustomElementsDirectory))
            {
              IndexFile indexFile2 = IndexFile.FromFile(new FileInfo(indexFile1));
              if (Directory.Exists(indexFile2.GetContentDirectory()))
                Directory.Delete(indexFile2.GetContentDirectory(), true);
            }
            args.StatusMessage = "Content cleared.";
            break;
          }
          break;
        case "community-reddit":
        case "reddit":
          this.SendDownloadRequest("https://raw.githubusercontent.com/community-elements/elements-reddit/master/reddit.index");
          break;
        case "core":
          this.SendDownloadRequest("https://raw.githubusercontent.com/aurorabuilder/elements/master/core.index");
          break;
        case "enable":
        case "on":
        case "ui":
          this._eventAggregator.Send<BundleCommandEvent>(new BundleCommandEvent(parameter));
          break;
        case "homebrew":
          this.SendDownloadRequest("https://raw.githubusercontent.com/aurorabuilder/elements/master/homebrew.index");
          break;
        case "supplements":
          this.SendDownloadRequest("https://raw.githubusercontent.com/aurorabuilder/elements/master/supplements.index");
          break;
        case "third-party":
          this.SendDownloadRequest("https://raw.githubusercontent.com/aurorabuilder/elements/master/third-party.index");
          break;
        case "unearthed-arcana":
          this.SendDownloadRequest("https://raw.githubusercontent.com/aurorabuilder/elements/master/unearthed-arcana.index");
          break;
        default:
          args.StatusMessage = $"Invalid @bundle command ({parameter})";
          args.IsDanger = true;
          MessageDialogContext.Current?.Show(args.StatusMessage);
          break;
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
      this._eventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"The index file '{file.Info.UpdateFilename}' has successfully been downloaded. Processing the file now."));
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

  private async void SendDownloadRequest(params string[] urls)
  {
    string[] strArray = urls;
    for (int index = 0; index < strArray.Length; ++index)
    {
      this._eventAggregator.Send<IndexDownloadRequestEvent>(new IndexDownloadRequestEvent(strArray[index]));
      await Task.Delay(1000);
    }
    strArray = (string[]) null;
  }
}
