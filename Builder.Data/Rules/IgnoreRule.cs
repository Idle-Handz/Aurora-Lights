namespace Builder.Data.Rules;

public sealed class IgnoreRule : RuleBase
{
	public string ID { get; set; }

	public IgnoreRule(ElementHeader parentHeader)
		: base("ignore", parentHeader)
	{
	}
}
