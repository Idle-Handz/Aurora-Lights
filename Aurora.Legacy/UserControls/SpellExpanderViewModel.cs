// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.SpellExpanderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.ViewModels.Base;

#nullable disable
namespace Builder.Presentation.UserControls;

public class SpellExpanderViewModel : ViewModelBase
{
  private Spell _element;

  public SpellExpanderViewModel()
  {
    if (this.IsInDesignMode)
    {
      this._element = new Spell();
      this._element.ElementHeader = new ElementHeader("Fireball", "Spell", "Player's Handbook", "ID_PHB_SPELL_FIREBALL");
    }
    else
      this.SubscribeWithEventAggregator();
  }

  public Spell SpellElement
  {
    get => this._element;
    set => this.SetProperty<Spell>(ref this._element, value, nameof (SpellElement));
  }
}
