using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public class MagicItemElementParser : ItemElementParser
{
	public override string ParserType => "Magic Item";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		MagicItemElement magicItemElement = base.ParseElement(elementNode).ConstructFrom<MagicItemElement, Item>();
		if (magicItemElement.ElementSetters.ContainsSetter("cost"))
		{
			magicItemElement.OverrideCost = true;
			if (magicItemElement.ElementSetters.GetSetter("cost").ContainsAttribute("override"))
			{
				magicItemElement.OverrideWeight = magicItemElement.GetSetterOverrideAttributeValue("cost");
			}
		}
		if (magicItemElement.ElementSetters.ContainsSetter("weight"))
		{
			magicItemElement.OverrideWeight = true;
			if (magicItemElement.ElementSetters.GetSetter("weight").ContainsAttribute("override"))
			{
				magicItemElement.OverrideWeight = magicItemElement.GetSetterOverrideAttributeValue("weight");
			}
		}
		return magicItemElement;
	}
}
