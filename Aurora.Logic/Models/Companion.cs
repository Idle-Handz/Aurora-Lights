// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Companion
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data.Elements;
using Builder.Presentation.Models.NewFolder1;
using Builder.Presentation.Services.Data;
using System.IO;

#nullable disable
namespace Builder.Presentation.Models;

public class Companion : StatisticsBase
{
  public Companion(CompanionElement element = null)
  {
    this.Abilities.DisablePointsCalculation = true;
    this.Statistics = new CompanionStatistics(this);
    if (element != null)
      this.SetTemplate(element);
  }

  public CompanionStatistics Statistics { get; }

  public CompanionElement Element { get; private set; }

  public FillableField CompanionName { get; } = new FillableField();

  public FillableField Portrait { get; } = new FillableField();

  public FillableField Initiative { get; } = new FillableField();

  public FillableField ArmorClass { get; } = new FillableField();

  public FillableField Speed { get; } = new FillableField();

  public FillableField MaxHp { get; } = new FillableField();

  public void SetTemplate(CompanionElement element)
  {
    this.Element = element;
    this.CompanionName.OriginalContent = element.Name;
    foreach (string file in Directory.Exists(DataManager.Current.UserDocumentsCompanionGalleryDirectory)
      ? Directory.GetFiles(DataManager.Current.UserDocumentsCompanionGalleryDirectory) : System.Array.Empty<string>())
    {
      if (file.ToLower().Contains(element.Name.ToLower()))
      {
        this.Portrait.OriginalContent = file;
        break;
      }
    }
    this.Abilities.Strength.BaseScore = element.Strength;
    this.Abilities.Dexterity.BaseScore = element.Dexterity;
    this.Abilities.Constitution.BaseScore = element.Constitution;
    this.Abilities.Intelligence.BaseScore = element.Intelligence;
    this.Abilities.Wisdom.BaseScore = element.Wisdom;
    this.Abilities.Charisma.BaseScore = element.Charisma;
    this.DisplayName = element.Name;
    this.DisplayBuild = $"{element.Size} {element.CreatureType?.ToLower()}, {element.Alignment?.ToLower()}";
    this.Initiative.OriginalContent = this.Abilities.Dexterity.ModifierString;
    this.ArmorClass.OriginalContent = element.ArmorClass;
    this.Speed.OriginalContent = element.Speed;
    this.MaxHp.OriginalContent = element.HitPoints;
  }

  public override void Reset()
  {
    base.Reset();
    this.Element = (CompanionElement) null;
    this.CompanionName.Clear();
    this.Portrait.Clear();
    this.Initiative.Clear();
    this.ArmorClass.Clear();
    this.Speed.Clear();
    this.MaxHp.Clear();
    this.Statistics.Reset();
  }

}
