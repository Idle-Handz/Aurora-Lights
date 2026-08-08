using System;

namespace Builder.Data.Rules.Attributes;

public sealed class GrantAttributes
{
	public string Type { get; set; }

	[Obsolete("use ID property")]
	public string Name { get; set; }

	public int RequiredLevel { get; set; }

	public string Requirements { get; set; }

	public string Id => Name;

	public GrantAttributes()
	{
		RequiredLevel = 1;
	}

	public bool HasRequirements()
	{
		return !string.IsNullOrWhiteSpace(Requirements);
	}

	public bool MeetsLevelRequirement(int level)
	{
		return RequiredLevel <= level;
	}
}
