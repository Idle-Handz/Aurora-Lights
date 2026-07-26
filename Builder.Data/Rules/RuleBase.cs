using System.Linq;

namespace Builder.Data.Rules;

public abstract class RuleBase
{
	public string RuleName { get; }

	public ElementHeader ElementHeader { get; set; }

	public ElementSetters Setters { get; set; }

	protected RuleBase(string ruleName, ElementHeader elementHeader)
	{
		ElementHeader = elementHeader;
		RuleName = ruleName;
		Setters = new ElementSetters();
	}

	public bool ContainsSetters()
	{
		return Setters?.Any() ?? false;
	}

	public override string ToString()
	{
		return RuleName + " (" + ElementHeader.Name + ")";
	}
}
