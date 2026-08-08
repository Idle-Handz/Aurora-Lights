namespace Aurora.Documents.Writers;

public abstract class LocalFontBase
{
	public struct LocalFont
	{
		public string Name { get; set; }

		public string Filename { get; set; }

		public LocalFont(string name, string filename)
		{
			Name = name;
			Filename = filename;
		}
	}

	public string DisplayName { get; protected set; }

	public float DefaultSize { get; protected set; }

	public LocalFont Regular { get; protected set; }

	public LocalFont Bold { get; protected set; }

	public LocalFont Italic { get; protected set; }

	public LocalFont BoldItalic { get; protected set; }

	protected LocalFontBase(string displayName, float defaultSize = 12f)
	{
		DisplayName = displayName;
		DefaultSize = defaultSize;
		Regular = new LocalFont("Calibri", "calibri.ttf");
		Bold = new LocalFont("Calibri Bold", "calibrib.ttf");
		Italic = new LocalFont("Calibri Italic", "calibrii.ttf");
		BoldItalic = new LocalFont("Calibri Bold Italic", "calibriz.ttf");
	}
}
