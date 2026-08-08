using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class RacialTraitElementParser : ElementParser
{
	public override string ParserType => "Racial Trait";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		return base.ParseElement(elementNode).Construct<RacialTrait>();
	}
}
