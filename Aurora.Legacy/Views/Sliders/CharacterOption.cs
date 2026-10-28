// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.CharacterOption
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Presentation.Utilities;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public class CharacterOption : ObservableObject
{
  private string _header;
  private string _description;
  private bool _isSelected;
  private bool _isEnabled;
  private bool _isCustom;
  private bool _isActive;

  public CharacterOption(ElementBase element)
  {
    this.Element = element;
    if (element == null)
      return;
    this.Header = element.Name;
    this.Description = !element.ElementSetters.ContainsSetter("short") ? (!element.SheetDescription.Any<ElementSheetDescriptions.SheetDescription>() ? ElementDescriptionGenerator.GeneratePlainDescription(element.Description) : element.SheetDescription.FirstOrDefault<ElementSheetDescriptions.SheetDescription>()?.Description) : element.ElementSetters.GetSetter("short").Value.ToString();
    this.IsSelected = element.Source == "Internal";
    this.IsEnabled = true;
    this.IsCustom = element.Source != "Internal";
  }

  public ElementBase Element { get; }

  public string Header
  {
    get => this._header;
    set => this.SetProperty<string>(ref this._header, value, nameof (Header));
  }

  public string Description
  {
    get => this._description;
    set => this.SetProperty<string>(ref this._description, value, nameof (Description));
  }

  public bool IsSelected
  {
    get => this._isSelected;
    set => this.SetProperty<bool>(ref this._isSelected, value, nameof (IsSelected));
  }

  public bool IsEnabled
  {
    get => this._isEnabled;
    set => this.SetProperty<bool>(ref this._isEnabled, value, nameof (IsEnabled));
  }

  public bool IsCustom
  {
    get => this._isCustom;
    set => this.SetProperty<bool>(ref this._isCustom, value, nameof (IsCustom));
  }

  public bool IsActive
  {
    get => this._isActive;
    set => this.SetProperty<bool>(ref this._isActive, value, nameof (IsActive));
  }

  public override string ToString() => $"{this.Header} ({this.IsSelected})";
}
