using System;
using System.Text;
using System.Text.RegularExpressions;
using Aurora.Documents.ExportContent.Notes;
using Aurora.Documents.Sheets;
using Aurora.Documents.Writers.Base;
using iTextSharp.text.pdf;

namespace Aurora.Documents.Writers;

public sealed class NotesPageWriter : CharacterSheetDocumentWriterBase
{
	public NotesPageWriter(CharacterSheetConfiguration configuration, PdfStamper stamper)
		: base(configuration, stamper)
	{
	}

	public void Write(NotesExportContent exportContent)
	{
		if (base.Configuration.IncludeFormatting)
		{
			ReplaceAreaField("notes_page_left", ToHtml(exportContent.LeftNotesColumn), 8.2f);
			ReplaceAreaField("notes_page_right", ToHtml(exportContent.RightNotesColumn), 8.2f);
		}
		else
		{
			Stamp("notes_page_left", exportContent.LeftNotesColumn);
			Stamp("notes_page_right", exportContent.RightNotesColumn);
		}
	}

	public string ToHtml(string input)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string[] array = Regex.Split(input, Environment.NewLine);
		foreach (string text in array)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				stringBuilder.Append("<p>&nbsp;</p>");
			}
			else
			{
				stringBuilder.Append("<p>" + text + "</p>");
			}
		}
		return stringBuilder.ToString();
	}
}
