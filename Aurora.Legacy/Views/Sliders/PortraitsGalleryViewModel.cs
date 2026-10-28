// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.PortraitsGalleryViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Presentation.Data;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public class PortraitsGalleryViewModel : ViewModelBase
{
  private List<string> _files = new List<string>();
  private bool _isShowAllPortraits;
  private bool _showMalePortraits = true;
  private bool _showFemalePortraits;
  private string _selectedPortrait;
  private bool _initial = true;
  private int _portraitsCount;
  private bool _hasResults;

  public PortraitsGalleryViewModel()
  {
    if (this.IsInDesignMode)
    {
      foreach (string portraitFilename in DesignData.PortraitFilenames)
        this.Portraits.Add(portraitFilename);
      this.SelectedPortrait = this.Portraits.FirstOrDefault<string>();
      this.HasResults = true;
    }
    else
    {
      this.GetFiles();
      FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(DataManager.Current.UserDocumentsPortraitsDirectory);
      fileSystemWatcher.Filter = "*.*";
      fileSystemWatcher.EnableRaisingEvents = true;
      fileSystemWatcher.Created += new FileSystemEventHandler(this.PortraitsFolderFilesChanged);
      fileSystemWatcher.Renamed += new RenamedEventHandler(this.PortraitsFolderFilesChanged);
      fileSystemWatcher.Deleted += new FileSystemEventHandler(this.PortraitsFolderFilesChanged);
      CharacterManager.Current.Character.PropertyChanged += new PropertyChangedEventHandler(this.Character_PropertyChanged);
    }
  }

  private void Character_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    if (!(e.PropertyName == "Gender"))
      return;
    if (this.Character.Gender.ToLower() == "male")
    {
      this.ShowMalePortraits = true;
      this.ShowFemalePortraits = false;
    }
    if (!(this.Character.Gender.ToLower() == "female"))
      return;
    this.ShowMalePortraits = false;
    this.ShowFemalePortraits = true;
  }

  private void PortraitsFolderFilesChanged(object sender, FileSystemEventArgs e)
  {
    if (!e.FullPath.EndsWith(".jpg") && !e.FullPath.EndsWith(".jpeg") && !e.FullPath.EndsWith(".png"))
      return;
    this.GetFiles();
  }

  private void GetFiles()
  {
    this._files = ((IEnumerable<string>) Directory.GetFiles(Path.Combine(DataManager.Current.UserDocumentsPortraitsDirectory))).Where<string>((Func<string, bool>) (x => x.ToLower().EndsWith(".jpg") || x.ToLower().EndsWith(".jpeg") || x.ToLower().EndsWith(".png"))).ToList<string>();
  }

  public Character Character => CharacterManager.Current.Character;

  public ObservableCollection<string> Portraits { get; } = new ObservableCollection<string>();

  public string SelectedPortrait
  {
    get => this._selectedPortrait;
    set => this.SetProperty<string>(ref this._selectedPortrait, value, nameof (SelectedPortrait));
  }

  public bool IsShowAllPortraits
  {
    get => this._isShowAllPortraits;
    set
    {
      this.SetProperty<bool>(ref this._isShowAllPortraits, value, nameof (IsShowAllPortraits));
      this.InitializeAsync();
    }
  }

  public bool ShowMalePortraits
  {
    get => this._showMalePortraits;
    set
    {
      this.SetProperty<bool>(ref this._showMalePortraits, value, nameof (ShowMalePortraits));
      if (!this._showMalePortraits && !this._showFemalePortraits)
        this.ShowFemalePortraits = true;
      else
        this.InitializeAsync();
    }
  }

  public bool ShowFemalePortraits
  {
    get => this._showFemalePortraits;
    set
    {
      this.SetProperty<bool>(ref this._showFemalePortraits, value, nameof (ShowFemalePortraits));
      if (!this._showFemalePortraits && !this._showMalePortraits)
        this.ShowMalePortraits = true;
      else
        this.InitializeAsync();
    }
  }

  public int PortraitsCount
  {
    get => this._portraitsCount;
    set => this.SetProperty<int>(ref this._portraitsCount, value, nameof (PortraitsCount));
  }

  public override Task InitializeAsync()
  {
    if (this._initial)
    {
      if (CharacterManager.Current.Character.Gender.ToLower() == "female")
      {
        this._showFemalePortraits = true;
        this._showMalePortraits = false;
        this.OnPropertyChanged("ShowFemalePortraits", "ShowMalePortraits");
      }
      this._initial = false;
    }
    ElementBaseCollection elements = CharacterManager.Current.GetElements();
    string lower1 = elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Race")))?.Name.ToLower();
    string lower2 = elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Sub Race")))?.Name.ToLower();
    this.Portraits.Clear();
    foreach (string file in this._files)
    {
      if (this.IsShowAllPortraits)
      {
        if (this.ShowMalePortraits && this.ShowFemalePortraits)
        {
          this.Portraits.Add(file);
        }
        else
        {
          string lower3 = file.ToLower();
          if (this.ShowMalePortraits && !lower3.Contains("female"))
            this.Portraits.Add(file);
          else if (this.ShowFemalePortraits && lower3.Contains("female"))
            this.Portraits.Add(file);
        }
      }
      else
      {
        string lower4 = file.ToLower();
        if (lower1 != null)
        {
          string str = lower1.Replace("-", " ").ToLower().Replace("half ", "");
          if (file.Contains(str) && (str != "orc" || !file.Contains("sorcerer")))
          {
            if (this.ShowMalePortraits && !lower4.Contains("female"))
              this.Portraits.Add(file);
            else if (this.ShowFemalePortraits && lower4.Contains("female"))
              this.Portraits.Add(file);
          }
        }
        if (lower2 != null && file.Contains(lower2.Replace("-", " ")))
        {
          if (this.ShowMalePortraits && !lower4.Contains("female"))
            this.Portraits.Add(file);
          else if (this.ShowFemalePortraits && lower4.Contains("female"))
            this.Portraits.Add(file);
        }
      }
    }
    if (this.Portraits.Distinct<string>().Count<string>() != this.Portraits.Count)
    {
      List<string> list = this.Portraits.Distinct<string>().ToList<string>();
      this.Portraits.Clear();
      foreach (string str in list)
        this.Portraits.Add(str);
    }
    this.PortraitsCount = this.Portraits.Count;
    this.HasResults = this.Portraits.Count > 0;
    return base.InitializeAsync();
  }

  public bool HasResults
  {
    get => this._hasResults;
    set => this.SetProperty<bool>(ref this._hasResults, value, nameof (HasResults));
  }
}
