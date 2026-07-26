using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class FeatFeatureParser : ElementParser
{
	public override string ParserType => "Feat Feature";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		return base.ParseElement(elementNode).Construct<FeatFeature>();
	}
}
