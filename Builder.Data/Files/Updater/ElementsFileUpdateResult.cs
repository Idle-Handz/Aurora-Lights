using System;

namespace Builder.Data.Files.Updater;

public class ElementsFileUpdateResult : FileUpdateResult<ElementsFile>
{
	public Version RequiredAppVersion { get; set; }

	public ElementsFileUpdateResult(bool success, ElementsFile file)
		: base(success, file)
	{
	}
}
