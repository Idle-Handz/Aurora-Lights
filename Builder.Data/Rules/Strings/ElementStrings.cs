using System.Collections.Generic;

namespace Builder.Data.Rules.Strings;

public static class ElementStrings
{
	public static class TypeStrings
	{
		public const string Core = "Core";

		public const string Internal = "Internal";

		public const string Grants = "Grants";

		public const string Level = "Level";

		public const string Alignment = "Alignment";

		public const string Deity = "Deity";

		public const string Size = "Size";

		public const string Vision = "Vision";

		public const string Proficiency = "Proficiency";

		public const string Race = "Race";

		public const string SubRace = "Sub Race";

		public const string RacialTrait = "Racial Trait";

		public const string Class = "Class";

		public const string ClassFeature = "Class Feature";

		public const string Archetype = "Archetype";

		public const string ArchetypeFeature = "Archetype Feature";

		public const string Language = "Language";

		public const string Background = "Background";

		public const string BackgroundFeature = "Background Feature";

		public const string Skill = "Skill";

		public const string Feat = "Feat";

		public const string Spell = "Spell";

		public const string Item = "Item";

		public const string ItemPack = "Item Pack";

		public const string RaceVariant = "Race Variant";

		public const string BackgroundVariant = "Background Variant";

		public const string AbilityScoreImprovement = "Ability Score Improvement";

		public const string FeatFeature = "Feat Feature";

		public static IEnumerable<string> Strings
		{
			get
			{
				yield return "Core";
				yield return "Internal";
				yield return "Grants";
				yield return "Level";
				yield return "Ability Score Improvement";
				yield return "Alignment";
				yield return "Deity";
				yield return "Size";
				yield return "Vision";
				yield return "Race";
				yield return "Sub Race";
				yield return "Racial Trait";
				yield return "Race Variant";
				yield return "Class";
				yield return "Class Feature";
				yield return "Archetype";
				yield return "Archetype Feature";
				yield return "Language";
				yield return "Background";
				yield return "Background Feature";
				yield return "Background Variant";
				yield return "Skill";
				yield return "Proficiency";
				yield return "Feat";
				yield return "Feat Feature";
				yield return "Spell";
				yield return "Item";
				yield return "Item Pack";
			}
		}
	}

	public const string Supports = "supports";

	public const string Requirements = "requirements";

	public const string Description = "description";

	public const string Sheet = "sheet";

	public const string Setters = "setters";

	public const string Rules = "rules";
}
