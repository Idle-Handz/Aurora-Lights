using System.Linq;
using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class RaceElementParser : ElementParser
{
	public override string ParserType => "Race";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Race race = base.ParseElement(elementNode).Construct<Race>();
		if (race.ElementSetters.ContainsSetter("names"))
		{
			foreach (ElementSetters.Setter item in race.ElementSetters.Where((ElementSetters.Setter x) => x.Name.Equals("names")))
			{
				RacialNames.RacialNamesCollection racialNamesCollection = new RacialNames.RacialNamesCollection(item.AdditionalAttributes["type"]);
				racialNamesCollection.AddRange(from s in item.Value.Split(',')
					select s.Trim());
				race.Names.NameCollections.Add(racialNamesCollection);
			}
			if (race.ElementSetters.ContainsSetter("names-format"))
			{
				race.Names.RandomizeNameFormat = race.ElementSetters.GetSetter("names-format").Value;
			}
			else
			{
				race.Names.RandomizeNameFormat = "$(name)";
			}
			if (race.Names.ContainsCollection("male"))
			{
				race.MaleNames.AddRange(race.Names.GetCollection("male"));
			}
			if (race.Names.ContainsCollection("female"))
			{
				race.MaleNames.AddRange(race.Names.GetCollection("female"));
			}
		}
		if (race.MaleNames.Count == 0)
		{
			race.MaleNames.Add("no male names");
		}
		if (race.FemaleNames.Count == 0)
		{
			race.FemaleNames.Add("no female names");
		}
		if (race.ElementSetters.ContainsSetter("height"))
		{
			race.BaseHeight = race.ElementSetters.GetSetter("height").Value;
		}
		if (race.ElementSetters.ContainsSetter("weight"))
		{
			race.BaseWeight = race.ElementSetters.GetSetter("weight").Value;
		}
		return race;
	}
}
