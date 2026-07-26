using System.Collections.Generic;

namespace Builder.Data;

public static class ElementTypeStrings
{
	public const string Level = "Level";

	public const string Alignment = "Alignment";

	public const string Size = "Size";

	public const string Vision = "Vision";

	public const string Proficiency = "Proficiency";

	public const string Race = "Race";

	public const string SubRace = "Sub Race";

	public const string RacialTrait = "Racial Trait";

	public const string Multiclass = "Multiclass";

	public const string Class = "Class";

	public const string ClassFeature = "Class Feature";

	public const string Archetype = "Archetype";

	public const string ArchetypeFeature = "Archetype Feature";

	public const string Language = "Language";

	public const string LanguageFeature = "Language Feature";

	public const string Background = "Background";

	public const string BackgroundFeature = "Background Feature";

	public const string Skill = "Skill";

	public const string Feat = "Feat";

	public const string Spell = "Spell";

	public const string Item = "Item";

	public const string MagicItem = "Magic Item";

	public const string ItemPack = "Item Pack";

	public const string Armor = "Armor";

	public const string Weapon = "Weapon";

	public const string RaceVariant = "Race Variant";

	public const string BackgroundVariant = "Background Variant";

	public const string AbilityScoreImprovement = "Ability Score Improvement";

	public const string FeatFeature = "Feat Feature";

	public const string Core = "Core";

	public const string Option = "Option";

	public const string Internal = "Internal";

	public const string Grants = "Grants";

	public const string Support = "Support";

	public const string BackgroundCharacteristics = "Background Characteristics";

	public const string Source = "Source";

	public const string Deity = "Deity";

	public const string WeaponProperty = "Weapon Property";

	public const string WeaponCategory = "Weapon Category";

	public const string ArmorGroup = "Armor Group";

	public const string WeaponGroup = "Weapon Group";

	public const string Condition = "Condition";

	public const string Monster = "Monster";

	public const string MonsterTrait = "Monster Trait";

	public const string MonsterAction = "Monster Action";

	public const string Companion = "Companion";

	public const string CompanionFeature = "Companion Feature";

	public const string CompanionTrait = "Companion Trait";

	public const string CompanionAction = "Companion Action";

	public const string CompanionReaction = "Companion Reaction";

	public const string Familiar = "Familiar";

	public const string FamiliarFeature = "Familiar Feature";

	public const string FamiliarTrait = "Familiar Trait";

	public const string FamiliarAction = "Familiar Action";

	public const string Dragonmark = "Dragonmark";

	public const string DragonmarkFeature = "Dragonmark Feature";

	public static string[] All => new List<string>
	{
		"Core", "Option", "Internal", "Grants", "Support", "Background Characteristics", "Source", "Deity", "Level", "Alignment",
		"Proficiency", "Race", "Sub Race", "Racial Trait", "Multiclass", "Class", "Class Feature", "Archetype", "Archetype Feature", "Language",
		"Language Feature", "Background", "Background Feature", "Background Variant", "Race Variant", "Feat", "Spell", "Item", "Armor", "Weapon",
		"Magic Item", "Item Pack", "Weapon Property", "Weapon Category", "Armor Group", "Weapon Group", "Ability Score Improvement", "Feat Feature", "Monster", "Monster Trait",
		"Monster Action", "Condition", "Companion", "Companion Feature", "Companion Trait", "Companion Action", "Companion Reaction", "Familiar", "Familiar Feature", "Familiar Trait",
		"Familiar Action", "Dragonmark", "Dragonmark Feature"
	}.ToArray();
}
