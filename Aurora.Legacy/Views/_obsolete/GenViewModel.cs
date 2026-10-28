// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views._obsolete.GenViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Xml;

#nullable disable
namespace Builder.Presentation.Views._obsolete;

public class GenViewModel : ViewModelBase
{
  private string _genName;
  private string _selectedType;
  private Source _selectedSource;
  private string _genId;
  private string _output;
  private GenElement _selectedGen;

  public GenViewModel()
  {
    IEnumerable<Source> sources = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Source")).Cast<Source>();
    this.Sources = new ObservableCollection<Source>();
    foreach (Source source in sources)
      this.Sources.Add(source);
    ObservableCollection<string> observableCollection = new ObservableCollection<string>();
    observableCollection.Add("Archetype");
    observableCollection.Add("Archetype Feature");
    this.Types = observableCollection;
    this.GenElements = new ObservableCollection<GenElement>();
  }

  public ObservableCollection<Source> Sources { get; set; }

  public ObservableCollection<string> Types { get; set; }

  public ObservableCollection<GenElement> GenElements { get; set; }

  public string GenName
  {
    get => this._genName;
    set
    {
      this.SetProperty<string>(ref this._genName, value, nameof (GenName));
      this.GenerateId();
    }
  }

  public string SelectedType
  {
    get => this._selectedType;
    set
    {
      this.SetProperty<string>(ref this._selectedType, value, nameof (SelectedType));
      this.GenerateId();
    }
  }

  public Source SelectedSource
  {
    get => this._selectedSource;
    set
    {
      this.SetProperty<Source>(ref this._selectedSource, value, nameof (SelectedSource));
      this.GenerateId();
    }
  }

  public string GenId
  {
    get => this._genId;
    set => this.SetProperty<string>(ref this._genId, value, nameof (GenId));
  }

  public string Output
  {
    get => this._output;
    set => this.SetProperty<string>(ref this._output, value, nameof (Output));
  }

  public GenElement SelectedGen
  {
    get => this._selectedGen;
    set => this.SetProperty<GenElement>(ref this._selectedGen, value, nameof (SelectedGen));
  }

  private string GenerateId()
  {
    string id = "";
    try
    {
      id = ElementsHelper.SanitizeID($"ID_{this.SelectedSource.AuthorAbbreviation}_{this.SelectedSource.Abbreviation}_{this.SelectedType}_{this.GenName}");
      this.GenId = id;
    }
    catch (Exception ex)
    {
    }
    return id;
  }

  public ICommand AddGenCommand => (ICommand) new RelayCommand(new Action(this.AddGen));

  public ICommand RemoveGenCommand => (ICommand) new RelayCommand(new Action(this.RemoveGen));

  public ICommand OutputGensCommand => (ICommand) new RelayCommand(new Action(this.OutputGens));

  private void AddGen()
  {
    this.GenElements.Add(new GenElement(this.GenName, this.SelectedType, this.SelectedSource.Name, this.GenId));
  }

  private void RemoveGen()
  {
    if (this.SelectedGen == null)
      return;
    this.GenElements.Remove(this.SelectedGen);
  }

  public void OutputGens()
  {
    XmlDocument node = new XmlDocument();
    XmlNode xmlNode = node.AppendChild((XmlNode) node.CreateElement("elements"));
    foreach (GenElement genElement in (Collection<GenElement>) this.GenElements)
    {
      XmlElement element = node.CreateElement("element");
      element.AppendAttribute("name", genElement.Name);
      element.AppendAttribute("type", genElement.Type);
      element.AppendAttribute("source", genElement.Source);
      element.AppendAttribute("id", genElement.ID);
      element.AppendChild((XmlNode) node.CreateElement("description")).AppendChild((XmlNode) node.CreateElement("p"));
      xmlNode.AppendChild((XmlNode) element);
    }
    this.Output = node.ToElementStringWithSpaces();
  }
}
