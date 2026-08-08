using System;
using System.Collections.Generic;
using System.Linq;

namespace Builder.Data;

public class ElementSetters : List<ElementSetters.Setter>
{
	public class Setter
	{
		public string Name { get; set; }

		public string Value { get; set; }

		public Dictionary<string, string> AdditionalAttributes { get; }

		public bool HasAdditionalAttributes => AdditionalAttributes.Any();

		public Setter(string name, string value)
		{
			Name = name;
			Value = value.Trim();
			AdditionalAttributes = new Dictionary<string, string>();
		}

		public bool ContainsAttribute(string name)
		{
			if (!HasAdditionalAttributes)
			{
				return false;
			}
			return AdditionalAttributes.ContainsKey(name);
		}

		public int ValueAsInteger()
		{
			return Convert.ToInt32(Value);
		}

		public bool ValueAsBool()
		{
			if (!string.IsNullOrWhiteSpace(Value))
			{
				return Convert.ToBoolean(Value);
			}
			return false;
		}

		public override string ToString()
		{
			return Name + ":" + Value;
		}
	}

	public bool ContainsSetter(string name)
	{
		return this.Any((Setter x) => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
	}

	public Setter GetSetter(string name)
	{
		return this.FirstOrDefault((Setter x) => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
	}

	public bool AttemptGetSetterValue(string name, out Setter setter)
	{
		setter = null;
		if (ContainsSetter(name))
		{
			setter = GetSetter(name);
			return true;
		}
		return false;
	}
}
