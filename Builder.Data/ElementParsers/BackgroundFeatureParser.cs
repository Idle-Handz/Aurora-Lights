using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class BackgroundFeatureParser : ElementParser
{
	public override string ParserType => "Background Feature";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		BackgroundFeature backgroundFeature = base.ParseElement(elementNode).Construct<BackgroundFeature>();
		backgroundFeature.IsPrimary = backgroundFeature.ElementSetters.GetSetter("primary")?.ValueAsBool() ?? false;
		return backgroundFeature;
	}
}
