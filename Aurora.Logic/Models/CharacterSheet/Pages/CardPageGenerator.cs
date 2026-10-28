// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.Pages.CardPageGenerator
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models.CharacterSheet.Pages.Content;
using Builder.Presentation.Models.CharacterSheet.PDF;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;

#nullable disable
namespace Builder.Presentation.Models.CharacterSheet.Pages;

public class CardPageGenerator : PageGenerator
{
  public int CardWidth { get; set; }

  public int CardHeight { get; set; }

  public int CardCount { get; protected set; }

  public int CurrentPosition { get; set; }

  public CardPageGenerator(int pageFollowNumber = 1)
    : base(pageFollowNumber)
  {
    this.CardWidth = 180;
    this.CardHeight = 240 /*0xF0*/;
    this.CurrentPosition = -1;
  }

  public void AddGenericCard(Builder.Presentation.Models.CharacterSheet.Pages.Content.GenericCardContent content)
  {
    this.IncrementCurrentPosition();
    PdfImportedPage importedPage = this.Writer.GetImportedPage(this.GetGenericCardReader(), 1);
    Rectangle cardRectangle = this.GetCardRectangle(this.CurrentPosition);
    this.Writer.DirectContentUnder.AddTemplate((PdfTemplate) importedPage, cardRectangle.Left, cardRectangle.Bottom);
    this.AddGenericFillableFields(this.CurrentPosition, content);
    ++this.CardCount;
  }

  public void AddSpellCard(SpellCardContent content)
  {
    this.IncrementCurrentPosition();
    PdfImportedPage importedPage = this.Writer.GetImportedPage(this.GetSpellCardReader(), 1);
    Rectangle cardRectangle = this.GetCardRectangle(this.CurrentPosition);
    this.Writer.DirectContentUnder.AddTemplate((PdfTemplate) importedPage, cardRectangle.Left, cardRectangle.Bottom);
    this.AddSpellCardFillableFields(this.CurrentPosition, content);
    ++this.CardCount;
  }

  public void AddAttackCard(AttackCardContent content)
  {
    this.IncrementCurrentPosition();
    PdfImportedPage importedPage = this.Writer.GetImportedPage(this.GetAttackCardReader(), 1);
    Rectangle cardRectangle = this.GetCardRectangle(this.CurrentPosition);
    this.Writer.DirectContentUnder.AddTemplate((PdfTemplate) importedPage, cardRectangle.Left, cardRectangle.Bottom);
    this.AddAttackCardFillableFields(this.CurrentPosition, content);
    ++this.CardCount;
  }

  private Rectangle GetCardRectangle(int position)
  {
    int cardWidth = this.CardWidth;
    int cardHeight = this.CardHeight;
    int pageMargin = this.PageMargin;
    int ury1 = this.PageHeight - this.PageMargin;
    switch (position)
    {
      case 0:
        return new Rectangle((float) pageMargin, (float) (ury1 - cardHeight), (float) (pageMargin + cardWidth), (float) ury1);
      case 1:
        int llx1 = pageMargin + (cardWidth + this.PageGutter);
        return new Rectangle((float) llx1, (float) (ury1 - cardHeight), (float) (llx1 + cardWidth), (float) ury1);
      case 2:
        int llx2 = pageMargin + (cardWidth + this.PageGutter) * 2;
        return new Rectangle((float) llx2, (float) (ury1 - cardHeight), (float) (llx2 + cardWidth), (float) ury1);
      case 3:
        int ury2 = ury1 - (cardHeight + this.PageGutter);
        return new Rectangle((float) pageMargin, (float) (ury2 - cardHeight), (float) (pageMargin + cardWidth), (float) ury2);
      case 4:
        int llx3 = pageMargin + (cardWidth + this.PageGutter);
        int ury3 = ury1 - (cardHeight + this.PageGutter);
        return new Rectangle((float) llx3, (float) (ury3 - cardHeight), (float) (llx3 + cardWidth), (float) ury3);
      case 5:
        int llx4 = pageMargin + (cardWidth + this.PageGutter) * 2;
        int ury4 = ury1 - (cardHeight + this.PageGutter);
        return new Rectangle((float) llx4, (float) (ury4 - cardHeight), (float) (llx4 + cardWidth), (float) ury4);
      case 6:
        int ury5 = ury1 - (cardHeight + this.PageGutter) * 2;
        return new Rectangle((float) pageMargin, (float) (ury5 - cardHeight), (float) (pageMargin + cardWidth), (float) ury5);
      case 7:
        int llx5 = pageMargin + (cardWidth + this.PageGutter);
        int ury6 = ury1 - (cardHeight + this.PageGutter) * 2;
        return new Rectangle((float) llx5, (float) (ury6 - cardHeight), (float) (llx5 + cardWidth), (float) ury6);
      case 8:
        int llx6 = pageMargin + (cardWidth + this.PageGutter) * 2;
        int ury7 = ury1 - (cardHeight + this.PageGutter) * 2;
        return new Rectangle((float) llx6, (float) (ury7 - cardHeight), (float) (llx6 + cardWidth), (float) ury7);
      default:
        throw new ArgumentException("card index based, between 0-8");
    }
  }

  private PdfReader GetGenericCardReader()
  {
    return this.GetResourceReader("Builder.Presentation.Resources.Sheets.Partial.card.pdf");
  }

  private PdfReader GetSpellCardReader()
  {
    return this.GetResourceReader("Builder.Presentation.Resources.Sheets.Partial.spellcard.pdf");
  }

  private PdfReader GetAttackCardReader()
  {
    return this.GetResourceReader("Builder.Presentation.Resources.Sheets.Partial.attackcard.pdf");
  }

  private void IncrementCurrentPosition()
  {
    if (this.CurrentPosition == 8)
    {
      this.Document.NewPage();
      this.CurrentPosition = 0;
      ++this.PageFollowNumber;
    }
    else
      ++this.CurrentPosition;
  }

  public override void StartNewPage()
  {
    this.CurrentPosition = -1;
    base.StartNewPage();
  }

  private string GetCurrentFieldSuffix() => $"_{this.PageFollowNumber}:{this.CurrentPosition}";

  private string GenerateCurrentFieldName(string name, bool flatten = false)
  {
    string currentFieldName = name + this.GetCurrentFieldSuffix();
    if (flatten)
      this.PartialFlatteningNames.Add(currentFieldName);
    return currentFieldName;
  }

  private void AddGenericFillableFields(int position, Builder.Presentation.Models.CharacterSheet.Pages.Content.GenericCardContent content)
  {
    FillableContentGenerator contentGenerator = new FillableContentGenerator(this.Writer);
    Rectangle cardRectangle = this.GetCardRectangle(position);
    float width1 = (float) this.CardWidth - 6f;
    float width2 = (float) this.CardWidth - 13f;
    Rectangle area1 = this.GetArea(cardRectangle.Left + 6.5f, (float) ((double) cardRectangle.Bottom + (double) this.CardHeight - 3.5), width2, 13.5f);
    Rectangle areaUnder1 = this.GetAreaUnder(area1, 9f, 3f);
    Rectangle area2 = this.GetArea(cardRectangle.Left + 3f, areaUnder1.Bottom - 2f, width1, 195f);
    Rectangle areaUnder2 = this.GetAreaUnder(area2, 8.5f, 1.5f);
    float requiredFontsize1 = contentGenerator.CalculateRequiredFontsize(content.Title, area1, 10f);
    contentGenerator.AddText(area1, this.GenerateCurrentFieldName("card_title"), content.Title, requiredFontsize1, 1);
    contentGenerator.SetItalic();
    contentGenerator.AddText(areaUnder1, this.GenerateCurrentFieldName("card_subtitle"), content.Subtitle, 6f, 1);
    contentGenerator.SetDefault();
    float requiredFontsize2 = contentGenerator.CalculateRequiredFontsize($"{content.DescriptionHtml}{Environment.NewLine}<p></p>", new Rectangle(area2.Left + 2f, area2.Bottom + 2f, area2.Right - 2f, area2.Top - 2f), 7f);
    contentGenerator.FillCardArea(new Rectangle(area2.Left + 2f, area2.Bottom + 2f, area2.Right - 2f, area2.Top - 2f), content.DescriptionHtml, requiredFontsize2);
    contentGenerator.AddText(areaUnder2, this.GenerateCurrentFieldName("card_footer_left"), content.LeftFooter, 6f);
    contentGenerator.AddText(areaUnder2, this.GenerateCurrentFieldName("card_footer_right"), content.RightFooter, 6f, 2);
  }

  private void AddSpellCardFillableFields(int position, SpellCardContent content)
  {
    FillableContentGenerator contentGenerator = new FillableContentGenerator(this.Writer);
    Rectangle cardRectangle = this.GetCardRectangle(position);
    float width1 = (float) this.CardWidth - 6f;
    float width2 = (float) this.CardWidth - 13f;
    Rectangle area1 = this.GetArea(cardRectangle.Left + 6.5f, (float) ((double) cardRectangle.Bottom + (double) this.CardHeight - 3.5), width2, 13.5f);
    Rectangle areaUnder1 = this.GetAreaUnder(area1, 9f, 3f);
    Rectangle area2 = this.GetArea((float) ((double) cardRectangle.Left + 6.5 + 45.0), areaUnder1.Bottom - 2.5f, width2 - 45f, 11f);
    Rectangle areaUnder2 = this.GetAreaUnder(area2, 11f, 0.0f);
    Rectangle areaUnder3 = this.GetAreaUnder(areaUnder2, 11f, 0.0f);
    Rectangle areaUnder4 = this.GetAreaUnder(areaUnder3, 11f, 0.0f);
    Rectangle area3 = this.GetArea(cardRectangle.Left + 3f, areaUnder1.Bottom - 47f, width1, 150f);
    Rectangle areaUnder5 = this.GetAreaUnder(area3, 8.5f, 1.5f);
    contentGenerator.FillCardField(area1, content.Title, FontsHelper.GetRegular(10f), 1);
    contentGenerator.SetItalic();
    contentGenerator.FillCardField(areaUnder1, content.Subtitle, FontsHelper.GetItalic(6f), 1);
    contentGenerator.SetDefault();
    contentGenerator.FillCardField(new Rectangle(area2.Left + 2f, area2.Bottom + 1f, area2.Right - 1f, area2.Top - 1.5f), content.CastingTime, FontsHelper.GetRegular(6f));
    contentGenerator.FillCardField(new Rectangle(areaUnder2.Left + 2f, areaUnder2.Bottom + 1f, areaUnder2.Right - 1f, areaUnder2.Top - 1.5f), content.Range, FontsHelper.GetRegular(6f));
    contentGenerator.FillCardField(new Rectangle(areaUnder3.Left + 2f, areaUnder3.Bottom + 1f, areaUnder3.Right - 1f, areaUnder3.Top - 1.5f), content.Duration, FontsHelper.GetRegular(6f));
    Rectangle componentArea = new Rectangle(areaUnder4.Left + 2f, areaUnder4.Bottom + 1f, areaUnder4.Right - 1f, areaUnder4.Top - 1.5f);
    string components = content.Components ?? string.Empty;
    string description = content.DescriptionHtml ?? string.Empty;
    if (contentGenerator.CalculateRequiredFontsize(components, componentArea, 6f) < 4.5f)
    {
      // Long material requirements need readable space, rather than tiny text
      // squeezed into the single-line component header.
      description = "<p><b>Components.</b> " + System.Net.WebUtility.HtmlEncode(components) + "</p>" + description;
      int materialStart = components.IndexOf('(');
      string prefix = materialStart > 0 ? components.Substring(0, materialStart).Trim() : string.Empty;
      components = prefix.Length is > 0 and < 20 ? prefix + " (details below)" : "See description";
    }
    contentGenerator.FillCardField(componentArea, components, FontsHelper.GetRegular(6f));
    float requiredFontsize = contentGenerator.CalculateRequiredFontsize($"{description}{Environment.NewLine}<p></p>", new Rectangle(area3.Left + 2f, area3.Bottom + 2f, area3.Right - 2f, area3.Top - 2f), 6f);
    contentGenerator.FillCardArea(new Rectangle(area3.Left + 2f, area3.Bottom + 2f, area3.Right - 2f, area3.Top - 2f), description, requiredFontsize);
    Rectangle area4 = new Rectangle(areaUnder5.Left + 3f, areaUnder5.Bottom, areaUnder5.Right - 3f, areaUnder5.Top);
    iTextSharp.text.Font italic = FontsHelper.GetItalic(6f);
    contentGenerator.FillCardField(area4, content.LeftFooter, italic);
    contentGenerator.FillCardField(area4, content.RightFooter, italic, 2);
  }

  private void AddAttackCardFillableFields(int position, AttackCardContent content)
  {
    FillableContentGenerator contentGenerator = new FillableContentGenerator(this.Writer);
    Rectangle cardRectangle = this.GetCardRectangle(position);
    float width1 = (float) this.CardWidth - 6f;
    float width2 = (float) this.CardWidth - 13f;
    Rectangle area1 = this.GetArea(cardRectangle.Left + 6.5f, (float) ((double) cardRectangle.Bottom + (double) this.CardHeight - 3.5), width2, 13.5f);
    Rectangle areaUnder1 = this.GetAreaUnder(area1, 9f, 3f);
    Rectangle area2 = this.GetArea((float) ((double) cardRectangle.Left + 6.5 + 45.0), areaUnder1.Bottom - 2.5f, width2 - 45f, 11f);
    Rectangle areaUnder2 = this.GetAreaUnder(area2, 11f, 0.0f);
    Rectangle areaUnder3 = this.GetAreaUnder(areaUnder2, 11f, 0.0f);
    Rectangle area3 = this.GetArea(cardRectangle.Left + 3f, areaUnder1.Bottom - 36f, width1, 161f);
    Rectangle areaUnder4 = this.GetAreaUnder(area3, 8.5f, 1.5f);
    contentGenerator.AddText(area1, this.GenerateCurrentFieldName("card_title"), content.Title, 10f, 1);
    contentGenerator.SetItalic();
    contentGenerator.AddText(areaUnder1, this.GenerateCurrentFieldName("card_subtitle"), content.Subtitle, 6f, 1);
    contentGenerator.SetDefault();
    contentGenerator.AddText(area2, this.GenerateCurrentFieldName("card_range"), content.Range);
    contentGenerator.AddText(areaUnder2, this.GenerateCurrentFieldName("card_attack"), content.Attack);
    contentGenerator.AddText(areaUnder3, this.GenerateCurrentFieldName("card_damage"), content.Damage);
    contentGenerator.AddText(area3, this.GenerateCurrentFieldName("card_description"), content.Description, 6f, multiline: true);
    contentGenerator.AddText(areaUnder4, this.GenerateCurrentFieldName("card_footer_left"), content.LeftFooter, 6f);
    contentGenerator.AddText(areaUnder4, this.GenerateCurrentFieldName("card_footer_right"), content.RightFooter, 6f, 2);
  }

  private Rectangle GetArea(float x, float y, float width, float height)
  {
    return new Rectangle(x, y - height, x + width, y);
  }

  private Rectangle GetAreaUnder(Rectangle area, float height, float offset)
  {
    return new Rectangle(area.Left, area.Bottom - offset - height, area.Right, area.Bottom - offset);
  }
}
