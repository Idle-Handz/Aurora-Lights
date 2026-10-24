using Aurora.Documents.Helpers;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Sheet.Spellcard
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data.Elements;
using Builder.Presentation.Services.Data;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

#nullable disable
namespace Builder.Presentation.Models.Sheet;

public class Spellcard
{
  private readonly string _fieldname;
  private readonly Spell _spell;

  public Spellcard(string fieldname, Spell spell)
  {
    this._fieldname = fieldname;
    this._spell = spell;
  }

  public void Stamp(PdfStamper stamper, Rectangle pageSize, int page, CardPosition position)
  {
    Rectangle cardRectangle = this.GetCardRectangle(pageSize, position);
    try
    {
      Image instance = Image.GetInstance(Path.Combine(DataManager.Current.LocalAppDataRootDirectory, "spellcard-background.jpg"));
      instance.SetAbsolutePosition(cardRectangle.Left, cardRectangle.Top - cardRectangle.Height);
      instance.ScaleToFit(cardRectangle.Width, cardRectangle.Height);
      stamper.GetUnderContent(page).AddImage(instance);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (Stamp));
    }
    iTextSharp.text.Font font1 = FontFactory.GetFont("Helvetica", 6f);
    iTextSharp.text.Font font2 = FontFactory.GetFont("Helvetica-Bold", 6f);
    iTextSharp.text.Font font3 = FontFactory.GetFont("Helvetica-BoldOblique", 6f);
    iTextSharp.text.Font font4 = FontFactory.GetFont("Helvetica-Bold", 9f);
    iTextSharp.text.Font font5 = FontFactory.GetFont("Helvetica-Oblique", 6f);
    Phrase phrase = new Phrase();
    phrase.Font = font1;
    Chunk chunk1 = new Chunk(this._spell.Name + Environment.NewLine);
    chunk1.Font = font4;

    phrase.Add((IElement) new Chunk(this._spell.Underline + Environment.NewLine + Environment.NewLine)
    {
      Font = font5
    });
    phrase.Add((IElement) new Chunk("Casting Time: ")
    {
      Font = font2
    });
    phrase.Add((IElement) new Chunk(this._spell.CastingTime + Environment.NewLine));
    phrase.Add((IElement) new Chunk("Range: ")
    {
      Font = font2
    });
    phrase.Add((IElement) new Chunk(this._spell.Range + Environment.NewLine));
    phrase.Add((IElement) new Chunk("Components: ")
    {
      Font = font2
    });
    phrase.Add((IElement) new Chunk(this._spell.GetComponentsString() + Environment.NewLine));
    phrase.Add((IElement) new Chunk("Duration: ")
    {
      Font = font2
    });
    phrase.Add((IElement) new Chunk(this._spell.Duration + Environment.NewLine + Environment.NewLine));
    MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(this._spell.Description));
    foreach (IElement to in HtmlWorker.ParseToList((TextReader) new StringReader(this._spell.Description), (StyleSheet) null))
    {
      this._spell.Name.Contains("Enhance Ability");
      foreach (Chunk chunk2 in (IEnumerable<Chunk>) to.Chunks)
      {
        chunk2.Font = font1;
        if (chunk2.Content.ToLower().Contains("at higher level"))
          chunk2.Font = font3;
        phrase.Add((IElement) chunk2);
        if (chunk2.Content != "\n")
          phrase.Add((IElement) new Chunk(Environment.NewLine));
      }
      phrase.Add((IElement) new Chunk(Environment.NewLine));
    }
    ColumnText columnText = new ColumnText(stamper.GetOverContent(page));
    columnText.SetSimpleColumn(this.GetCardRectangle(pageSize, position, 2));
    columnText.AddElement(new Paragraph(10f, chunk1));
    columnText.AddElement(new Paragraph(phrase) { Leading = 8f });
    columnText.Go();
  }

  private Rectangle GetCardRectangle(Rectangle pageSize, CardPosition position, int padding = 0)
  {
    int num1 = (int) (((double) pageSize.Width - 80.0) / 3.0);
    int num2 = (int) (((double) pageSize.Height - 80.0) / 3.0);
    int num3 = 20;
    int num4 = 20;
    switch (position)
    {
      case CardPosition.UpperLeft:
        num4 = num4 + (20 + num2) + (20 + num2);
        break;
      case CardPosition.UpperCenter:
        num3 += 20 + num1;
        num4 = num4 + (20 + num2) + (20 + num2);
        break;
      case CardPosition.UpperRight:
        num3 = num3 + (20 + num1) + (20 + num1);
        num4 = num4 + (20 + num2) + (20 + num2);
        break;
      case CardPosition.CenterLeft:
        num4 += 20 + num2;
        break;
      case CardPosition.CenterCenter:
        num3 += 20 + num1;
        num4 += 20 + num2;
        break;
      case CardPosition.CenterRight:
        num3 = num3 + (20 + num1) + (20 + num1);
        num4 += 20 + num2;
        break;
      case CardPosition.BottomCenter:
        num3 += 20 + num1;
        break;
      case CardPosition.BottomRight:
        num3 = num3 + (20 + num1) + (20 + num1);
        break;
    }
    return new Rectangle((float) (num3 + padding), (float) (num4 + padding), (float) (num3 + num1 - padding * 2), (float) (num4 + num2 - padding * 2));
  }
}
