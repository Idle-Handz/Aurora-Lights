// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.Filters.InlineSpellFilterViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data.Elements;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.UserControls.Filters;

public class InlineSpellFilterViewModel : ViewModelBase
{
  private bool _isSchoolFilterAvailable;
  private bool _isSourceFilterAvailable;

  public InlineSpellFilterViewModel()
  {
    this._isSchoolFilterAvailable = true;
    this._isSourceFilterAvailable = true;
    this.Filter = new SpellcastingFilter((IEnumerable<string>) new string[2]
    {
      "",
      ""
    }, (IEnumerable<string>) new string[2]{ "", "" });
    this.Filter.Clear();
    if (!this.IsInDesignMode)
      return;
    this.Filter.IncludeCantrips = false;
    this.Filter.Include1 = false;
    this.Filter.Include2 = true;
    this.Filter.Include3 = true;
    this.Filter.Include4 = true;
    this.Filter.Include5 = false;
    this.Filter.Include6 = false;
    this.Filter.Include7 = false;
    this.Filter.Include8 = false;
    this.Filter.Include9 = false;
  }

  public void ClearFilters() => this.Filter.Clear();

  public SpellcastingFilter Filter { get; }

  public ICommand ClearFiltersCommand => (ICommand) new RelayCommand(new Action(this.ClearFilters));

  public bool IsSchoolFilterAvailable
  {
    get => this._isSchoolFilterAvailable;
    set
    {
      this.SetProperty<bool>(ref this._isSchoolFilterAvailable, value, nameof (IsSchoolFilterAvailable));
    }
  }

  public bool IsSourceFilterAvailable
  {
    get => this._isSourceFilterAvailable;
    set
    {
      this.SetProperty<bool>(ref this._isSourceFilterAvailable, value, nameof (IsSourceFilterAvailable));
    }
  }

  public IEnumerable<Spell> SourceSpells { get; set; }

  public IEnumerable<Spell> FilteredSpells { get; set; }

  public void ApplyFilter()
  {
    SpellcastingFilter filter = this.Filter;
    List<Spell> spellList = new List<Spell>();
    if (!string.IsNullOrWhiteSpace(filter.Name))
    {
      string name = filter.Name.ToLower().Trim();
      spellList.AddRange(this.SourceSpells.Where<Spell>((Func<Spell, bool>) (x => x.Name.ToLower().Contains(name))));
    }
    if (!filter.IncludeCantrips)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 0)).ToList<Spell>();
    if (!filter.Include1)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 1)).ToList<Spell>();
    if (!filter.Include2)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 2)).ToList<Spell>();
    if (!filter.Include3)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 3)).ToList<Spell>();
    if (!filter.Include4)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 4)).ToList<Spell>();
    if (!filter.Include5)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 5)).ToList<Spell>();
    if (!filter.Include6)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 6)).ToList<Spell>();
    if (!filter.Include7)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 7)).ToList<Spell>();
    if (!filter.Include8)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 8)).ToList<Spell>();
    if (!filter.Include9)
      spellList = spellList.Where<Spell>((Func<Spell, bool>) (x => x.Level != 9)).ToList<Spell>();
    this.FilteredSpells = (IEnumerable<Spell>) new List<Spell>((IEnumerable<Spell>) spellList);
  }
}
