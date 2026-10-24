// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.CompendiumControlViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.UserControls;

public class CompendiumControlViewModel : 
  ViewModelBase,
  ISubscriber<ElementsCollectionPopulatedEvent>,
  ISubscriber<QuickSearchBarEventArgs>,
  ISubscriber<CompendiumShowSourceEventArgs>
{
  private ElementBaseCollection _elements;
  private string _searchCriteria;
  private string _targetType;
  private string _targetSource;
  private ElementBase _selectedSearchResult;

  public CompendiumControlViewModel()
  {
    this.TargetTypes.Add(string.Empty);
    this.TargetSources.Add(string.Empty);
    this.TargetType = "";
    this.TargetSource = "";
    if (this.IsInDesignMode)
      return;
    this.SubscribeWithEventAggregator();
    this.Populate();
    this.SearchCriteria = "";
  }

  public ElementBaseCollection SearchResults { get; } = new ElementBaseCollection();

  public ObservableCollection<string> TargetTypes { get; } = new ObservableCollection<string>();

  public ObservableCollection<string> TargetSources { get; } = new ObservableCollection<string>();

  public string SearchCriteria
  {
    get => this._searchCriteria;
    set
    {
      this.SetProperty<string>(ref this._searchCriteria, value, nameof (SearchCriteria));
      this.Search();
    }
  }

  public string TargetSource
  {
    get => this._targetSource;
    set
    {
      this.SetProperty<string>(ref this._targetSource, value, nameof (TargetSource));
      this.Search();
    }
  }

  public string TargetType
  {
    get => this._targetType;
    set
    {
      this.SetProperty<string>(ref this._targetType, value, nameof (TargetType));
      this.Search();
    }
  }

  public ElementBase SelectedSearchResult
  {
    get => this._selectedSearchResult;
    set
    {
      this.SetProperty<ElementBase>(ref this._selectedSearchResult, value, nameof (SelectedSearchResult));
      if (this._selectedSearchResult == null)
        return;
      this.EventAggregator.Send<CompendiumElementDescriptionDisplayRequestEvent>(new CompendiumElementDescriptionDisplayRequestEvent(this._selectedSearchResult));
    }
  }

  private void Search()
  {
    if (this._elements == null)
      return;
    string criteria = this.SearchCriteria.ToLower().Trim();
    IEnumerable<ElementBase> source1 = (IEnumerable<ElementBase>) new List<ElementBase>((IEnumerable<ElementBase>) this._elements);
    if (!string.IsNullOrWhiteSpace(this.TargetSource))
      source1 = source1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Source.ToLower().Contains(this.TargetSource.ToLower())));
    if (!string.IsNullOrWhiteSpace(this.TargetType))
      source1 = source1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals(this.TargetType)));
    if (string.IsNullOrWhiteSpace(criteria))
    {
      this.SearchResults.Clear();
      this.SearchResults.AddRange((IEnumerable<ElementBase>) source1.OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)));
      Logger.Warning($"{this.SearchResults.Count} results");
    }
    else
    {
      ElementBaseCollection source2 = new ElementBaseCollection(source1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Name.ToLower().Contains(criteria))));
      foreach (ElementBase elementBase in source1)
      {
        if (elementBase.Supports.Any<string>((Func<string, bool>) (x => x.ToLower().Contains(criteria))) && !source2.Contains(elementBase))
          source2.Add(elementBase);
      }
      this.SearchResults.Clear();
      this.SearchResults.AddRange((IEnumerable<ElementBase>) source2.OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)));
      Logger.Warning($"{this.SearchResults.Count} results");
    }
  }

  private void Populate()
  {
    this._elements = new ElementBaseCollection((IEnumerable<ElementBase>) DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.IncludeInCompendium && !x.Type.Equals("Level") && !x.Type.Equals("Core") && !x.Type.Equals("Source") && !x.Type.Equals("Support") && !x.Type.Equals("Grants") && !x.Type.Equals("Ignore") && !x.Type.Equals("Ability Score Improvement") && !x.Name.StartsWith("Ability Score Increase (") && !x.Source.Equals("Internal"))).ToList<ElementBase>());
    foreach (string str in (IEnumerable<string>) this._elements.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Type)).Distinct<string>().OrderBy<string, string>((Func<string, string>) (x => x)))
      this.TargetTypes.Add(str);
    string[] strArray1 = new string[7]
    {
      "Core",
      "Level",
      "Support",
      "Grants",
      "Source",
      "Spellcasting Focus Group",
      "Ability Score Improvement"
    };
    foreach (string str in strArray1)
      this.TargetTypes.Remove(str);
    foreach (string str in (IEnumerable<string>) this._elements.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Source)).Distinct<string>().OrderBy<string, string>((Func<string, string>) (x => x)))
      this.TargetSources.Add(str);
    string[] strArray2 = new string[2]{ "Core", "Internal" };
    foreach (string str in strArray2)
      this.TargetSources.Remove(str);
  }

  public void OnHandleEvent(ElementsCollectionPopulatedEvent args) => this.Populate();

  public void OnHandleEvent(QuickSearchBarEventArgs args)
  {
    if (!args.IsSearch)
      return;
    this.SearchCriteria = args.SearchCriteria;
  }

  public ICommand DeveloperCopyDetailsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.CopyDeveloperDetails));
  }

  private void CopyDeveloperDetails()
  {
    if (this.SelectedSearchResult == null)
      return;
    Clipboard.SetText(this.SelectedSearchResult.ElementNodeString);
  }

  public void OnHandleEvent(CompendiumShowSourceEventArgs args)
  {
    this._searchCriteria = "";
    this._targetType = "";
    this.OnPropertyChanged("SearchCriteria");
    this.OnPropertyChanged("TargetType");
    this.TargetSource = args.SourceName;
  }
}
