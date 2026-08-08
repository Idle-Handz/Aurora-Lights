using System;

namespace Builder.Data.Files.Updater;

public class IndexUpdateResult : FileUpdateResult<IndexFile>
{
	public Version RequiredAppVersion { get; set; }

	public IndexUpdateResult(bool success, IndexFile file)
		: base(success, file)
	{
	}
}
