using System;

namespace Builder.Data.Rules.Attributes;

public class StatisticAttributes
{
	public string Name { get; set; }

	public string Value { get; set; }

	[Obsolete("use Bonus for stacking type")]
	public string Type { get; set; }

	public string Bonus => Type;

	public int Level { get; set; }

	public string Requirements { get; set; }

	public string Equipped { get; set; }

	[Obsolete]
	public string NotEquipped { get; set; }

	public bool Inline { get; set; }

	[Obsolete("use Maximum for capped value")]
	public string Cap { get; set; }

	public string Alt { get; set; }

	public bool Merge { get; set; }

	public string Minimum { get; set; }

	public string Maximum => Cap;

	public bool HasBonus => !string.IsNullOrWhiteSpace(Type);

	public bool HasEquipmentConditions => !string.IsNullOrWhiteSpace(Equipped);

	public bool HasRequirementsConditions => !string.IsNullOrWhiteSpace(Requirements);

	[Obsolete]
	public bool HasNotEquipmentConditions => !string.IsNullOrWhiteSpace(NotEquipped);

	[Obsolete("use HasMaximum instead of HasCap for new value")]
	public bool HasCap => !string.IsNullOrWhiteSpace(Cap);

	public bool HasMaximum => !string.IsNullOrWhiteSpace(Cap);

	public bool HasMinimum => !string.IsNullOrWhiteSpace(Minimum);

	public bool HasAlt => !string.IsNullOrWhiteSpace(Alt);

	public StatisticAttributes()
	{
		Level = 1;
	}

	public bool IsNumberValue()
	{
		int result;
		return int.TryParse(Value, out result);
	}

	public int GetValue()
	{
		return Convert.ToInt32(Value);
	}

	public bool MeetsLevelRequirement(int level)
	{
		return Level <= level;
	}

	public bool IsNumberCap()
	{
		int result;
		return int.TryParse(Cap, out result);
	}

	public int GetCapValue()
	{
		return Convert.ToInt32(Cap);
	}
}
