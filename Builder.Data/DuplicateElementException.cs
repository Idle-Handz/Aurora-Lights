using System;

namespace Builder.Data;

public class DuplicateElementException : Exception
{
	public DuplicateElementException(string elementName, string filename)
		: base("Duplicated ID on '" + elementName + "' in '" + filename + "'")
	{
	}
}
