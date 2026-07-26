using System;
using System.Xml;
using Builder.Core.Logging;
using Builder.Data.Rules.Strings;

namespace Builder.Data.Rules.Parsers;

public class GrantRuleParser : RuleParser
{
	public GrantRuleParser()
		: base("grant", RuleStrings.GrantStrings.Required)
	{
	}

	public override RuleBase Parse(XmlNode ruleNode, ElementHeader elementHeader)
	{
		GrantRule grantRule = new GrantRule(elementHeader);
		foreach (XmlAttribute attribute in GetAttributes(ruleNode))
		{
			switch (attribute.Name)
			{
			case "id":
				grantRule.Attributes.Name = attribute.Value;
				continue;
			case "name":
				grantRule.Attributes.Name = attribute.Value;
				continue;
			case "type":
				grantRule.Attributes.Type = attribute.Value;
				continue;
			case "level":
				grantRule.Attributes.RequiredLevel = Convert.ToInt32(attribute.Value);
				continue;
			case "requirements":
				grantRule.Attributes.Requirements = attribute.Value;
				continue;
			case "spellcasting":
				grantRule.Setters.Add(new ElementSetters.Setter("spellcasting", attribute.Value));
				continue;
			case "prepared":
				grantRule.Setters.Add(new ElementSetters.Setter("prepared", attribute.Value));
				continue;
			}
			Logger.Warning("unable to parse [" + attribute.Name + ":" + attribute.Value + "] attribute on the " + grantRule.RuleName + " rule in [" + grantRule.ElementHeader.Name + "]");
		}
		grantRule.Setters.AddRange(base.ParseSetters(ruleNode, elementHeader));
		return grantRule;
	}
}
