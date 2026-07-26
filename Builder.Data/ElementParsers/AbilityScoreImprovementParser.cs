using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class AbilityScoreImprovementParser : ElementParser
{
	public override string ParserType => "Ability Score Improvement";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		return base.ParseElement(elementNode).Construct<AbilityScoreImprovement>();
	}
}
