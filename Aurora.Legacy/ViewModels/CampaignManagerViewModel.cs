// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.CampaignManagerViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Data;
using Builder.Presentation.Models.Campaign;
using Builder.Presentation.Models.Sources;
using Builder.Presentation.Services.Sources;
using Builder.Presentation.Telemetry;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.Views;
using Builder.Presentation.Views.Sliders;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.ViewModels;

public sealed class CampaignManagerViewModel : 
  ViewModelBase,
  ISubscriber<RestrictedSourcesLoadRequest>
{
  private string _name;
  private SourcesGroup _selectedSourcesGroup;
  private SourceItem _selectedSourceItem;
  private IEnumerable<SourceItem> _selectedSourceItems;
  private bool _isCompendiumSeachAvailable;

  public CampaignManagerViewModel()
  {
    this._name = "";
    if (this.IsInDesignMode)
    {
      this.Name = "Design Time Campaign";
      this.InitializeDesignData();
    }
    else
    {
      this.ViewInCompendiumCommand = new RelayCommand(new Action(this.ViewInCompendium), new Func<bool>(this.CanViewInCompendium));
      this.ViewInCompendiumCommand.RaiseCanExecuteChanged();
      this.SubscribeWithEventAggregator();
      this.SelectedSourcesGroup = this.SourcesManager.SourceGroups.FirstOrDefault<SourcesGroup>();
      SourcesGroup selectedSourcesGroup = this.SelectedSourcesGroup;
      this.SelectedSourceItem = selectedSourcesGroup != null ? selectedSourcesGroup.Sources.FirstOrDefault<SourceItem>() : (SourceItem) null;
      this.SourcesManager.LoadDefaults();
      this.SendDisplayRequest();
    }
  }

  public CharacterManager Manager => CharacterManager.Current;

  public SourcesManager SourcesManager => CharacterManager.Current.SourcesManager;

  public string Name
  {
    get => this._name;
    set => this.SetProperty<string>(ref this._name, value, nameof (Name));
  }

  public SourcesGroup SelectedSourcesGroup
  {
    get => this._selectedSourcesGroup;
    set
    {
      this.SetProperty<SourcesGroup>(ref this._selectedSourcesGroup, value, nameof (SelectedSourcesGroup));
    }
  }

  public SourceItem SelectedSourceItem
  {
    get => this._selectedSourceItem;
    set
    {
      this.SetProperty<SourceItem>(ref this._selectedSourceItem, value, nameof (SelectedSourceItem));
      this.SendDisplayRequest();
      this.ViewInCompendiumCommand?.RaiseCanExecuteChanged();
      this.IsCompendiumSeachAvailable = this.CanViewInCompendium();
    }
  }

  public IEnumerable<SourceItem> SelectedSourceItems
  {
    get => this._selectedSourceItems;
    set
    {
      this.SetProperty<IEnumerable<SourceItem>>(ref this._selectedSourceItems, value, nameof (SelectedSourceItems));
    }
  }

  private void SendDisplayRequest()
  {
    this.EventAggregator.Send<SourceElementDescriptionDisplayRequestEvent>(new SourceElementDescriptionDisplayRequestEvent((ElementBase) this._selectedSourceItem?.Source));
  }

  public ICommand ApplyRestrictionsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ApplyRestrictions));
  }

  public ICommand LoadRestrictionsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.LoadRestrictions));
  }

  public ICommand ClearRestrictionsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ClearRestrictions));
  }

  public ICommand StoreDefaultRestrictionsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.StoreDefaultRestrictions));
  }

  public ICommand RestrictAllPlaytestCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.RestrictAllPlaytest));
  }

  public RelayCommand ViewInCompendiumCommand { get; }

  public bool IsCompendiumSeachAvailable
  {
    get => this._isCompendiumSeachAvailable;
    set
    {
      this.SetProperty<bool>(ref this._isCompendiumSeachAvailable, value, nameof (IsCompendiumSeachAvailable));
    }
  }

  public ICommand ToggleSelectedSourceGroupCommand
  {
    get
    {
      return (ICommand) new RelayCommand<SourcesGroup>(new Action<SourcesGroup>(this.ToggleSelectedSourceGroup));
    }
  }

  public ICommand ToggleSelectedSourceItemCommand
  {
    get
    {
      return (ICommand) new RelayCommand<SourceItem>(new Action<SourceItem>(this.ToggleSelectedSourceItem));
    }
  }

  public ICommand ToggleSelectedSourceItemsCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ToggleSelectedSourceItems));
  }

  public ICommand ToggleSameAuthorSourcesCommand
  {
    get
    {
      return (ICommand) new RelayCommand<SourceItem>(new Action<SourceItem>(this.ToggleSameAuthorSources));
    }
  }

  private void ApplyRestrictions() => this.SourcesManager.ApplyRestrictions(true);

  private void LoadRestrictions() => this.SourcesManager.LoadDefaults();

  private void ClearRestrictions() => this.SourcesManager.ClearRestrictions();

  private void RestrictAllPlaytest()
  {
    foreach (SourcesGroup sourceGroup in (Collection<SourcesGroup>) this.SourcesManager.SourceGroups)
    {
      foreach (SourceItem source in (Collection<SourceItem>) sourceGroup.Sources)
      {
        if (source.Source.IsPlaytestContent)
          source.SetIsChecked(new bool?(false), false, true);
      }
    }
  }

  private void ViewInCompendium()
  {
    if (this.SelectedSourceItem == null)
      return;
    string name = this.SelectedSourceItem.Source.Name;
    AnalyticsEventHelper.SourcesCompendiumLookup(name);
    this.EventAggregator.Send<CompendiumShowSourceEventArgs>(new CompendiumShowSourceEventArgs(name));
    this.EventAggregator.Send<ShowSliderEvent>(new ShowSliderEvent(Slider.Compendium));
  }

  private bool CanViewInCompendium() => this.SelectedSourceItem != null;

  private void StoreDefaultRestrictions() => this.SourcesManager.StoreDefaults();

  private void ToggleSelectedSourceGroup(SourcesGroup group)
  {
    if (group == null || !group.AllowUnchecking || !group.IsChecked.HasValue)
      return;
    group.SetIsChecked(new bool?(!group.IsChecked.Value), true);
  }

  private void ToggleSelectedSourceItem(SourceItem source)
  {
    if (source == null || !source.AllowUnchecking || !source.IsChecked.HasValue)
      return;
    source.SetIsChecked(new bool?(!source.IsChecked.Value), false, true);
  }

  private void ToggleSelectedSourceItems()
  {
    if (this.SelectedSourceItems == null)
      return;
    foreach (SourceItem selectedSourceItem in this.SelectedSourceItems)
      this.ToggleSelectedSourceItem(selectedSourceItem);
  }

  private void ToggleSameAuthorSources(object parameter)
  {
    if (parameter == null)
      return;
    SourceItem source = parameter as SourceItem;
    foreach (SourceItem source1 in this.SelectedSourcesGroup.Sources.Where<SourceItem>((Func<SourceItem, bool>) (x => x.Source.Author == source.Source.Author)))
      this.ToggleSelectedSourceItem(source1);
  }

  [Obsolete]
  public void OnHandleEvent(RestrictedSourcesLoadRequest args)
  {
    this.SourcesManager.ClearRestrictions(false);
    foreach (SourcesGroup sourceGroup in (Collection<SourcesGroup>) this.SourcesManager.SourceGroups)
    {
      foreach (SourceItem source in (Collection<SourceItem>) sourceGroup.Sources)
      {
        if (args.SourceIds.Contains<string>(source.Source.Id))
          source.SetIsChecked(new bool?(false), false, true);
      }
    }
    this.SourcesManager.ApplyRestrictions();
  }
}
