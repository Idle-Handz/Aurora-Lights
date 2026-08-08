using System;
using System.Globalization;
using System.Linq;
using System.Xml;
using Builder.Core.Logging;
using Builder.Data.Elements;
using Builder.Data.Extensions;

namespace Builder.Data.ElementParsers;

public class ItemElementParser : ElementParser
{
	public const string ValuableSetterName = "valuable";

	public override string ParserType => "Item";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Item item = base.ParseElement(elementNode).Construct<Item>();
		item.Category = item.ElementSetters.GetSetter("category")?.Value ?? "";
		if (item.ElementSetters.ContainsSetter("cost"))
		{
			int.TryParse(item.ElementSetters.GetSetter("cost").Value, out var result);
			item.Cost = result;
		}
		else if (item.ElementSetters.ContainsSetter("price"))
		{
			int.TryParse(item.ElementSetters.GetSetter("price").Value, out var result2);
			item.Cost = result2;
		}
		if (item.ElementSetters.ContainsSetter("weight"))
		{
			ElementSetters.Setter setter = item.ElementSetters.GetSetter("weight");
			item.Weight = setter.Value ?? "";
			if (setter.AdditionalAttributes.ContainsKey("lb"))
			{
				string text = setter.AdditionalAttributes["lb"];
				if (text.Contains(","))
				{
					Logger.Warning($"{item} contains bad weight lb ,");
				}
				decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var result3);
				item.CalculableWeight = result3;
			}
			if (setter.AdditionalAttributes.ContainsKey("excludeEncumbrance"))
			{
				item.ExcludeFromEncumbrance = Convert.ToBoolean(setter.AdditionalAttributes["excludeEncumbrance"]);
			}
		}
		if (item.ElementSetters.ContainsSetter("stackable"))
		{
			item.IsStackable = item.ElementSetters.GetSetter("stackable").ValueAsBool();
		}
		if (item.ElementSetters.ContainsSetter("stash"))
		{
			item.IsStash = item.ElementSetters.GetSetter("stash").ValueAsBool();
		}
		if (item.ElementSetters.ContainsSetter("consumable"))
		{
			item.IsConsumable = item.ElementSetters.GetSetter("consumable").ValueAsBool();
		}
		if (item.ElementSetters.ContainsSetter("attunement"))
		{
			item.RequiresAttunement = item.ElementSetters.GetSetter("attunement").ValueAsBool();
		}
		if (item.ElementSetters.ContainsSetter("inventory-hidden"))
		{
			item.HideFromInventory = item.ElementSetters.GetSetter("inventory-hidden").ValueAsBool();
		}
		item.Slot = item.ElementSetters.GetSetter("slot")?.Value ?? "";
		if (item.Slot.Contains(","))
		{
			item.Slots.AddRange(from x in item.Slot.Split(',')
				select x.Trim());
		}
		else
		{
			item.Slots.Add(item.Slot);
		}
		item.ItemType = item.ElementSetters.GetSetter("type")?.Value ?? "";
		item.Rarity = item.ElementSetters.GetSetter("rarity")?.Value ?? "";
		if (item.ElementSetters.ContainsSetter("valuable"))
		{
			item.IsValuable = item.ElementSetters.GetSetter("valuable").ValueAsBool();
		}
		item.Versatile = item.ElementSetters.GetSetter("versatile")?.Value ?? "";
		if (item.ElementSetters.ContainsSetter("range"))
		{
			item.Range = item.ElementSetters.GetSetter("range").Value;
		}
		if (item.ElementSetters.ContainsSetter("damage"))
		{
			ElementSetters.Setter setter2 = item.ElementSetters.GetSetter("damage");
			item.Damage = setter2.Value;
			if (setter2.HasAdditionalAttributes && setter2.AdditionalAttributes.ContainsKey("type"))
			{
				item.DamageType = setter2.AdditionalAttributes["type"];
			}
			if (setter2.HasAdditionalAttributes && setter2.AdditionalAttributes.ContainsKey("modifier"))
			{
				item.DamageAbilityModifier = setter2.AdditionalAttributes["modifier"];
			}
		}
		item.DisplayArmorClass = item.ElementSetters.GetSetter("armorClass")?.Value ?? "";
		item.DisplayStrength = item.ElementSetters.GetSetter("strength")?.Value ?? "";
		item.DisplayStealth = item.ElementSetters.GetSetter("stealth")?.Value ?? "";
		item.Enhancement = item.ElementSetters.GetSetter("enhancement")?.Value ?? "";
		item.NameFormat = item.ElementSetters.GetSetter("name-format")?.Value ?? "";
		if (!string.IsNullOrWhiteSpace(item.Rarity))
		{
			item.Keywords.Add(item.Rarity.ToLowerInvariant());
		}
		item.Container = (item.ElementSetters.ContainsSetter("container") ? item.ElementSetters.GetSetter("container").Value : "");
		foreach (XmlNode item2 in from XmlNode x in GetSetterNode(elementNode).ChildNodes
			where x.ContainsAttribute("name")
			select x)
		{
			string attributeValue = item2.GetAttributeValue("name");
			try
			{
				if (attributeValue == "cost")
				{
					switch (item2.GetAttributeValue("currency"))
					{
					case "cp":
					case "copper":
						item.CurrencyAbbreviation = "cp";
						item.Currency = "copper";
						break;
					case "sp":
					case "silver":
						item.CurrencyAbbreviation = "sp";
						item.Currency = "silver";
						break;
					case "ep":
					case "electrum":
						item.CurrencyAbbreviation = "ep";
						item.Currency = "electrum";
						break;
					case "gp":
					case "gold":
						item.CurrencyAbbreviation = "gp";
						item.Currency = "gold";
						break;
					case "pp":
					case "platinum":
						item.CurrencyAbbreviation = "pp";
						item.Currency = "platinum";
						break;
					default:
						Logger.Warning($"unknown currency attribute value in {item}");
						break;
					}
					item2.GetAttributeAsBoolean("override");
				}
				else if (attributeValue == "stash" && item2.ContainsAttribute("weightless"))
				{
					string attributeValue2 = item2.GetAttributeValue("weightless");
					item.IsWeightlessStash = Convert.ToBoolean(attributeValue2);
				}
			}
			catch (Exception ex)
			{
				Logger.Exception(ex, "ParseElement");
			}
		}
		ParseExtractionNode(elementNode["extract"], item);
		return item;
	}

	private void ParseExtractionNode(XmlNode extractNode, Item element)
	{
		if (extractNode == null)
		{
			return;
		}
		foreach (XmlNode item in from XmlNode x in extractNode.ChildNodes
			where x.Name.Equals("item")
			select x)
		{
			string innerText = item.GetInnerText();
			int num = 1;
			if (item.ContainsAttribute("amount") && int.TryParse(item.GetAttributeValue("amount"), out var result))
			{
				num = result;
			}
			if (element.Extractables.ContainsKey(innerText))
			{
				element.Extractables[innerText] += num;
			}
			else
			{
				element.Extractables.Add(innerText, num);
			}
		}
		XmlNode xmlNode = extractNode.ChildNodes.Cast<XmlNode>().FirstOrDefault((XmlNode x) => x.Name.Equals("description"));
		if (xmlNode != null)
		{
			element.ExtractableDescription = xmlNode.GetInnerText();
		}
		else
		{
			element.ExtractableDescription = "";
		}
		element.IsExtractable = element.Extractables.Any();
	}

	protected override void ParseSetterNodes(XmlNode elementNode, ElementBase element)
	{
		base.ParseSetterNodes(elementNode, element);
	}
}
