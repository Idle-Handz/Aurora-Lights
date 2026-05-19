// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Sheet.SpellcastingSheetInfo
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Collections.Generic;

#nullable disable
namespace Builder.Presentation.Models.Sheet;

public class SpellcastingSheetInfo
{
  public string SpellcastingClass { get; set; }

  public string SpellcastingAbility { get; set; }

  public string SpellSaveDifficultyClass { get; set; }

  public string SpellAttackBonus { get; set; }

  public SpellcastingSheetInfo.SpellsContainer Cantrips { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 8);

  public SpellcastingSheetInfo.SpellsContainer Spells1st { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 12);

  public SpellcastingSheetInfo.SpellsContainer Spells2nd { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 13);

  public SpellcastingSheetInfo.SpellsContainer Spells3rd { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 13);

  public SpellcastingSheetInfo.SpellsContainer Spells4th { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 13);

  public SpellcastingSheetInfo.SpellsContainer Spells5th { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 9);

  public SpellcastingSheetInfo.SpellsContainer Spells6th { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 9);

  public SpellcastingSheetInfo.SpellsContainer Spells7th { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 9);

  public SpellcastingSheetInfo.SpellsContainer Spells8th { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 7);

  public SpellcastingSheetInfo.SpellsContainer Spells9th { get; } = new SpellcastingSheetInfo.SpellsContainer(0, 0, 7);

  public class SpellsContainer : List<SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder>
  {
    public int TotalSlots { get; set; }

    public int ExpendedSlots { get; set; }

    public int MaximumDisplayable { get; set; }

    public SpellsContainer(int totalSlots, int expendedSlots, int maximumDisplayable)
    {
      this.TotalSlots = totalSlots;
      this.ExpendedSlots = expendedSlots;
      this.MaximumDisplayable = maximumDisplayable;
    }

    public SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder GetSpell(int lineNumber)
    {
      try
      {
        return this[lineNumber - 1];
      }
      catch (ArgumentOutOfRangeException)
      {
        return new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(string.Empty);
      }
    }

    public class SpellPlaceholder
    {
      public string Name { get; set; }

      public bool IsPrepared { get; set; }

      public SpellPlaceholder(string name, bool isPrepared = false)
      {
        this.Name = name;
        this.IsPrepared = isPrepared;
      }
    }
  }
}
