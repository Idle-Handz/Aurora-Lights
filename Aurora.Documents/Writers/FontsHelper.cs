using System;
using System.IO;
using iTextSharp.text;

namespace Aurora.Documents.Writers;

public class FontsHelper
{
	private LocalFontBase _font;

	public FontsHelper()
		: this(new Calibri())
	{
	}

	public FontsHelper(LocalFontBase font)
	{
		_font = font;
	}

	public FontsHelper SetFont(LocalFontBase font)
	{
		_font = font;
		return this;
	}

	public Font GetRegular(float size = 12f)
	{
		return GetFont(_font.Regular.Name, _font.Regular.Filename, size) ?? FontFactory.GetFont("Helvetica", size);
	}

	public Font GetBold(float size = 12f)
	{
		return GetFont(_font.Bold.Name, _font.Bold.Filename, size) ?? FontFactory.GetFont("Helvetica-Bold", size);
	}

	public Font GetItalic(float size = 12f)
	{
		return GetFont(_font.Italic.Name, _font.Italic.Filename, size) ?? FontFactory.GetFont("Helvetica-Oblique", size);
	}

	public Font GetBoldItalic(float size = 12f)
	{
		return GetFont(_font.BoldItalic.Name, _font.BoldItalic.Filename, size) ?? FontFactory.GetFont("Helvetica-BoldOblique", size);
	}

	private static Font GetFont(string fontName, string filename, float size = 0f)
	{
		if (FontFactory.IsRegistered(filename))
		{
			return FontFactory.GetFont(fontName, "Identity-H", embedded: true, size);
		}
		FontFactory.Register(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), filename));
		return FontFactory.GetFont(fontName, "Identity-H", embedded: true, size);
	}
}
