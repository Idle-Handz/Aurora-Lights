using System.Collections.Generic;
using System.Linq;
using System.Text;
using Builder.Data.Elements;

namespace Builder.Data;

public static class ElementDescriptionHelper
{
	public static string GenerateDescriptionBase(ElementBase element, IEnumerable<ElementBase> elements = null)
	{
		return element.Type switch
		{
			"Weapon" => GenerateWeaponDescription(element as WeaponElement, elements),
			"Armor" => GenerateArmorDescription(element as ArmorElement),
			"Item" => GenerateItemDescription(element as Item),
			"Magic Item" => GenerateMagicItemDescription(element as MagicItemElement),
			"Spell" => GenerateSpellDescription(element as Spell),
			_ => element.Description,
		};
	}

	private static string GenerateSpellDescription(Spell element)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<p class=\"underline\" style=\"padding-top:-5px\">" + element.GetShortDescription() + "</p>");
		stringBuilder.Append("<ul class=\"unstyled\">");
		stringBuilder.Append("<li><strong>Casting Time:</strong> " + element.CastingTime + "</li>");
		stringBuilder.Append("<li><strong>Range:</strong> " + element.Range + "</li>");
		stringBuilder.Append("<li><strong>Components:</strong> " + element.GetComponentsString() + "</li>");
		stringBuilder.Append("<li><strong>Duration:</strong> " + element.Duration + "</li>");
		stringBuilder.Append("</ul>");
		stringBuilder.Append("<br/>");
		stringBuilder.Append(element.Description);
		return stringBuilder.ToString();
	}

	private static string GenerateItemDescription(Item element)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (element.IsValuable)
		{
			stringBuilder.Append("<p class=\"underline\" style=\"padding-top:-5px\">");
			stringBuilder.Append(element.Category + ", " + element.DisplayPrice);
			stringBuilder.Append("</p>");
		}
		stringBuilder.Append(element.Description);
		return stringBuilder.ToString();
	}

	private static string GenerateMagicItemDescription(MagicItemElement element)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<p class=\"underline\" style=\"padding-top:-5px\">");
		if (string.IsNullOrWhiteSpace(element.ItemType))
		{
			stringBuilder.Append("Magic item");
		}
		else
		{
			string setterAdditionAttribute = element.GetSetterAdditionAttribute("type");
			stringBuilder.Append((setterAdditionAttribute != null) ? (element.ItemType + " (" + setterAdditionAttribute + ")") : (element.ItemType ?? ""));
		}
		if (string.IsNullOrWhiteSpace(element.Rarity))
		{
			stringBuilder.Append(" ");
		}
		else
		{
			stringBuilder.Append(", ");
			stringBuilder.Append(element.Rarity.ToLowerInvariant() + " ");
		}
		if (element.RequiresAttunement)
		{
			string setterAdditionAttribute2 = element.GetSetterAdditionAttribute("attunement");
			stringBuilder.Append((setterAdditionAttribute2 != null) ? ("(requires attunement " + setterAdditionAttribute2 + ")") : "(requires attunement)");
		}
		stringBuilder.Append("</p>");
		stringBuilder.Append(element.Description);
		return stringBuilder.ToString();
	}

	private static string GenerateArmorDescription(ArmorElement element)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("<p>");
		stringBuilder.AppendLine(element.ElementSetters.ContainsSetter("armorClass") ? ("<br/><b><i>Armor Class. </i></b>" + element.ElementSetters.GetSetter("armorClass").Value) : "");
		stringBuilder.AppendLine(element.ElementSetters.ContainsSetter("strength") ? ("<br/><b><i>Strength. </i></b>" + element.ElementSetters.GetSetter("strength").Value) : "<br/><b><i>Strength. </i></b>—");
		stringBuilder.AppendLine(element.ElementSetters.ContainsSetter("stealth") ? ("<br/><b><i>Stealth. </i></b>" + element.ElementSetters.GetSetter("stealth").Value) : "<br/><b><i>Stealth. </i></b>—");
		stringBuilder.AppendLine("</p>");
		stringBuilder.Append(element.Description);
		return stringBuilder.ToString();
	}

	private static string GenerateWeaponDescription(WeaponElement element, IEnumerable<ElementBase> elements)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (ElementBase item in (from x in elements
			where x.Type.Equals("Weapon Category")
			orderby x.Name
			select x).ToList())
		{
			if (element.Supports.Contains(item.Id))
			{
				stringBuilder.AppendLine("<p class=\"underline\">" + item.Name + " Weapon</p>");
			}
		}
		List<ElementBase> source = elements.Where((ElementBase x) => x.Type.Equals("Weapon Property")).ToList();
		stringBuilder.Append("<p>");
		List<string> list = new List<string>();
		foreach (string support in element.Supports)
		{
			ElementBase elementBase = source.FirstOrDefault((ElementBase x) => x.Id.Equals(support));
			if (elementBase == null)
			{
				continue;
			}
			string text = (list.Any() ? elementBase.Name.ToLowerInvariant() : elementBase.Name);
			switch (elementBase.Id)
			{
			case "ID_INTERNAL_WEAPON_PROPERTY_THROWN":
			case "ID_INTERNAL_WEAPON_PROPERTY_AMMUNITION":
				list.Add(text + " (" + element.Range + ")");
				break;
			case "ID_INTERNAL_WEAPON_PROPERTY_VERSATILE":
				list.Add(text + " (" + element.Versatile + ")");
				break;
			case "ID_INTERNAL_WEAPON_PROPERTY_SPECIAL":
				if (element.Supports.Count((string x) => x.Contains("WEAPON_PROPERTY_SPECIAL")) == 1)
				{
					list.Add(text + " (" + element.Versatile + ")");
				}
				break;
			default:
				list.Add(text);
				break;
			}
		}
		stringBuilder.AppendLine(list.Any() ? ("<b><i>Properties. </i></b>" + string.Join(", ", list.OrderBy((string x) => x))) : "<b><i>Properties. </i></b>—");
		stringBuilder.AppendLine("<br/><b><i>Damage. </i></b>" + element.Damage + " " + element.DamageType);
		stringBuilder.Append("</p>");
		stringBuilder.AppendLine("Proficiency with a " + element.Name.ToLowerInvariant() + " allows you to add your proficiency bonus to the attack roll for any attack you make with it.");
		stringBuilder.Append(element.Description);
		return stringBuilder.ToString();
	}
}
