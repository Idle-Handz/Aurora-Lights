using System;

namespace Builder.Data;

public class MissingSetterException : Exception
{
	public ElementHeader ElementHeader { get; }

	public string RequiredSetterName { get; }

	public MissingSetterException(ElementHeader elementHeader, string requiredSetterName)
		: base($"the required setter '{requiredSetterName}' is missing on the '{elementHeader}' element.")
	{
		ElementHeader = elementHeader;
		RequiredSetterName = requiredSetterName;
	}
}
