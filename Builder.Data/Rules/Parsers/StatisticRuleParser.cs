using System;
using System.Collections.Generic;
using System.Xml;
using Builder.Core.Logging;
using Builder.Data.Rules.Strings;

namespace Builder.Data.Rules.Parsers;

public class StatisticRuleParser : RuleParser
{
	public StatisticRuleParser()
		: base("stat", RuleStrings.StatisticStrings.Required)
	{
	}

	public static string MapLegacyName(string input)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>
		{
			{ "Strength Maximum", "strength:max" },
			{ "Dexterity Maximum", "dexterity:max" },
			{ "Constitution Maximum", "constitution:max" },
			{ "Intelligence Maximum", "intelligence:max" },
			{ "Wisdom Maximum", "wisdom:max" },
			{ "Charisma Maximum", "charisma:max" },
			{ "Strength Modifier", "strength:modifier" },
			{ "Dexterity Modifier", "dexterity:modifier" },
			{ "Constitution Modifier", "constitution:modifier" },
			{ "Intelligence Modifier", "intelligence:modifier" },
			{ "Wisdom Modifier", "wisdom:modifier" },
			{ "Charisma Modifier", "charisma:modifier" },
			{ "Strength Saving Throw Proficiency", "strength:save:proficiency" },
			{ "Dexterity Saving Throw Proficiency", "dexterity:save:proficiency" },
			{ "Constitution Saving Throw Proficiency", "constitution:save:proficiency" },
			{ "Intelligence Saving Throw Proficiency", "intelligence:save:proficiency" },
			{ "Wisdom Saving Throw Proficiency", "wisdom:save:proficiency" },
			{ "Charisma Saving Throw Proficiency", "charisma:save:proficiency" },
			{ "Strength Saving Throw Misc", "strength:save:misc" },
			{ "Dexterity Saving Throw Misc", "dexterity:save:misc" },
			{ "Constitution Saving Throw Misc", "constitution:save:misc" },
			{ "Intelligence Saving Throw Misc", "intelligence:save:misc" },
			{ "Wisdom Saving Throw Misc", "wisdom:save:misc" },
			{ "Charisma Saving Throw Misc", "charisma:save:misc" },
			{ "Acrobatics Proficiency", "Acrobatics:Proficiency" },
			{ "Animal Handling Proficiency", "Animal Handling:Proficiency" },
			{ "Arcana Proficiency", "Arcana:Proficiency" },
			{ "Athletics Proficiency", "Athletics:Proficiency" },
			{ "Deception Proficiency", "Deception:Proficiency" },
			{ "History Proficiency", "History:Proficiency" },
			{ "Insight Proficiency", "Insight:Proficiency" },
			{ "Intimidation Proficiency", "Intimidation:Proficiency" },
			{ "Investigation Proficiency", "Investigation:Proficiency" },
			{ "Medicine Proficiency", "Medicine:Proficiency" },
			{ "Nature Proficiency", "Nature:Proficiency" },
			{ "Perception Proficiency", "Perception:Proficiency" },
			{ "Performance Proficiency", "Performance:Proficiency" },
			{ "Persuasion Proficiency", "Persuasion:Proficiency" },
			{ "Religion Proficiency", "Religion:Proficiency" },
			{ "Sleight of Hand Proficiency", "Sleight of Hand:Proficiency" },
			{ "Stealth Proficiency", "Stealth:Proficiency" },
			{ "Survival Proficiency", "Survival:Proficiency" },
			{ "Acrobatics Misc", "Acrobatics:Misc" },
			{ "Animal Handling Misc", "Animal Handling:Misc" },
			{ "Arcana Misc", "Arcana:Misc" },
			{ "Athletics Misc", "Athletics:Misc" },
			{ "Deception Misc", "Deception:Misc" },
			{ "History Misc", "History:Misc" },
			{ "Insight Misc", "Insight:Misc" },
			{ "Intimidation Misc", "Intimidation:Misc" },
			{ "Investigation Misc", "Investigation:Misc" },
			{ "Medicine Misc", "Medicine:Misc" },
			{ "Nature Misc", "Nature:Misc" },
			{ "Perception Misc", "Perception:Misc" },
			{ "Performance Misc", "Performance:Misc" },
			{ "Persuasion Misc", "Persuasion:Misc" },
			{ "Religion Misc", "Religion:Misc" },
			{ "Sleight of Hand Misc", "Sleight of Hand:Misc" },
			{ "Stealth Misc", "Stealth:Misc" },
			{ "Survival Misc", "Survival:Misc" },
			{ "Acrobatics Passive", "acrobatics:passive" },
			{ "Animal Handling Passive", "animal handling:passive" },
			{ "Arcana Passive", "arcana:passive" },
			{ "Athletics Passive", "athletics:passive" },
			{ "Deception Passive", "deception:passive" },
			{ "History Passive", "history:passive" },
			{ "Insight Passive", "insight:passive" },
			{ "Intimidation Passive", "intimidation:passive" },
			{ "Investigation Passive", "investigation:passive" },
			{ "Medicine Passive", "medicine:passive" },
			{ "Nature Passive", "nature:passive" },
			{ "Perception Passive", "perception:passive" },
			{ "Performance Passive", "performance:passive" },
			{ "Persuasion Passive", "persuasion:passive" },
			{ "Religion Passive", "religion:passive" },
			{ "Sleight of Hand Passive", "sleight of hand:passive" },
			{ "Stealth Passive", "stealth:passive" },
			{ "Survival Passive", "survival:passive" },
			{ "Proficiency Half", "Proficiency:Half" },
			{ "Proficiency Half Rounded Down", "Proficiency:Half" },
			{ "Proficiency Half Rounded Up", "Proficiency:Half:up" },
			{ "ac:dexterity:mod:cap:armor:medium", "ac:armored:dexterity:cap" }
		};
		if (dictionary.ContainsKey(input))
		{
			Logger.Warning("mapping [" + input + "] to [" + dictionary[input].ToLowerInvariant() + "]");
			return dictionary[input].ToLowerInvariant();
		}
		return input.ToLowerInvariant();
	}

	public override RuleBase Parse(XmlNode ruleNode, ElementHeader elementHeader)
	{
		StatisticRule statisticRule = new StatisticRule(elementHeader);
		foreach (XmlAttribute attribute in GetAttributes(ruleNode))
		{
			switch (attribute.Name)
			{
			case "name":
			{
				string name = MapLegacyName(attribute.Value);
				statisticRule.Attributes.Name = name;
				break;
			}
			case "bonus":
				statisticRule.Attributes.Type = attribute.Value;
				break;
			case "value":
			{
				string value = MapLegacyName(attribute.Value.TrimStart('+'));
				if (attribute.Value.StartsWith("-"))
				{
					value = MapLegacyName(attribute.Value.TrimStart('-'));
					value = "-" + value;
				}
				statisticRule.Attributes.Value = value;
				break;
			}
			case "level":
				statisticRule.Attributes.Level = Convert.ToInt32(attribute.Value);
				break;
			case "requirements":
				statisticRule.Attributes.Requirements = attribute.Value;
				break;
			case "inline":
				statisticRule.Attributes.Inline = Convert.ToBoolean(attribute.Value);
				break;
			case "equipped":
				statisticRule.Attributes.Equipped = attribute.Value;
				break;
			case "not-equipped":
				statisticRule.Attributes.NotEquipped = attribute.Value;
				break;
			case "minimum":
				statisticRule.Attributes.Minimum = attribute.Value;
				break;
			case "maximum":
			case "cap":
				statisticRule.Attributes.Cap = attribute.Value;
				break;
			case "alt":
				statisticRule.Attributes.Alt = attribute.Value;
				break;
			case "merge":
				statisticRule.Attributes.Merge = Convert.ToBoolean(attribute.Value);
				break;
			default:
				Logger.Warning("unable to parse [" + attribute.Name + ":" + attribute.Value + "] attribute on the " + statisticRule.RuleName + " rule in [" + statisticRule.ElementHeader.Name + "]");
				break;
			}
		}
		statisticRule.Setters = base.ParseSetters(ruleNode, elementHeader);
		return statisticRule;
	}
}
