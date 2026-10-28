// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.Pages.SpellcastingPageGenerator
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models.CharacterSheet.Content;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Models.CharacterSheet.Pages;

public class SpellcastingPageGenerator : PageGenerator
{
  private PdfReader _headerReader;
  private PdfReader _topReader;
  private PdfReader _middleReader;
  private PdfReader _bottomReader;

  public int HeaderHeight { get; set; }

  public int HeaderOffset { get; set; }

  public int SectionTopHeight { get; set; }

  public int SectionMiddleHeight { get; set; }

  public int SectionBottomHeight { get; set; }

  public int SectionMargin { get; set; }

  public Rectangle LatestArea { get; set; }

  public SpellcastingPageGenerator(int pageFollowNumber = 1)
    : base(pageFollowNumber)
  {
    this.HeaderHeight = 100;
    this.HeaderOffset = 10;
    this.SectionTopHeight = 24;
    this.SectionMiddleHeight = 12;
    this.SectionBottomHeight = 24;
    this.SectionMargin = 0;
    this._headerReader = CharacterSheetResources.PartialSpellcastingHeader().CreateReader();
    this._topReader = CharacterSheetResources.PartialSpellcastingTop().CreateReader();
    this._middleReader = CharacterSheetResources.PartialSpellcastingMiddle().CreateReader();
    this._bottomReader = CharacterSheetResources.PartialSpellcastingBottom().CreateReader();
  }

  public void Add(
    CharacterSheetSpellcastingPageExportContent spellcastingContent,
    Rectangle newLatestArea = null)
  {
    if (this.LatestArea == null || newLatestArea != null)
    {
      Rectangle rectangle = this.GetArea(0.0f, (float) (this.PageHeight - this.HeaderOffset), (float) this.PageWidth, (float) this.HeaderHeight);
      if (newLatestArea != null)
        rectangle = newLatestArea;
      this.PlacePage(this._headerReader, rectangle);
      this.AddHeaderFields(rectangle, spellcastingContent);
      Rectangle previousRectangle1 = this.AddSection(rectangle, spellcastingContent.Cantrips, spellcastingContent.SpellcastingClass);
      if (this.RequiresNewPage(previousRectangle1, spellcastingContent.Spells1))
        previousRectangle1 = this.AppendNewPage(spellcastingContent);
      Rectangle previousRectangle2 = this.AddSection(previousRectangle1, spellcastingContent.Spells1, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
      if (this.RequiresNewPage(previousRectangle2, spellcastingContent.Spells2))
        previousRectangle2 = this.AppendNewPage(spellcastingContent);
      Rectangle previousRectangle3 = this.AddSection(previousRectangle2, spellcastingContent.Spells2, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
      if (this.RequiresNewPage(previousRectangle3, spellcastingContent.Spells3))
        previousRectangle3 = this.AppendNewPage(spellcastingContent);
      Rectangle previousRectangle4 = this.AddSection(previousRectangle3, spellcastingContent.Spells3, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
      if (this.RequiresNewPage(previousRectangle4, spellcastingContent.Spells4))
        previousRectangle4 = this.AppendNewPage(spellcastingContent);
      Rectangle previousRectangle5 = this.AddSection(previousRectangle4, spellcastingContent.Spells4, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
      if (this.RequiresNewPage(previousRectangle5, spellcastingContent.Spells5))
        previousRectangle5 = this.AppendNewPage(spellcastingContent);
      Rectangle previousRectangle6 = this.AddSection(previousRectangle5, spellcastingContent.Spells5, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
      if (this.RequiresNewPage(previousRectangle6, spellcastingContent.Spells6))
        previousRectangle6 = this.AppendNewPage(spellcastingContent);
      Rectangle previousRectangle7 = this.AddSection(previousRectangle6, spellcastingContent.Spells6, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
      if (this.RequiresNewPage(previousRectangle7, spellcastingContent.Spells7))
        previousRectangle7 = this.AppendNewPage(spellcastingContent);
      Rectangle previousRectangle8 = this.AddSection(previousRectangle7, spellcastingContent.Spells7, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
      if (this.RequiresNewPage(previousRectangle8, spellcastingContent.Spells8))
        previousRectangle8 = this.AppendNewPage(spellcastingContent);
      Rectangle previousRectangle9 = this.AddSection(previousRectangle8, spellcastingContent.Spells8, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
      if (this.RequiresNewPage(previousRectangle9, spellcastingContent.Spells9))
        previousRectangle9 = this.AppendNewPage(spellcastingContent);
      this.LatestArea = this.AddSection(previousRectangle9, spellcastingContent.Spells9, spellcastingContent.SpellcastingClass, spellcastingContent.IsMulticlassSpellcaster);
    }
    else
    {
      int num1 = 0 + (this.HeaderHeight + this.HeaderOffset);
      if (spellcastingContent.Cantrips.Spells.Any<CharacterSheetSpellcastingPageExportContent.SpellExportContent>())
      {
        int num2 = num1 + this.SectionMargin + this.SectionTopHeight;
        int num3 = 0;
        if (spellcastingContent.Cantrips.Spells.Count > 5)
        {
          num3 = (spellcastingContent.Cantrips.Spells.Count - 5) / 3;
          if ((spellcastingContent.Cantrips.Spells.Count - 5) % 3 > 0)
            ++num3;
        }
        for (int index = 0; index < num3; ++index)
          num2 += this.SectionMiddleHeight;
        num1 = num2 + this.SectionBottomHeight;
      }
      if ((double) num1 <= (double) this.LatestArea.Bottom)
      {
        Rectangle area = this.GetArea(0.0f, this.LatestArea.Bottom - ((double) num1 <= (double) this.LatestArea.Bottom - 20.0 ? 30f : (float) this.HeaderOffset), (float) this.PageWidth, (float) this.HeaderHeight);
        this.Add(spellcastingContent, area);
      }
      else
      {
        this.NewPage();
        this.LatestArea = (Rectangle) null;
        this.Add(spellcastingContent);
      }
    }
  }

  private Rectangle AddSection(
    Rectangle previousRectangle,
    CharacterSheetSpellcastingPageExportContent.SpellcastingLevelExportContent spells,
    string className,
    bool isMulticlass = false)
  {
    if (!spells.Spells.Any<CharacterSheetSpellcastingPageExportContent.SpellExportContent>() && spells.AvailableSlots <= 0)
      return previousRectangle;
    int num1 = Math.Max(5, spells.Spells.Count);
    int num2 = 0;
    if (num1 > 5)
    {
      num2 = (num1 - 5) / 3;
      if ((num1 - 5) % 3 > 0)
        ++num2;
    }
    FillableContentGenerator contentGenerator = new FillableContentGenerator(this.Writer);
    Rectangle areaUnder1 = this.GetAreaUnder(previousRectangle, 24f, (double) Math.Abs(previousRectangle.Height - 100f) < 1.0 ? 10f : (float) this.SectionMargin);
    this._topReader = CharacterSheetResources.PartialSpellcastingTop(spells.Level).CreateReader();
    this.PlacePage(this._topReader, areaUnder1);
    int index1 = 0;
    Rectangle area1 = this.GetArea((float) ((double) areaUnder1.Left + (double) this.PageMargin + 20.0), areaUnder1.Top - 13f, 154f, 10f);
    Rectangle area2 = this.GetArea((float) ((double) area1.Right + (double) this.PageGutter + 26.0), area1.Top, 154f, 10f);
    Rectangle area3 = this.GetArea((float) ((double) area2.Right + (double) this.PageGutter + 26.0), area1.Top, 154f, 10f);
    float x = area1.Right - 3.2f;
    float y = area1.Bottom + 5f;
    this.GetArea(x - 4f, y + 5f, 8f, 10f);
    for (int index2 = 0; index2 < spells.AvailableSlots; ++index2)
    {
      this.Writer.DirectContentUnder.SetColorFill(BaseColor.White);
      this.Writer.DirectContentUnder.Circle(x, y, 3f);
      this.Writer.DirectContentUnder.Fill();
      Rectangle area4 = this.GetArea(x - 4f, y + 5f, 8f, 10f);
      contentGenerator.AddCheck(area4, this.GenerateCurrentFieldName($"slot{index2}_{spells.Level}:{index1}_", isMulticlass ? "multiclass" : className));
      x -= 10f;
    }
    if (spells.AvailableSlots > 0)
    {
      contentGenerator.SetBold();
      contentGenerator.SetColor(BaseColor.White);
      string currentFieldName = this.GenerateCurrentFieldName($"slots_{spells.Level}:{index1}_", className);
      contentGenerator.AddText(this.GetArea(area1.Left + 35f, area1.Top, 40f, area1.Height), currentFieldName, $"{spells.AvailableSlots} SPELL SLOTS", 5f);
      this.PartialFlatteningNames.Add(currentFieldName);
      contentGenerator.ResetColor();
      contentGenerator.SetDefault();
    }
    contentGenerator.AddText(area2, this.GenerateCurrentFieldName($"spells_{spells.Level}:{index1}_", className), spells.Get(index1)?.ToString(), 7f);
    if (spells.Level != 0)
      contentGenerator.AddCheck(this.GetAreaLeftOf(area2, 8f, 5f), this.GenerateCurrentFieldName($"prepare_{spells.Level}:{index1}_", className), spells.Get(index1));
    int index3 = index1 + 1;
    contentGenerator.AddText(area3, this.GenerateCurrentFieldName($"spells_{spells.Level}:{index3}_", className), spells.Get(index3)?.ToString(), 7f);
    if (spells.Level != 0)
      contentGenerator.AddCheck(this.GetAreaLeftOf(area3, 8f, 5f), this.GenerateCurrentFieldName($"prepare_{spells.Level}:{index3}_", className), spells.Get(index3));
    int index4 = index3 + 1;
    Rectangle area5 = areaUnder1;
    for (int index5 = 0; index5 < num2; ++index5)
    {
      area5 = this.GetAreaUnder(area5, 12f, 0.0f);
      this.PlacePage(this._middleReader, area5);
      Rectangle area6 = this.GetArea((float) ((double) area5.Left + (double) this.PageMargin + 20.0), area5.Top - 1f, 154f, 10f);
      Rectangle area7 = this.GetArea((float) ((double) area6.Right + (double) this.PageGutter + 26.0), area6.Top, 154f, 10f);
      Rectangle area8 = this.GetArea((float) ((double) area7.Right + (double) this.PageGutter + 26.0), area6.Top, 154f, 10f);
      contentGenerator.AddText(area6, this.GenerateCurrentFieldName($"spells_{spells.Level}:{index4}_", className), spells.Get(index4)?.ToString(), 7f);
      if (spells.Level != 0)
        contentGenerator.AddCheck(this.GetAreaLeftOf(area6, 8f, 4.5f), this.GenerateCurrentFieldName($"prepare_{spells.Level}:{index4}_", className), spells.Get(index4));
      int index6 = index4 + 1;
      contentGenerator.AddText(area7, this.GenerateCurrentFieldName($"spells_{spells.Level}:{index6}_", className), spells.Get(index6)?.ToString(), 7f);
      if (spells.Level != 0)
        contentGenerator.AddCheck(this.GetAreaLeftOf(area7, 8f, 5f), this.GenerateCurrentFieldName($"prepare_{spells.Level}:{index6}_", className), spells.Get(index6));
      int index7 = index6 + 1;
      contentGenerator.AddText(area8, this.GenerateCurrentFieldName($"spells_{spells.Level}:{index7}_", className), spells.Get(index7)?.ToString(), 7f);
      if (spells.Level != 0)
        contentGenerator.AddCheck(this.GetAreaLeftOf(area8, 8f, 5f), this.GenerateCurrentFieldName($"prepare_{spells.Level}:{index7}_", className), spells.Get(index7));
      index4 = index7 + 1;
    }
    Rectangle areaUnder2 = this.GetAreaUnder(area5, 24f, 0.0f);
    this.PlacePage(this._bottomReader, areaUnder2);
    Rectangle area9 = this.GetArea((float) ((double) areaUnder2.Left + (double) this.PageMargin + 20.0), areaUnder2.Top - 1f, 154f, 10f);
    Rectangle area10 = this.GetArea((float) ((double) area9.Right + (double) this.PageGutter + 26.0), area9.Top, 154f, 10f);
    Rectangle area11 = this.GetArea((float) ((double) area10.Right + (double) this.PageGutter + 26.0), area9.Top, 154f, 10f);
    contentGenerator.AddText(area9, this.GenerateCurrentFieldName($"spells_{spells.Level}:{index4}_", className), spells.Get(index4)?.ToString(), 7f);
    if (spells.Level != 0)
      contentGenerator.AddCheck(this.GetAreaLeftOf(area9, 8f, 4.5f), this.GenerateCurrentFieldName($"prepare_{spells.Level}:{index4}_", className), spells.Get(index4));
    int index8 = index4 + 1;
    contentGenerator.AddText(area10, this.GenerateCurrentFieldName($"spells_{spells.Level}:{index8}_", className), spells.Get(index8)?.ToString(), 7f);
    if (spells.Level != 0)
      contentGenerator.AddCheck(this.GetAreaLeftOf(area10, 8f, 5f), this.GenerateCurrentFieldName($"prepare_{spells.Level}:{index8}_", className), spells.Get(index8));
    int index9 = index8 + 1;
    contentGenerator.AddText(area11, this.GenerateCurrentFieldName($"spells_{spells.Level}:{index9}_", className), spells.Get(index9)?.ToString(), 7f);
    if (spells.Level != 0)
      contentGenerator.AddCheck(this.GetAreaLeftOf(area11, 8f, 5f), this.GenerateCurrentFieldName($"prepare_{spells.Level}:{index9}_", className), spells.Get(index9));
    return areaUnder2;
  }

  private void AddHeaderFields(
    Rectangle area,
    CharacterSheetSpellcastingPageExportContent content)
  {
    content.SpellcastingClass.ToLower().Replace(" ", "_");
    FillableContentGenerator contentGenerator = new FillableContentGenerator(this.Writer);
    contentGenerator.AddText(this.GetArea((float) ((double) area.Left + (double) this.PageMargin + 40.0), area.Top - 59f, 170f, 20f), this.GenerateCurrentFieldName("spellcasting_class", content.SpellcastingClass), content.ToString(), 10f, 1);
    Rectangle area1 = this.GetArea((float) ((double) area.Left + (double) this.PageMargin + 267.0), area.Top - 32f, 49f, 16f);
    Rectangle area2 = this.GetArea(area1.Left + 75f, area1.Top, area1.Width, area1.Height);
    Rectangle area3 = this.GetArea(area2.Left + 75f, area1.Top, area1.Width, area1.Height);
    Rectangle area4 = this.GetArea(area3.Left + 75f, area1.Top, area1.Width, area1.Height);
    contentGenerator.AddText(area1, this.GenerateCurrentFieldName("spellcasting_ability", content.SpellcastingClass), content.Ability, alignment: 1);
    contentGenerator.AddText(area2, this.GenerateCurrentFieldName("spellcasting_bonus", content.SpellcastingClass), content.AttackBonus, alignment: 1);
    contentGenerator.AddText(area3, this.GenerateCurrentFieldName("spellcasting_save", content.SpellcastingClass), content.Save, alignment: 1);
    contentGenerator.AddText(area4, this.GenerateCurrentFieldName("spellcasting_prepare", content.SpellcastingClass), content.PrepareCount, alignment: 1);
  }

  private void NewPage()
  {
    this.Document.NewPage();
    ++this.PageFollowNumber;
    this.LatestArea = (Rectangle) null;
  }

  private Rectangle AppendNewPage(
    CharacterSheetSpellcastingPageExportContent content)
  {
    this.Document.NewPage();
    ++this.PageFollowNumber;
    this.LatestArea = this.GetArea(0.0f, (float) (this.PageHeight - this.HeaderOffset), (float) this.PageWidth, (float) this.HeaderHeight);
    this.PlacePage(this._headerReader, this.LatestArea);
    this.AddHeaderFields(this.LatestArea, content);
    return this.LatestArea;
  }

  private float CalculateRequireSectionHeight(
    Rectangle previousRectangle,
    CharacterSheetSpellcastingPageExportContent.SpellcastingLevelExportContent spells)
  {
    if (!spells.Spells.Any<CharacterSheetSpellcastingPageExportContent.SpellExportContent>())
      return 0.0f;
    float bottom1 = previousRectangle.Bottom;
    int num1 = Math.Max(5, spells.Spells.Count);
    int num2 = 0;
    if (num1 > 5)
    {
      num2 = (num1 - 5) / 3;
      if ((num1 - 5) % 3 > 0)
        ++num2;
    }
    FillableContentGenerator contentGenerator = new FillableContentGenerator(this.Writer);
    Rectangle areaUnder = this.GetAreaUnder(previousRectangle, 24f, (double) Math.Abs(previousRectangle.Height - 100f) < 1.0 ? 10f : (float) this.SectionMargin);
    for (int index = 0; index < num2; ++index)
      areaUnder = this.GetAreaUnder(areaUnder, 12f, 0.0f);
    float bottom2 = this.GetAreaUnder(areaUnder, 24f, 0.0f).Bottom;
    return bottom1 - bottom2;
  }

  private bool RequiresNewPage(
    Rectangle previousRectangle,
    CharacterSheetSpellcastingPageExportContent.SpellcastingLevelExportContent spells)
  {
    return (double) previousRectangle.Bottom - (double) this.CalculateRequireSectionHeight(previousRectangle, spells) <= (double) this.PageMargin;
  }

  private string GenerateCurrentFieldName(string name, string className)
  {
    return $"{name}:{className}:{this.PageFollowNumber}";
  }

  private Rectangle GetArea(float x, float y, float width, float height)
  {
    return new Rectangle(x, y - height, x + width, y);
  }

  private Rectangle GetAreaUnder(Rectangle area, float height, float offset)
  {
    return new Rectangle(area.Left, area.Bottom - offset - height, area.Right, area.Bottom - offset);
  }

  private Rectangle GetAreaLeftOf(Rectangle area, float width, float offset)
  {
    return new Rectangle(area.Left - offset - width, area.Bottom, area.Left - offset, area.Top);
  }

  public override void Dispose()
  {
    base.Dispose();
    this._headerReader?.Dispose();
    this._topReader?.Dispose();
    this._middleReader?.Dispose();
    this._bottomReader?.Dispose();
  }
}
