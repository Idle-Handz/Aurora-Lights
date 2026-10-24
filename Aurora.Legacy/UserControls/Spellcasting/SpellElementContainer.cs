// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.Spellcasting.SpellElementContainer
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Data.Elements;

#nullable disable
namespace Builder.Presentation.UserControls.Spellcasting;

public class SpellElementContainer : ElementContainer
{
  private bool _isPrepared;

  public SpellElementContainer(ElementBase element, SpellcastingInformation info = null)
    : base(element)
  {
    this.Info = info;
  }

  public SpellcastingInformation Info { get; }

  public bool HasSpellcastingInformation => this.Info != null;

  public bool IsPrepared
  {
    get => this._isPrepared;
    set => this.SetProperty<bool>(ref this._isPrepared, value, nameof (IsPrepared));
  }
}
