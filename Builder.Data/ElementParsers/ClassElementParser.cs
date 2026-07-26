using System.Xml;
using Builder.Data.Elements;
using Builder.Data.Extensions;

namespace Builder.Data.ElementParsers;

public class ClassElementParser : ElementParser
{
	public override string ParserType => "Class";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Class obj = base.ParseElement(elementNode).Construct<Class>();
		ValidateElementSetters(obj, "hd");
		obj.HitDice = obj.ElementSetters.GetSetter("hd").Value;
		if (obj.ElementSetters.ContainsSetter("short"))
		{
			obj.Short = obj.ElementSetters.GetSetter("short").Value;
		}
		XmlElement xmlElement = elementNode["multiclass"];
		if (xmlElement != null)
		{
			obj.MulticlassId = xmlElement.GetAttributeValue("id");
			obj.CanMulticlass = true;
		}
		return obj;
	}
}
