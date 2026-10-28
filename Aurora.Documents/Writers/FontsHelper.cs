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
		return GetFont(_font.Regular.Name, _font.Regular.Filename, size) ?? PortablePdfFonts.GetFont(size);
	}

	public Font GetBold(float size = 12f)
	{
		return GetFont(_font.Bold.Name, _font.Bold.Filename, size) ?? PortablePdfFonts.GetFont(size, Font.BOLD);
	}

	public Font GetItalic(float size = 12f)
	{
		return GetFont(_font.Italic.Name, _font.Italic.Filename, size) ?? PortablePdfFonts.GetFont(size, Font.ITALIC);
	}

	public Font GetBoldItalic(float size = 12f)
	{
		return GetFont(_font.BoldItalic.Name, _font.BoldItalic.Filename, size) ?? PortablePdfFonts.GetFont(size, Font.BOLDITALIC);
	}

	private static Font GetFont(string fontName, string filename, float size = 0f)
	{
		// Android has no desktop Calibri installation. Never depend on a font registry
		// populated by another export or fail before reaching the embedded fallback.
		if (OperatingSystem.IsAndroid()) return null;
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), filename);
			if (!File.Exists(path)) return null;
			if (!FontFactory.IsRegistered(fontName)) FontFactory.Register(path);
			var font = FontFactory.GetFont(fontName, "Identity-H", embedded: true, size);
			return font?.BaseFont is not null ? font : null;
		}
		catch (Exception) { return null; }
	}
}
