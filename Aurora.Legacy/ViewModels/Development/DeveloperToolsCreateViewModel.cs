// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Development.DeveloperToolsCreateViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.Views.Development;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Xml;

#nullable disable
namespace Builder.Presentation.ViewModels.Development;

public class DeveloperToolsCreateViewModel : ViewModelBase
{
  private readonly ElementBaseCollection _elements = new ElementBaseCollection();
  private GenerationElement _element;
  private string _generatedXmlNode;
  private bool _isAutoGenerateIdEnabled;
  private bool _isCustomElement;
  private string _customAbbreviation;
  private ElementBase _selectedElement;
  private string _selectedSupport;

  public DeveloperToolsCreateViewModel()
  {
    this._isAutoGenerateIdEnabled = true;
    this._isCustomElement = true;
    this._customAbbreviation = "WOTC";
    if (this.IsInDesignMode)
    {
      this.GeneratedXmlNode = "<element name=\"design generated\"";
    }
    else
    {
      this._elements.AddRange((IEnumerable<ElementBase>) DataManager.Current.ElementsCollection);
      this.Elements.AddRange((IEnumerable<ElementBase>) this._elements);
      this.SelectedElement = this.Elements.First<ElementBase>();
      this._element = new GenerationElement();
      this._element.PropertyChanged += new PropertyChangedEventHandler(this.ElementPropertyChanged);
      this.ElementTypes = new List<string>(this._elements.GroupBy<ElementBase, string>((Func<ElementBase, string>) (e => e.Type)).Select<IGrouping<string, ElementBase>, string>((Func<IGrouping<string, ElementBase>, string>) (g => g.First<ElementBase>().Type)));
      this.ElementSources = new List<string>(this._elements.GroupBy<ElementBase, string>((Func<ElementBase, string>) (e => e.Source)).Select<IGrouping<string, ElementBase>, string>((Func<IGrouping<string, ElementBase>, string>) (g => g.First<ElementBase>().Source)));
      List<string> list1 = this._elements.Select<ElementBase, List<string>>((Func<ElementBase, List<string>>) (x => x.Supports)).SelectMany<List<string>, string>((Func<List<string>, IEnumerable<string>>) (x => (IEnumerable<string>) x)).GroupBy<string, string>((Func<string, string>) (x => x)).Select<IGrouping<string, string>, string>((Func<IGrouping<string, string>, string>) (x => x.First<string>())).Where<string>((Func<string, bool>) (x => x.Length > 0)).OrderBy<string, string>((Func<string, string>) (x => x)).ToList<string>();
      List<string> list2 = this._elements.Select<ElementBase, IEnumerable<StatisticRule>>((Func<ElementBase, IEnumerable<StatisticRule>>) (x => x.GetStatisticRules())).SelectMany<IEnumerable<StatisticRule>, StatisticRule>((Func<IEnumerable<StatisticRule>, IEnumerable<StatisticRule>>) (x => x)).Select<StatisticRule, string>((Func<StatisticRule, string>) (x => x.Attributes.Name)).GroupBy<string, string>((Func<string, string>) (x => x)).Select<IGrouping<string, string>, string>((Func<IGrouping<string, string>, string>) (x => x.First<string>())).OrderBy<string, string>((Func<string, string>) (x => x)).ToList<string>();
      this.ExistingSupports = new List<string>((IEnumerable<string>) list1);
      this.ExistingStatNames = new List<string>((IEnumerable<string>) list2);
      this._element.Name = "Weapon Proficiency (Tail)";
      this._element.Type = this.ElementTypes.First<string>((Func<string, bool>) (x => x.StartsWith("Proficiency")));
      this._element.Source = this.ElementSources.First<string>((Func<string, bool>) (x => x.StartsWith("Player")));
    }
  }

  public List<string> ElementTypes { get; set; }

  public List<string> ElementSources { get; set; }

  public List<string> ExistingStatNames { get; set; }

  public GenerationElement Element
  {
    get => this._element;
    set => this.SetProperty<GenerationElement>(ref this._element, value, nameof (Element));
  }

  public string GeneratedXmlNode
  {
    get => this._generatedXmlNode;
    set => this.SetProperty<string>(ref this._generatedXmlNode, value, nameof (GeneratedXmlNode));
  }

  public bool IsAutoGenerateIdEnabled
  {
    get => this._isAutoGenerateIdEnabled;
    set
    {
      this.SetProperty<bool>(ref this._isAutoGenerateIdEnabled, value, nameof (IsAutoGenerateIdEnabled));
      if (!this._isAutoGenerateIdEnabled)
        return;
      this._element.Id = this.GenerateUniqueId(this._element.Name, this._element.Type, this.IsCustomElement);
    }
  }

  public bool IsCustomElement
  {
    get => this._isCustomElement;
    set
    {
      this.SetProperty<bool>(ref this._isCustomElement, value, nameof (IsCustomElement));
      this._element.Id = this.GenerateUniqueId(this._element.Name, this._element.Type, this.IsCustomElement);
    }
  }

  public string CustomAbbreviation
  {
    get => this._customAbbreviation;
    set
    {
      this.SetProperty<string>(ref this._customAbbreviation, value, nameof (CustomAbbreviation));
      if (string.IsNullOrWhiteSpace(this._customAbbreviation))
      {
        this.IsCustomElement = false;
      }
      else
      {
        this.IsCustomElement = true;
        if (!this._isAutoGenerateIdEnabled)
          return;
        this._element.Id = this.GenerateUniqueId(this._element.Name, this._element.Type, this.IsCustomElement);
      }
    }
  }

  public ElementBaseCollection Elements { get; } = new ElementBaseCollection();

  public ElementBase SelectedElement
  {
    get => this._selectedElement;
    set
    {
      this.SetProperty<ElementBase>(ref this._selectedElement, value, nameof (SelectedElement));
    }
  }

  public ICommand LoadSelectedElementCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.LoadSelectedElement));
  }

  private void LoadSelectedElement()
  {
    if (this.SelectedElement == null)
      return;
    this._element.Name = this.SelectedElement.Name;
    this._element.Type = this.SelectedElement.Type;
    this._element.Source = this.SelectedElement.Source;
    this._element.Id = this.SelectedElement.Id;
  }

  public List<string> ExistingSupports { get; set; }

  public string SelectedSupport
  {
    get => this._selectedSupport;
    set => this.SetProperty<string>(ref this._selectedSupport, value, nameof (SelectedSupport));
  }

  public ICommand AddSupportCommand => (ICommand) new RelayCommand(new Action(this.AddSupport));

  private void AddSupport() => this._element.Supports.Add(this.SelectedSupport);

  private void GenerateNode()
  {
    ElementBase elementBase = new ElementBase(this._element.Name, this._element.Type, this._element.Source, this._element.Id);
    foreach (string support in (Collection<string>) this._element.Supports)
      elementBase.Supports.Add(support);
    this.GeneratedXmlNode = elementBase.GenerateElementNode().GenerateCleanOutput();
  }

  private void ElementPropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    if ((e.PropertyName == "Name" || e.PropertyName == "Type") && this._isAutoGenerateIdEnabled)
      this._element.Id = this.GenerateUniqueId(this._element.Name, this._element.Type, this.IsCustomElement);
    this.GenerateNode();
  }

  private string GenerateUniqueId(string elementName, string type, bool isCustom)
  {
    string[] strArray = new string[3]{ "/", "'", "’" };
    foreach (string oldValue in strArray)
      elementName = elementName.Replace(oldValue, "");
    string upper = type.Replace(" ", "").ToUpper();
    string str = elementName.Replace(" ", "_").ToUpper().Trim();
    return $"ID{(isCustom ? "_" + this._customAbbreviation : "")}_{upper}_{str}".ToUpper();
  }

  private static string Beautify(XmlDocument doc)
  {
    StringBuilder output = new StringBuilder();
    XmlWriterSettings settings = new XmlWriterSettings()
    {
      Indent = true,
      IndentChars = "  ",
      NewLineChars = Environment.NewLine,
      NewLineHandling = NewLineHandling.Replace
    };
    using (XmlWriter w = XmlWriter.Create(output, settings))
      doc.Save(w);
    return output.ToString();
  }
}
