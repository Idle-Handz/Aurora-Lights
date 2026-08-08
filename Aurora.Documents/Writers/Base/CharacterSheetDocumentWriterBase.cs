using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aurora.Documents.Sheets;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;

namespace Aurora.Documents.Writers.Base;

public abstract class CharacterSheetDocumentWriterBase : DocumentWriterBase
{
	public CharacterSheetConfiguration Configuration { get; protected set; }

	public PdfStamper Stamper { get; protected set; }

	protected bool FillReplaceFieldBackground { get; set; }

	protected bool SuffixWithWhitespace { get; set; }

	protected CharacterSheetDocumentWriterBase(CharacterSheetConfiguration configuration, PdfStamper stamper)
	{
		Configuration = configuration;
		Stamper = stamper;
		FillReplaceFieldBackground = false;
		SuffixWithWhitespace = false;
	}

	public void SetConfiguration(CharacterSheetConfiguration configuration)
	{
		Configuration = configuration;
	}

	public void SetStamper(PdfStamper stamper)
	{
		Stamper = stamper;
	}

	protected virtual void Stamp(string field, string content, bool setFontSize = false, float fontSize = 8f)
	{
		if (setFontSize)
		{
			SetFontSize(field, fontSize);
		}
		Stamper.AcroFields.SetField(field, content);
	}

	protected virtual void StampCollection(Dictionary<string, string> collection, bool setFontSize = false, float fontSize = 8f)
	{
		foreach (KeyValuePair<string, string> item in collection)
		{
			Stamp(item.Key, item.Value, setFontSize, fontSize);
		}
	}

	protected virtual void SetFontSize(string field, float fontSize = 8f)
	{
		Stamper.AcroFields.SetFieldProperty(field, "textsize", fontSize, null);
	}

	protected virtual void SetBoldProperty(string field)
	{
		Stamper.AcroFields.SetFieldProperty(field, "textfont", base.Fonts.GetBold().BaseFont, null);
	}

	protected virtual void ClearBackgroundColor(string field)
	{
		Stamper.AcroFields.SetFieldProperty(field, "bgcolor", null, null);
	}

	protected virtual void SetBackgroundColor(string field)
	{
		Stamper.AcroFields.SetFieldProperty(field, "bgcolor", BaseColor.WHITE, null);
	}

	protected virtual void SetBackgroundColor(string field, BaseColor color)
	{
		Stamper.AcroFields.SetFieldProperty(field, "bgcolor", color, null);
	}

	protected virtual void SetTextColor(string field)
	{
		Stamper.AcroFields.SetFieldProperty(field, "textcolor", BaseColor.BLACK, null);
	}

	protected virtual void SetTextColor(string field, BaseColor color)
	{
		Stamper.AcroFields.SetFieldProperty(field, "textcolor", color, null);
	}

	protected Rectangle GetFieldRectangle(string field)
	{
		return Stamper.AcroFields.GetFieldPositions(field)[0].position;
	}

	protected bool ReplaceImageField(string name, string imagePath)
	{
		AcroFields.FieldPosition fieldPosition = Stamper.AcroFields.GetFieldPositions(name)?.FirstOrDefault();
		if (fieldPosition == null)
		{
			return false;
		}
		if (!File.Exists(imagePath))
		{
			return false;
		}
		PushbuttonField pushbuttonField = new PushbuttonField(Stamper.Writer, fieldPosition.position, name + ":replaced")
		{
			Layout = 2,
			Image = Image.GetInstance(imagePath),
			ProportionalIcon = true,
			Options = 1
		};
		Stamper.AddAnnotation(pushbuttonField.Field, fieldPosition.page);
		Stamper.AcroFields.RemoveField(name);
		return true;
	}

	protected void ReplaceAreaField(string name, string content, float fontSize = 8f, float topOffset = 1f)
	{
		if (!Stamper.AcroFields.Fields.ContainsKey(name))
		{
			return;
		}
		Rectangle position = Stamper.AcroFields.GetFieldPositions(name)[0].position;
		Rectangle position2 = Stamper.AcroFields.GetFieldPositions(name)[0].position;
		if (!FillReplaceFieldBackground)
		{
			position2.Left += 3f;
			position2.Right -= 3f;
			position2.Top += topOffset;
		}
		int page = Stamper.AcroFields.GetFieldPositions(name)[0].page;
		Stamper.AcroFields.RemoveField(name);
		Font regular = base.Fonts.GetRegular(fontSize);
		float num = ColumnText.FitText(regular, content + Environment.NewLine + "<p></p>", position2, fontSize, 1);
		num = ColumnText.FitText(regular, content, position2, fontSize, 1);
		if (SuffixWithWhitespace && num < fontSize)
		{
			string text = "";
			if (content.Length > 2500)
			{
				text = text + "<p>&nbsp;</p><p>&nbsp;</p><p>&nbsp;</p><p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 3200)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 4000)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 4800)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 5600)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 6200)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 7000)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 8000)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 9000)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 10000)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			num = ColumnText.FitText(regular, content + text, position2, fontSize, 1);
		}
		bool flag = CanFitWithDefaultLineHeight(position2, content);
		if (FillReplaceFieldBackground || !flag)
		{
			PdfContentByte overContent = Stamper.GetOverContent(page);
			position.BackgroundColor = BaseColor.WHITE;
			overContent.Rectangle(position);
		}
		ColumnText columnText = new ColumnText(Stamper.GetOverContent(page));
		columnText.SetSimpleColumn(position2);
		FillColumn(columnText, content, num, flag ? 10f : num);
		columnText.Go();
	}

	private void FillColumn(ColumnText column, string description, float fontSize, float lineHeight = 10f)
	{
		List<IElement> list = HTMLWorker.ParseToList(new StringReader(description), null);
		foreach (IElement item in list)
		{
			Paragraph paragraph = new Paragraph(fontSize);
			if (item.Chunks.Count == 1 && string.IsNullOrWhiteSpace(item.Chunks[0].Content))
			{
				continue;
			}
			foreach (Chunk chunk in item.Chunks)
			{
				if (item is List { Symbol: var symbol })
				{
					Chunk element = new Chunk(string.Format("{0}{1} {2}", "     ", symbol, chunk.Content) + Environment.NewLine);
					paragraph.Add(element);
				}
				else
				{
					paragraph.Add(chunk);
				}
			}
			if (!object.Equals(item, list.Last()) || item is List)
			{
				paragraph.Add(Environment.NewLine);
			}
			StyleElement(paragraph, fontSize, lineHeight);
			column.AddText(paragraph);
		}
	}

	private void StyleElement(IElement element, float fontSize, float lineHeight = 10f)
	{
		foreach (Chunk chunk in element.Chunks)
		{
			chunk.setLineHeight(lineHeight);
			switch (chunk.Font.Style)
			{
			case 0:
				chunk.Font = base.Fonts.GetRegular(fontSize);
				break;
			case 1:
				chunk.Font = base.Fonts.GetBold(fontSize);
				break;
			case 2:
				chunk.Font = base.Fonts.GetItalic(fontSize);
				break;
			case 3:
				chunk.Font = base.Fonts.GetBoldItalic(fontSize);
				break;
			default:
				chunk.Font = base.Fonts.GetRegular(fontSize);
				_ = Debugger.IsAttached;
				break;
			}
		}
	}

	private float GetRequiredFontSize(Rectangle rectangle, string content, float maximumFontSize)
	{
		return ColumnText.FitText(base.Fonts.GetRegular(), content, rectangle, maximumFontSize, 1);
	}

	public bool CanFitWithDefaultLineHeight(Rectangle position, string content, float fontSize = 10f)
	{
		Font regular = base.Fonts.GetRegular(fontSize);
		float num = ColumnText.FitText(regular, content + Environment.NewLine + "<p></p>", position, fontSize, 1);
		num = ColumnText.FitText(regular, content, position, fontSize, 1);
		if (SuffixWithWhitespace && num < fontSize)
		{
			string text = "";
			if (content.Length > 2500)
			{
				text = text + "<p>&nbsp;</p><p>&nbsp;</p><p>&nbsp;</p><p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 3200)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 4000)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 4800)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 5600)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 6200)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			if (content.Length > 7000)
			{
				text = text + "<p>&nbsp;</p>" + Environment.NewLine;
			}
			num = ColumnText.FitText(regular, content + text, position, fontSize, 1);
		}
		if (num < fontSize)
		{
			return false;
		}
		return true;
	}
}
