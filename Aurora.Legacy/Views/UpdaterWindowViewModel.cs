// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.UpdaterWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data.Files;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

#nullable disable
namespace Builder.Presentation.Views;

public class UpdaterWindowViewModel : ViewModelBase
{
  private string _statusMessage;
  private int _progressPercentage;
  private string _fileName;
  private InformationSection _informationBlock;

  public UpdaterWindowViewModel()
  {
    this._statusMessage = "Index Updater";
    this._progressPercentage = 67;
    if (Debugger.IsAttached)
      Debugger.Break();
    this._informationBlock = new InformationSection();
    this._informationBlock.DisplayName = "Display Name";
    this._informationBlock.Description = "Description";
    this._informationBlock.Author = "Author";
    this._informationBlock.UpdateUrl = "indices/20160101.index";
    if (this.IsInDesignMode)
    {
      this._fileName = "unearthed-arcana";
      this._informationBlock.DisplayName = "DisplayName";
      this._informationBlock.Description = "Description";
      this._informationBlock.Author = "Author";
      this._informationBlock.UpdateFilename = this._fileName + ".index";
      this._informationBlock.UpdateUrl = "UpdateUrl";
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

  public string FileName
  {
    get => this._fileName;
    set
    {
      this.SetProperty<string>(ref this._fileName, value, nameof (FileName));
      this.InformationBlock.UpdateFilename = this._fileName + ".index";
    }
  }

  public InformationSection InformationBlock
  {
    get => this._informationBlock;
    set
    {
      this.SetProperty<InformationSection>(ref this._informationBlock, value, nameof (InformationBlock));
    }
  }

  public RelayCommand GenerateIndexCommand => new RelayCommand(new Action(this.GenerateIndexFile));

  public RelayCommand LoadCommand => new RelayCommand(new Action(this.Load));

  public RelayCommand UpdateCommand => new RelayCommand(new Action(this.Update));

  private void Load()
  {
    IndexFile indexFile = new IndexFile(new FileInfo(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, "unearthed-arcana.index")));
    indexFile.Load();
    Console.WriteLine();
    indexFile.Save();
  }

  private void Update()
  {
    string path = Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, "unearthed-arcana");
    if (Directory.Exists(path))
      return;
    Directory.CreateDirectory(path);
  }

  public void GenerateIndexFile()
  {
    FileInfo fileInfo = new FileInfo(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, this.FileName + ".index"));
    IndexFile indexFile = new IndexFile(fileInfo);
    indexFile.Info.DisplayName = this.InformationBlock.DisplayName;
    indexFile.Info.Description = this.InformationBlock.Description;
    indexFile.Info.Author = this.InformationBlock.Author;
    indexFile.Info.AuthorUrl = "http:";
    indexFile.Info.Revised = UpdaterWindowViewModel.GetCurrentTimestamp();
    indexFile.Info.UpdateFilename = this.InformationBlock.UpdateFilename;
    indexFile.Info.UpdateUrl = this.InformationBlock.UpdateUrl;
    indexFile.Files.Add(new IndexFile.FileEntry("20150202.xml", "http://dnd.wizards.com/articles/unearthed-arcana/unearthed-arcana-eberron/20150202.xml"));
    indexFile.Files.Add(new IndexFile.FileEntry("20150302.xml", "http://dnd.wizards.com/articles/unearthed-arcana/unearthed-arcana-eberron/20150302.xml"));
    indexFile.Files.Add(new IndexFile.FileEntry("20150406.xml", "http://dnd.wizards.com/articles/unearthed-arcana/unearthed-arcana-eberron/20150406.xml"));
    indexFile.Files.Add(new IndexFile.FileEntry("20111111.xml", "data/elements/some-obsolete-file.xml", true));
    indexFile.Save();
    this.StatusMessage = "saved index file: " + fileInfo.Name;
  }

  private async Task DownloadFiles()
  {
    this.StatusMessage = "Downloading Files...";
    this.ProgressPercentage = 0;
    await this.DownloadFile("phb-spells.xml", "https://raw.githubusercontent.com/swdriessen/Builder-Data-Elements/master/elements/core/players-handbook/spells.xml");
    this.ProgressPercentage = 33;
    await this.DownloadFile("pota-spells.xml", "https://raw.githubusercontent.com/swdriessen/Builder-Data-Elements/master/elements/adventures/princes-of-the-apocalypse/spells.xml");
    this.ProgressPercentage = 67;
    await this.DownloadFile("scag-spells.xml", "https://raw.githubusercontent.com/swdriessen/Builder-Data-Elements/master/elements/supplements/sword-coast-adventurers-guide/spells.xml");
    this.ProgressPercentage = 100;
  }

  private async Task DownloadFile(string name, string url)
  {
    string stringAsync = await new HttpClient().GetStringAsync(url);
    File.WriteAllText(Path.Combine(Path.Combine(DataManager.Current.UserDocumentsCustomElementsDirectory, "unearthed-arcana"), name), stringAsync);
  }

  private static string GetCurrentTimestamp()
  {
    return ((int) (TimeZoneInfo.ConvertTimeToUtc(DateTime.Now) - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds).ToString();
  }
}
