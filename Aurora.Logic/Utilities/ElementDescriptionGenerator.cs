// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Utilities.ElementDescriptionGenerator
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models.CharacterSheet.PDF;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

#nullable disable
namespace Builder.Presentation.Utilities;

public class ElementDescriptionGenerator
{
  public static string GeneratePlainDescription(string description)
  {
    StringBuilder stringBuilder1 = new StringBuilder();
    foreach (IElement to in HTMLWorker.ParseToList((TextReader) new StringReader(description), (StyleSheet) null))
    {
      StringBuilder stringBuilder2 = new StringBuilder();
      foreach (Chunk chunk in (IEnumerable<Chunk>) to.Chunks)
      {
        if (to is List)
        {
          Chunk symbol = (to as List).Symbol;
          stringBuilder2.AppendLine($"{symbol} {chunk.Content}");
        }
        else
          stringBuilder2.Append(chunk.Content);
      }
      stringBuilder1.AppendLine(stringBuilder2.ToString());
      stringBuilder1.AppendLine();
    }
    return stringBuilder1.ToString();
  }

  public static IEnumerable<Paragraph> GenerateColumnDescription(string description, float fontsize)
  {
    List<Paragraph> columnDescription = new List<Paragraph>();
    iTextSharp.text.Font regular = FontsHelper.GetRegular();
    FontsHelper.GetBoldItalic();
    List<IElement> toList = HTMLWorker.ParseToList((TextReader) new StringReader(description), (StyleSheet) null);
    Paragraph paragraph = new Paragraph();
    foreach (IElement element in toList)
    {
      element.GetType();
      List list = element as List;
      foreach (Chunk chunk in (IEnumerable<Chunk>) element.Chunks)
        chunk.Font = regular;
      paragraph.Add(element);
    }
    columnDescription.Add(paragraph);
    return (IEnumerable<Paragraph>) columnDescription;
  }

  public static void FillColumn(ColumnText column, string description, float fontsize)
  {
    List<IElement> toList = HTMLWorker.ParseToList((TextReader) new StringReader(description), (StyleSheet) null);
    column.SetLeading(fontsize, 1f);
    float lineheight = fontsize + 0.0f;
    string content = "     ";
    bool flag = false;
    foreach (IElement element in toList)
    {
      Paragraph paragraph = new Paragraph(fontsize);
      foreach (Chunk chunk1 in (IEnumerable<Chunk>) element.Chunks)
      {
        if (element is List)
        {
          paragraph.IndentationLeft = 5f;
          Chunk symbol = (element as List).Symbol;
          Chunk chunk2 = new Chunk($"{content}{symbol} {chunk1.Content}" + Environment.NewLine);
          paragraph.Add((IElement) chunk2);
        }
        else if (element is Header)
        {
          if (Debugger.IsAttached)
            Debugger.Break();
        }
        else
        {
          if (flag)
          {
            Chunk chunk3 = new Chunk(content)
            {
              Font = {
                Size = fontsize
              }
            };
            paragraph.Add((IElement) chunk3);
            flag = false;
          }
          else if (element.Chunks.Count > 1 && chunk1 == element.Chunks.First<Chunk>())
          {
            Chunk chunk4 = new Chunk(content)
            {
              Font = {
                Size = fontsize
              }
            };
            paragraph.Add((IElement) chunk4);
          }
          else if (chunk1.Content.ToLower().Contains("at higher level"))
          {
            Chunk chunk5 = new Chunk(content)
            {
              Font = {
                Size = fontsize
              }
            };
            paragraph.Add((IElement) chunk5);
          }
          paragraph.Add((IElement) chunk1);
        }
      }
      flag = !(element is List);
      if (element != toList.Last<IElement>() || element is List)
        paragraph.Add(Environment.NewLine);
      foreach (Chunk chunk in (IEnumerable<Chunk>) paragraph.Chunks)
      {
        chunk.setLineHeight(lineheight);
        chunk.Font = string.IsNullOrWhiteSpace(chunk.Content) || paragraph.Chunks.Count <= 2 ? FontsHelper.GetRegular(fontsize) : (chunk.Content.Length > 26 || chunk.Equals((object) paragraph.Chunks.Last<Chunk>()) ? FontsHelper.GetRegular(fontsize) : FontsHelper.GetBoldItalic(fontsize));
      }
      column.AddText((Phrase) paragraph);
    }
  }

  public static void FillSheetColumn(
    ColumnText column,
    string description,
    float fontsize,
    bool dynamicBoldItalic = true)
  {
    List<IElement> toList = HTMLWorker.ParseToList((TextReader) new StringReader(description), (StyleSheet) null);
    column.SetLeading(fontsize, 1f);
    float lineheight = fontsize + 0.0f;
    string str = "     ";
    foreach (IElement element in toList)
    {
      Paragraph paragraph = new Paragraph(fontsize);
      foreach (Chunk chunk1 in (IEnumerable<Chunk>) element.Chunks)
      {
        if (element is List)
        {
          Chunk symbol = (element as List).Symbol;
          Chunk chunk2 = new Chunk($"{str}{symbol} {chunk1.Content}" + Environment.NewLine);
          paragraph.Add((IElement) chunk2);
        }
        else
          paragraph.Add((IElement) chunk1);
      }
      if (element != toList.Last<IElement>() || element is List)
        paragraph.Add(Environment.NewLine);
      if (dynamicBoldItalic)
      {
        bool flag = false;
        foreach (Chunk chunk in (IEnumerable<Chunk>) paragraph.Chunks)
        {
          chunk.setLineHeight(lineheight);
          if (!flag && chunk == paragraph.Chunks.First<Chunk>() && !string.IsNullOrWhiteSpace(chunk.Content))
          {
            chunk.Font = FontsHelper.GetBoldItalic(fontsize);
            flag = true;
          }
          else if (!flag && !chunk.Equals((object) paragraph.Chunks.Last<Chunk>()) && !string.IsNullOrWhiteSpace(chunk.Content))
          {
            chunk.Font = FontsHelper.GetBoldItalic(fontsize);
            flag = true;
          }
          else
            chunk.Font = FontsHelper.GetRegular(fontsize);
        }
      }
      else
      {
        foreach (Chunk chunk in (IEnumerable<Chunk>) paragraph.Chunks)
        {
          chunk.setLineHeight(lineheight);
          chunk.Font = FontsHelper.GetRegular(fontsize);
        }
      }
      column.AddText((Phrase) paragraph);
    }
  }

  public static void FillSheetField(ColumnText column, string description, float fontsize)
  {
    ElementDescriptionGenerator.FillSheetColumn(column, description, fontsize, false);
  }
}
