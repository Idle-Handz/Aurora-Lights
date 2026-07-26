using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Builder.Core;
using Builder.Core.Logging;
using Builder.Data.Extensions;

namespace Builder.Data.Files;

public class IndexFile
{
	public class FileEntry : ObservableObject
	{
		private string _name;

		private string _url;

		private bool _isObsolete;

		private bool _isIndex;

		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				SetProperty(ref _name, value, "Name");
			}
		}

		public string Url
		{
			get
			{
				return _url;
			}
			set
			{
				SetProperty(ref _url, value, "Url");
			}
		}

		public bool IsObsolete
		{
			get
			{
				return _isObsolete;
			}
			set
			{
				SetProperty(ref _isObsolete, value, "IsObsolete");
			}
		}

		public bool IsIndex
		{
			get
			{
				return _isIndex;
			}
			set
			{
				SetProperty(ref _isIndex, value, "IsIndex");
			}
		}

		public FileEntry(string name, string url, bool isObsolete = false)
		{
			Name = name;
			Url = url;
			IsObsolete = isObsolete;
		}

		public override string ToString()
		{
			return Name + " [" + Url + "]";
		}
	}

	public string Content { get; private set; }

	public FileInfo FileInfo { get; private set; }

	public Version MinimumAppVersion { get; set; }

	public InformationSection Info { get; } = new InformationSection();

	public ObservableCollection<FileEntry> Files { get; } = new ObservableCollection<FileEntry>();

	private IndexFile(string content)
	{
		Content = content;
	}

	public IndexFile(FileInfo fileInfo)
	{
		FileInfo = fileInfo;
	}

	public void Save()
	{
		Save(FileInfo);
	}

	public void Save(FileInfo file)
	{
		FileInfo = file;
		XmlDocument xmlDocument = new XmlDocument();
		XmlNode xmlNode = xmlDocument.AppendChild(xmlDocument.CreateElement("index"));
		if (MinimumAppVersion != null)
		{
			xmlNode.AppendAttribute("app", MinimumAppVersion.ToString());
		}
		XmlNode xmlNode2 = xmlNode.AppendChild(xmlDocument.CreateElement("info"));
		if (!string.IsNullOrWhiteSpace(Info.DisplayName))
		{
			xmlNode2.AppendChild(xmlDocument.CreateElement("name")).InnerText = Info.DisplayName;
		}
		else
		{
			xmlNode2.AppendChild(xmlDocument.CreateElement("name")).InnerText = file.Name;
		}
		if (!string.IsNullOrWhiteSpace(Info.Description))
		{
			xmlNode2.AppendChild(xmlDocument.CreateElement("description")).InnerText = Info.Description;
		}
		if (!string.IsNullOrWhiteSpace(Info.Author))
		{
			XmlNode xmlNode3 = xmlNode2.AppendChild(xmlDocument.CreateElement("author"));
			xmlNode3.InnerText = Info.Author;
			if (!string.IsNullOrWhiteSpace(Info.AuthorUrl))
			{
				xmlNode3.AppendAttribute("url", Info.AuthorUrl);
			}
		}
		XmlNode xmlNode4 = xmlNode2.AppendChild(xmlDocument.CreateElement("update"));
		xmlNode4.AppendAttribute("version", Info.Version.ToString());
		if (!string.IsNullOrWhiteSpace(Info.Revised))
		{
			xmlNode4.AppendAttribute("revised", Info.Revised);
		}
		XmlNode parentNode = xmlNode4.AppendChild(xmlDocument.CreateElement("file"));
		parentNode.AppendAttribute("name", Info.UpdateFilename);
		parentNode.AppendAttribute("url", Info.UpdateUrl);
		XmlNode xmlNode5 = xmlNode.AppendChild(xmlDocument.CreateElement("files"));
		foreach (FileEntry file2 in Files)
		{
			if (file2.IsObsolete)
			{
				XmlNode parentNode2 = xmlNode5.AppendChild(xmlDocument.CreateElement("obsolete"));
				parentNode2.AppendAttribute("name", file2.Name);
				parentNode2.AppendAttribute("url", file2.Url);
			}
			else
			{
				XmlNode parentNode3 = xmlNode5.AppendChild(xmlDocument.CreateElement("file"));
				parentNode3.AppendAttribute("name", file2.Name);
				parentNode3.AppendAttribute("url", file2.Url);
			}
		}
		using XmlTextWriter xmlTextWriter = new XmlTextWriter(FileInfo.FullName, Encoding.UTF8);
		xmlTextWriter.Formatting = Formatting.Indented;
		xmlTextWriter.IndentChar = '\t';
		xmlTextWriter.Indentation = 1;
		xmlDocument.Save(xmlTextWriter);
	}

	public void SaveContent()
	{
		SaveContent(FileInfo);
	}

	public void SaveContent(FileInfo fileInfo)
	{
		FileInfo = fileInfo;
		File.WriteAllText(fileInfo.FullName, Content);
	}

	public void Load()
	{
		Load(File.ReadAllText(FileInfo.FullName));
	}

	private void Load(string xml)
	{
		Content = xml;
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.LoadXml(xml);
		if (xmlDocument.DocumentElement != null && xmlDocument.DocumentElement.HasAttributes && xmlDocument.DocumentElement.ContainsAttribute("app"))
		{
			string text = xmlDocument.DocumentElement.GetAttributeValue("app");
			if (text.Length == 1)
			{
				text += ".0";
			}
			MinimumAppVersion = new Version(text);
		}
		PopulateInformationSection(xmlDocument.DocumentElement?["info"]);
		XmlElement xmlElement = xmlDocument.DocumentElement?["files"];
		if (xmlElement == null)
		{
			return;
		}
		Files.Clear();
		foreach (XmlNode item in from XmlNode x in xmlElement.ChildNodes
			where x.NodeType == XmlNodeType.Element
			select x)
		{
			string attributeValue = item.GetAttributeValue("name");
			string attributeValue2 = item.GetAttributeValue("url");
			Files.Add(new FileEntry(attributeValue, attributeValue2, item.Name.Equals("obsolete"))
			{
				IsIndex = attributeValue.EndsWith("index")
			});
		}
	}

	protected void PopulateInformationSection(XmlNode infoNode)
	{
		if (infoNode == null)
		{
			throw new NullReferenceException("missing info node in index file");
		}
		Info.DisplayName = infoNode["name"]?.GetInnerText();
		Info.Description = infoNode["description"]?.GetInnerText();
		XmlElement xmlElement = infoNode["author"];
		if (xmlElement != null)
		{
			Info.Author = xmlElement.GetInnerText();
			Info.AuthorUrl = (xmlElement.ContainsAttribute("url") ? xmlElement.GetAttributeValue("url").Trim() : "");
		}
		XmlElement xmlElement2 = infoNode["update"];
		if (xmlElement2 == null)
		{
			throw new NullReferenceException("missing update node in info block");
		}
		if (xmlElement2.ContainsAttribute("version"))
		{
			Info.Version = new Version(xmlElement2.GetAttributeValue("version"));
			if (xmlElement2.ContainsAttribute("revised"))
			{
				Info.Revised = xmlElement2.GetAttributeValue("revised");
			}
			XmlElement xmlElement3 = xmlElement2["file"];
			if (xmlElement3 == null)
			{
				throw new ArgumentNullException("fileNode");
			}
			if (!xmlElement3.ContainsAttribute("url"))
			{
				throw new ArgumentException("missing url attribute on the file node");
			}
			if (!xmlElement3.ContainsAttribute("name"))
			{
				throw new ArgumentException("missing name attribute on the file node");
			}
			Info.UpdateFilename = xmlElement3.GetAttributeValue("name");
			Info.UpdateUrl = xmlElement3.GetAttributeValue("url");
			return;
		}
		throw new ArgumentException("missing version attribute on the update node");
	}

	private string GetContentFolderName()
	{
		return FileInfo.Name.Replace(FileInfo.Extension, "");
	}

	public string GetContentDirectory()
	{
		return Path.Combine(FileInfo.DirectoryName, GetContentFolderName());
	}

	public static IndexFile FromFile(FileInfo file)
	{
		IndexFile indexFile = new IndexFile(file);
		indexFile.Load();
		return indexFile;
	}

	public static async Task<IndexFile> FromUrl(string url)
	{
		try
		{
			using HttpClient client = new HttpClient();
			string text = await client.GetStringAsync(url);
			IndexFile indexFile = new IndexFile(text);
			indexFile.Load(text);
			return indexFile;
		}
		catch (HttpRequestException)
		{
			url = url.Replace("https://", "http://");
			Logger.Warning("retry once without https: " + url);
		}
		using HttpClient fallbackClient = new HttpClient();
		string text2 = await fallbackClient.GetStringAsync(url);
		IndexFile indexFile2 = new IndexFile(text2);
		indexFile2.Load(text2);
		return indexFile2;
	}

	public bool ContainsElementFiles()
	{
		return Files.Any((FileEntry file) => !file.IsIndex);
	}

	public override string ToString()
	{
		return $"{Info.DisplayName} [{Files.Count}]";
	}

	public bool RequiresUpdate(IndexFile remote)
	{
		return Info.Version.CompareTo(remote.Info.Version) == -1;
	}

	public bool MeetsMinimumAppVersionRequirements(Version appVersion)
	{
		if (MinimumAppVersion == null)
		{
			return true;
		}
		if (appVersion.CompareTo(MinimumAppVersion) < 0)
		{
			return false;
		}
		return true;
	}
}
