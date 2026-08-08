using Aurora.Documents.ExportContent;

namespace Aurora.Documents.Sheets;

public abstract class CharacterSheetBase
{
	public CharacterSheetConfiguration Configuration { get; }

	protected CharacterSheetBase(CharacterSheetConfiguration configuration)
	{
		Configuration = configuration;
	}

	public abstract void Generate(IExportContentProvider contentProvider);
}
