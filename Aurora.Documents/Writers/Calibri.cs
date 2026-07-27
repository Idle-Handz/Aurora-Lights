namespace Aurora.Documents.Writers;

public class Calibri : LocalFontBase
{
	public Calibri()
		: base("Calibri")
	{
		base.Regular = new LocalFont("Calibri", "calibri.ttf");
		base.Bold = new LocalFont("Calibri Bold", "calibrib.ttf");
		base.Italic = new LocalFont("Calibri Italic", "calibrii.ttf");
		base.BoldItalic = new LocalFont("Calibri Bold Italic", "calibriz.ttf");
	}
}
