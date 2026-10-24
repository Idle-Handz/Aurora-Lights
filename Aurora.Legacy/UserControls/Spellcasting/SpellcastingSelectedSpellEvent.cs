// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.Spellcasting.SpellcastingSelectedSpellEvent
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Data.Elements;

#nullable disable
namespace Builder.Presentation.UserControls.Spellcasting;

public class SpellcastingSelectedSpellEvent : EventBase
{
  public SpellcastingInformation Information { get; }

  public Spell SelectedSpell { get; }

  public SpellcastingSelectedSpellEvent(SpellcastingInformation information, Spell selectedSpell)
  {
    this.Information = information;
    this.SelectedSpell = selectedSpell;
  }
}
