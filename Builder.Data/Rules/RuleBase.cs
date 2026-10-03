using System;
using System.Collections.Generic;
using System.Linq;

namespace Builder.Data.Rules;

public abstract class RuleBase
{
	public string RuleName { get; }

	public ElementHeader ElementHeader { get; set; }

	public ElementSetters Setters { get; set; }

	/// <summary>
	/// Attributes the parsers did not recognise, kept as written instead of discarded. A host that
	/// understands an attribute this version does not can still read it, and nothing here changes how
	/// the rule behaves.
	/// </summary>
	public IDictionary<string, string> PreservedAttributes { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

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
