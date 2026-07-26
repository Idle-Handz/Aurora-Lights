using System.Linq;

namespace Builder.Data;

public static class ElementsHelper
{
	public static string SanitizeID(string input)
	{
		string text = input.Trim();
		string[] array = new string[3] { " ", "-", "/" };
		foreach (string oldValue in array)
		{
			text = text.Replace(oldValue, "_");
		}
		foreach (char item in from x in text.ToCharArray()
			where !char.IsLetterOrDigit(x) && !x.Equals('_')
			select x)
		{
			text = text.Replace(item.ToString(), "");
		}
		return text.ToUpperInvariant();
	}

	public static bool ValidateID(string input)
	{
		string text = SanitizeID(input);
		if (text.StartsWith("ID_"))
		{
			return text.Equals(input);
		}
		return false;
	}
}
