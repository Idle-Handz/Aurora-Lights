using System.Collections.Generic;

namespace Builder.Data.Rules.Strings;

public static class RuleStrings
{
	public static class GrantStrings
	{
		public const string Type = "type";

		public const string Name = "name";

		public const string RequiredLevel = "level";

		public const string Requirements = "requirements";

		public const string Spellcasting = "spellcasting";

		public const string Prepared = "prepared";

		public static IEnumerable<string> Strings
		{
			get
			{
				yield return "type";
				yield return "name";
				yield return "level";
				yield return "requirements";
				yield return "spellcasting";
				yield return "prepared";
			}
		}

		public static IEnumerable<string> Required
		{
			get
			{
				yield return "type";
				yield return "name";
			}
		}
	}

	public static class SelectStrings
	{
		public const string Type = "type";

		public const string Name = "name";

		public const string Number = "number";

		public const string RequiredLevel = "level";

		public const string Requirements = "requirements";

		public const string Supports = "supports";

		public const string Optional = "optional";

		public const string Default = "default";

		public const string DefaultSelection = "default-behaviour";

		public const string Existing = "existing";

		public const string Spellcasting = "spellcasting";

		public static IEnumerable<string> Strings
		{
			get
			{
				yield return "type";
				yield return "name";
				yield return "number";
				yield return "level";
				yield return "requirements";
				yield return "supports";
				yield return "optional";
				yield return "default";
				yield return "default-behaviour";
				yield return "existing";
				yield return "spellcasting";
			}
		}

		public static IEnumerable<string> Required
		{
			get
			{
				yield return "type";
				yield return "name";
			}
		}
	}

	public static class StatisticStrings
	{
		public const string Name = "name";

		public const string Value = "value";

		public const string Type = "bonus";

		public const string Bonus = "bonus";

		public const string RequiredLevel = "level";

		public const string Requirements = "requirements";

		public const string Inline = "inline";

		public const string Equipped = "equipped";

		public const string NotEquipped = "not-equipped";

		public const string Cap = "cap";

		public const string Maximum = "maximum";

		public const string Minimum = "minimum";

		public const string Alt = "alt";

		public const string Merge = "merge";

		public static IEnumerable<string> Strings
		{
			get
			{
				yield return "name";
				yield return "value";
				yield return "bonus";
				yield return "bonus";
				yield return "level";
				yield return "requirements";
				yield return "inline";
				yield return "equipped";
				yield return "not-equipped";
				yield return "cap";
				yield return "alt";
				yield return "merge";
				yield return "maximum";
				yield return "minimum";
			}
		}

		public static IEnumerable<string> Required
		{
			get
			{
				yield return "name";
			}
		}
	}

	public const string Grant = "grant";

	public const string Select = "select";

	public const string Statistic = "stat";

	public static IEnumerable<string> Strings
	{
		get
		{
			yield return "grant";
			yield return "select";
			yield return "stat";
		}
	}
}
