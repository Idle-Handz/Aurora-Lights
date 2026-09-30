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

	private readonly HashSet<SpellcastingList> _mergedExtensions = new HashSet<SpellcastingList>();

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

	/// <summary>Copies owned lists while retaining the identity of lists borrowed from other features.</summary>
	public SpellcastingInformation CloneForSelection()
	{
		var copy = new SpellcastingInformation(ElementHeader)
		{
			Name = Name,
			AbilityName = AbilityName,
			Prepare = Prepare,
			PrepareFromSpellList = PrepareFromSpellList,
			AssignToAllSpellcastingClasses = AssignToAllSpellcastingClasses,
			AllowSpellSwap = AllowSpellSwap,
			IsExtension = IsExtension,
		};
		if (InitialSupportedSpellsExpression is { } initial)
			copy.InitialSupportedSpellsExpression = new SpellcastingList(initial.Supports, initial.Known);
		foreach (var list in ExtendedSupportedSpellsExpressions)
		{
			if (_mergedExtensions.Contains(list))
				copy.MergeExtended(new List<SpellcastingList> { list });
			else
				copy.ExtendedSupportedSpellsExpressions.Add(new SpellcastingList(list.Supports, list.Known));
		}
		return copy;
	}

	public void MergeExtended(List<SpellcastingList> extendedSupportedSpellsExpressions)
	{
		foreach (SpellcastingList extendedSupportedSpellsExpression in extendedSupportedSpellsExpressions)
		{
			if (!ExtendedSupportedSpellsExpressions.Contains(extendedSupportedSpellsExpression))
			{
				ExtendedSupportedSpellsExpressions.Add(extendedSupportedSpellsExpression);
				_mergedExtensions.Add(extendedSupportedSpellsExpression);
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
				_mergedExtensions.Remove(expression);
				continue;
			}
			SpellcastingList spellcastingList = ExtendedSupportedSpellsExpressions.FirstOrDefault((SpellcastingList x) => x.UniqueIdentifier.Equals(expression.UniqueIdentifier));
			if (spellcastingList != null)
			{
				ExtendedSupportedSpellsExpressions.Remove(spellcastingList);
				_mergedExtensions.Remove(spellcastingList);
			}
		}
	}
}
