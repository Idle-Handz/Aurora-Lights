using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class LevelElementParser : ElementParser
{
	private const string LevelSetter = "level";

	private const string ExperienceSetter = "experience";

	public override string ParserType => "Level";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		LevelElement levelElement = base.ParseElement(elementNode).Construct<LevelElement>();
		ValidateElementSetters(levelElement, "level", "experience");
		levelElement.Level = levelElement.ElementSetters.GetSetter("level").ValueAsInteger();
		levelElement.RequiredExperience = levelElement.ElementSetters.GetSetter("experience").ValueAsInteger();
		return levelElement;
	}
}
