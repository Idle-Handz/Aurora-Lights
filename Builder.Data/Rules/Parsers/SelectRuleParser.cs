using System;
using System.Xml;
using Builder.Core.Logging;
using Builder.Data.Extensions;
using Builder.Data.Rules.Strings;

namespace Builder.Data.Rules.Parsers;

public class SelectRuleParser : RuleParser
{
	public SelectRuleParser()
		: base("select", RuleStrings.SelectStrings.Required)
	{
	}

	public override RuleBase Parse(XmlNode ruleNode, ElementHeader elementHeader)
	{
		SelectRule selectRule = new SelectRule(elementHeader);
		selectRule.Setters = base.ParseSetters(ruleNode, elementHeader);
		foreach (XmlAttribute attribute in GetAttributes(ruleNode))
		{
			switch (attribute.Name)
			{
			case "type":
				selectRule.Attributes.Type = attribute.Value.Trim();
				continue;
			case "name":
				selectRule.Attributes.Name = attribute.Value.Trim();
				continue;
			case "number":
				selectRule.Attributes.Number = Convert.ToInt32(attribute.Value);
				continue;
			case "level":
				selectRule.Attributes.RequiredLevel = Convert.ToInt32(attribute.Value);
				continue;
			case "requirements":
				selectRule.Attributes.Requirements = attribute.Value.Trim();
				continue;
			case "supports":
				selectRule.Attributes.Supports = attribute.Value.Trim();
				continue;
			case "optional":
				selectRule.Attributes.Optional = Convert.ToBoolean(attribute.Value);
				continue;
			case "default":
				selectRule.Attributes.Default = attribute.Value.Trim();
				continue;
			case "default-behaviour":
				selectRule.Attributes.DefaultSelection = attribute.Value.Trim();
				continue;
			case "existing":
				selectRule.Attributes.Existing = Convert.ToBoolean(attribute.Value);
				continue;
			case "spellcasting":
				selectRule.Attributes.SpellcastingName = attribute.Value.Trim();
				continue;
			case "prepared":
				selectRule.Setters.Add(new ElementSetters.Setter("prepared", attribute.Value));
				continue;
			case "allowReplace":
				selectRule.Setters.Add(new ElementSetters.Setter("allowReplace", attribute.Value));
				continue;
			case "stat":
				selectRule.Attributes.ListSelectionInlineStatisticName = attribute.Value.Trim();
				continue;
			}
			Logger.Warning("unable to parse [" + attribute.Name + ":" + attribute.Value + "] attribute on the " + selectRule.RuleName + " rule in [" + selectRule.ElementHeader.Name + "]");
		}
		if (selectRule.Attributes.Type.Equals("Spell") && string.IsNullOrWhiteSpace(selectRule.Attributes.SpellcastingName) && (elementHeader.Type.Equals("Class") || elementHeader.Type.Equals("Archetype") || elementHeader.Type.Equals("Class Feature") || elementHeader.Type.Equals("Archetype Feature")))
		{
			Logger.Warning($"missing 'spellcasting' attribute on select rule in {elementHeader}");
		}
		if (string.IsNullOrWhiteSpace(selectRule.Attributes.Name))
		{
			Logger.Warning($"missing 'name' attribute on select rule in {elementHeader}");
			selectRule.Attributes.Name = elementHeader.Type + " (" + elementHeader.Name + ")";
		}
		if (selectRule.Attributes.IsList)
		{
			foreach (XmlNode childNode in ruleNode.ChildNodes)
			{
				int id = Convert.ToInt32(childNode.GetAttributeValue("id").Trim());
				string innerText = childNode.GetInnerText();
				selectRule.Attributes.ListItems.Add(new SelectionRuleListItem(id, innerText));
			}
		}
		return selectRule;
	}
}
