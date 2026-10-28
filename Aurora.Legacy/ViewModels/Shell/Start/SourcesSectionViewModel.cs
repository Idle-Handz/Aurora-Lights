// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Shell.Start.SourcesSectionViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data.Files;
using Builder.Data.Files.Updater;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Events.Shell;

using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.QuickBar.Commands;
using Builder.Presentation.Telemetry;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.ViewModels.Shell.Start;

public sealed class SourcesSectionViewModel : 
  ViewModelBase,
  ISubscriber<IndexDownloadRequestEvent>,
  ISubscriber<BundleCommandEvent>
{
  private readonly IndicesUpdateService _updateService;
  private IndexFile _selectedIndex;
  private string _remoteUrl;
  private bool _isCheckingForContentUpdates;
  private int _updateCount;
  private string _updateBadgeContent;
  private string _fileChangedStatusMessage;
  private int _fetchCount;
  private string _restartBadgeCount;
  private int _progress;
  private bool _awaitingRestart;
  private bool _containsIndices;
  private bool _bundlesEnabled;

  public SourcesSectionViewModel()
  {
    this._updateService = new IndicesUpdateService(new Version(Resources.AppVersionCheck));
    this._updateService.StatusChanged += new EventHandler<IndicesUpdateStatusChangedEventArgs>(this.UpdateServiceStatusChanged);
    this._updateService.FileUpdated += new EventHandler<IndicesUpdateStatusChangedEventArgs>(this._updateService_FileUpdated);
    if (this.IsInDebugMode)
      this._remoteUrl = "https://raw.githubusercontent.com/aurorabuilder/elements/master/core.index";
    if (this.IsInDesignMode)
    {
      this.InitializeDesignData();
    }
    else
    {
      int num = ApplicationManager.Current.IsInDeveloperMode ? 1 : 0;
      this.FetchCustomFileCommand = (ICommand) new RelayCommand(new Action(this.FetchCustomFile), new Func<bool>(this.CanFetchFile));
      this.UpdateCustomFilesCommand = new RelayCommand(new Action(this.UpdateCustomFiles), new Func<bool>(this.CanUpdateCustomFiles));
      this.CleanCustomFilesCommand = new RelayCommand(new Action(this.CleanCustomFiles), new Func<bool>(this.CanCleanCustomFiles));
      this.LoadIndices();
      this._selectedIndex = this.Indices.FirstOrDefault<IndexFile>();
      if (this.Settings.StartupCheckForContentUpdated)
        this.PerformStartupContentUpdateCheck();
      if (this.Settings.Bundle)
        this.BundlesEnabled = true;
      this.SubscribeWithEventAggregator();
    }
  }

  private async Task PerformStartupContentUpdateCheck()
  {
    await Task.Delay(10000);
    if (ApplicationManager.Current.UpdateAvailable)
    {
      this.FileChangedStatusMessage = "An application update is available. The automatic updating of content files was halted to avoid possible incompatible updates to the files.";
    }
    else
    {
      if (this.IsCheckingForContentUpdates || this.AwaitingRestart)
        return;
      this.UpdateCustomFiles();
    }
  }

  private void _updateService_FileUpdated(object sender, IndicesUpdateStatusChangedEventArgs e)
  {
    Dispatcher.CurrentDispatcher.Invoke((Action) (() =>
    {
      Logger.Warning(e.StatusMessage ?? "");
      this.FileChangedStatusMessage = e.StatusMessage;
      ++this.UpdateCount;
      this.RestartBadgeCount = this.UpdateCount.ToString();
    }));
  }

  private void UpdateServiceStatusChanged(object sender, IndicesUpdateStatusChangedEventArgs e)
  {
    Dispatcher.CurrentDispatcher.Invoke((Action) (() =>
    {
      if (e.StatusMessage.ToLower().Contains("index"))
        this.FileChangedStatusMessage = e.StatusMessage;
      this.Progress = e.ProgressPercentage;
    }));
  }

  public ObservableCollection<IndexFile> Indices { get; } = new ObservableCollection<IndexFile>();

  public ObservableCollection<ElementsFile> ContentFiles { get; } = new ObservableCollection<ElementsFile>();

  public IndexFile SelectedIndex
  {
    get => this._selectedIndex;
    set => this.SetProperty<IndexFile>(ref this._selectedIndex, value, nameof (SelectedIndex));
  }

  public string RemoteUrl
  {
    get => this._remoteUrl;
    set
    {
      this.SetProperty<string>(ref this._remoteUrl, value, nameof (RemoteUrl));
      if (!(this.FetchCustomFileCommand is RelayCommand customFileCommand))
        return;
      customFileCommand.RaiseCanExecuteChanged();
    }
  }

  public ICommand OpenCustomDocumentsFolderCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.OpenCustomDocumentsFolder));
  }

  public ICommand OpenUserCustomDocumentsFolderCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.OpenUserCustomDocumentsFolder));
  }

  public RelayCommand UpdateCustomFilesCommand { get; }

  public RelayCommand CleanCustomFilesCommand { get; }

  public ICommand RestartCommand => (ICommand) new RelayCommand(new Action(this.Restart));

  public ICommand ClearCommand => (ICommand) new RelayCommand(new Action(this.Clear));

  private void Restart()
  {
    if (MessageBox.Show("Your content files have been updated, do you want to restart the application to reload the content?", "Aurora", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
      return;
    ApplicationManager.Current.RestartApplication();
  }

  private void Clear()
  {
    if (MessageBox.Show("Are you sure you want to clear the folder ", "Aurora Builder", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
      AnalyticsEventHelper.ContentClear(true);
    else
      AnalyticsEventHelper.ContentClear(false);
  }

  public bool IsCheckingForContentUpdates
  {
    get => this._isCheckingForContentUpdates;
    set
    {
      this.SetProperty<bool>(ref this._isCheckingForContentUpdates, value, nameof (IsCheckingForContentUpdates));
    }
  }

  public int UpdateCount
  {
    get => this._updateCount;
    set => this.SetProperty<int>(ref this._updateCount, value, nameof (UpdateCount));
  }

  public string UpdateBadgeContent
  {
    get => this._updateBadgeContent;
    set
    {
      this.SetProperty<string>(ref this._updateBadgeContent, value, nameof (UpdateBadgeContent));
    }
  }

  public string FileChangedStatusMessage
  {
    get => this._fileChangedStatusMessage;
    set
    {
      this.SetProperty<string>(ref this._fileChangedStatusMessage, value, nameof (FileChangedStatusMessage));
    }
  }

  public int FetchCount
  {
    get => this._fetchCount;
    set => this.SetProperty<int>(ref this._fetchCount, value, nameof (FetchCount));
  }

  public string RestartBadgeCount
  {
    get => this._restartBadgeCount;
    set => this.SetProperty<string>(ref this._restartBadgeCount, value, nameof (RestartBadgeCount));
  }

  public int Progress
  {
    get => this._progress;
    set => this.SetProperty<int>(ref this._progress, value, nameof (Progress));
  }

  public bool AwaitingRestart
  {
    get => this._awaitingRestart;
    set => this.SetProperty<bool>(ref this._awaitingRestart, value, nameof (AwaitingRestart));
  }

  public ICommand FetchCustomFileCommand { get; }

  private async void FetchCustomFile()
  {
    SourcesSectionViewModel sectionViewModel = this;
    try
    {
      if (string.IsNullOrWhiteSpace(sectionViewModel.RemoteUrl))
        return;
      sectionViewModel.RemoteUrl = sectionViewModel.RemoteUrl.Trim();
      if (!sectionViewModel.RemoteUrl.StartsWith("http") && !sectionViewModel.RemoteUrl.Contains("http"))
        sectionViewModel.RemoteUrl = "http://" + sectionViewModel.RemoteUrl;
      if (sectionViewModel.RemoteUrl.ToLowerInvariant().EndsWith("/user.index"))
        MessageDialogService.Show("The name 'user' is a reserved name for index files. It's recommended you use 'user-yourname' instead for a personal index file.");
      else if (sectionViewModel.RemoteUrl.EndsWith(".index"))
      {
        sectionViewModel.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Fetching " + sectionViewModel.RemoteUrl));
        IndexFile indexFile = await IndexFile.FromUrl(sectionViewModel.RemoteUrl);
        if (!indexFile.MeetsMinimumAppVersionRequirements(sectionViewModel._updateService.AppVersion))
        {
          MessageDialogService.Show($"You need to run Aurora v{indexFile.MinimumAppVersion} to include this file. It might contain updates that are not compatible with the version you're currently running.");
        }
        else
        {
          indexFile.SaveContent(new FileInfo(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, indexFile.Info.UpdateFilename)));
          sectionViewModel.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"The index file '{indexFile.Info.UpdateFilename}' has successfully been fetched. Run the 'update custom files' command to pull in the content."));
          AnalyticsEventHelper.ContentDownloadIndex(sectionViewModel.RemoteUrl);
          sectionViewModel.FetchCount++;
          sectionViewModel.UpdateBadgeContent = sectionViewModel.FetchCount.ToString();
          if (!(sectionViewModel.RemoteUrl == "https://raw.githubusercontent.com/aurorabuilder/elements/master/supplements.index"))
            return;
          sectionViewModel.IncludeCoreIndexIfMissing();
        }
      }
      else
        MessageDialogService.Show("This field is used to fetch .index files only.");
    }
    catch (HttpRequestException ex)
    {
      ex.Data[(object) "404"] = (object) sectionViewModel.RemoteUrl;
      Logger.Exception((Exception) ex, nameof (FetchCustomFile));
      MessageDialogService.ShowException((Exception) ex);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (FetchCustomFile));
      MessageDialogService.ShowException(ex);
    }
  }

  private bool CanFetchFile() => !string.IsNullOrWhiteSpace(this.RemoteUrl);

  private void OpenCustomDocumentsFolder()
  {
    Process.Start(DataManager.Current.UserDocumentsCustomElementsDirectory);
  }

  private void OpenUserCustomDocumentsFolder()
  {
    Process.Start(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, "user"));
  }

  private async void UpdateCustomFiles()
  {
    SourcesSectionViewModel sectionViewModel = this;
    try
    {
      sectionViewModel.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Checking for content updates..."));
      sectionViewModel.FileChangedStatusMessage = "Checking for content updates...";
      sectionViewModel.IsCheckingForContentUpdates = true;
      sectionViewModel.UpdateCustomFilesCommand.RaiseCanExecuteChanged();
      sectionViewModel.CleanCustomFilesCommand.RaiseCanExecuteChanged();
      int num = await sectionViewModel._updateService.UpdateIndexFiles(DataManager.Current.UserDocumentsCustomElementsDirectory) ? 1 : 0;
      sectionViewModel.LoadIndices();
      if (num != 0)
      {
        sectionViewModel.AwaitingRestart = true;
        sectionViewModel.FileChangedStatusMessage = "Your content files have been updated, restart the application to reload the content.";
        sectionViewModel.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Your content files have been updated, restart the application to reload the content."));
        AdditionalContentUpdatedEvent args = new AdditionalContentUpdatedEvent(sectionViewModel.RestartBadgeCount);
        sectionViewModel.EventAggregator.Send<AdditionalContentUpdatedEvent>(args);
      }
      else
      {
        sectionViewModel.FileChangedStatusMessage = "Last checked for content updates at " + DateTime.Now.ToShortTimeString();
        sectionViewModel.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Last checked for content updates at " + DateTime.Now.ToShortTimeString()));
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (UpdateCustomFiles));
      if (ex.Data.Contains((object) "404"))
      {
        string str = ex.Data[(object) "404"].ToString();
        sectionViewModel.FileChangedStatusMessage = $"The remote file with url '{str}' doesn't exist.";
        MessageDialogService.Show($"The remote file with url '{str}' doesn't exist.", "Aurora - 404 File Not Found");
      }
      else
      {
        sectionViewModel.FileChangedStatusMessage = ex.Message ?? "";
        MessageDialogService.ShowException(ex);
      }
    }
    finally
    {
      sectionViewModel.IsCheckingForContentUpdates = false;
      sectionViewModel.UpdateCustomFilesCommand.RaiseCanExecuteChanged();
      sectionViewModel.CleanCustomFilesCommand.RaiseCanExecuteChanged();
      sectionViewModel.FetchCount = 0;
      sectionViewModel.UpdateBadgeContent = "";
    }
  }

  private bool CanUpdateCustomFiles() => !this.IsCheckingForContentUpdates;

  private void CleanCustomFiles()
  {
    try
    {
      if (this.IsCheckingForContentUpdates)
      {
        this.CleanCustomFilesCommand.RaiseCanExecuteChanged();
      }
      else
      {
        string[] files = Directory.GetFiles(DataManager.Current.UserDocumentsCustomElementsDirectory, "*.index");
        if (!((IEnumerable<string>) files).Any<string>())
          return;
        List<string> stringList1 = new List<string>();
        StringBuilder stringBuilder1 = new StringBuilder("This action will remove the content from the following folders that are created by the index files:");
        stringBuilder1.AppendLine();
        foreach (string fileName in files)
        {
          string contentDirectory = IndexFile.FromFile(new FileInfo(fileName)).GetContentDirectory();
          stringBuilder1.AppendLine(contentDirectory ?? "");
          if (!contentDirectory.Equals(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, "user"), StringComparison.OrdinalIgnoreCase) && Directory.Exists(contentDirectory))
            stringList1.Add(contentDirectory);
        }
        stringBuilder1.AppendLine();
        stringBuilder1.AppendLine("Once removed you can run the content updates to download the latest content.");
        stringBuilder1.AppendLine();
        stringBuilder1.AppendLine("Continue?");
        if (MessageBox.Show(stringBuilder1.ToString(), "Clear Custom Files", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
          return;
        List<string> stringList2 = new List<string>();
        int num1 = 0;
        foreach (string path in stringList1)
        {
          if (Directory.Exists(path))
          {
            Directory.Delete(path, true);
            ++num1;
            stringList2.Add(path);
          }
        }
        StringBuilder stringBuilder2 = new StringBuilder("Your content folders have been cleared.");
        if (num1 == stringList1.Count)
        {
          this.FileChangedStatusMessage = stringBuilder2.ToString();
        }
        else
        {
          StringBuilder stringBuilder3 = new StringBuilder("The following content folders have been cleared:");
          foreach (string str in stringList2)
            stringBuilder3.AppendLine(str ?? "");
          int num2 = (int) MessageBox.Show(stringBuilder3.ToString(), "Content Cleared.");
        }
        this.UpdateCount = 0;
        this.RestartBadgeCount = "";
        this.UpdateBadgeContent = num1 > 0 ? $"{num1}" : "";
      }
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex, "Error while trying to clear custom folders.");
    }
    finally
    {
      this.CleanCustomFilesCommand.RaiseCanExecuteChanged();
    }
  }

  private bool CanCleanCustomFiles()
  {
    if (this.IsCheckingForContentUpdates)
      return false;
    string[] files = Directory.GetFiles(DataManager.Current.UserDocumentsCustomElementsDirectory, "*.index");
    List<string> source = new List<string>();
    foreach (string fileName in files)
    {
      string contentDirectory = IndexFile.FromFile(new FileInfo(fileName)).GetContentDirectory();
      if (Directory.Exists(contentDirectory))
        source.Add(contentDirectory);
    }
    return source.Any<string>();
  }

  private void IncludeCoreIndexIfMissing()
  {
    if (File.Exists(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, "core.index")))
      return;
    AnalyticsEventHelper.ApplicationEvent("auto_include_core");
    if (MessageBox.Show("You have added the supplements index file.\r\n\r\nIt is required to include at least the core index when adding index files. This will override the content from the bundled system reference document, will include all content from the core rulebooks, and will have the latest updates.\r\n\r\nDo you want to include the core index now?", "Aurora - Additional Content", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
      return;
    this.RemoteUrl = "https://raw.githubusercontent.com/aurorabuilder/elements/master/core.index";
    this.FetchCustomFile();
  }

  private void LoadIndices()
  {
    string[] files = Directory.GetFiles(DataManager.Current.UserDocumentsCustomElementsDirectory, "*.index", SearchOption.AllDirectories);
    this.Indices.Clear();
    foreach (IndexFile indexFile in ((IEnumerable<string>) files).Select<string, IndexFile>((Func<string, IndexFile>) (x => new IndexFile(new FileInfo(x)))))
    {
      indexFile.Load();
      if (indexFile.ContainsElementFiles())
        this.Indices.Add(indexFile);
    }
    this.ContainsIndices = this.Indices.Any<IndexFile>();
  }

  public bool ContainsIndices
  {
    get => this._containsIndices;
    set => this.SetProperty<bool>(ref this._containsIndices, value, nameof (ContainsIndices));
  }

  protected override void InitializeDesignData()
  {
    this._remoteUrl = "https://raw.githubusercontent.com/swdriessen/dndbuilder/master/elements/aurora.index";
    foreach (IndexFile indexFile in ((IEnumerable<string>) Directory.GetFiles("C:\\Users\\bas_d\\Documents\\5e Character Builder\\custom", "*.index", SearchOption.AllDirectories)).Select<string, IndexFile>((Func<string, IndexFile>) (x => new IndexFile(new FileInfo(x)))))
    {
      indexFile.Load();
      this.Indices.Add(indexFile);
    }
    this._selectedIndex = this.Indices.FirstOrDefault<IndexFile>();
  }

  public ICommand DownloadIndexCommand
  {
    get
    {
      return (ICommand) new RelayCommand<string>((Action<string>) (async parameter =>
      {
        if (string.IsNullOrWhiteSpace(parameter))
          return;
        string url = "";
        if (parameter != null)
        {
          switch (parameter)
          {
            case "core":
              url = "https://raw.githubusercontent.com/aurorabuilder/elements/master/core.index";
              break;
            case "dndwiki":
              url = "https://raw.githubusercontent.com/community-elements/elements-dndwiki/master/dndwiki.index";
              break;
            case "homebrew":
              url = "https://raw.githubusercontent.com/aurorabuilder/elements/master/homebrew.index";
              break;
            case "reddit":
              url = "https://raw.githubusercontent.com/community-elements/elements-reddit/master/reddit.index";
              break;
            case "supplements":
              url = "https://raw.githubusercontent.com/aurorabuilder/elements/master/supplements.index";
              break;
            case "third-party":
              url = "https://raw.githubusercontent.com/aurorabuilder/elements/master/third-party.index";
              break;
            case "unearthed-arcana":
              url = "https://raw.githubusercontent.com/aurorabuilder/elements/master/unearthed-arcana.index";
              break;
          }
        }
        if (string.IsNullOrWhiteSpace(url))
          return;
        await this.DownloadIndexAsync(url);
      }));
    }
  }

  public bool BundlesEnabled
  {
    get => this._bundlesEnabled;
    set => this.SetProperty<bool>(ref this._bundlesEnabled, value, nameof (BundlesEnabled));
  }

  public async void OnHandleEvent(IndexDownloadRequestEvent args)
  {
    SourcesSectionViewModel sectionViewModel = this;
    if (string.IsNullOrWhiteSpace(args.Url))
      return;
    await sectionViewModel.DownloadIndexAsync(args.Url);
    if (!sectionViewModel.CanUpdateCustomFiles())
      return;
    sectionViewModel.EventAggregator.Send<SelectionRuleNavigationArgs>(new SelectionRuleNavigationArgs(NavigationLocation.StartCustomContent));
  }

  private async Task DownloadIndexAsync(string url)
  {
    SourcesSectionViewModel sectionViewModel = this;
    try
    {
      if (string.IsNullOrWhiteSpace(url))
        return;
      url = url.Trim();
      if (!url.StartsWith("http") && !url.Contains("http"))
        url = "http://" + url;
      if (url.EndsWith(".index"))
      {
        sectionViewModel.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Downloading " + url));
        IndexFile indexFile = await IndexFile.FromUrl(url);
        string updateFilename = indexFile.Info.UpdateFilename;
        indexFile.SaveContent(new FileInfo(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, updateFilename)));
        sectionViewModel.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"The index file '{updateFilename}' has been downloaded successfully."));
        sectionViewModel.FetchCount++;
        sectionViewModel.UpdateBadgeContent = sectionViewModel.FetchCount.ToString();
        AnalyticsEventHelper.ContentDownloadIndex(url, true);
      }
      else
        MessageDialogService.Show("This field is used to fetch .index files only.");
    }
    catch (HttpRequestException ex)
    {
      ex.Data[(object) "404"] = (object) url;
      Logger.Exception((Exception) ex, nameof (DownloadIndexAsync));
      MessageDialogService.ShowException((Exception) ex);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (DownloadIndexAsync));
      MessageDialogService.ShowException(ex);
    }
  }

  public void OnHandleEvent(BundleCommandEvent args)
  {
    switch (args.Command)
    {
      case "on":
      case "enable":
        this.BundlesEnabled = true;
        break;
    }
    if (!this.BundlesEnabled)
      return;
    this.Settings.Bundle = true;
    this.Settings.Save();
  }
}
