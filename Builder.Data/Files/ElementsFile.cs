using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Builder.Core.Logging;
using Builder.Data.Extensions;

namespace Builder.Data.Files;

public class ElementsFile
{
	public string Content { get; set; }

	public FileInfo FileInfo { get; private set; }

	public InformationSection Info { get; } = new InformationSection();

	public Version MinimumAppVersion { get; set; }

	public bool Ignore { get; set; }

	public ObservableCollection<XmlNode> ElementNodes { get; set; } = new ObservableCollection<XmlNode>();

	public ObservableCollection<XmlNode> ExtendNodes { get; set; } = new ObservableCollection<XmlNode>();

	public ElementsFile(string content)
	{
		Content = content;
	}

	public ElementsFile(FileInfo fileInfo)
	{
		FileInfo = fileInfo;
	}

	public void SaveContent(FileInfo file)
	{
		FileInfo = file;
		if (!Directory.Exists(file.DirectoryName) && file.DirectoryName != null)
		{
			Logger.Info("creating directory " + file.DirectoryName + " while saving content");
			Directory.CreateDirectory(file.DirectoryName);
		}
		File.WriteAllText(FileInfo.FullName, Content);
	}

	public void Load()
	{
		Load(File.ReadAllText(FileInfo.FullName));
	}

	public void Load(string xml)
	{
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
		if (xmlDocument.DocumentElement != null && xmlDocument.DocumentElement.HasAttributes && xmlDocument.DocumentElement.ContainsAttribute("ignore"))
		{
			string attributeValue = xmlDocument.DocumentElement.GetAttributeValue("ignore");
			Ignore = Convert.ToBoolean(attributeValue);
		}
		XmlElement xmlElement = xmlDocument.DocumentElement?["info"];
		if (xmlElement != null)
		{
			Info.ContainsInfoNode = true;
			PopulateInformationSection(xmlElement);
		}
		else if (FileInfo != null)
		{
			Info.DisplayName = FileInfo.Name;
		}
		IEnumerable<XmlNode> enumerable = (from XmlNode x in xmlDocument.DocumentElement?.ChildNodes
			where x.NodeType == XmlNodeType.Element && x.Name.Equals("element")
			select x);
		if (enumerable != null)
		{
			ElementNodes.Clear();
			foreach (XmlNode item in enumerable)
			{
				ElementNodes.Add(item);
			}
		}
		IEnumerable<XmlNode> enumerable2 = (from XmlNode x in xmlDocument.DocumentElement?.ChildNodes
			where x.NodeType == XmlNodeType.Element && x.Name.Equals("append")
			select x);
		if (enumerable2 == null)
		{
			return;
		}
		ExtendNodes.Clear();
		foreach (XmlNode item2 in enumerable2)
		{
			ExtendNodes.Add(item2);
		}
	}

	protected void PopulateInformationSection(XmlNode infoNode)
	{
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
				throw new ArgumentNullException("fileNode", "missing file node in update node");
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

	public static async Task<ElementsFile> FromUrl(string url)
	{
		try
		{
			using HttpClient client = new HttpClient();
			string text = await client.GetStringAsync(url);
			ElementsFile elementsFile = new ElementsFile(text);
			elementsFile.Load(text);
			return elementsFile;
		}
		catch (HttpRequestException)
		{
			url = url.Replace("https://", "http://");
			Logger.Warning("retry once without https: " + url);
		}
		using HttpClient fallbackClient = new HttpClient();
		string text2 = await fallbackClient.GetStringAsync(url);
		ElementsFile elementsFile2 = new ElementsFile(text2);
		elementsFile2.Load(text2);
		return elementsFile2;
	}

	public static ElementsFile FromFile(FileInfo file)
	{
		try
		{
			ElementsFile elementsFile = new ElementsFile(file);
			elementsFile.Load();
			return elementsFile;
		}
		catch (Exception ex)
		{
			throw new ElementsFileLoadException(ex.Message ?? "", ex);
		}
	}

	public bool RequiresUpdate(ElementsFile remote)
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
