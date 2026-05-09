// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.BundleManagerSliderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data.Files;
using Builder.Data.Files.Updater;
using Builder.Core;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Properties;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public class BundleManagerSliderViewModel : ViewModelBase
{
  private readonly IndicesUpdateService _updateService;
  private bool _isCheckContentUpdatesOnStartupEnabled;
  private bool _isUpdatingFiles;

  public BundleManagerSliderViewModel()
  {
    this._updateService = new IndicesUpdateService(new Version(Resources.AppVersionCheck));
    this._updateService.StatusChanged += new EventHandler<IndicesUpdateStatusChangedEventArgs>(this.UpdateServiceStatusChanged);
    if (this.IsInDesignMode)
    {
      IndexFile indexFile1 = new IndexFile((FileInfo) null)
      {
        Info = {
          DisplayName = "Core",
          Description = "The content from the core rulebooks.",
          Author = "Wizards of the Coast",
          UpdateFilename = "core.index",
          UpdateUrl = "online/core.index",
          Version = new Version(1, 0, 1)
        }
      };
      IndexFile indexFile2 = new IndexFile((FileInfo) null)
      {
        Info = {
          DisplayName = "Supplements",
          Description = "Supplements from Wizards of the Coast to expand your core gaming elements.",
          Author = "Wizards of the Coast",
          UpdateFilename = "supplements.index",
          UpdateUrl = "online/supplements.index",
          Version = new Version(1, 2, 1)
        }
      };
      foreach (IndexFile file in ((IEnumerable<string>) Directory.GetFiles("C:\\Users\\bas_d\\Documents\\5e Character Builder\\custom", "*.index", SearchOption.AllDirectories)).Select<string, IndexFile>((Func<string, IndexFile>) (x => new IndexFile(new FileInfo(x)))))
      {
        file.Load();
        this.Indices.Add(new ContentFileContainer(file));
      }
    }
    else
    {
      this.UpdateFilesCommand = new RelayCommand(new Action(this.UpdateFiles), new Func<bool>(this.CanUpdateFiles));
      this.IsCheckContentUpdatesOnStartupEnabled = this.Settings.StartupCheckForContentUpdated;
      this.LoadIndices();
      this.SubscribeWithEventAggregator();
    }
  }

  public bool IsCheckContentUpdatesOnStartupEnabled
  {
    get => this._isCheckContentUpdatesOnStartupEnabled;
    set
    {
      this.SetProperty<bool>(ref this._isCheckContentUpdatesOnStartupEnabled, value, nameof (IsCheckContentUpdatesOnStartupEnabled));
      this.Settings.StartupCheckForContentUpdated = value;
      this.Settings.Save();
    }
  }

  public bool IsUpdatingFiles
  {
    get => this._isUpdatingFiles;
    set
    {
      this.SetProperty<bool>(ref this._isUpdatingFiles, value, nameof (IsUpdatingFiles));
      this.UpdateFilesCommand?.RaiseCanExecuteChanged();
    }
  }

  public ObservableCollection<ContentFileContainer> Indices { get; } = new ObservableCollection<ContentFileContainer>();

  public RelayCommand UpdateFilesCommand { get; }

  private void LoadIndices()
  {
    string[] files = Directory.GetFiles(DataManager.Current.UserDocumentsCustomElementsDirectory, "*.index", SearchOption.AllDirectories);
    this.Indices.Clear();
    foreach (IndexFile file in ((IEnumerable<string>) files).Select<string, IndexFile>((Func<string, IndexFile>) (x => new IndexFile(new FileInfo(x)))))
    {
      file.Load();
      if (file.ContainsElementFiles())
        this.Indices.Add(new ContentFileContainer(file));
    }
  }

  private void UpdateServiceStatusChanged(object sender, IndicesUpdateStatusChangedEventArgs e)
  {
    this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(e.StatusMessage ?? string.Empty)
    {
      IsSuccess = true,
      ProgressPercentage = e.ProgressPercentage
    });
  }

  private bool CanUpdateFiles() => !this.IsUpdatingFiles;

  private async void UpdateFiles()
  {
    try
    {
      this.IsUpdatingFiles = true;
      this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Checking for content updates..."));
      bool updated = await this._updateService.UpdateIndexFiles(DataManager.Current.UserDocumentsCustomElementsDirectory);
      this.LoadIndices();
      if (updated)
      {
        this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Your content files have been updated, restart the application to reload the content."));
        this.EventAggregator.Send<SelectionRuleNavigationArgs>(new SelectionRuleNavigationArgs(NavigationLocation.StartCustomContent));
        if (MessageBox.Show("Your content files have been updated, do you want to restart the application to reload the content?", "Aurora", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
          ApplicationManager.Current.RestartApplication();
      }
      else
        this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("Last checked for content updates at " + DateTime.Now.ToShortTimeString()));
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
      this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(ex.Message ?? "Unable to update content files.")
      {
        IsDanger = true
      });
    }
    finally
    {
      this.IsUpdatingFiles = false;
    }
  }
}
