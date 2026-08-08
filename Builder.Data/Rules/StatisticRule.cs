using Builder.Data.Rules.Attributes;

namespace Builder.Data.Rules;

public sealed class StatisticRule : RuleBase
{
	public StatisticAttributes Attributes { get; }

	public StatisticRule(ElementHeader parentHeader)
		: base("stat", parentHeader)
	{
		Attributes = new StatisticAttributes();
	}

	public override string ToString()
	{
		string text = (Attributes.HasAlt ? Attributes.Alt : base.ElementHeader.Name);
		return text + " name:" + Attributes.Name + " value:" + Attributes.Value;
	}
}
