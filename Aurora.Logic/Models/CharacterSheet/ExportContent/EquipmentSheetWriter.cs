using Aurora.Documents.Helpers;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.ExportContent.EquipmentSheetWriter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Aurora.Documents.ExportContent.Equipment;
using Aurora.Documents.Sheets;
using Builder.Core.Logging;
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

public class EquipmentSheetWriter
{
  private readonly CharacterSheetConfiguration _configuration;

  public EquipmentSheetWriter(CharacterSheetConfiguration configuration)
  {
    this._configuration = configuration;
  }

  public void Write(PdfStamper stamper, EquipmentExportContent exportContent)
  {
    int num1 = 0;
    foreach (InventoryItemExportContent itemExportContent in exportContent.AdventuringGear)
    {
      if (num1 != 40)
      {
        stamper.AcroFields.SetFieldValue($"equipment_page_gear_name.{num1}", itemExportContent.Name);
        stamper.AcroFields.SetFieldValue($"equipment_page_gear_count.{num1}", itemExportContent.Amount);
        stamper.AcroFields.SetFieldValue($"equipment_page_gear_weight.{num1}", itemExportContent.Weight);
        ++num1;
      }
      else
        break;
    }
    int num2 = 0;
    foreach (InventoryItemExportContent magicItem in exportContent.MagicItems)
    {
      if (num2 != 20)
      {
        stamper.AcroFields.SetFieldValue($"equipment_page_magic_gear_name.{num2}", magicItem.Name);
        stamper.AcroFields.SetFieldValue($"equipment_page_magic_gear_count.{num2}", magicItem.Amount);
        stamper.AcroFields.SetFieldValue($"equipment_page_magic_gear_weight.{num2}", magicItem.Weight);
        ++num2;
      }
      else
        break;
    }
    int num3 = 0;
    foreach (InventoryItemExportContent valuable in exportContent.Valuables)
    {
      if (num3 != 10)
      {
        stamper.AcroFields.SetFieldValue($"equipment_page_valuable_name.{num3}", valuable.Name);
        stamper.AcroFields.SetFieldValue($"equipment_page_valuable_count.{num3}", valuable.Amount);
        stamper.AcroFields.SetFieldValue($"equipment_page_valuable_weight.{num3}", valuable.Weight);
        ++num3;
      }
      else
        break;
    }
    int num4 = 0;
    foreach (StoredItemsExportContent storageLocation in exportContent.StorageLocations)
    {
      if (num4 != 2)
      {
        stamper.AcroFields.SetFieldValue($"equipment_page_vehicle_{num4 + 1}_name", storageLocation.Name);
        int num5 = 0;
        foreach (InventoryItemExportContent itemExportContent in storageLocation.Items)
        {
          if (num5 != 10)
          {
            stamper.AcroFields.SetFieldValue($"equipment_page_vehicle_{num4 + 1}_name.{num5}", itemExportContent.Name);
            stamper.AcroFields.SetFieldValue($"equipment_page_vehicle_{num4 + 1}_count.{num5}", itemExportContent.Amount);
            stamper.AcroFields.SetFieldValue($"equipment_page_vehicle_{num4 + 1}_weight.{num5}", itemExportContent.Weight);
            ++num5;
          }
          else
            break;
        }
        ++num4;
      }
      else
        break;
    }
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "equipment_page_coins_cp",
        exportContent.Coinage.Copper
      },
      {
        "equipment_page_coins_sp",
        exportContent.Coinage.Silver
      },
      {
        "equipment_page_coins_ep",
        exportContent.Coinage.Electrum
      },
      {
        "equipment_page_coins_gp",
        exportContent.Coinage.Gold
      },
      {
        "equipment_page_coins_pp",
        exportContent.Coinage.Platinum
      },
      {
        "equipment_page_weight_capacity",
        exportContent.CarryingCapacity
      },
      {
        "equipment_page_weight_drag",
        exportContent.DragCapacity
      },
      {
        "equipment_page_weight_carried",
        exportContent.WeightCarried
      },
      {
        "equipment_page_additional_treasure",
        exportContent.AdditionalTreasure
      },
      {
        "equipment_page_quest_items",
        exportContent.QuestItems
      }
    })
      stamper.AcroFields.SetFieldValue(keyValuePair.Key, keyValuePair.Value);
    if (this._configuration.IncludeFormatting)
      this.ReplaceField(stamper, "equipment_page_magic_items", exportContent.AttunedMagicItems, 8.2f);
    else
      stamper.AcroFields.SetFieldValue("equipment_page_magic_items", exportContent.AttunedMagicItems);
  }

  private void WriteImage(PdfStamper stamper, string fieldName, string imagePath)
  {
    try
    {
      IList<PdfLayoutExtensions.FieldPosition> fieldPositions = stamper.AcroFields.GetWidgetPositions(fieldName);
      PdfLayoutExtensions.FieldPosition fieldPosition = fieldPositions != null ? fieldPositions.FirstOrDefault<PdfLayoutExtensions.FieldPosition>() : (PdfLayoutExtensions.FieldPosition) null;
      if (fieldPosition == null || !File.Exists(imagePath))
        return;
      PushbuttonField pushbuttonField1 = new PushbuttonField(stamper.Writer, fieldPosition.position, fieldName + ":replaced");
      pushbuttonField1.Layout = 2;
      pushbuttonField1.Image = Image.GetInstance(imagePath);
      pushbuttonField1.ProportionalIcon = true;
      pushbuttonField1.Options = 1;
      PushbuttonField pushbuttonField2 = pushbuttonField1;
      stamper.AddAnnotation((PdfAnnotation) pushbuttonField2.Field, fieldPosition.page);
      stamper.AcroFields.RemoveField(fieldName);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (WriteImage));
    }
  }

  private void ReplaceField(
    PdfStamper stamper,
    string name,
    string htmlContent,
    float fontsize = 7f,
    bool dynamic = true)
  {
    if (!stamper.AcroFields.Fields.ContainsKey(name))
      return;
    Rectangle position1 = stamper.AcroFields.GetWidgetPositions(name)[0].position;
    Rectangle position2 = stamper.AcroFields.GetWidgetPositions(name)[0].position;
    int page = stamper.AcroFields.GetWidgetPositions(name)[0].page;
    stamper.AcroFields.RemoveField(name);
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
    PdfContentByte overContent = stamper.GetOverContent(page);
    position1.BackgroundColor = BaseColor.White;
    Rectangle rectangle = position1;
    overContent.Rectangle(rectangle);
    ColumnText column = new ColumnText(stamper.GetOverContent(page));
    column.SetSimpleColumn(position2);
    ElementDescriptionGenerator.FillSheetColumn(column, htmlContent, fontsize1, dynamic);
    column.Go();
  }
}
