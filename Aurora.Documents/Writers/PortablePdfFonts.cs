using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Aurora.Documents.Writers;

/// <summary>Embedded PDF fonts, independent of the host's installed fonts.</summary>
public static class PortablePdfFonts
{
    private static readonly Lazy<BaseFont>[] Fonts =
    {
        Load("Regular"), Load("Bold"), Load("Italic"), Load("BoldItalic")
    };

    private static Lazy<BaseFont> Load(string style) => new(() =>
    {
        string filename = $"OpenSans-{style}.ttf";
        using Stream source = typeof(PortablePdfFonts).Assembly.GetManifestResourceStream(
            "Aurora.Documents.Resources.Fonts." + filename)
            ?? throw new InvalidOperationException("A bundled PDF font is missing: " + filename);
        using var bytes = new MemoryStream();
        source.CopyTo(bytes);
        return BaseFont.CreateFont(filename, BaseFont.IDENTITY_H, true, true, bytes.ToArray(), null);
    });

    public static BaseFont GetBaseFont(int style = Font.NORMAL) => Fonts[style & Font.BOLDITALIC].Value;

    public static Font GetFont(float size = 12f, int style = Font.NORMAL) =>
        new(GetBaseFont(style), size, Font.NORMAL);
}
