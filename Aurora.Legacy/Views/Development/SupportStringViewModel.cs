// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.SupportStringViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class SupportStringViewModel : ViewModelBase
{
  private ElementsOrganizer _organizer;
  private string _supportString;
  private string _selectedType;
  private ElementBase _selectedSupportedElement;
  private string _regexString;
  private string _message;
  private string _generatedSupport;

  public SupportStringViewModel()
  {
    this._supportString = "";
    if (this.IsInDesignMode)
      return;
    this._organizer = new ElementsOrganizer((IEnumerable<ElementBase>) DataManager.Current.ElementsCollection);
    this.Elements.AddRange(this._organizer.Elements);
    this.Types.Add("");
    this.Types.AddRange((IEnumerable<string>) ElementTypeStrings.All);
  }

  public List<string> Types { get; } = new List<string>();

  public string SelectedType
  {
    get => this._selectedType;
    set
    {
      this.SetProperty<string>(ref this._selectedType, value, nameof (SelectedType));
      this.RunSupports();
    }
  }

  public ElementBaseCollection Elements { get; } = new ElementBaseCollection();

  public ElementBaseCollection SupportedElements { get; } = new ElementBaseCollection();

  public ElementBase SelectedSupportedElement
  {
    get => this._selectedSupportedElement;
    set
    {
      this.SetProperty<ElementBase>(ref this._selectedSupportedElement, value, nameof (SelectedSupportedElement));
    }
  }

  public string SupportString
  {
    get => this._supportString;
    set => this.SetProperty<string>(ref this._supportString, value, nameof (SupportString));
  }

  public string RegexString
  {
    get => this._regexString;
    set => this.SetProperty<string>(ref this._regexString, value, nameof (RegexString));
  }

  public string Message
  {
    get => this._message;
    set => this.SetProperty<string>(ref this._message, value, nameof (Message));
  }

  public string GeneratedSupport
  {
    get => this._generatedSupport;
    set => this.SetProperty<string>(ref this._generatedSupport, value, nameof (GeneratedSupport));
  }

  public void RunSupports()
  {
    this.Message = "";
    this.SupportedElements.Clear();
    this.SelectedSupportedElement = (ElementBase) null;
    try
    {
      string supportString = this.SupportString;
      ElementBaseCollection elements = new ElementBaseCollection(this.Elements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == this.SelectedType)));
      ExpressionInterpreter expressionInterpreter = new ExpressionInterpreter();
      if (!string.IsNullOrWhiteSpace(supportString))
        this.SupportedElements.AddRange(expressionInterpreter.EvaluateSupportsExpression<ElementBase>(supportString, (IEnumerable<ElementBase>) elements));
      else
        this.SupportedElements.AddRange((IEnumerable<ElementBase>) elements);
      if (string.IsNullOrWhiteSpace(supportString))
      {
        this.SupportedElements.Clear();
        this.SupportedElements.AddRange(this.Elements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == this.SelectedType)));
        this.Message = $"{this.SupportedElements.Count} supported {this.SelectedType} elements found";
        this.GeneratedSupport = "";
      }
      else
      {
        this.Message = $"{this.SupportedElements.Count} supported {this.SelectedType} elements found matching [{supportString}]";
        this.GeneratedSupport = $"<select type=\"{this.SelectedType}\" name=\"Your Select Name\" supports=\"{supportString}\" />";
      }
    }
    catch (Exception ex)
    {
      this.Message = ex.Message;
    }
  }
}
