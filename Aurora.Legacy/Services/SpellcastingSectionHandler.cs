// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.SpellcastingSectionHandler
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Presentation.Extensions;
using Builder.Presentation.Interfaces;
using Builder.Presentation.UserControls.Spellcasting;
using Builder.Presentation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Services;

public class SpellcastingSectionHandler : ISpellcastingSectionHandler
{
  private static SpellcastingSectionHandler _instance;
  private readonly ObservableCollection<SpellcasterSelectionControl> _spellcastingSections;

  private SpellcastingSectionHandler()
  {
    Logger.Initializing((object) nameof (SpellcastingSectionHandler));
    this._spellcastingSections = new ObservableCollection<SpellcasterSelectionControl>();
    // Register with the Aurora.Logic seam so cross-platform code can reach us.
    SpellcastingSectionContext.Current = this;
  }

  public static SpellcastingSectionHandler Current
  {
    get
    {
      return SpellcastingSectionHandler._instance ?? (SpellcastingSectionHandler._instance = new SpellcastingSectionHandler());
    }
  }

  public void Add(SpellcasterSelectionControl section) => this._spellcastingSections.Add(section);

  public bool Remove(string identifier)
  {
    return this._spellcastingSections.Remove(this.GetSpellcasterSection(identifier));
  }

  public SpellcasterSelectionControl GetSpellcasterSection(string identifier)
  {
    return this._spellcastingSections.FirstOrDefault<SpellcasterSelectionControl>((Func<SpellcasterSelectionControl, bool>) (x => x.GetViewModel<SpellcasterSelectionControlViewModel>().Information.UniqueIdentifier.Equals(identifier)));
  }

  public SpellcasterSelectionControlViewModel GetSpellcasterSectionViewModel(string identifier)
  {
    SpellcasterSelectionControl spellcasterSection = this.GetSpellcasterSection(identifier);
    return spellcasterSection == null ? (SpellcasterSelectionControlViewModel) null : spellcasterSection.GetViewModel<SpellcasterSelectionControlViewModel>();
  }

  public bool SetPrepareSpell(SpellcastingInformation information, string elementId)
  {
    SpellcasterSelectionControl spellcasterSection = this.GetSpellcasterSection(information.UniqueIdentifier);
    SpellcasterSelectionControlViewModel viewModel = spellcasterSection != null ? spellcasterSection.GetViewModel<SpellcasterSelectionControlViewModel>() : (SpellcasterSelectionControlViewModel) null;
    SelectionElement selectionElement = viewModel != null ? viewModel.KnownSpells.FirstOrDefault<SelectionElement>((Func<SelectionElement, bool>) (x => x.Element.Id.Equals(elementId))) : (SelectionElement) null;
    if (selectionElement == null || selectionElement.IsChosen)
      return false;
    viewModel.SelectedKnownSpell = selectionElement;
    viewModel.TogglePrepareSpellCommand.Execute((object) null);
    return false;
  }

  public IOrderedEnumerable<SelectionElement> GetSpells(SpellcastingInformation information)
  {
    SpellcasterSelectionControl spellcasterSection = this.GetSpellcasterSection(information.UniqueIdentifier);
    SpellcasterSelectionControlViewModel viewModel = spellcasterSection != null ? spellcasterSection.GetViewModel<SpellcasterSelectionControlViewModel>() : (SpellcasterSelectionControlViewModel) null;
    return viewModel == null ? (IOrderedEnumerable<SelectionElement>) null : viewModel.KnownSpells.OrderByDescending<SelectionElement, bool>((Func<SelectionElement, bool>) (x => x.IsChosen)).ThenBy<SelectionElement, int>((Func<SelectionElement, int>) (x => x.Element.AsElement<Spell>().Level)).ThenBy<SelectionElement, string>((Func<SelectionElement, string>) (x => x.Element.Name));
  }
}
