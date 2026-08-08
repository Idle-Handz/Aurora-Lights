using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public class ArmorElementParser : ItemElementParser
{
	public override string ParserType => "Armor";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		ArmorElement armorElement = base.ParseElement(elementNode).ConstructFrom<ArmorElement, Item>();
		armorElement.Supports.Add(armorElement.Name);
		armorElement.Supports.Add(armorElement.Name.ToLowerInvariant());
		return armorElement;
	}
}
