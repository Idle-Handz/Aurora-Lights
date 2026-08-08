using System.Collections.Generic;

namespace Builder.Data.Rules.Attributes;

public class SelectAttributes
{
	public string Type { get; set; }

	public string Name { get; set; }

	public int RequiredLevel { get; set; }

	public string Requirements { get; set; }

	public int Number { get; set; }

	public string Supports { get; set; }

	public bool Optional { get; set; }

	public string Default { get; set; }

	public string DefaultSelection { get; set; }

	public bool Existing { get; set; }

	public string SpellcastingName { get; set; }

	public bool MultipleNumberCount => Number > 1;

	public bool IsList => Type.Equals("List");

	public List<SelectionRuleListItem> ListItems { get; set; } = new List<SelectionRuleListItem>();

	public string ListSelectionInlineStatisticName { get; set; }

	public SelectAttributes()
	{
		Number = 1;
		Optional = false;
		RequiredLevel = 1;
		Existing = false;
	}

	public bool MeetsLevelRequirement(int level)
	{
		return RequiredLevel <= level;
	}

	public bool ContainsSpellcastingName()
	{
		return !string.IsNullOrWhiteSpace(SpellcastingName);
	}

	public bool ContainsSupports()
	{
		return !string.IsNullOrWhiteSpace(Supports);
	}

	public bool ContainsDefaultSelection()
	{
		return !string.IsNullOrWhiteSpace(Default);
	}

	public bool ContainsDynamicSupports()
	{
		return Supports.Contains("$(");
	}

	public bool SupportsElementIdRange()
	{
		if (Supports == null)
		{
			return false;
		}
		if (Supports.Contains("|"))
		{
			return !Supports.Contains("||");
		}
		return false;
	}
}
