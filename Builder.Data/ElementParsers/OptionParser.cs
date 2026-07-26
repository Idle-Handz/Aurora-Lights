using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public class OptionParser : ElementParser
{
	public override string ParserType => "Option";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Option option = base.ParseElement(elementNode).Construct<Option>();
		option.IsInternal = option.Source == "Internal";
		option.IsDefault = option.ElementSetters.GetSetter("default")?.ValueAsBool() ?? false;
		return option;
	}
}
