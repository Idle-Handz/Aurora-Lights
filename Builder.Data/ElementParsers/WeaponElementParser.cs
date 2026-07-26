using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public class WeaponElementParser : ItemElementParser
{
	public override string ParserType => "Weapon";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		WeaponElement weaponElement = base.ParseElement(elementNode).ConstructFrom<WeaponElement, Item>();
		weaponElement.Supports.Add(weaponElement.Name);
		weaponElement.Supports.Add(weaponElement.Name.ToLowerInvariant());
		return weaponElement;
	}
}
