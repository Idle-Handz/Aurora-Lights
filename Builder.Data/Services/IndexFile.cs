using System.Collections.Generic;

namespace Builder.Data.Services;

public class IndexFile : ContentFile
{
	public List<ContentFileReference> FileReferences { get; }

	public IndexFile()
	{
		FileReferences = new List<ContentFileReference>();
	}
}
