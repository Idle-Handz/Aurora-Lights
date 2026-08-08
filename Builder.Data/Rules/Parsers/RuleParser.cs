using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using Builder.Data.Extensions;

namespace Builder.Data.Rules.Parsers;

public abstract class RuleParser
{
	public string Name { get; }

	public IEnumerable<string> RequiredAttributes { get; }

	protected RuleParser(string name, IEnumerable<string> requiredAttributes)
	{
		Name = name;
		RequiredAttributes = requiredAttributes;
	}

	protected IEnumerable<XmlAttribute> GetAttributes(XmlNode ruleNode)
	{
		return ruleNode?.Attributes?.Cast<XmlAttribute>();
	}

	public bool ValidateRequiredAttributes(XmlNode ruleNode)
	{
		IEnumerable<string> source = GetAttributes(ruleNode)?.Select((XmlAttribute x) => x.Name);
		foreach (string requiredAttribute in RequiredAttributes)
		{
			if (!source.Contains(requiredAttribute))
			{
				return false;
			}
		}
		return true;
	}

	public abstract RuleBase Parse(XmlNode ruleNode, ElementHeader elementHeader);

	protected virtual ElementSetters ParseSetters(XmlNode ruleNode, ElementHeader elementHeader)
	{
		ElementSetters elementSetters = new ElementSetters();
		foreach (XmlNode item in from XmlNode x in ruleNode.ChildNodes
			where x.NodeType != XmlNodeType.Comment
			select x)
		{
			if (!item.Name.Equals("set"))
			{
				continue;
			}
			if (!item.ContainsAttribute("name"))
			{
				throw new ArgumentException("a set node has no 'name' attribute in the " + ruleNode.Name + " rule on '" + elementHeader.Name + "'");
			}
			ElementSetters.Setter setter = new ElementSetters.Setter(item.GetAttributeValue("name"), item.GetInnerText());
			if (item.HasAttributes())
			{
				foreach (XmlAttribute item2 in item.Attributes.Cast<XmlAttribute>())
				{
					if (item2.Name != "name")
					{
						setter.AdditionalAttributes.Add(item2.Name, item2.Value);
					}
				}
			}
			elementSetters.Add(setter);
		}
		return elementSetters;
	}

	public static IEnumerable<RuleParser> GetImplementations()
	{
		return (from parser in Assembly.GetAssembly(typeof(RuleParser)).GetTypes()
			where parser.IsClass && !parser.IsAbstract && parser.IsSubclassOf(typeof(RuleParser))
			select (RuleParser)Activator.CreateInstance(parser)).ToList();
	}
}
