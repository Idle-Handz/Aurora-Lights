using Aurora.Documents.Helpers;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.FillableContentGenerator
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models.CharacterSheet.Content;
using Builder.Presentation.Models.CharacterSheet.PDF;
using Builder.Presentation.Models.Sheet;
using Builder.Presentation.Utilities;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Models.CharacterSheet;

public class FillableContentGenerator
{
  private readonly PdfWriter _writer;
  private readonly BaseFont _defaultFont;
  private readonly BaseFont _italicFont;
  private readonly BaseFont _boldFont;
  private readonly BaseFont _boldItalicFont;
  private BaseFont _currentFont;
  private BaseColor _currentFontColor;
  private const string ListIndentation = "    ";

  public FillableContentGenerator(PdfWriter writer)
  {
    this._writer = writer;
    this._defaultFont = FontsHelper.GetRegular().BaseFont;
    this._italicFont = FontsHelper.GetItalic().BaseFont;
    this._boldFont = FontsHelper.GetBold().BaseFont;
    this._boldItalicFont = FontsHelper.GetBoldItalic().BaseFont;
    this._currentFont = this._defaultFont;
    this._currentFontColor = BaseColor.Black;
  }

  public void SetDefault() => this._currentFont = this._defaultFont;

  public void SetBold() => this._currentFont = this._boldFont;

  public void SetItalic() => this._currentFont = this._italicFont;

  public void SetBoldItalic() => this._currentFont = this._boldItalicFont;

  public void SetColor(BaseColor color) => this._currentFontColor = color;

  public void ResetColor() => this._currentFontColor = BaseColor.Black;

  [Obsolete]
  public BaseFont GetCurrentFont() => this._currentFont;

  public static iTextSharp.text.Font GetFont(string fontName, string filename, float size = 0.0f)
  {
    return FontsHelper.GetFont(fontName, filename, size) ?? FontFactory.GetFont("Helvetica", size);
  }

  public iTextSharp.text.Font GetCurrentAsFont(float size) => new iTextSharp.text.Font(this._currentFont, size);

  public TextField CreateText(
    Rectangle area,
    string name,
    string content = "",
    float fontsize = 0.0f,
    int alignment = 0)
  {
    TextField textField = new TextField(this._writer, area, name);
    textField.Font = this._currentFont;
    textField.Alignment = alignment;
    textField.FontSize = fontsize;
    textField.Options = 12582912 /*0xC00000*/;
    textField.TextColor = this._currentFontColor;
    TextField text = textField;
    if (!string.IsNullOrWhiteSpace(content))
      text.Text = content;
    return text;
  }

  public TextField CreateArea(
    Rectangle area,
    string name,
    string content = "",
    float fontsize = 0.0f,
    int alignment = 0)
  {
    TextField text = this.CreateText(area, name, content, fontsize, alignment);
    text.Options = 46141440 /*0x02C01000*/;
    return text;
  }

  public TextField AddText(
    Rectangle area,
    string name,
    string content = "",
    float fontsize = 0.0f,
    int alignment = 0,
    bool multiline = false)
  {
    TextField textField = multiline ? this.CreateArea(area, name, content, fontsize, alignment) : this.CreateText(area, name, content, fontsize, alignment);
    this._writer.AddAnnotation((PdfAnnotation) textField.GetTextField());
    return textField;
  }

  public RadioCheckField AddCheck(
    Rectangle area,
    string name,
    CharacterSheetSpellcastingPageExportContent.SpellExportContent spell = null)
  {
    RadioCheckField radioCheckField = new RadioCheckField(this._writer, area, name, "Yes");
    radioCheckField.Checked = spell != null && spell.IsPrepared;
    this._writer.AddAnnotation((PdfAnnotation) radioCheckField.CheckField);
    return radioCheckField;
  }

  public float CalculateRequiredFontsize(
    string content,
    Rectangle contentArea,
    float maximumFontSize)
  {
    return PdfSignatureAppearance.FitText(FontsHelper.GetRegular(), content, contentArea, maximumFontSize, 0);
  }

  public void FillArea(
    Rectangle descriptionArea,
    string content,
    float requiredFontSize,
    int alignment = 0)
  {
    ColumnText column = new ColumnText(this._writer.DirectContent);
    column.Alignment = alignment;
    column.SetSimpleColumn(descriptionArea);
    ElementDescriptionGenerator.FillColumn(column, content, requiredFontSize);
    column.Go();
  }

  public void FillCardArea(
    Rectangle descriptionArea,
    string content,
    float requiredFontSize,
    int alignment = 0)
  {
    ColumnText CreateColumn(float size)
    {
      var column = new ColumnText(this._writer.DirectContent) { Alignment = alignment };
      column.SetSimpleColumn(descriptionArea);
      this.FillCardDescription(column, content, size);
      return column;
    }

    // Measure the formatted paragraphs, lists and tables that will actually be
    // painted. The plain-text estimate can otherwise clip a card's final lines.
    float size = requiredFontSize;
    if (ColumnText.HasMoreText(CreateColumn(size).Go(true)))
    {
      float lower = 0.5f;
      float upper = size;
      if (ColumnText.HasMoreText(CreateColumn(lower).Go(true)))
        throw new InvalidOperationException("The card description cannot fit in its available area.");
      for (int step = 0; step < 12; step++)
      {
        float candidate = (lower + upper) / 2f;
        if (ColumnText.HasMoreText(CreateColumn(candidate).Go(true))) upper = candidate;
        else lower = candidate;
      }
      size = lower;
    }
    if (ColumnText.HasMoreText(CreateColumn(size).Go()))
      throw new InvalidOperationException("The card description was not fully written.");
  }

  public void FillCardField(Rectangle area, string description, iTextSharp.text.Font font, int alignment = 0)
  {
    float requiredFontsize = this.CalculateRequiredFontsize(description.Trim(), area, font.Size);
    ColumnText columnText = new ColumnText(this._writer.DirectContent);
    columnText.Alignment = alignment;
    columnText.SetSimpleColumn(area);
    columnText.SetLeading(requiredFontsize, 0f);
    Chunk chunk = new Chunk(description, font);
    chunk.Font.Size = requiredFontsize;

    Paragraph paragraph = new Paragraph(chunk);
    columnText.AddText(chunk);
    columnText.Go();
  }

  public void Fill(Rectangle area, ContentArea content, int alignment = 0)
  {
    float requiredFontsize = this.CalculateRequiredFontsize(content.ToString(), area, 8f);
    ColumnText columnText = new ColumnText(this._writer.DirectContent)
    {
      Alignment = alignment
    };
    columnText.SetSimpleColumn(area);
    columnText.SetLeading(requiredFontsize, 0f);
    foreach (ContentLine contentLine in (List<ContentLine>) content)
    {
      Paragraph paragraph = new Paragraph(requiredFontsize);
      if (contentLine.HasName())
        paragraph.Add((IElement) new Chunk(contentLine.Name + ". ", FontsHelper.GetBoldItalic(requiredFontsize)));
      if (contentLine.HasContent())
      {
        this.GetElements(contentLine.Content);
        paragraph.Add((IElement) new Chunk(" " + contentLine.Content, FontsHelper.GetRegular(requiredFontsize)));
      }
    }
    columnText.Go();
  }

  private void FillCardDescription(ColumnText column, string description, float fontsize)
  {
    column.SetLeading(fontsize, 0f);
    List<IElement> elements = this.GetElements(description);
    bool flag1 = false;
    foreach (IElement element in elements)
    {
      Paragraph paragraph = new Paragraph(fontsize);
      switch (element)
      {
        case PdfPTable pdfPtable:
          List<PdfPRow> rows = pdfPtable.Rows;
          foreach (PdfPRow pdfProw in rows)
          {
            Chunk chunk1 = new Chunk();
            chunk1.Append("    ");
            paragraph.Add((IElement) chunk1);
            bool flag2 = pdfProw == rows.FirstOrDefault<PdfPRow>();
            foreach (PdfPCell cell in pdfProw.GetCells())
            {
              // Spanning cells leave null placeholders in subsequent columns/rows.
              if (cell?.CompositeElements == null) continue;
              foreach (IElement compositeElement in cell.CompositeElements)
              {
                foreach (Chunk chunk2 in (IEnumerable<Chunk>) compositeElement.Chunks)
                {
                  if (flag2)
                    chunk2.Font.SetStyle(1);
                  paragraph.Add((IElement) chunk2);
                }
                paragraph.Add((IElement) this.CreateIndentationChunk());
              }
            }
            paragraph.Add(new Chunk(Environment.NewLine));
          }
          paragraph.Add(new Chunk(Environment.NewLine));
          break;
        case List listElement:
          paragraph.Add((IElement) this.CreateListChunk(listElement));
          paragraph.Add(new Chunk(Environment.NewLine));
          break;
        default:
          foreach (Chunk chunk in (IEnumerable<Chunk>) element.Chunks)
          {
            if (flag1)
            {
              paragraph.Add((IElement) this.CreateIndentationChunk());
              flag1 = false;
            }
            else if (element.Chunks.Count > 1 && chunk == element.Chunks.First<Chunk>() && elements.First<IElement>() != element)
              paragraph.Add((IElement) this.CreateIndentationChunk());
            else if (chunk.Content.ToLower().Contains("at higher level"))
              paragraph.Add((IElement) this.CreateIndentationChunk());
            paragraph.Add((IElement) chunk);
          }
          if (!element.Equals((object) elements.Last<IElement>()))
          {
            paragraph.Add(new Chunk(Environment.NewLine));
            break;
          }
          break;
      }
      flag1 = !(element is List);
      this.FormatCardParagraphChunks(paragraph, fontsize);
      column.AddText((Phrase) paragraph);
    }
  }

  private void FillCardField(ColumnText column, string description, iTextSharp.text.Font font)
  {
    column.SetLeading(font.Size, 0f);
    Paragraph paragraph = new Paragraph(font.Size, description, font);
    column.AddText((Phrase) paragraph);
  }

  private List<IElement> GetElements(string description)
  {
    if (description != null)
      return HtmlWorker.ParseToList((TextReader) new StringReader(description), (StyleSheet) null);
    if (Debugger.IsAttached)
      Debugger.Break();
    return (List<IElement>) null;
  }

  private Chunk CreateListChunk(List listElement)
  {
    Chunk listChunk = new Chunk();
    foreach (IElement item in listElement.Items)
    {
      if (item is List nested)
      {
        listChunk.Append(this.CreateListChunk(nested).Content);
        continue;
      }
      string text = string.Concat(item.Chunks.Select(chunk => chunk.Content)).Trim();
      if (text.Length == 0) continue;
      string symbol = (item as ListItem)?.ListSymbol?.Content ?? listElement.Symbol.Content;
      listChunk.Append($"    {symbol} {text}{Environment.NewLine}");
    }
    return listChunk;
  }

  private Chunk CreateIndentationChunk() => new Chunk("    ");

  private void FormatCardParagraphChunks(Paragraph paragraph, float fontsize)
  {
    foreach (Chunk chunk in (IEnumerable<Chunk>) paragraph.Chunks)
    {

      switch (chunk.Font.Style)
      {
        case -1:
        case 0:
          chunk.Font = FontsHelper.GetRegular(fontsize);
          continue;
        case 1:
          chunk.Font = FontsHelper.GetBold(fontsize);
          continue;
        case 2:
          chunk.Font = FontsHelper.GetItalic(fontsize);
          continue;
        case 3:
          chunk.Font = FontsHelper.GetBoldItalic(fontsize);
          continue;
        default:
          chunk.Font = FontsHelper.GetRegular(fontsize);
          continue;
      }
    }
  }

  private void FormatParagraphWithFont(Paragraph paragraph, iTextSharp.text.Font font)
  {
    foreach (Chunk chunk in (IEnumerable<Chunk>) paragraph.Chunks)
    {

      chunk.Font = font;
    }
  }
}
