namespace Builder.Data;

public static class ElementSetterNameStrings
{
	public const string HitDice = "hd";

	public const string SpellcastingClass = "spellcastingClass";

	public const string SpellcastingAbility = "spellcastingAbility";

	public const string Male = "male";

	public const string Female = "female";

	public const string Category = "category";

	public const string Cost = "cost";

	public const string Weight = "weight";

	public const string Container = "container";

	public const string IsMagic = "isMagic";

	public const string Attunement = "attunement";

	public const string Rarity = "rarity";

	public static string[] All => new string[12]
	{
		"hd", "spellcastingClass", "spellcastingAbility", "male", "female", "category", "cost", "weight", "container", "isMagic",
		"attunement", "rarity"
	};
}
