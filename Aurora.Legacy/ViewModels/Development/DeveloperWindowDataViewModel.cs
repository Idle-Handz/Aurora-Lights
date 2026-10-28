// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Development.DeveloperWindowDataViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

#nullable disable
namespace Builder.Presentation.ViewModels.Development;

public sealed class DeveloperWindowDataViewModel : ViewModelBase
{
  private string _selectedType;
  private string _name;
  private ElementBase _selectedFilterElement;
  private bool _copyGrant;
  private bool _copySelect;
  private string _grant;
  private string _select;

  public DeveloperWindowDataViewModel()
  {
    if (this.IsInDesignMode)
      return;
    this.ElementTypes = new ObservableCollection<string>(DataManager.Current.ElementsCollection.GroupBy<ElementBase, string>((Func<ElementBase, string>) (e => e.Type)).Select<IGrouping<string, ElementBase>, string>((Func<IGrouping<string, ElementBase>, string>) (g => g.First<ElementBase>().Type)));
    this.FilteredElements = new ElementBaseCollection();
  }

  public ObservableCollection<string> ElementTypes { get; private set; }

  public ElementBaseCollection FilteredElements { get; set; }

  public ElementBase SelectedFilterElement
  {
    get => this._selectedFilterElement;
    set
    {
      this.SetProperty<ElementBase>(ref this._selectedFilterElement, value, nameof (SelectedFilterElement));
      if (this._selectedFilterElement == null)
      {
        this.Select = "";
        this.Grant = "";
      }
      else
      {
        this.Grant = $"<grant type=\"{this._selectedFilterElement.Type}\" name=\"{this._selectedFilterElement.Id}\" />";
        this.Select = $"<select type=\"{this._selectedFilterElement.Type}\" />";
        if (this._copyGrant)
          Clipboard.SetText(this.Grant);
        if (!this._copySelect)
          return;
        Clipboard.SetText(this.Select);
      }
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

  public string NameFilter
  {
    get => this._name;
    set
    {
      this.SetProperty<string>(ref this._name, value, nameof (NameFilter));
      this.Filter();
    }
  }

  public void Filter()
  {
    List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == this._selectedType)).ToList<ElementBase>();
    if (!string.IsNullOrWhiteSpace(this.NameFilter))
      list = list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Name.ToLower().Contains(this.NameFilter.ToLower()))).ToList<ElementBase>();
    this.FilteredElements.Clear();
    this.FilteredElements.AddRange((IEnumerable<ElementBase>) list);
  }

  public bool CopyGrant
  {
    get => this._copyGrant;
    set
    {
      this.SetProperty<bool>(ref this._copyGrant, value, nameof (CopyGrant));
      this._copySelect = !this._copyGrant;
      this.OnPropertyChanged("CopySelect");
    }
  }

  public bool CopySelect
  {
    get => this._copySelect;
    set
    {
      this.SetProperty<bool>(ref this._copySelect, value, nameof (CopySelect));
      this._copyGrant = !this._copySelect;
      this.OnPropertyChanged("CopyGrant");
    }
  }

  public string Grant
  {
    get => this._grant;
    set => this.SetProperty<string>(ref this._grant, value, nameof (Grant));
  }

  public string Select
  {
    get => this._select;
    set => this.SetProperty<string>(ref this._select, value, nameof (Select));
  }
}
