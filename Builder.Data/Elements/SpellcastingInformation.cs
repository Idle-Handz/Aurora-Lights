using System;
using System.Collections.Generic;
using System.Linq;
using Builder.Core.Logging;

namespace Builder.Data.Elements;

public class SpellcastingInformation
{
	public class SpellcastingList
	{
		public string UniqueIdentifier { get; private set; }

		public bool Known { get; set; }

		public string Supports { get; set; }

		public bool IsId => Supports.Trim().StartsWith("ID_");

		public SpellcastingList(string supports, bool known = false)
		{
			Supports = supports;
			Known = known;
			UniqueIdentifier = Guid.NewGuid().ToString("D");
		}

		public override string ToString()
		{
			return Supports;
		}
	}

	public string UniqueIdentifier { get; private set; }

	public ElementHeader ElementHeader { get; }

	public string Name { get; set; }

	public string AbilityName { get; set; }

	public SpellcastingList InitialSupportedSpellsExpression { get; set; }

	public bool Prepare { get; set; }

	public bool PrepareFromSpellList { get; set; }

	public bool AssignToAllSpellcastingClasses { get; set; }

	public bool AllowSpellSwap { get; set; }

	public bool IsExtension { get; set; }

	public List<SpellcastingList> ExtendedSupportedSpellsExpressions { get; } = new List<SpellcastingList>();

	public SpellcastingInformation(ElementHeader elementHeader)
	{
		UniqueIdentifier = Guid.NewGuid().ToString("D");
		ElementHeader = elementHeader;
	}

	public bool ContainsInitialSpellcastingList()
	{
		return InitialSupportedSpellsExpression != null;
	}

	public bool ContainsExtendedSpellcastingList()
	{
		return ExtendedSupportedSpellsExpressions.Any();
	}

	public string GetPrepareAmountStatisticName()
	{
		return (Name + ":spellcasting:prepare").ToLowerInvariant();
	}

	public string GetKnownSpellsAmountStatisticName()
	{
		return (Name + ":spellcasting:spells known").ToLowerInvariant();
	}

	public string GetCantripAmountStatisticName()
	{
		return (Name + ":spellcasting:cantrips known").ToLowerInvariant();
	}

	public string GetSlotStatisticName(int slot)
	{
		return $"{Name}:spellcasting:slots:{slot}".ToLowerInvariant();
	}

	public string GetSpellAttackStatisticName()
	{
		return ("spellcasting:attack:" + AbilityName.Substring(0, 3)).ToLowerInvariant();
	}

	public string GetSpellSaveStatisticName()
	{
		return ("spellcasting:dc:" + AbilityName.Substring(0, 3)).ToLowerInvariant();
	}

	public string GetSpellcasterSpellAttackStatisticName()
	{
		return (Name + ":spellcasting:attack").ToLowerInvariant();
	}

	public string GetSpellcasterSpellSaveStatisticName()
	{
		return (Name + ":spellcasting:dc").ToLowerInvariant();
	}

	public override string ToString()
	{
		return $"{Name} [ex:{IsExtension}] [prep:{Prepare}] [from known list:{PrepareFromSpellList}]";
	}

	public void MergeExtended(List<SpellcastingList> extendedSupportedSpellsExpressions)
	{
		foreach (SpellcastingList extendedSupportedSpellsExpression in extendedSupportedSpellsExpressions)
		{
			if (!ExtendedSupportedSpellsExpressions.Contains(extendedSupportedSpellsExpression))
			{
				ExtendedSupportedSpellsExpressions.Add(extendedSupportedSpellsExpression);
			}
			else
			{
				Logger.Warning($"trying to merge existing extends into {this}");
			}
		}
	}

	public void Unmerge(List<SpellcastingList> extendedSupportedSpellsExpressions)
	{
		foreach (SpellcastingList expression in extendedSupportedSpellsExpressions)
		{
			if (ExtendedSupportedSpellsExpressions.Contains(expression))
			{
				ExtendedSupportedSpellsExpressions.Remove(expression);
				continue;
			}
			SpellcastingList spellcastingList = ExtendedSupportedSpellsExpressions.FirstOrDefault((SpellcastingList x) => x.UniqueIdentifier.Equals(expression.UniqueIdentifier));
			if (spellcastingList != null)
			{
				ExtendedSupportedSpellsExpressions.Remove(spellcastingList);
			}
		}
	}
}
