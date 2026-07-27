using Aurora.Documents.Helpers;
using Aurora.Documents.Resources;

namespace Aurora.Documents.Writers.Base;

public abstract class DocumentWriterBase
{
	protected DocumentResources Resources { get; }

	protected FontsHelper Fonts { get; }

	protected DescriptionConverter DescriptionConverter { get; }

	protected DocumentWriterBase()
	{
		Resources = new DocumentResources();
		Fonts = new FontsHelper();
		DescriptionConverter = new DescriptionConverter(Fonts);
	}
}
