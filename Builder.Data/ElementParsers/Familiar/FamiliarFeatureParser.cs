using System.Xml;
using Builder.Data.Elements.Familiar;

namespace Builder.Data.ElementParsers.Familiar;

public sealed class FamiliarFeatureParser : ElementParser
{
	public override string ParserType => "Familiar Feature";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		return base.ParseElement(elementNode).Construct<FamiliarFeatureElement>();
	}
}
