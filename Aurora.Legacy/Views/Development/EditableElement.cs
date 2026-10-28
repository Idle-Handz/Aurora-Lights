// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.EditableElement
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class EditableElement : ObservableObject
{
  private string _name;
  private string _type;
  private string _source;
  private string _id;
  private string _description;
  private string _supports;
  private string _prerequisites;
  private string _requirements;
  private string _sheetAlternativeName;
  private string _sheetDescription;
  private bool _sheetDisplay;

  public EditableElement() => this._name = "New Element";

  public string Name
  {
    get => this._name;
    set => this.SetProperty<string>(ref this._name, value, nameof (Name));
  }

  public string Type
  {
    get => this._type;
    set => this.SetProperty<string>(ref this._type, value, nameof (Type));
  }

  public string Source
  {
    get => this._source;
    set => this.SetProperty<string>(ref this._source, value, nameof (Source));
  }

  public string Id
  {
    get => this._id;
    set => this.SetProperty<string>(ref this._id, value, nameof (Id));
  }

  public string Description
  {
    get => this._description;
    set => this.SetProperty<string>(ref this._description, value, nameof (Description));
  }

  public string Supports
  {
    get => this._supports;
    set => this.SetProperty<string>(ref this._supports, value, nameof (Supports));
  }

  public string Prerequisites
  {
    get => this._prerequisites;
    set => this.SetProperty<string>(ref this._prerequisites, value, nameof (Prerequisites));
  }

  public string Requirements
  {
    get => this._requirements;
    set => this.SetProperty<string>(ref this._requirements, value, nameof (Requirements));
  }

  public string SheetAlternativeName
  {
    get => this._sheetAlternativeName;
    set
    {
      this.SetProperty<string>(ref this._sheetAlternativeName, value, nameof (SheetAlternativeName));
    }
  }

  public string SheetDescription
  {
    get => this._sheetDescription;
    set => this.SetProperty<string>(ref this._sheetDescription, value, nameof (SheetDescription));
  }

  public bool SheetDisplay
  {
    get => this._sheetDisplay;
    set => this.SetProperty<bool>(ref this._sheetDisplay, value, nameof (SheetDisplay));
  }
}
