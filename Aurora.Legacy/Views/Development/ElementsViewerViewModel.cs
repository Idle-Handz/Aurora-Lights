// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.ElementsViewerViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Xml;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class ElementsViewerViewModel : ViewModelBase, ISubscriber<ElementsCollectionPopulatedEvent>
{
  private string _filterName;
  private string _selectedType;
  private string _selectedSource;
  private bool _supressFilter;
  private ElementBase _selectedElement;
  private string _styleSheet;
  private string _input;
  private string _output;

  public ElementsViewerViewModel()
  {
    this._styleSheet = DataManager.Current.GetResourceWebDocument("stylesheet.css");
    this._output = $"<style>{this._styleSheet}</style><body><h3>DWARF</h3>{DataManager.Current.GetResourceWebDocument("design-data.html")}</body>";
    if (this.IsInDesignMode)
      return;
    this.Elements = new ObservableCollection<ElementBase>();
    this.FilteredElements = new ObservableCollection<ElementBase>();
    this.Types = new ObservableCollection<string>();
    this.Sources = new ObservableCollection<string>();
    this.SubscribeWithEventAggregator();
    this.OnHandleEvent((ElementsCollectionPopulatedEvent) null);
  }

  public ObservableCollection<string> Types { get; set; }

  public ObservableCollection<string> Sources { get; set; }

  public ObservableCollection<ElementBase> Elements { get; }

  public ObservableCollection<ElementBase> FilteredElements { get; set; }

  public string FilterName
  {
    get => this._filterName;
    set
    {
      this.SetProperty<string>(ref this._filterName, value, nameof (FilterName));
      this.Filter();
    }
  }

  public string SelectedType
  {
    get => this._selectedType;
    set
    {
      this.SetProperty<string>(ref this._selectedType, value, nameof (SelectedType));
      this.Filter();
    }
  }

  public string SelectedSource
  {
    get => this._selectedSource;
    set
    {
      this.SetProperty<string>(ref this._selectedSource, value, nameof (SelectedSource));
      this.Filter();
    }
  }

  public bool SupressFilter
  {
    get => this._supressFilter;
    set => this.SetProperty<bool>(ref this._supressFilter, value, nameof (SupressFilter));
  }

  public ElementBase SelectedElement
  {
    get => this._selectedElement;
    set
    {
      this.SetProperty<ElementBase>(ref this._selectedElement, value, nameof (SelectedElement));
      if (this._selectedElement != null)
        this.EventAggregator.Send<ElementDescriptionDisplayRequestEvent>(new ElementDescriptionDisplayRequestEvent(this._selectedElement));
      if (this._selectedElement != null)
      {
        try
        {
          StringBuilder stringBuilder = new StringBuilder();
          foreach (string str in this._selectedElement.ElementNode["description"].ChildNodes.Cast<XmlNode>().Select<XmlNode, string>((Func<XmlNode, string>) (x => x.OuterXml)))
            stringBuilder.AppendLine(str);
          this.Input = stringBuilder.ToString();
        }
        catch (Exception ex)
        {
          this.Input = ex.Message;
        }
      }
      else
        this.Input = "";
    }
  }

  public ICommand FilterCommand => (ICommand) new RelayCommand(new Action(this.Filter));

  public ICommand ResetCommand => (ICommand) new RelayCommand(new Action(this.Reset));

  private void Reset()
  {
    this.SupressFilter = true;
    this.SelectedType = "--";
    this.SelectedSource = "--";
    this.SupressFilter = false;
    this.FilterName = "";
  }

  private void Filter()
  {
    if (this.SupressFilter || this.IsInDesignMode)
      return;
    this.FilteredElements.Clear();
    foreach (ElementBase element in (Collection<ElementBase>) this.Elements)
      this.FilteredElements.Add(element);
    if (!string.IsNullOrWhiteSpace(this.SelectedType) && this.SelectedType != "--")
    {
      List<ElementBase> list = this.FilteredElements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == this.SelectedType)).ToList<ElementBase>();
      this.FilteredElements.Clear();
      foreach (ElementBase elementBase in list)
        this.FilteredElements.Add(elementBase);
    }
    if (!string.IsNullOrWhiteSpace(this.SelectedSource) && this.SelectedSource != "--")
    {
      List<ElementBase> list = this.FilteredElements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Source.Equals(this.SelectedSource))).ToList<ElementBase>();
      this.FilteredElements.Clear();
      foreach (ElementBase elementBase in list)
        this.FilteredElements.Add(elementBase);
    }
    if (string.IsNullOrWhiteSpace(this.FilterName))
      return;
    List<ElementBase> list1 = this.FilteredElements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Name.ToLower().Contains(this.FilterName.ToLower().Trim()) || x.Id.ToLower().Contains(this.FilterName.ToLower().Trim()))).ToList<ElementBase>();
    this.FilteredElements.Clear();
    foreach (ElementBase elementBase in list1)
      this.FilteredElements.Add(elementBase);
  }

  public void OnHandleEvent(ElementsCollectionPopulatedEvent args)
  {
    foreach (ElementBase elements in (Collection<ElementBase>) DataManager.Current.ElementsCollection)
      this.Elements.Add(elements);
    this.Types.Add("--");
    foreach (string str in ElementTypeStrings.All)
      this.Types.Add(str);
    this.Sources.Add("--");
    foreach (string str in (IEnumerable<string>) this.Elements.GroupBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Source)).Select<IGrouping<string, ElementBase>, string>((Func<IGrouping<string, ElementBase>, string>) (x => x.Key)).OrderBy<string, string>((Func<string, string>) (x => x)))
      this.Sources.Add(str);
    this.Reset();
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

  private void Generate()
  {
    try
    {
      if (this.SelectedElement == null || this.IsInDesignMode)
      {
        this.Output = "null";
      }
      else
      {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("<html>");
        stringBuilder.AppendLine("<body>");
        stringBuilder.AppendLine($"<h2>{this.SelectedElement.Name.ToUpper()}</h2>");
        if (this.SelectedElement.Type == "Spell")
        {
          Spell selectedElement = (Spell) this.SelectedElement;
          stringBuilder.AppendLine($"<p class=\"flavor\">{selectedElement.GetShortDescription()}</p>");
          stringBuilder.AppendLine("<p>");
          stringBuilder.AppendLine($"<span class=\"emphasis\">Casting Time:</span>{selectedElement.CastingTime}<br/>");
          stringBuilder.AppendLine($"<span class=\"emphasis\">Range:</span>{selectedElement.Range}<br/>");
          stringBuilder.AppendLine($"<span class=\"emphasis\">Components:</span>{selectedElement.GetComponentsString()}<br/>");
          stringBuilder.AppendLine($"<span class=\"emphasis\">Duration:</span>{selectedElement.Duration}<br/>");
          stringBuilder.AppendLine("</p>");
        }
        else if (this.SelectedElement.Type == "Item")
        {
          Item selectedElement = (Item) this.SelectedElement;
          stringBuilder.AppendLine("<p>");
          stringBuilder.AppendLine($"<span class=\"emphasis\">Category:</span>{selectedElement.Category}<br/>");
          stringBuilder.AppendLine($"<span class=\"emphasis\">Cost:</span>{selectedElement.Cost} {selectedElement.CurrencyAbbreviation}<br/>");
          stringBuilder.AppendLine($"<span class=\"emphasis\">Weight:</span>{selectedElement.Weight}<br/>");
          stringBuilder.AppendLine("</p>");
        }
        if (this.Input.Contains("<div element="))
        {
          string str = this.Input.Replace("<br>", "<br/>");
          XmlDocument xmlDocument = new XmlDocument();
          xmlDocument.LoadXml($"<div>{str}</div>");
          if (xmlDocument.DocumentElement != null)
          {
            foreach (XmlNode childNode in xmlDocument.DocumentElement.ChildNodes)
            {
              if (childNode.Name == "div" && childNode.ContainsAttribute("element"))
              {
                string injectedElementId = childNode.GetAttributeValue("element");
                ElementBase elementBase = DataManager.Current.ElementsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id == injectedElementId));
                if (elementBase == null)
                  Logger.Warning($"the injected element '{this.SelectedElement.Name}' with the id '{injectedElementId}' is not found.");
                else
                  childNode.InnerXml = $"<h5 class=\"h5-enhance\">{elementBase.Name.ToUpper()}</h5>{elementBase.Description}";
              }
            }
          }
          stringBuilder.AppendLine(xmlDocument.InnerXml);
        }
        else
          stringBuilder.AppendLine(this.Input);
        stringBuilder.AppendLine("<h6>SOURCE</h6>");
        stringBuilder.AppendLine($"<p class=\"flavor\">{this.SelectedElement.Source}</p>");
        stringBuilder.AppendLine("</body>");
        stringBuilder.AppendLine("</html>");
        this.Output = stringBuilder.ToString();
      }
    }
    catch (Exception ex)
    {
    }
  }
}
