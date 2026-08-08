using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Builder.Core.Logging;

namespace Builder.Data.Extensions;

public static class XmlExtension
{
	public static bool HasAttributes(this XmlNode node)
	{
		if (node.Attributes != null)
		{
			return node.Attributes.Count > 0;
		}
		return false;
	}

	public static bool ContainsAttribute(this XmlNode node, string attributeName)
	{
		if (node.Attributes != null && node.Attributes.Count > 0)
		{
			return node.Attributes[attributeName] != null;
		}
		return false;
	}

	public static string GetAttributeValue(this XmlNode node, string attributeName)
	{
		if (node == null)
		{
			throw new ArgumentNullException("node");
		}
		if (node.Attributes != null && node.Attributes.Count > 0 && node.Attributes[attributeName] != null)
		{
			return node.Attributes[attributeName].Value;
		}
		throw new ArgumentException("no '" + attributeName + "' attribute on '" + node.Name + "' node (" + node.OuterXml + ")");
	}

	public static string GetInnerText(this XmlNode node)
	{
		if (node == null)
		{
			Logger.Warning($"trying to get innertext from NULL node {node}");
			return "";
		}
		if (node.HasChildNodes && node.ChildNodes.Count == 1 && node.FirstChild.NodeType == XmlNodeType.CDATA)
		{
			node.FirstChild.GetInnerText();
		}
		return node.InnerText.Trim();
	}

	public static void AppendChildren(this XmlNode node, params XmlNode[] nodes)
	{
		foreach (XmlNode newChild in nodes)
		{
			node.AppendChild(newChild);
		}
	}

	public static bool GetAttributeAsBoolean(this XmlNode node, string attributeName)
	{
		if (node.ContainsAttribute(attributeName))
		{
			return Convert.ToBoolean(node.GetAttributeValue(attributeName));
		}
		return false;
	}

	public static void AppendAttribute(this XmlNode parentNode, string name, string value)
	{
		if (parentNode.OwnerDocument == null)
		{
			throw new NullReferenceException("OwnerDocument");
		}
		XmlAttribute xmlAttribute = parentNode.OwnerDocument.CreateAttribute(name);
		xmlAttribute.Value = value;
		parentNode.Attributes?.Append(xmlAttribute);
	}

	public static void AppendAttributes(this XmlNode parentNode, Dictionary<string, string> attributesDictionary)
	{
		foreach (KeyValuePair<string, string> item in attributesDictionary)
		{
			parentNode.AppendAttribute(item.Key, item.Value);
		}
	}

	public static IEnumerable<XmlNode> NonCommentChildNodes(this XmlNode node)
	{
		return from XmlNode x in node.ChildNodes
			where x.NodeType != XmlNodeType.Comment
			select x;
	}

	public static T AsElement<T>(this ElementBase element) where T : ElementBase
	{
		return element as T;
	}

	public static string ToElementString(this XmlNode node)
	{
		if (node == null)
		{
			return "NULL";
		}
		using StringWriter stringWriter = new StringWriter();
		using (XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter))
		{
			xmlTextWriter.Formatting = Formatting.Indented;
			xmlTextWriter.IndentChar = '\t';
			xmlTextWriter.Indentation = 1;
			node.WriteTo(xmlTextWriter);
		}
		return stringWriter.ToString();
	}

	public static string ToElementStringWithSpaces(this XmlNode node)
	{
		if (node == null)
		{
			return "NULL";
		}
		using StringWriter stringWriter = new StringWriter();
		using (XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter))
		{
			xmlTextWriter.Formatting = Formatting.Indented;
			xmlTextWriter.IndentChar = ' ';
			xmlTextWriter.Indentation = 2;
			node.WriteTo(xmlTextWriter);
		}
		return stringWriter.ToString();
	}

	public static XmlNode AppendChild(this XmlNode parentNode, string name, string content)
	{
		return parentNode.AppendChild(name, content, isCData: false);
	}

	public static XmlNode AppendChild(this XmlNode parentNode, string name, string content, bool isCData)
	{
		XmlNode xmlNode = parentNode.AppendChild(parentNode.OwnerDocument.CreateNode(XmlNodeType.Element, name, null));
		if (isCData)
		{
			xmlNode.AppendChild(parentNode.OwnerDocument.CreateCDataSection(content));
		}
		else
		{
			xmlNode.InnerText = content;
		}
		return xmlNode;
	}

	public static bool ContainsChildNode(this XmlNode parentNode, string name)
	{
		return parentNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name.Equals(name)) != null;
	}

	public static XmlNode GetChildNode(this XmlNode parentNode, string name)
	{
		return parentNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name.Equals(name));
	}
}
