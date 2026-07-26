using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class CompanionElementParser : ElementParser
{
	public override string ParserType => "Companion";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		CompanionElement companionElement = base.ParseElement(elementNode).Construct<CompanionElement>();
		ValidateElementSetters(companionElement, "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma", "type", "size", "challenge");
		companionElement.Strength = companionElement.ElementSetters.GetSetter("strength").ValueAsInteger();
		companionElement.Dexterity = companionElement.ElementSetters.GetSetter("dexterity").ValueAsInteger();
		companionElement.Constitution = companionElement.ElementSetters.GetSetter("constitution").ValueAsInteger();
		companionElement.Intelligence = companionElement.ElementSetters.GetSetter("intelligence").ValueAsInteger();
		companionElement.Wisdom = companionElement.ElementSetters.GetSetter("wisdom").ValueAsInteger();
		companionElement.Charisma = companionElement.ElementSetters.GetSetter("charisma").ValueAsInteger();
		companionElement.ArmorClass = companionElement.ElementSetters.GetSetter("ac").Value;
		if (companionElement.ElementSetters.GetSetter("ac").HasAdditionalAttributes)
		{
			companionElement.ElementSetters.GetSetter("ac").AdditionalAttributes.ContainsKey("type");
		}
		companionElement.HitPoints = companionElement.ElementSetters.GetSetter("hp")?.Value ?? "";
		companionElement.Speed = companionElement.ElementSetters.GetSetter("speed")?.Value ?? "";
		companionElement.Senses = companionElement.ElementSetters.GetSetter("senses")?.Value ?? "";
		companionElement.Languages = companionElement.ElementSetters.GetSetter("languages")?.Value ?? "—";
		companionElement.SavingThrows = companionElement.ElementSetters.GetSetter("saves")?.Value ?? "";
		companionElement.Skills = companionElement.ElementSetters.GetSetter("skills")?.Value ?? "";
		companionElement.DamageVulnerabilities = companionElement.ElementSetters.GetSetter("damageVulnerabilities")?.Value ?? "";
		companionElement.DamageResistances = companionElement.ElementSetters.GetSetter("damageResistances")?.Value ?? "";
		companionElement.DamageImmunities = companionElement.ElementSetters.GetSetter("damageImmunities")?.Value ?? "";
		companionElement.DamageVulnerabilities = companionElement.ElementSetters.GetSetter("vulnerabilities")?.Value ?? "";
		companionElement.DamageResistances = companionElement.ElementSetters.GetSetter("resistances")?.Value ?? "";
		companionElement.DamageImmunities = companionElement.ElementSetters.GetSetter("immunities")?.Value ?? "";
		companionElement.ConditionResistances = companionElement.ElementSetters.GetSetter("conditionResistances")?.Value ?? "";
		companionElement.ConditionImmunities = companionElement.ElementSetters.GetSetter("conditionImmunities")?.Value ?? "";
		companionElement.ConditionVulnerabilities = companionElement.ElementSetters.GetSetter("conditionVulnerabilities")?.Value ?? "";
		companionElement.CreatureType = companionElement.ElementSetters.GetSetter("type")?.Value ?? "";
		companionElement.Size = companionElement.ElementSetters.GetSetter("size").Value;
		companionElement.Alignment = companionElement.ElementSetters.GetSetter("alignment").Value;
		companionElement.Challenge = companionElement.ElementSetters.GetSetter("challenge").Value;
		companionElement.Experience = companionElement.ElementSetters.GetSetter("experience")?.Value ?? "";
		ElementSetters.Setter setter = companionElement.ElementSetters.GetSetter("traits");
		if (setter != null)
		{
			IEnumerable<string> collection = from x in setter.Value.Split(',')
				select x.Trim();
			companionElement.Traits.AddRange(collection);
		}
		ElementSetters.Setter setter2 = companionElement.ElementSetters.GetSetter("actions");
		if (setter2 != null)
		{
			IEnumerable<string> collection2 = from x in setter2.Value.Split(',')
				select x.Trim();
			companionElement.Actions.AddRange(collection2);
		}
		ElementSetters.Setter setter3 = companionElement.ElementSetters.GetSetter("reactions");
		if (setter3 != null)
		{
			IEnumerable<string> collection3 = from x in setter3.Value.Split(',')
				select x.Trim();
			companionElement.Reactions.AddRange(collection3);
		}
		companionElement.Supports.Add(companionElement.CreatureType);
		companionElement.Supports.Add(companionElement.Size);
		companionElement.Supports.Add(companionElement.Challenge);
		switch (companionElement.Challenge)
		{
		case "5":
		case "6":
		case "7":
		case "8":
			companionElement.Proficiency = 3;
			break;
		case "9":
		case "10":
		case "11":
		case "12":
			companionElement.Proficiency = 4;
			break;
		case "13":
		case "14":
		case "15":
		case "16":
			companionElement.Proficiency = 5;
			break;
		case "17":
		case "18":
		case "19":
		case "20":
			companionElement.Proficiency = 6;
			break;
		case "21":
		case "22":
		case "23":
		case "24":
			companionElement.Proficiency = 7;
			break;
		case "25":
		case "26":
		case "27":
		case "28":
			companionElement.Proficiency = 8;
			break;
		case "29":
		case "30":
			companionElement.Proficiency = 9;
			break;
		default:
			companionElement.Proficiency = 2;
			break;
		}
		if (companionElement.ElementSetters.ContainsSetter("proficiency"))
		{
			companionElement.Proficiency = companionElement.ElementSetters.GetSetter("proficiency").ValueAsInteger();
		}
		if (!companionElement.ElementSetters.ContainsSetter("short"))
		{
			companionElement.ElementSetters.Add(new ElementSetters.Setter("short", companionElement.Size + " " + companionElement.CreatureType.ToLowerInvariant() + ", " + companionElement.Alignment.ToLowerInvariant()));
		}
		return companionElement;
	}
}
