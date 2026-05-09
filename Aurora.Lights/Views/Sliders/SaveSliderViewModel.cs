// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.SaveSliderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Telemetry;
using Builder.Presentation.ViewModels.Base;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public sealed class SaveSliderViewModel : ViewModelBase
{
  private CharacterFile _characterFile;
  private string _portraitFileName;
  private string _displayName;
  private string _displayBuild;
  private string _filename;
  private bool _isFileNameSanitized;
  private bool _isPortraitEmbedded;
  private bool _isFavorite;
  private string _groupName;
  private string _filepath;

  public event EventHandler? CloseRequested;

  public Builder.Presentation.Models.Character Character => CharacterManager.Current.Character;

  public SaveSliderViewModel()
  {
    if (!this.IsInDesignMode)
      return;
    this.InitializeDesignData();
  }

  public CharacterFile CharacterFile
  {
    get => this._characterFile;
    set => this.SetProperty<CharacterFile>(ref this._characterFile, value, nameof (CharacterFile));
  }

  public string PortraitFileName
  {
    get => this._portraitFileName;
    set => this.SetProperty<string>(ref this._portraitFileName, value, nameof (PortraitFileName));
  }

  public string DisplayName
  {
    get => this._displayName;
    set => this.SetProperty<string>(ref this._displayName, value, nameof (DisplayName));
  }

  public string DisplayBuild
  {
    get => this._displayBuild;
    set => this.SetProperty<string>(ref this._displayBuild, value, nameof (DisplayBuild));
  }

  public string Filename
  {
    get => this._filename;
    set
    {
      this.SetProperty<string>(ref this._filename, value, nameof (Filename));
      if (this._filename == null)
        return;
      this.SanitizeFilename();
    }
  }

  public bool IsFileNameSanitized
  {
    get => this._isFileNameSanitized;
    set
    {
      this.SetProperty<bool>(ref this._isFileNameSanitized, value, nameof (IsFileNameSanitized));
    }
  }

  public bool IsPortraitEmbedded
  {
    get => this._isPortraitEmbedded;
    set => this.SetProperty<bool>(ref this._isPortraitEmbedded, value, nameof (IsPortraitEmbedded));
  }

  public bool IsFavorite
  {
    get => this._isFavorite;
    set => this.SetProperty<bool>(ref this._isFavorite, value, nameof (IsFavorite));
  }

  public string GroupName
  {
    get => this._groupName;
    set => this.SetProperty<string>(ref this._groupName, value, nameof (GroupName));
  }

  public ObservableCollection<string> Groups { get; } = new ObservableCollection<string>();

  public string Filepath
  {
    get => this._filepath;
    set => this.SetProperty<string>(ref this._filepath, value, nameof (Filepath));
  }

  public ICommand SaveCommand => (ICommand) new RelayCommand(new Action(this.Save));

  public ICommand CancelCommand => (ICommand) new RelayCommand(new Action(this.Cancel));

  public ICommand ChangePortraitCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ChangePortrait));
  }

  public override Task InitializeAsync(InitializationArguments args)
  {
    this.CharacterFile = args.Argument as CharacterFile;
    this.Groups.Clear();
    foreach (string characterGroup in (Collection<string>) ApplicationManager.Current.CharacterGroups)
      this.Groups.Add(characterGroup);
    this.PortraitFileName = CharacterManager.Current.Character.PortraitFilename;
    this.DisplayName = CharacterManager.Current.Character.Name;
    this.DisplayBuild = CharacterManager.Current.Character.CharacterBuildString;
    this.IsPortraitEmbedded = true;
    if (this.CharacterFile.IsNew)
    {
      this.Filename = this.DisplayName;
      this.IsFavorite = false;
      this.GroupName = "Characters";
    }
    else
    {
      this.Filename = new FileInfo(this.CharacterFile.FilePath).Name.Replace(".dnd5e", "");
      this.IsFavorite = this.CharacterFile.IsFavorite;
      this.GroupName = this.CharacterFile.CollectionGroupName;
    }
    return (Task) Task.FromResult<bool>(true);
  }

  private void ChangePortrait()
  {
    try
    {
      OpenFileDialog openFileDialog1 = new OpenFileDialog();
      openFileDialog1.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
      OpenFileDialog openFileDialog2 = openFileDialog1;
      bool? nullable = openFileDialog2.ShowDialog();
      if (!nullable.HasValue || !nullable.Value)
        return;
      string portraitsDirectory = DataManager.Current.UserDocumentsPortraitsDirectory;
      FileInfo fileInfo = new FileInfo(openFileDialog2.FileName);
      string lower = fileInfo.Name.ToLower();
      string str = Path.Combine(portraitsDirectory, lower);
      if (!File.Exists(str))
      {
        using (Bitmap bitmap = new Bitmap(Image.FromFile(fileInfo.FullName, false)))
          bitmap.Save(str);
      }
      CharacterManager.Current.Character.PortraitFilename = str;
      this.PortraitFileName = str;
      CharacterManager.Current.Status.IsUserPortrait = true;
    }
    catch (IOException ex)
    {
      Logger.Exception((Exception) ex, nameof (ChangePortrait));
      int num = (int) MessageBox.Show(ex.Message, "IO Exception @ ChangePortrait");
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (ChangePortrait));
      int num = (int) MessageBox.Show(ex.Message, "Exception @ ChangePortrait");
    }
  }

  private void Save()
  {
    this.EventAggregator.Send<CharacterSavingEvent>(new CharacterSavingEvent());
    try
    {
      CharacterFile file = CharacterManager.Current.File;
      string str1 = this.SanitizeFilename();
      string str2 = file.IsNew ? DataManager.Current.UserDocumentsRootDirectory : new FileInfo(file.FilePath).DirectoryName;
      SaveFileDialog saveFileDialog = new SaveFileDialog();
      saveFileDialog.DefaultExt = "dnd5e";
      saveFileDialog.AddExtension = true;
      saveFileDialog.FileName = str1;
      saveFileDialog.InitialDirectory = str2;
      saveFileDialog.Title = $"Save Character ({this.Character.Name})";
      saveFileDialog.Filter = "DND5E (*.dnd5e)|*.dnd5e";
      bool? nullable = saveFileDialog.ShowDialog();
      bool flag = true;
      if (nullable.GetValueOrDefault() == flag & nullable.HasValue)
      {
        if (!saveFileDialog.FileName.Equals(file.FilePath))
        {
          file = new CharacterFile(saveFileDialog.FileName);
          file.IsNew = true;
        }
        file.FileName = saveFileDialog.SafeFileName;
        file.FilePath = saveFileDialog.FileName;
        file.InitializeDisplayPropertiesFromCharacter(this.Character);
        file.IsFavorite = this.IsFavorite;
        file.DisplayPortraitFilePath = this.PortraitFileName;
        file.CollectionGroupName = this.GroupName;
        if (file.Save())
        {
          CharacterManager.Current.Status.HasChanges = false;
          this.EventAggregator.Send<CharacterSavedEvent>(new CharacterSavedEvent(file));
          AnalyticsEventHelper.CharacterSave(file.DisplayRace, file.DisplayClass, file.DisplayBackground, file.DisplayLevel, CharacterManager.Current.Status.HasMulticlass, CharacterManager.Current.Status.HasSpellcasting, CharacterManager.Current.Status.HasCompanion);
          this.CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        if (!file.FilePath.Contains(DataManager.Current.UserDocumentsRootDirectory))
          DataManager.Current.AppendCharacterFileLocation(file.FilePath);
      }
      file.FilePath.Equals(this.Filepath);
      int num = this.IsPortraitEmbedded ? 1 : 0;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (Save));
      MessageDialogService.ShowException(ex);
    }
  }

  private void Cancel()
  {
  }

  protected override void InitializeDesignData()
  {
    this._portraitFileName = "../../Resources/default-portrait.png";
    this._displayName = "Jalan Melthrohe";
    this._displayBuild = "Level 5 Human Fighter (Eldritch Knight)";
    this._filename = "jalan5";
    this._filepath = "a cool file path //";
    this._isPortraitEmbedded = true;
    this._isFavorite = true;
    this._isFileNameSanitized = true;
    this._groupName = "group";
  }

  private string SanitizeFilename()
  {
    char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
    string filename = this.Filename;
    foreach (char ch in invalidFileNameChars)
      filename = filename.Replace(ch.ToString(), "");
    this.IsFileNameSanitized = this.Filename != filename;
    this.Filepath = Path.Combine(DataManager.Current.GetCombinedCharacterFilePath(filename));
    return filename;
  }
}
