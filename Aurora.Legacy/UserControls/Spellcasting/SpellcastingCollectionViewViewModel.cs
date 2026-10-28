// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.Spellcasting.SpellcastingCollectionViewViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Data;
using Builder.Presentation.Events.Character;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace Builder.Presentation.UserControls.Spellcasting;

public class SpellcastingCollectionViewViewModel : 
  ViewModelBase,
  ISubscriber<CharacterManagerElementRegistered>,
  ISubscriber<CharacterManagerElementUnregistered>
{
  private ElementBase _selectedElement;

  public SpellcastingCollectionViewViewModel()
  {
    if (this.IsInDesignMode)
      return;
    this.SubscribeWithEventAggregator();
  }

  public ElementBaseCollection SpellCollection { get; } = new ElementBaseCollection();

  public ElementBase SelectedElement
  {
    get => this._selectedElement;
    set
    {
      this.SetProperty<ElementBase>(ref this._selectedElement, value, nameof (SelectedElement));
      this.EventAggregator.Send<ElementDescriptionDisplayRequestEvent>(new ElementDescriptionDisplayRequestEvent(this._selectedElement));
    }
  }

  private void PopulateSpellCollection()
  {
    IEnumerable<ElementBase> elements = CharacterManager.Current.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Spell")));
    this.SpellCollection.Clear();
    this.SpellCollection.AddRange(elements);
  }

  public void OnHandleEvent(CharacterManagerElementRegistered args)
  {
    this.PopulateSpellCollection();
  }

  public void OnHandleEvent(CharacterManagerElementUnregistered args)
  {
    this.PopulateSpellCollection();
  }
}
