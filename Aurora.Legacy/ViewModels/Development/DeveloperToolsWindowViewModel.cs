// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Development.DeveloperToolsWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Data;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Xml;

#nullable disable
namespace Builder.Presentation.ViewModels.Development;

public sealed class DeveloperToolsWindowViewModel : 
  ViewModelBase,
  ISubscriber<ElementDescriptionDisplayRequestEvent>
{
  private string _styleSheet;
  private string _input;
  private string _output;
  private bool _isSelectionEnabled;
  private bool _isContextMenuEnabled;
  private ElementBase _selectedElement;
  private bool _autoGenerateId;
  private ElementBase _createElement;
  private string _createElementOutput;
  private string _createElementName;
  private string _createElementType;
  private string _createElementSource;
  private string _createElementID;

  public DeveloperToolsWindowViewModel()
  {
    this._isSelectionEnabled = true;
    this._isContextMenuEnabled = true;
    this._autoGenerateId = true;
    this._styleSheet = DataManager.Current.GetResourceWebDocument("stylesheet.css");
    if (this.IsInDesignMode)
    {
      this._output = $"<style>{this._styleSheet}</style><body><h3>DWARF</h3>{DataManager.Current.GetResourceWebDocument("design-data.html")}</body>";
      this.InitializeDesignData();
    }
    else
    {
      this.Elements = DataManager.Current.ElementsCollection;
      this._selectedElement = this.Elements.First<ElementBase>();
      IEnumerable<string> collection1 = DataManager.Current.ElementsCollection.GroupBy<ElementBase, string>((Func<ElementBase, string>) (e => e.Type)).Select<IGrouping<string, ElementBase>, string>((Func<IGrouping<string, ElementBase>, string>) (g => g.First<ElementBase>().Type));
      IEnumerable<string> collection2 = DataManager.Current.ElementsCollection.GroupBy<ElementBase, string>((Func<ElementBase, string>) (e => e.Source)).Select<IGrouping<string, ElementBase>, string>((Func<IGrouping<string, ElementBase>, string>) (g => g.First<ElementBase>().Source));
      this.ElementTypes = new ObservableCollection<string>(collection1);
      this.ElementSources = new ObservableCollection<string>(collection2);
      this._createElementName = "element";
      this._createElementType = this.ElementTypes.First<string>();
      this._createElementSource = this.ElementSources.First<string>();
      this.GenerateDescriptionNodeCommand = (ICommand) new RelayCommand(new Action(this.GenerateDescriptionNode));
      this.EventAggregator.Subscribe((object) this);
      this._createElement = new ElementBase()
      {
        ElementHeader = new ElementHeader("", "", "", "")
      };
    }
  }

  public string StyleSheet
  {
    get => this._styleSheet;
    set
    {
      this.SetProperty<string>(ref this._styleSheet, value, nameof (StyleSheet));
      this.Generate();
    }
  }

  public string Input
  {
    get => this._input;
    set
    {
      this.SetProperty<string>(ref this._input, value, nameof (Input));
      this.Generate();
    }
  }

  public string Output
  {
    get => this._output;
    set => this.SetProperty<string>(ref this._output, value, nameof (Output));
  }

  public bool IsSelectionEnabled
  {
    get => this._isSelectionEnabled;
    set => this.SetProperty<bool>(ref this._isSelectionEnabled, value, nameof (IsSelectionEnabled));
  }

  public bool IsContextMenuEnabled
  {
    get => this._isContextMenuEnabled;
    set
    {
      this.SetProperty<bool>(ref this._isContextMenuEnabled, value, nameof (IsContextMenuEnabled));
    }
  }

  public ElementBaseCollection Elements { get; }

  public ElementBase SelectedElement
  {
    get => this._selectedElement;
    set
    {
      this.SetProperty<ElementBase>(ref this._selectedElement, value, nameof (SelectedElement));
      this.Input = this._selectedElement == null ? "" : this._selectedElement.Description;
    }
  }

  public ICommand GenerateDescriptionNodeCommand { get; }

  private void GenerateDescriptionNode()
  {
    XmlDocument xmlDocument = new XmlDocument();
    xmlDocument.AppendChild((XmlNode) xmlDocument.CreateElement("description")).AppendChild((XmlNode) xmlDocument.CreateCDataSection(this.Input));
    Clipboard.SetText(xmlDocument.OuterXml);
  }

  private void Generate()
  {
    if (this.SelectedElement == null)
    {
      this.Output = "";
    }
    else
    {
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.AppendLine("<html>");
      stringBuilder.AppendLine("<body>");
      stringBuilder.AppendLine($"<h2>{this.SelectedElement.Name.ToUpper()}</h2>");
      stringBuilder.AppendLine(this.Input);
      stringBuilder.AppendLine("<h6>SOURCE</h6>");
      stringBuilder.AppendLine($"<p class=\"flavor\">{this.SelectedElement.Source}</p>");
      stringBuilder.AppendLine("</body>");
      stringBuilder.AppendLine("</html>");
      this.Output = stringBuilder.ToString();
    }
  }

  public void OnHandleEvent(ElementDescriptionDisplayRequestEvent args)
  {
    this.SelectedElement = args.Element;
  }

  protected override void InitializeDesignData()
  {
    this.CreateElementName = "grewgwrewg";
    this.CreateElementType = "rewgre";
    this.CreateElementSource = "5435432";
    this.CreateElementID = "regreg";
    this.CreateElementOutput = "CreateElementOutput";
  }

  public ObservableCollection<string> ElementTypes { get; private set; }

  public ObservableCollection<string> ElementSources { get; private set; }

  public bool AutoGenerateId
  {
    get => this._autoGenerateId;
    set => this.SetProperty<bool>(ref this._autoGenerateId, value, nameof (AutoGenerateId));
  }

  public ElementBase CreateElement
  {
    get => this._createElement;
    set => this.SetProperty<ElementBase>(ref this._createElement, value, nameof (CreateElement));
  }

  public string CreateElementOutput
  {
    get => this._createElementOutput;
    set
    {
      this.SetProperty<string>(ref this._createElementOutput, value, nameof (CreateElementOutput));
    }
  }

  private string GenerateUniqueId(string elementName, string type)
  {
    string[] strArray = new string[3]{ " ", "/", "'" };
    foreach (string oldValue in strArray)
      elementName = elementName.Replace(oldValue, "");
    return $"ID_{type.Replace(" ", "").ToUpper()}_{elementName.Trim().ToUpper()}";
  }

  private void GenerateElementOutput()
  {
    StringBuilder stringBuilder = new StringBuilder();
    if (this.CreateElementName.Contains(";"))
    {
      string createElementName = this.CreateElementName;
      char[] chArray = new char[1]{ ';' };
      foreach (string elementName in createElementName.Split(chArray))
      {
        stringBuilder.AppendLine($"<element name=\"{elementName}\" type=\"{this.CreateElementType}\" source=\"{this.CreateElementSource}\" id=\"{this.GenerateUniqueId(elementName, this.CreateElementType)}\">");
        stringBuilder.AppendLine("\t<description>");
        stringBuilder.AppendLine("\t\t" + this.Input);
        stringBuilder.AppendLine("\t</description>");
        if (this.CreateElementType != "Item")
        {
          stringBuilder.AppendLine("\t<rules />");
        }
        else
        {
          stringBuilder.AppendLine("\t<setters>");
          stringBuilder.AppendLine("\t\t<set name=\"category\">Adventuring Gear</set>");
          stringBuilder.AppendLine("\t\t<set name=\"cost\" currency=\"gp\">1</set>");
          stringBuilder.AppendLine("\t\t<set name=\"weight\" lb=\"1\">1 lbs.</set>");
          stringBuilder.AppendLine("\t\t<set name=\"container\"></set>");
          stringBuilder.AppendLine("\t</setters>");
        }
        stringBuilder.AppendLine("</element>");
      }
    }
    else
    {
      stringBuilder.AppendLine($"<element name=\"{this.CreateElementName}\" type=\"{this.CreateElementType}\" source=\"{this.CreateElementSource}\" id=\"{this.CreateElementID}\">");
      stringBuilder.AppendLine("\t<description>");
      stringBuilder.AppendLine("\t\t" + this.Input);
      stringBuilder.AppendLine("\t</description>");
      if (this.CreateElementType != "Item")
      {
        stringBuilder.AppendLine("\t<rules />");
      }
      else
      {
        stringBuilder.AppendLine("\t<setters>");
        stringBuilder.AppendLine("\t\t<set name=\"category\">Adventuring Gear</set>");
        stringBuilder.AppendLine("\t\t<set name=\"cost\" currency=\"gp\">1</set>");
        stringBuilder.AppendLine("\t\t<set name=\"weight\" lb=\"1\">1 lbs.</set>");
        stringBuilder.AppendLine("\t\t<set name=\"container\"></set>");
        stringBuilder.AppendLine("\t</setters>");
      }
      stringBuilder.AppendLine("</element>");
    }
    this.CreateElementOutput = stringBuilder.ToString();
  }

  public string CreateElementName
  {
    get => this._createElementName;
    set
    {
      this.SetProperty<string>(ref this._createElementName, value, nameof (CreateElementName));
      if (this.AutoGenerateId)
        this.CreateElementID = this.GenerateUniqueId(this.CreateElementName, this.CreateElementType);
      this.GenerateElementOutput();
    }
  }

  public string CreateElementType
  {
    get => this._createElementType;
    set
    {
      this.SetProperty<string>(ref this._createElementType, value, nameof (CreateElementType));
      if (this.AutoGenerateId)
        this.CreateElementID = this.GenerateUniqueId(this.CreateElementName, this.CreateElementType);
      this.GenerateElementOutput();
    }
  }

  public string CreateElementSource
  {
    get => this._createElementSource;
    set
    {
      this.SetProperty<string>(ref this._createElementSource, value, nameof (CreateElementSource));
      this.GenerateElementOutput();
    }
  }

  public string CreateElementID
  {
    get => this._createElementID;
    set
    {
      this.SetProperty<string>(ref this._createElementID, value, nameof (CreateElementID));
      this.GenerateElementOutput();
    }
  }
}
