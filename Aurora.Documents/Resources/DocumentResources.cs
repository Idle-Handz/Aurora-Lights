using System.IO;
using System.Reflection;

namespace Aurora.Documents.Resources;

public class DocumentResources
{
	public Stream GetResource(string resource)
	{
		return Assembly.GetAssembly(typeof(DocumentResources)).GetManifestResourceStream(resource);
	}
}
