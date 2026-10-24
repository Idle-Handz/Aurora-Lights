using Aurora.Documents.Helpers;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Sheet.Itemcard
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Shell.Items;
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

public class Itemcard
{
  private readonly string _fieldname;
  private readonly RefactoredEquipmentItem _item;

  public Itemcard(string fieldname, RefactoredEquipmentItem item)
  {
    this._fieldname = fieldname;
    this._item = item;
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
    FontFactory.GetFont("Helvetica-Bold", 6f);
    FontFactory.GetFont("Helvetica-BoldOblique", 6f);
    iTextSharp.text.Font font2 = FontFactory.GetFont("Helvetica-Bold", 9f);
    iTextSharp.text.Font font3 = FontFactory.GetFont("Helvetica-Oblique", 6f);
    Phrase phrase = new Phrase();
    phrase.Font = font1;
    Chunk chunk1 = new Chunk(string.IsNullOrWhiteSpace(this._item.AlternativeName) ? this._item.DisplayName + Environment.NewLine : this._item.AlternativeName + Environment.NewLine);
    chunk1.Font = font2;

    if (this._item.IsAdorned || this._item.Item.Type.Equals("Magic Item"))
    {
      StringBuilder stringBuilder = new StringBuilder();
      Builder.Data.Elements.Item obj = this._item.IsAdorned ? this._item.AdornerItem : this._item.Item;
      if (!string.IsNullOrWhiteSpace(obj.ItemType))
      {
        string additionAttribute = obj.GetSetterAdditionAttribute("type");
        stringBuilder.Append(additionAttribute != null ? $"{obj.ItemType} ({additionAttribute}), " : obj.ItemType + ", ");
      }
      else
        stringBuilder.Append("Magic item, ");
      if (!string.IsNullOrWhiteSpace(obj.Rarity))
        stringBuilder.Append(obj.Rarity.ToLower() + " ");
      if (obj.RequiresAttunement)
      {
        string additionAttribute = obj.GetSetterAdditionAttribute("attunement");
        stringBuilder.Append(additionAttribute != null ? $"(requires attunement {additionAttribute})" : "(requires attunement)");
      }
      phrase.Add((IElement) new Chunk(stringBuilder.ToString() + Environment.NewLine)
      {
        Font = font3
      });
      phrase.Add((IElement) new Chunk(Environment.NewLine));
    }
    else if (this._item.IsAdorned || !this._item.Item.Type.Equals("Weapon") && !this._item.Item.Type.Equals("Armor"))
    {
      phrase.Add((IElement) new Chunk(this._item.Item.Category + Environment.NewLine)
      {
        Font = font3
      });
      phrase.Add((IElement) new Chunk(Environment.NewLine));
    }
    List<IElement> toList = HtmlWorker.ParseToList((TextReader) new StringReader(this._item.IsAdorned ? this._item.AdornerItem.Description : this._item.Item.Description), (StyleSheet) null);
    if (!this._item.IsAdorned && (this._item.Item.Type.Equals("Weapon") || this._item.Item.Type.Equals("Armor")))
      toList = HtmlWorker.ParseToList((TextReader) new StringReader(DescriptionPanelViewModelBase.GenerateHeaderForCard((ElementBase) this._item.Item)), (StyleSheet) null);
    foreach (IElement element in toList)
    {
      StringBuilder stringBuilder = new StringBuilder();
      foreach (Chunk chunk2 in (IEnumerable<Chunk>) element.Chunks)
      {
        if (element.GetType() == typeof (List))
        {
          stringBuilder.Append((object) (element as List).Symbol);
          stringBuilder.AppendLine(chunk2.Content);
        }
        else
          stringBuilder.Append(chunk2.Content);
      }
      Chunk chunk3 = new Chunk(stringBuilder.ToString() + Environment.NewLine)
      {
        Font = font1
      };
      phrase.Add((IElement) chunk3);
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
