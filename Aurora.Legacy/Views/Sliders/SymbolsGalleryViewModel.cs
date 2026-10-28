// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.SymbolsGalleryViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Data;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public class SymbolsGalleryViewModel : ViewModelBase
{
  private List<string> _files = new List<string>();
  private string _selectedPortrait;
  private bool _initial = true;
  private int _portraitsCount;
  private bool _hasResults;

  public SymbolsGalleryViewModel()
  {
    if (this.IsInDesignMode)
    {
      foreach (string symbolFilename in DesignData.SymbolFilenames)
        this.Portraits.Add(symbolFilename);
      this.SelectedPortrait = this.Portraits.FirstOrDefault<string>();
      this.HasResults = true;
    }
    else
    {
      this.GetFiles();
      FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(DataManager.Current.UserDocumentsSymbolsGalleryDirectory);
      fileSystemWatcher.Filter = "*.*";
      fileSystemWatcher.EnableRaisingEvents = true;
      fileSystemWatcher.Created += new FileSystemEventHandler(this.GalleryFolderFilesChanged);
      fileSystemWatcher.Renamed += new RenamedEventHandler(this.GalleryFolderFilesChanged);
      fileSystemWatcher.Deleted += new FileSystemEventHandler(this.GalleryFolderFilesChanged);
    }
  }

  private void GalleryFolderFilesChanged(object sender, FileSystemEventArgs e)
  {
    if (!e.FullPath.EndsWith(".jpg") && !e.FullPath.EndsWith(".jpeg") && !e.FullPath.EndsWith(".png"))
      return;
    this.GetFiles();
  }

  public Character Character => CharacterManager.Current.Character;

  public ObservableCollection<string> Portraits { get; } = new ObservableCollection<string>();

  public string SelectedPortrait
  {
    get => this._selectedPortrait;
    set => this.SetProperty<string>(ref this._selectedPortrait, value, nameof (SelectedPortrait));
  }

  public int PortraitsCount
  {
    get => this._portraitsCount;
    set => this.SetProperty<int>(ref this._portraitsCount, value, nameof (PortraitsCount));
  }

  public bool HasResults
  {
    get => this._hasResults;
    set => this.SetProperty<bool>(ref this._hasResults, value, nameof (HasResults));
  }

  public override Task InitializeAsync()
  {
    if (this._initial)
      this._initial = false;
    this.Portraits.Clear();
    foreach (string file in this._files)
      this.Portraits.Add(file);
    this.PortraitsCount = this.Portraits.Count;
    this.HasResults = this.Portraits.Count > 0;
    return base.InitializeAsync();
  }

  private void GetFiles()
  {
    this._files = ((IEnumerable<string>) Directory.GetFiles(Path.Combine(DataManager.Current.UserDocumentsSymbolsGalleryDirectory))).Where<string>((Func<string, bool>) (x => x.ToLower().EndsWith(".jpg") || x.ToLower().EndsWith(".jpeg") || x.ToLower().EndsWith(".png"))).ToList<string>();
  }
}
