using System;
using System.Runtime.Serialization;

namespace Builder.Data.Files;

[Serializable]
public class ElementsFileLoadException : Exception
{
	public ElementsFileLoadException()
	{
	}

	public ElementsFileLoadException(string message)
		: base(message)
	{
	}

	public ElementsFileLoadException(string message, Exception inner)
		: base(message, inner)
	{
	}

	protected ElementsFileLoadException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
