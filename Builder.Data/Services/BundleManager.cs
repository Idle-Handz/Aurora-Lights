using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace Builder.Data.Services;

public sealed class BundleManager
{
	private readonly string _directoryRoot;

	public BundleManager(string directoryRoot)
	{
		_directoryRoot = directoryRoot;
	}

	public IEnumerable<string> GetIndexFiles()
	{
		return Directory.GetFiles(_directoryRoot, "*.index", SearchOption.AllDirectories);
	}

	public async Task<XmlDocument> FromUrl(string url)
	{
		using HttpClient client = new HttpClient();
		string xml = await client.GetStringAsync(url);
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.LoadXml(xml);
		return xmlDocument;
	}
}
