using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class DeityElementParser : ElementParser
{
	public override string ParserType => "Deity";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Deity deity = base.ParseElement(elementNode).Construct<Deity>();
		ValidateElementSetters(deity, "alignment", "domains", "symbol");
		deity.Alignment = deity.ElementSetters.GetSetter("alignment").Value;
		deity.Domains = deity.ElementSetters.GetSetter("domains").Value;
		deity.Symbol = deity.ElementSetters.GetSetter("symbol").Value;
		deity.Gender = deity.ElementSetters.GetSetter("gender")?.Value ?? "";
		return deity;
	}
}
