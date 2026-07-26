using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class DragonmarkParser : ElementParser
{
	public override string ParserType => "Dragonmark";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		return base.ParseElement(elementNode).Construct<Dragonmark>();
	}
}
