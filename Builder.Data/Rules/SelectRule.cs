using System;
using Builder.Data.Rules.Attributes;

namespace Builder.Data.Rules;

public class SelectRule : RuleBase
{
	public SelectAttributes Attributes { get; }

	public string UniqueIdentifier { get; private set; }

	public bool HasRenewedIdentifier { get; set; }

	public SelectRule(ElementHeader elementHeader)
		: base("select", elementHeader)
	{
		Attributes = new SelectAttributes();
		UniqueIdentifier = Guid.NewGuid().ToString("D");
	}

	public override string ToString()
	{
		return Attributes.Name + " [" + base.ToString() + "] " + UniqueIdentifier;
	}

	public void RenewIdentifier()
	{
		UniqueIdentifier = Guid.NewGuid().ToString("D");
		HasRenewedIdentifier = true;
	}
}
