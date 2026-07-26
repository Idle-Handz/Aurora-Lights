using System.Xml;
using Builder.Data.Elements.Familiar;

namespace Builder.Data.ElementParsers.Familiar;

public sealed class FamiliarActionParser : ElementParser
{
	public override string ParserType => "Familiar Action";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		return base.ParseElement(elementNode).Construct<FamiliarActionElement>();
	}
}
