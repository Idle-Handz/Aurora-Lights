using System;
using System.Collections.Generic;

namespace Builder.Data.Elements;

public class Item : ElementBase
{
	public override bool AllowMultipleElements => true;

	public string Category { get; set; }

	public int Cost { get; set; }

	public string Currency { get; set; }

	public string CurrencyAbbreviation { get; set; }

	[Obsolete("item base doesn't need override, magic item has OverrideCost property")]
	public bool CurrencyOverride { get; set; }

	public string DisplayPrice
	{
		get
		{
			if (Cost == 0)
			{
				return "—";
			}
			return $"{Cost} {CurrencyAbbreviation?.ToLowerInvariant()}";
		}
	}

	public string Weight { get; set; }

	public decimal CalculableWeight { get; set; }

	public bool IncludeInCarriedWeight { get; set; } = true;

	public string DisplayWeight
	{
		get
		{
			if (CalculableWeight == 0m)
			{
				return "—";
			}
			return Weight;
		}
	}

	public bool IsStackable { get; set; }

	public bool IsStash { get; set; }

	public bool IsWeightlessStash { get; set; }

	public bool IsConsumable { get; set; }

	public bool RequiresAttunement { get; set; }

	public string Rarity { get; set; }

	public string ItemType { get; set; }

	public bool IsValuable { get; set; }

	public string Slot { get; set; }

	public List<string> Slots { get; set; } = new List<string>();

	public bool IsEquippable => !string.IsNullOrWhiteSpace(Slot);

	public bool HasMultipleSlots => Slots.Count > 1;

	public string ItemSupports { get; set; }

	public bool RequiresParentItem => !string.IsNullOrWhiteSpace(ItemSupports);

	public string NameFormat { get; set; }

	[Obsolete]
	public string Container { get; set; }

	public string Enhancement { get; set; }

	public string Damage { get; set; }

	public string DamageType { get; set; }

	public string DamageAbilityModifier { get; set; }

	public string DisplayDamage => Damage + " " + DamageType?.ToLowerInvariant();

	public string Range { get; set; }

	public string Versatile { get; set; }

	public bool HasVersatile => !string.IsNullOrWhiteSpace(Versatile);

	public string DisplayArmorClass { get; set; }

	public string DisplayStrength { get; set; }

	public string DisplayStealth { get; set; }

	public List<string> ArmorGroups { get; } = new List<string>();

	public List<string> WeaponGroups { get; } = new List<string>();

	public List<string> WeaponProperties { get; } = new List<string>();

	public string DisplayArmorGroups => string.Join(", ", ArmorGroups);

	public string DisplayWeaponGroups => string.Join(", ", WeaponGroups);

	public string DisplayWeaponProperties => string.Join(", ", WeaponProperties);

	public bool IsExtractable { get; set; }

	public string ExtractableDescription { get; set; }

	public bool HasExtractableDescription => !string.IsNullOrWhiteSpace(ExtractableDescription);

	public Dictionary<string, int> Extractables { get; } = new Dictionary<string, int>();

	public bool ExcludeFromEncumbrance { get; set; }

	public bool HideFromInventory { get; set; }

	public override string ToString()
	{
		return base.Name + " (" + Category + ")";
	}
}
