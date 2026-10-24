using Aurora.Documents.Writers;
using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfDocument = UglyToad.PdfPig.PdfDocument;

namespace Aurora.Tests.Tests;

public sealed class PortablePdfFontsTests
{
    [Theory]
    [InlineData(Font.NORMAL, "Regular")]
    [InlineData(Font.BOLD, "Bold")]
    [InlineData(Font.ITALIC, "Italic")]
    [InlineData(Font.BOLDITALIC, "BoldItalic")]
    public void MissingDesktopFontsUseEmbeddedRealStylesAndKeepUnicode(int style, string name)
    {
        var fonts = new FontsHelper(new MissingFont());
        var font = style switch
        {
            Font.BOLD => fonts.GetBold(), Font.ITALIC => fonts.GetItalic(),
            Font.BOLDITALIC => fonts.GetBoldItalic(), _ => fonts.GetRegular()
        };
        font.BaseFont.Should().BeSameAs(PortablePdfFonts.GetBaseFont(style));
        font.BaseFont.PostscriptFontName.Should().Contain("OpenSans");
        if (style != Font.NORMAL) font.BaseFont.PostscriptFontName.Should().Contain(name);
        const string text = "Élodie Łódź, café, naïve, ±2, Ω, Ж. Final marker.";
        foreach (char c in text) font.BaseFont.CharExists(c).Should().BeTrue("the embedded font must include {0}", c);
        string? qa = Environment.GetEnvironmentVariable("AURORA_PDF_QA_DIRECTORY");
        string path = Path.Combine(qa ?? Path.GetTempPath(), "portable-font-" + name + ".pdf");
        try
        {
            using (var stream = File.Create(path))
            using (var document = new Document())
            {
                PdfWriter.GetInstance(document, stream);
                document.Open();
                document.Add(new Paragraph(text, font));
            }
            using var independent = PdfDocument.Open(path);
            independent.GetPage(1).Text.Should().Contain(text);
            using var reader = new PdfReader(path);
            reader.GetPageN(1).GetAsDict(PdfName.Resources).GetAsDict(PdfName.Font).Keys.Should().NotBeEmpty();
        }
        finally { if (qa is null) File.Delete(path); }
    }

    private sealed class MissingFont : LocalFontBase
    {
        public MissingFont() : base("Unavailable font")
        {
            Regular = Bold = Italic = BoldItalic = new LocalFont("Missing test font", "aurora-absent-font.ttf");
        }
    }
}
