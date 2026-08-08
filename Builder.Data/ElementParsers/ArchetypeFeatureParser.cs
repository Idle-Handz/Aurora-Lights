using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class ArchetypeFeatureParser : ElementParser
{
	public override string ParserType => "Archetype Feature";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		return base.ParseElement(elementNode).Construct<ArchetypeFeature>();
	}
}
