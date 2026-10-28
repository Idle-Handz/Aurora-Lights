// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.SaveCharacterWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.ViewModels;

public sealed class SaveCharacterWindowViewModel : ViewModelBase
{
  private string _filename;
  private CharacterFile _characterFile;

  public SaveCharacterWindowViewModel()
  {
    if (!this.IsInDesignMode)
      return;
    this._characterFile = new CharacterFile("path")
    {
      DisplayName = "Jalan Melthrohe",
      DisplayLevel = "3",
      DisplayRace = "Human",
      DisplayClass = "Fighter",
      DisplayPortraitFilePath = ""
    };
    this._filename = "jalan-3";
  }

  public Character Character => CharacterManager.Current.Character;

  public string Filename
  {
    get => this._filename;
    set
    {
      this.SetProperty<string>(ref this._filename, value, nameof (Filename));
      this.SanitizeFilename();
    }
  }

  public CharacterFile CharacterFile
  {
    get => this._characterFile;
    set => this.SetProperty<CharacterFile>(ref this._characterFile, value, nameof (CharacterFile));
  }

  public ICommand SaveCharacterCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.SaveCharacter));
  }

  public override Task InitializeAsync(InitializationArguments args)
  {
    if (!(args.Argument is CharacterFile characterFile))
      throw new NullReferenceException("file");
    this.CharacterFile = characterFile;
    this.Filename = this.CharacterFile.IsNew ? this.CharacterFile.DisplayName : new FileInfo(this.CharacterFile.FilePath).Name.Replace(".dnd5e", "");
    return base.InitializeAsync(args);
  }

  private void SaveCharacter()
  {
    if (string.IsNullOrWhiteSpace(this.Filename))
      this.Filename = this.CharacterFile.DisplayName.ToLower();
    if (File.Exists(this.Filename))
      Logger.Info(this.Filename + " exists, overwriting file.");
    this.CharacterFile.Save();
  }

  private void SanitizeFilename()
  {
    string filename = this.Filename;
    this.CharacterFile.FilePath = Path.Combine(DataManager.Current.GetCombinedCharacterFilePath(((IEnumerable<char>) Path.GetInvalidFileNameChars()).Aggregate<char, string>(filename, (Func<string, char, string>) ((current, invalidChar) => current.Replace(invalidChar.ToString(), "")))));
  }
}
