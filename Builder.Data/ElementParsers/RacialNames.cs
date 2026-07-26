using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Builder.Data.ElementParsers;

public class RacialNames
{
	public class RacialNamesCollection : List<string>
	{
		public string Type { get; }

		public RacialNamesCollection(string type)
		{
			Type = type;
		}
	}

	private readonly Random _rnd;

	public List<RacialNamesCollection> NameCollections { get; } = new List<RacialNamesCollection>();

	public string RandomizeNameFormat { get; set; } = "$(name)";

	public RacialNames()
	{
		_rnd = new Random(Environment.TickCount);
	}

	public RacialNamesCollection GetCollection(string type)
	{
		return NameCollections.FirstOrDefault((RacialNamesCollection x) => x.Type == type);
	}

	public bool ContainsCollection(string type)
	{
		return NameCollections.Any((RacialNamesCollection x) => x.Type == type);
	}

	public string GenerateRandomName(string name = "male")
	{
		string text = RandomizeNameFormat.Replace("$(name)", "$(" + name + ")");
		text = text.Replace("{{name}}", "{{" + name + "}}");
		foreach (RacialNamesCollection nameCollection in NameCollections)
		{
			text = text.Replace("$(" + nameCollection.Type + ")", nameCollection[_rnd.Next(nameCollection.Count)]);
		}
		foreach (Match item in Regex.Matches(text, "{{(.*?)}}"))
		{
			string text2 = item.Value.Substring(2, item.Value.Length - 4).Trim();
			foreach (RacialNamesCollection nameCollection2 in NameCollections)
			{
				if (text2.Equals(nameCollection2.Type))
				{
					text = text.Replace(item.Value, nameCollection2[_rnd.Next(nameCollection2.Count)]);
				}
			}
		}
		return text;
	}
}
