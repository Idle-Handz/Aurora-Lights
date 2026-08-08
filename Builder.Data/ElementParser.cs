using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Builder.Core.Logging;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Rules;
using Builder.Data.Rules.Parsers;

namespace Builder.Data;

public class ElementParser
{
	private readonly IEnumerable<RuleParser> _ruleParsers;

	protected const string SupportsNodeName = "supports";

	protected const string RequirementsNodeName = "requirements";

	protected const string DescriptionNodeName = "description";

	public virtual string ParserType => "default";

	public ElementParser()
	{
		_ruleParsers = ElementParserFactory.GetRuleParsers();
	}

	public virtual ElementHeader ParseElementHeader(XmlNode elementNode)
	{
		string name = elementNode.GetAttributeValue("name").Trim();
		string type = elementNode.GetAttributeValue("type").Trim();
		string source = elementNode.GetAttributeValue("source").Trim();
		string id = elementNode.GetAttributeValue("id").Trim();
		return new ElementHeader(name, type, source, id);
	}

	public virtual ElementHeader ParseExtendElementHeader(XmlNode elementNode)
	{
		string type = string.Empty;
		if (elementNode.ContainsAttribute("type"))
		{
			type = elementNode.GetAttributeValue("type").Trim();
		}
		string id = elementNode.GetAttributeValue("id").Trim();
		return new ElementHeader(string.Empty, type, string.Empty, id);
	}

	public virtual ElementBase ParseElement(XmlNode elementNode)
	{
		return ParseElement(elementNode, ParseElementHeader(elementNode));
	}

	public virtual ElementBase ParseElement(XmlNode elementNode, ElementHeader elementHeader)
	{
		ElementBase elementBase = new ElementBase(elementHeader);
		elementBase.ElementNode = elementNode;
		elementBase.ElementNodeString = elementNode.ToElementString();
		ParseSupports(elementNode, elementBase);
		ParseRequirements(elementNode, elementBase);
		ParseDescription(elementNode, elementBase);
		ParseSheetDescriptions(elementNode, elementBase);
		ParseSetterNodes(elementNode, elementBase);
		ParseRules(elementNode, elementBase);
		ParseSpellcastingSection(elementNode, elementBase);
		ParseEquipmentNode(elementNode, elementBase);
		ParseCompendiumNode(elementNode, elementBase);
		ParseSourceNode(elementNode, elementBase);
		return elementBase;
	}

	private static void ParseSupports(XmlNode elementNode, ElementBase element)
	{
		XmlElement xmlElement = elementNode["supports"];
		if (xmlElement == null)
		{
			return;
		}
		foreach (string item in from x in xmlElement.GetInnerText().Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries)
			select x.Trim())
		{
			if (element.Supports.Contains(item))
			{
				Logger.Warning($"duplicated support '{item}' removed while parsing '{element}'");
			}
			else
			{
				element.Supports.Add(item);
			}
		}
	}

	private static void ParseRequirements(XmlNode elementNode, ElementBase element)
	{
		element.Prerequisite = string.Empty;
		XmlElement xmlElement = elementNode["prerequisite"];
		if (xmlElement != null)
		{
			element.Prerequisite = xmlElement.GetInnerText();
		}
		else
		{
			xmlElement = elementNode["prerequisites"];
			if (xmlElement != null)
			{
				element.Prerequisite = xmlElement.GetInnerText();
			}
		}
		element.Requirements = string.Empty;
		XmlElement xmlElement2 = elementNode["requirements"];
		if (xmlElement2 != null)
		{
			element.Requirements = xmlElement2.GetInnerText();
		}
	}

	private static void ParseDescription(XmlNode elementNode, ElementBase element)
	{
		element.Description = string.Empty;
		XmlElement xmlElement = elementNode["description"];
		if (xmlElement == null || !xmlElement.HasChildNodes)
		{
			return;
		}
		if (xmlElement.FirstChild.NodeType == XmlNodeType.CDATA)
		{
			element.Description = xmlElement.FirstChild.GetInnerText();
		}
		else if (xmlElement.FirstChild.Name == "html")
		{
			if (xmlElement.FirstChild.HasChildNodes && xmlElement.FirstChild.FirstChild.NodeType != XmlNodeType.Text)
			{
				element.Description = xmlElement.FirstChild.InnerXml;
			}
			else
			{
				element.Description = xmlElement.FirstChild.GetInnerText();
			}
		}
		else if (xmlElement.FirstChild.NodeType == XmlNodeType.Text)
		{
			element.Description = xmlElement.FirstChild.GetInnerText();
			Logger.Warning($"{element} has no actual html tags in description");
		}
		else
		{
			element.Description = xmlElement.InnerXml;
		}
	}

	private static void ParseSheetDescriptions(XmlNode elementNode, ElementBase element)
	{
		try
		{
			XmlElement xmlElement = elementNode["sheet"];
			if (xmlElement == null)
			{
				return;
			}
			if (xmlElement.ContainsAttribute("alt"))
			{
				element.SheetDescription.AlternateName = xmlElement.GetAttributeValue("alt");
			}
			if (xmlElement.ContainsAttribute("display"))
			{
				element.SheetDescription.DisplayOnSheet = Convert.ToBoolean(xmlElement.GetAttributeValue("display"));
			}
			if (xmlElement.ContainsAttribute("usage"))
			{
				element.SheetDescription.Usage = xmlElement.GetAttributeValue("usage");
			}
			if (xmlElement.ContainsAttribute("action"))
			{
				element.SheetDescription.Action = xmlElement.GetAttributeValue("action");
			}
			if (!xmlElement.HasChildNodes)
			{
				return;
			}
			foreach (XmlNode item in from XmlNode x in xmlElement.ChildNodes
				where x.NodeType == XmlNodeType.Element
				select x)
			{
				ElementSheetDescriptions.SheetDescription sheetDescription = new ElementSheetDescriptions.SheetDescription(item.GetInnerText());
				if (item.ContainsAttribute("level"))
				{
					sheetDescription.Level = Convert.ToInt32(item.GetAttributeValue("level"));
				}
				if (item.ContainsAttribute("usage"))
				{
					sheetDescription.Usage = item.GetAttributeValue("usage");
					sheetDescription.HasUsage = true;
				}
				if (item.ContainsAttribute("action"))
				{
					sheetDescription.Action = item.GetAttributeValue("action");
					sheetDescription.HasAction = true;
				}
				element.SheetDescription.Add(sheetDescription);
			}
		}
		catch (Exception ex)
		{
			Logger.Warning($"unable to parse sheet descriptions for {element}");
			ex.Data["warning"] = $"unable to parse sheet descriptions for {element}";
			Logger.Exception(ex, "ParseSheetDescriptions");
		}
	}

	protected XmlNode GetSetterNode(XmlNode elementNode)
	{
		return elementNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name == "setters");
	}

	protected virtual void ParseSetterNodes(XmlNode elementNode, ElementBase element)
	{
		if (elementNode["set"] != null)
		{
			Logger.Warning($"found a setter node outside of the setters node in {element}");
		}
		XmlElement xmlElement = elementNode["setters"];
		if (xmlElement == null)
		{
			return;
		}
		foreach (XmlNode item in from XmlNode x in xmlElement.ChildNodes
			where x.NodeType != XmlNodeType.Comment
			select x)
		{
			if (!item.ContainsAttribute("name"))
			{
				throw new ArgumentException($"a setter node has no 'name' attribute on '{element}'");
			}
			string attributeValue = item.GetAttributeValue("name");
			string innerText = item.GetInnerText();
			ElementSetters.Setter setter = new ElementSetters.Setter(attributeValue, innerText);
			foreach (XmlAttribute item2 in item.Attributes.Cast<XmlAttribute>())
			{
				if (item2.Name != "name")
				{
					setter.AdditionalAttributes.Add(item2.Name, item2.Value);
				}
			}
			element.ElementSetters.Add(setter);
		}
		if (element.ElementSetters.ContainsSetter("allow duplicate"))
		{
			element.AllowDuplicate = Convert.ToBoolean(element.ElementSetters.GetSetter("allow duplicate").Value);
		}
		if (element.ElementSetters.ContainsSetter("sourceUrl"))
		{
			element.SourceUrl = element.ElementSetters.GetSetter("sourceUrl").Value;
		}
		if (element.ElementSetters.AttemptGetSetterValue("keywords", out var setter2))
		{
			element.Keywords.AddRange(from x in setter2.Value.Split(',')
				select x.Trim().ToLowerInvariant());
		}
		if (element.ElementSetters.ContainsSetter("prerequisite") && string.IsNullOrWhiteSpace(element.Prerequisite))
		{
			element.Prerequisite = element.ElementSetters.GetSetter("prerequisite").Value;
		}
	}

	protected void ValidateElementSetters(ElementBase element, params string[] requiredSetters)
	{
		foreach (string text in requiredSetters)
		{
			if (!element.ElementSetters.ContainsSetter(text))
			{
				throw new MissingSetterException(element.ElementHeader, text);
			}
		}
	}

	protected void ParseRules(XmlNode elementNode, ElementBase element)
	{
		element.Rules.Clear();
		XmlElement xmlElement = elementNode["rules"];
		if (xmlElement == null)
		{
			return;
		}
		foreach (XmlNode item2 in from XmlNode x in xmlElement
			where x.NodeType != XmlNodeType.Comment
			select x)
		{
			try
			{
				if (item2.Name == "inline")
				{
					Logger.Warning($"skipping not implemented 'inline' element on {element}");
					continue;
				}
				string ruleNodeName = item2.Name;
				if (item2.Name.Equals("statistic", StringComparison.OrdinalIgnoreCase))
				{
					ruleNodeName = "stat";
				}
				RuleBase item = _ruleParsers.Single((RuleParser x) => x.Name == ruleNodeName).Parse(item2, element.ElementHeader);
				element.Rules.Add(item);
			}
			catch (InvalidOperationException ex)
			{
				ex.Data["warning"] = $"Invalid ruleNode [{item2.Name}] in [{element}]";
				Logger.Warning($"a wild ruleNode appeared! [{item2.Name}] in [{element}]");
				throw;
			}
		}
	}

	protected XmlNode GetSpellcastingNode(XmlNode elementNode)
	{
		return elementNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name == "spellcasting");
	}

	protected virtual void ParseSpellcastingSection(XmlNode elementNode, ElementBase element)
	{
		XmlNode spellcastingNode = GetSpellcastingNode(elementNode);
		if (spellcastingNode == null)
		{
			return;
		}
		SpellcastingInformation spellcastingInformation = new SpellcastingInformation(element.ElementHeader)
		{
			Name = (spellcastingNode.ContainsAttribute("name") ? spellcastingNode.GetAttributeValue("name") : "N/A"),
			AbilityName = (spellcastingNode.ContainsAttribute("ability") ? spellcastingNode.GetAttributeValue("ability") : "N/A"),
			Prepare = spellcastingNode.GetAttributeAsBoolean("prepare"),
			IsExtension = spellcastingNode.GetAttributeAsBoolean("extend"),
			AllowSpellSwap = (spellcastingNode.ContainsAttribute("allowReplace") && spellcastingNode.GetAttributeAsBoolean("allowReplace"))
		};
		spellcastingInformation.AssignToAllSpellcastingClasses = spellcastingNode.GetAttributeAsBoolean("all");
		if (spellcastingInformation.Name.Equals("N/A") && !spellcastingInformation.AssignToAllSpellcastingClasses)
		{
			Logger.Warning($"<spellcasting> node without @name on {element}");
		}
		foreach (XmlNode item in spellcastingNode.NonCommentChildNodes())
		{
			bool known = false;
			if (item.ContainsAttribute("known"))
			{
				known = Convert.ToBoolean(item.GetAttributeValue("known"));
			}
			switch (item.Name)
			{
			case "list":
				spellcastingInformation.InitialSupportedSpellsExpression = new SpellcastingInformation.SpellcastingList(item.GetInnerText(), known);
				break;
			case "extend":
				spellcastingInformation.ExtendedSupportedSpellsExpressions.Add(new SpellcastingInformation.SpellcastingList(item.GetInnerText(), known));
				break;
			}
		}
		if (!spellcastingInformation.IsExtension && spellcastingInformation.ContainsInitialSpellcastingList() && spellcastingInformation.InitialSupportedSpellsExpression.Known)
		{
			spellcastingInformation.PrepareFromSpellList = true;
		}
		element.SpellcastingInformation = spellcastingInformation;
	}

	protected XmlNode GetEquipmentNode(XmlNode elementNode)
	{
		return elementNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name == "equipment");
	}

	protected virtual void ParseEquipmentNode(XmlNode elementNode, ElementBase element)
	{
		XmlNode equipmentNode = GetEquipmentNode(elementNode);
		if (equipmentNode != null)
		{
			ElementEquipment elementEquipment = new ElementEquipment
			{
				Name = (equipmentNode.ContainsAttribute("name") ? equipmentNode.GetAttributeValue("name") : (element.Name + " Equipment"))
			};
			XmlNode xmlNode = equipmentNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name == "description");
			if (xmlNode != null)
			{
				elementEquipment.Description = ((xmlNode.FirstChild.NodeType == XmlNodeType.CDATA) ? xmlNode.FirstChild.GetInnerText() : xmlNode.InnerXml);
			}
			element.Equipment = elementEquipment;
		}
	}

	protected XmlNode GetCompendiumNode(XmlNode elementNode)
	{
		return elementNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name == "compendium");
	}

	protected virtual void ParseCompendiumNode(XmlNode elementNode, ElementBase element)
	{
		XmlNode compendiumNode = GetCompendiumNode(elementNode);
		if (compendiumNode != null && compendiumNode.ContainsAttribute("display"))
		{
			element.IncludeInCompendium = compendiumNode.GetAttributeAsBoolean("display");
		}
	}

	protected XmlNode GetSourceNode(XmlNode elementNode)
	{
		return elementNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name == "source");
	}

	protected virtual void ParseSourceNode(XmlNode elementNode, ElementBase element)
	{
		XmlNode sourceNode = GetSourceNode(elementNode);
		if (sourceNode == null)
		{
			return;
		}
		element.ElementSource = new ElementSourceInfo();
		if (element.ElementHeader.Source.StartsWith("ID_"))
		{
			Logger.Warning($"id in source attribute on {element} not yet supported");
		}
		if (!sourceNode.HasAttributes())
		{
			string innerText = sourceNode.GetInnerText();
			if (innerText.StartsWith("ID_"))
			{
				element.ElementSource.SourceId = innerText;
			}
			else
			{
				element.ElementSource.Source = innerText;
			}
		}
		if (sourceNode.ContainsAttribute("name"))
		{
			element.ElementSource.Source = sourceNode.GetAttributeValue("name");
		}
		if (sourceNode.ContainsAttribute("id"))
		{
			element.ElementSource.SourceId = sourceNode.GetAttributeValue("id");
		}
		element.ElementSource.OverrideUrl = sourceNode.GetChildNode("url")?.GetInnerText() ?? "";
		element.ElementSource.Page = sourceNode.GetChildNode("page")?.GetInnerText() ?? "";
	}

	[Obsolete]
	protected void ValidateRequiredSetters(ElementBase element, params string[] requiredSetters)
	{
		using IEnumerator<string> enumerator = requiredSetters.Where((string setter) => !element.ElementSetters.ContainsSetter(setter)).GetEnumerator();
		if (enumerator.MoveNext())
		{
			string current = enumerator.Current;
			throw new MissingSetterException(element.ElementHeader, current);
		}
	}

	protected void ValidateNodeAttributes(XmlNode node, string identifier, params string[] requiredAttributes)
	{
		using IEnumerator<string> enumerator = requiredAttributes.Where((string attribute) => !node.ContainsAttribute(attribute)).GetEnumerator();
		if (enumerator.MoveNext())
		{
			string current = enumerator.Current;
			throw new ArgumentException("a " + node.Name + " node on '" + identifier + "' has no required '" + current + "' attribute");
		}
	}
}
