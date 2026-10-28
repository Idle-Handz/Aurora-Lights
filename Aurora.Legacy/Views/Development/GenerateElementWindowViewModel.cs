// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.GenerateElementWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Data.Extensions;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class GenerateElementWindowViewModel : ViewModelBase
{
  private ElementBaseCollection _elements;
  private EditableElement _element;
  private string _authorAbbreviation;
  private string _sourceAbbreviation;
  private string _selectedInternalType;
  private string _selectedInternalSource;
  private string _output;

  public GenerateElementWindowViewModel()
  {
    this._authorAbbreviation = "wotc";
    this._sourceAbbreviation = "srd";
    this._element = new EditableElement();
    this._elements = new ElementBaseCollection((IEnumerable<ElementBase>) DataManager.Current.ElementsCollection);
    this.InternalTypes = new ObservableCollection<string>((IEnumerable<string>) this._elements.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Type)).Distinct<string>().OrderBy<string, string>((Func<string, string>) (x => x)));
    this.InternalSources = new ObservableCollection<string>((IEnumerable<string>) this._elements.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Source)).Distinct<string>().OrderBy<string, string>((Func<string, string>) (x => x)));
    this.Element.PropertyChanged += new PropertyChangedEventHandler(this.Element_PropertyChanged);
  }

  private void Element_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    this.GenerateElementNode();
  }

  public ObservableCollection<string> InternalTypes { get; }

  public ObservableCollection<string> InternalSources { get; }

  public ObservableCollection<string> InternalSupports { get; } = new ObservableCollection<string>();

  public ObservableCollection<string> InternalSetters { get; } = new ObservableCollection<string>();

  public string SelectedInternalType
  {
    get => this._selectedInternalType;
    set
    {
      this.SetProperty<string>(ref this._selectedInternalType, value, nameof (SelectedInternalType));
      this.Element.Type = this._selectedInternalType;
    }
  }

  public string SelectedInternalSource
  {
    get => this._selectedInternalSource;
    set
    {
      this.SetProperty<string>(ref this._selectedInternalSource, value, nameof (SelectedInternalSource));
      string str1 = "";
      if (!string.IsNullOrWhiteSpace(this._selectedInternalSource))
      {
        string selectedInternalSource = this._selectedInternalSource;
        char[] chArray = new char[1]{ ' ' };
        foreach (string str2 in selectedInternalSource.Split(chArray))
        {
          if (!string.IsNullOrWhiteSpace(str2))
            str1 += str2.Trim()[0].ToString();
        }
        this.SourceAbbreviation = str1;
      }
      this.Element.Source = this._selectedInternalSource;
    }
  }

  public EditableElement Element
  {
    get => this._element;
    set => this.SetProperty<EditableElement>(ref this._element, value, nameof (Element));
  }

  public string AuthorAbbreviation
  {
    get => this._authorAbbreviation;
    set
    {
      this.SetProperty<string>(ref this._authorAbbreviation, value, nameof (AuthorAbbreviation));
    }
  }

  public string SourceAbbreviation
  {
    get => this._sourceAbbreviation;
    set
    {
      this.SetProperty<string>(ref this._sourceAbbreviation, value, nameof (SourceAbbreviation));
    }
  }

  public ICommand NewCommand => (ICommand) new RelayCommand(new Action(this.New));

  public ICommand CopyOutputCommand => (ICommand) new RelayCommand(new Action(this.CopyOutput));

  private void CopyOutput() => Clipboard.SetText(this.Output);

  public void New()
  {
    this.Element = new EditableElement();
    this.Element.Name = nameof (New);
    this.Element.Type = this.SelectedInternalType;
    this.Element.Source = this.SelectedInternalSource;
    this.Element.Id = this.GenerateUniqueId();
    this.Element.SheetDisplay = true;
    this.Element.PropertyChanged += new PropertyChangedEventHandler(this.Element_PropertyChanged);
    this.OnPropertyChanged("Element");
  }

  public string GenerateUniqueId()
  {
    return $"ID_{this.AuthorAbbreviation}_{this.SourceAbbreviation}_{this.Element.Type}_{this.Element.Name}".Replace(" ", "_").Replace("__", "_").ToUpper();
  }

  public void GenerateElementNode()
  {
    try
    {
      this.Element.Id = this.GenerateUniqueId();
      XmlDocument xmlDocument = new XmlDocument();
      XmlNode node1 = xmlDocument.CreateNode(XmlNodeType.Element, "element", (string) null);
      node1.Attributes.Append(xmlDocument.CreateAttribute("name")).Value = this.Element.Name;
      node1.Attributes.Append(xmlDocument.CreateAttribute("type")).Value = this.Element.Type;
      node1.Attributes.Append(xmlDocument.CreateAttribute("source")).Value = this.Element.Source;
      node1.Attributes.Append(xmlDocument.CreateAttribute("id")).Value = this.Element.Id;
      XmlNode node2 = xmlDocument.CreateNode(XmlNodeType.Element, "supports", (string) null);
      node1.AppendChild(node2);
      if (!string.IsNullOrWhiteSpace(this.Element.Supports))
        node2.InnerText = this.Element.Supports;
      XmlNode node3 = xmlDocument.CreateNode(XmlNodeType.Element, "prerequisites", (string) null);
      node1.AppendChild(node3);
      if (!string.IsNullOrWhiteSpace(this.Element.Prerequisites))
        node3.InnerText = this.Element.Prerequisites;
      XmlNode node4 = xmlDocument.CreateNode(XmlNodeType.Element, "requirements", (string) null);
      node1.AppendChild(node4);
      if (!string.IsNullOrWhiteSpace(this.Element.Requirements))
        node4.InnerText = this.Element.Requirements;
      XmlNode node5 = xmlDocument.CreateNode(XmlNodeType.Element, "description", (string) null);
      node1.AppendChild(node5);
      if (!string.IsNullOrWhiteSpace(this.Element.Description))
        node5.InnerText = this.Element.Description;
      XmlNode node6 = xmlDocument.CreateNode(XmlNodeType.Element, "sheet", (string) null);
      node1.AppendChild(node6);
      if (!string.IsNullOrWhiteSpace(this.Element.SheetAlternativeName))
        node6.Attributes.Append(xmlDocument.CreateAttribute("alt")).Value = this.Element.SheetAlternativeName;
      node6.Attributes.Append(xmlDocument.CreateAttribute("display")).Value = this.Element.SheetDisplay.ToString().ToLower();
      if (!string.IsNullOrWhiteSpace(this.Element.SheetDescription))
      {
        XmlNode node7 = xmlDocument.CreateNode(XmlNodeType.Element, "description", (string) null);
        node6.AppendChild(node7);
        node7.InnerText = this.Element.SheetDescription;
      }
      if (!string.IsNullOrWhiteSpace(this.Element.Description))
        node5.InnerText = this.Element.Description;
      XmlNode node8 = xmlDocument.CreateNode(XmlNodeType.Element, "setters", (string) null);
      node1.AppendChild(node8);
      XmlNode node9 = xmlDocument.CreateNode(XmlNodeType.Element, "rules", (string) null);
      node1.AppendChild(node9);
      this.Output = node1.ToElementString();
    }
    catch (Exception ex)
    {
    }
  }

  public string Output
  {
    get => this._output;
    set => this.SetProperty<string>(ref this._output, value, nameof (Output));
  }
}
