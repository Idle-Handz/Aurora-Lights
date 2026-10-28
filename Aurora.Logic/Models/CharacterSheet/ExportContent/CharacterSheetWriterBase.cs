using Aurora.Documents.Helpers;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.ExportContent.CharacterSheetWriterBase
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Aurora.Documents.Sheets;
using Builder.Presentation.Models.CharacterSheet.PDF;
using Builder.Presentation.Utilities;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Models.CharacterSheet.ExportContent;

public class CharacterSheetWriterBase
{
  protected PdfStamper Stamper { get; }

  public CharacterSheetConfiguration Configuration { get; }

  public CharacterSheetWriterBase(PdfStamper stamper, CharacterSheetConfiguration configuration)
  {
    this.Stamper = stamper;
    this.Configuration = configuration;
  }

  protected bool ReplaceImageField(string name, string imagePath)
  {
    IList<PdfLayoutExtensions.FieldPosition> fieldPositions = this.Stamper.AcroFields.GetWidgetPositions(name);
    PdfLayoutExtensions.FieldPosition fieldPosition = fieldPositions != null ? fieldPositions.FirstOrDefault<PdfLayoutExtensions.FieldPosition>() : (PdfLayoutExtensions.FieldPosition) null;
    if (fieldPosition == null || !File.Exists(imagePath))
      return false;
    Image instance = Image.GetInstance(imagePath);
    PushbuttonField pushbuttonField = new PushbuttonField(this.Stamper.Writer, fieldPosition.position, name + ":replaced");
    pushbuttonField.Layout = 2;
    pushbuttonField.Image = instance;
    pushbuttonField.ProportionalIcon = true;
    pushbuttonField.Options = 1;
    this.Stamper.AddAnnotation((PdfAnnotation) pushbuttonField.Field, fieldPosition.page);
    this.Stamper.AcroFields.RemoveField(name);
    return true;
  }

  protected void ReplaceField(string name, string htmlContent, float fontsize = 7f, bool dynamic = true)
  {
    if (!this.Stamper.AcroFields.Fields.ContainsKey(name))
      return;
    Rectangle position1 = this.Stamper.AcroFields.GetWidgetPositions(name)[0].position;
    Rectangle position2 = this.Stamper.AcroFields.GetWidgetPositions(name)[0].position;
    int page = this.Stamper.AcroFields.GetWidgetPositions(name)[0].page;
    this.Stamper.AcroFields.RemoveField(name);
    iTextSharp.text.Font regular = FontsHelper.GetRegular();
    float fontsize1 = PdfSignatureAppearance.FitText(regular, $"{htmlContent}{Environment.NewLine}<p></p>", position2, fontsize, 1);
    if ((double) fontsize1 < (double) fontsize)
    {
      string str = "";
      if (htmlContent.Length > 2500)
        str = $"{str}<p>&nbsp;</p><p>&nbsp;</p><p>&nbsp;</p><p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 3200)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 4000)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 4800)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 5600)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 6200)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 7000)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      fontsize1 = PdfSignatureAppearance.FitText(regular, htmlContent + str + Environment.NewLine, position2, fontsize, 1);
    }
    PdfContentByte overContent = this.Stamper.GetOverContent(page);
    position1.BackgroundColor = BaseColor.White;
    Rectangle rectangle = position1;
    overContent.Rectangle(rectangle);
    ColumnText column = new ColumnText(this.Stamper.GetOverContent(page));
    column.SetSimpleColumn(position2);
    ElementDescriptionGenerator.FillSheetColumn(column, htmlContent, fontsize1, dynamic);
    column.Go();
  }
}
