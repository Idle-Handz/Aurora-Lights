using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Rules;

namespace Builder.Data.ElementParsers;

public class MulticlassElementParser : ElementParser
{
	public override string ParserType => "Multiclass";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Multiclass multiclass = (new ClassElementParser().ParseElement(elementNode) as Class).ConstructFrom<Multiclass, Class>();
		if (multiclass.CanMulticlass)
		{
			XmlElement xmlElement = elementNode["multiclass"];
			ElementHeader elementHeader = new ElementHeader(multiclass.Name, "Multiclass", multiclass.Source, xmlElement.GetAttributeValue("id"));
			if (multiclass.Id.Equals(elementHeader.Id))
			{
				throw new Exception("Invalid multiclass ID on " + multiclass.Name + ". The ID of your multiclass is the same as your class. You can change _CLASS_ to _MULTICLASS_ in the ID of your multiclass.");
			}
			multiclass.ElementHeader = elementHeader;
			XmlNode xmlNode = xmlElement["prerequisite"];
			if (xmlNode != null)
			{
				multiclass.MulticlassPrerequisites = xmlNode.GetInnerText();
				if (multiclass.HasPrerequisites)
				{
					multiclass.Prerequisite = multiclass.Prerequisite + ", " + multiclass.MulticlassPrerequisites;
				}
				else
				{
					multiclass.Prerequisite = multiclass.MulticlassPrerequisites;
				}
			}
			XmlNode xmlNode2 = xmlElement["requirements"];
			if (xmlNode2 != null)
			{
				multiclass.MulticlassRequirements = xmlNode2.GetInnerText();
				if (multiclass.HasRequirements)
				{
					multiclass.Requirements = multiclass.Requirements + "&&" + multiclass.MulticlassRequirements;
				}
				else
				{
					multiclass.Requirements = multiclass.MulticlassRequirements;
				}
			}
			XmlNode xmlNode3 = xmlElement["description"];
			if (xmlNode3 != null)
			{
				multiclass.MulticlassDescription = ((xmlNode3.FirstChild.NodeType == XmlNodeType.CDATA) ? xmlNode3.FirstChild.GetInnerText() : xmlNode3.InnerXml);
				multiclass.Description = multiclass.MulticlassDescription + multiclass.Description;
			}
			base.ParseSetterNodes(xmlElement, multiclass);
			if (multiclass.ElementSetters.ContainsSetter("multiclass proficiencies"))
			{
				multiclass.MulticlassProficiencies = multiclass.ElementSetters.GetSetter("multiclass proficiencies").Value;
			}
			else
			{
				multiclass.MulticlassProficiencies = "—";
			}
			if (xmlElement["rules"] != null)
			{
				List<RuleBase> collection = multiclass.Rules.Select((RuleBase x) => x.Copy()).ToList();
				ParseRules(xmlElement, multiclass);
				multiclass.Rules.InsertRange(0, collection);
			}
			return multiclass;
		}
		return null;
	}
}
