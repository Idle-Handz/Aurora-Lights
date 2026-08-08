using System;
using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class ClassFeatureParser : ElementParser
{
	public override string ParserType => "Class Feature";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		ClassFeature classFeature = base.ParseElement(elementNode).Construct<ClassFeature>();
		if (classFeature.ElementSetters.ContainsSetter("Allow Duplicate"))
		{
			classFeature.AllowDuplicate = Convert.ToBoolean(classFeature.ElementSetters.GetSetter("Allow Duplicate").Value);
		}
		return classFeature;
	}
}
