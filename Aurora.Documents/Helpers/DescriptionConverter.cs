using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Aurora.Documents.Writers;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;

namespace Aurora.Documents.Helpers;

[Obsolete("copy of the one in presentation project, todo; check if it works okay")]
public class DescriptionConverter
{
	private readonly FontsHelper _fontsHelper;

	public DescriptionConverter(FontsHelper fontsHelper)
	{
		_fontsHelper = fontsHelper;
	}

	public string GeneratePlainDescription(string description)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (IElement item in HTMLWorker.ParseToList(new StringReader(description), null))
		{
			StringBuilder stringBuilder2 = new StringBuilder();
			foreach (Chunk chunk in item.Chunks)
			{
				if (item is List)
				{
					Chunk symbol = (item as List).Symbol;
					stringBuilder2.AppendLine($"{symbol} {chunk.Content}");
				}
				else
				{
					stringBuilder2.Append(chunk.Content);
				}
			}
			stringBuilder.AppendLine(stringBuilder2.ToString());
			if (item.Chunks.Count > 0)
			{
				stringBuilder.AppendLine();
			}
		}
		return stringBuilder.ToString();
	}

	public IEnumerable<Paragraph> GenerateColumnDescription(string description, float fontsize)
	{
		List<Paragraph> list = new List<Paragraph>();
		Font regular = _fontsHelper.GetRegular();
		_fontsHelper.GetBoldItalic();
		List<IElement> list2 = HTMLWorker.ParseToList(new StringReader(description), null);
		Paragraph paragraph = new Paragraph();
		foreach (IElement item in list2)
		{
			item.GetType();
			if (item is Paragraph)
			{
				_ = (Paragraph)item;
			}
			_ = item is List;
			foreach (Chunk chunk in item.Chunks)
			{
				chunk.Font = regular;
			}
			paragraph.Add(item);
		}
		list.Add(paragraph);
		return list;
	}

	public void FillColumn(ColumnText column, string description, float fontsize)
	{
		List<IElement> list = HTMLWorker.ParseToList(new StringReader(description), null);
		column.SetLeading(fontsize, 1f);
		float lineHeight = fontsize + 0f;
		string text = "     ";
		bool flag = false;
		foreach (IElement item in list)
		{
			Paragraph paragraph = new Paragraph(fontsize);
			foreach (Chunk chunk in item.Chunks)
			{
				if (item is List)
				{
					paragraph.IndentationLeft = 5f;
					Chunk symbol = (item as List).Symbol;
					Chunk element = new Chunk($"{text}{symbol} {chunk.Content}" + Environment.NewLine);
					paragraph.Add(element);
					continue;
				}
				if (item is Header)
				{
					if (Debugger.IsAttached)
					{
						Debugger.Break();
					}
					continue;
				}
				if (flag)
				{
					Chunk element2 = new Chunk(text)
					{
						Font =
						{
							Size = fontsize
						}
					};
					paragraph.Add(element2);
					flag = false;
				}
				else if (item.Chunks.Count > 1 && chunk == item.Chunks.First())
				{
					Chunk element3 = new Chunk(text)
					{
						Font =
						{
							Size = fontsize
						}
					};
					paragraph.Add(element3);
				}
				else if (chunk.Content.ToLower().Contains("at higher level"))
				{
					Chunk element4 = new Chunk(text)
					{
						Font =
						{
							Size = fontsize
						}
					};
					paragraph.Add(element4);
				}
				paragraph.Add(chunk);
			}
			flag = !(item is List);
			if (item != list.Last() || item is List)
			{
				paragraph.Add(Environment.NewLine);
			}
			foreach (Chunk chunk2 in paragraph.Chunks)
			{
				chunk2.setLineHeight(lineHeight);
				if (!string.IsNullOrWhiteSpace(chunk2.Content) && paragraph.Chunks.Count > 2)
				{
					if (chunk2.Content.Length > 26 || chunk2.Equals(paragraph.Chunks.Last()))
					{
						chunk2.Font = _fontsHelper.GetRegular(fontsize);
					}
					else
					{
						chunk2.Font = _fontsHelper.GetBoldItalic(fontsize);
					}
				}
				else
				{
					chunk2.Font = _fontsHelper.GetRegular(fontsize);
				}
			}
			column.AddText(paragraph);
		}
	}

	public void FillSheetColumn(ColumnText column, string description, float fontsize, bool dynamicBoldItalic = true)
	{
		List<IElement> list = HTMLWorker.ParseToList(new StringReader(description), null);
		column.SetLeading(fontsize, 1f);
		float lineHeight = fontsize + 0f;
		string arg = "     ";
		foreach (IElement item in list)
		{
			Paragraph paragraph = new Paragraph(fontsize);
			foreach (Chunk chunk in item.Chunks)
			{
				if (item is List)
				{
					Chunk symbol = (item as List).Symbol;
					Chunk element = new Chunk($"{arg}{symbol} {chunk.Content}" + Environment.NewLine);
					paragraph.Add(element);
				}
				else
				{
					paragraph.Add(chunk);
				}
			}
			if (item != list.Last() || item is List)
			{
				paragraph.Add(Environment.NewLine);
			}
			if (dynamicBoldItalic)
			{
				bool flag = false;
				foreach (Chunk chunk2 in paragraph.Chunks)
				{
					chunk2.setLineHeight(lineHeight);
					if (!flag && chunk2 == paragraph.Chunks.First() && !string.IsNullOrWhiteSpace(chunk2.Content))
					{
						chunk2.Font = _fontsHelper.GetBoldItalic(fontsize);
						flag = true;
					}
					else if (!flag && !chunk2.Equals(paragraph.Chunks.Last()) && !string.IsNullOrWhiteSpace(chunk2.Content))
					{
						chunk2.Font = _fontsHelper.GetBoldItalic(fontsize);
						flag = true;
					}
					else
					{
						chunk2.Font = _fontsHelper.GetRegular(fontsize);
					}
				}
			}
			else
			{
				foreach (Chunk chunk3 in paragraph.Chunks)
				{
					chunk3.setLineHeight(lineHeight);
					chunk3.Font = _fontsHelper.GetRegular(fontsize);
				}
			}
			column.AddText(paragraph);
		}
	}

	public void FillSheetField(ColumnText column, string description, float fontsize)
	{
		FillSheetColumn(column, description, fontsize, dynamicBoldItalic: false);
	}
}
