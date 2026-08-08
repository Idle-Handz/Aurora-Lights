using Builder.Data.Rules.Attributes;

namespace Builder.Data.Rules;

public sealed class GrantRule : RuleBase
{
	public GrantAttributes Attributes { get; }

	public GrantRule(ElementHeader parentHeader)
		: base("grant", parentHeader)
	{
		Attributes = new GrantAttributes();
	}

	public bool HasRequirements()
	{
		return Attributes.HasRequirements();
	}

	public override string ToString()
	{
		return base.ToString() + " type:" + Attributes.Type + " name:" + Attributes.Name;
	}
}
