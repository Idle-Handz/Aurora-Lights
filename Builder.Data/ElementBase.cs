using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using Builder.Data.Elements;
using Builder.Data.Rules;

namespace Builder.Data;

public class ElementBase
{
	public ElementBaseCollection RuleElements { get; set; }

	public Dictionary<string, SelectionRuleListItem> SelectionRuleListItems { get; set; }

	[Obsolete("refactor this property out, it has been replaced by ElementSetters")]
	public Dictionary<string, string> Setters { get; set; }

	public XmlNode ElementNode { get; set; }

	public string ElementNodeString { get; set; }

	public ElementHeader ElementHeader { get; set; }

	public string Name => ElementHeader.Name;

	public string Type => ElementHeader.Type;

	public string Source => ElementHeader.Source;

	public string Id => ElementHeader.Id;

	public virtual bool AllowMultipleElements => false;

	public virtual bool AllowDuplicate { get; set; }

	public bool IsExtended { get; set; }

	public string SourceUrl { get; set; }

	public List<string> Supports { get; set; }

	public bool HasSupports => Supports.Any();

	public List<string> Keywords { get; set; }

	public string Prerequisite { get; set; }

	public bool HasPrerequisites => !string.IsNullOrWhiteSpace(Prerequisite);

	public string Requirements { get; set; }

	public bool HasRequirements => !string.IsNullOrWhiteSpace(Requirements);

	public string Description { get; set; }

	public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

	public string GeneratedDescription { get; set; }

	public bool HasGeneratedDescription => !string.IsNullOrWhiteSpace(GeneratedDescription);

	public ElementSheetDescriptions SheetDescription { get; set; } = new ElementSheetDescriptions();

	public ElementSetters ElementSetters { get; set; } = new ElementSetters();

	public List<RuleBase> Rules { get; set; } = new List<RuleBase>();

	public bool HasRules => Rules.Any();

	public bool ContainsGrantRules => Rules.Any((RuleBase x) => x.RuleName == "grant");

	public bool ContainsSelectRules => Rules.Any((RuleBase x) => x.RuleName == "select");

	public bool ContainsStatisticRules => Rules.Any((RuleBase x) => x.RuleName == "stat");

	public AquisitionInfo Aquisition { get; set; } = new AquisitionInfo();

	public SpellcastingInformation SpellcastingInformation { get; set; }

	public bool HasSpellcastingInformation => SpellcastingInformation != null;

	public ElementBase SourceElement { get; set; }

	public bool HasSourceElement => SourceElement != null;

	public ElementEquipment Equipment { get; set; }

	public bool HasEquipment => Equipment != null;

	public bool IncludeInCompendium { get; set; } = true;

	public ElementSourceInfo ElementSource { get; set; }

	public bool HasElementSource => ElementSource != null;

	public ElementBase()
		: this("", "", "", "")
	{
	}

	public ElementBase(string name, string type, string source, string id)
		: this(new ElementHeader(name, type, source, id))
	{
	}

	public ElementBase(ElementHeader elementElementHeader)
	{
		ElementHeader = elementElementHeader;
		Description = "";
		Supports = new List<string>();
		RuleElements = new ElementBaseCollection();
		Keywords = new List<string>();
		SelectionRuleListItems = new Dictionary<string, SelectionRuleListItem>();
	}

	public bool AttemptGetSetterValue(string name, out ElementSetters.Setter setter)
	{
		setter = null;
		if (ElementSetters.ContainsSetter(name))
		{
			setter = ElementSetters.GetSetter(name);
			return true;
		}
		return false;
	}

	public bool GetSetterOverrideAttributeValue(string setterName)
	{
		ElementSetters.Setter setter = ElementSetters.GetSetter(setterName);
		if (setter != null && setter.HasAdditionalAttributes && setter.AdditionalAttributes.ContainsKey("override"))
		{
			return Convert.ToBoolean(setter.AdditionalAttributes["override"]);
		}
		return false;
	}

	public string GetSetterAdditionAttribute(string setterName)
	{
		ElementSetters.Setter setter = ElementSetters.GetSetter(setterName);
		if (setter != null && setter.HasAdditionalAttributes && setter.AdditionalAttributes.ContainsKey("addition"))
		{
			return setter.AdditionalAttributes["addition"].Trim();
		}
		return null;
	}

	public IEnumerable<GrantRule> GetGrantRules()
	{
		return Rules.Where((RuleBase x) => x.RuleName == "grant").Cast<GrantRule>();
	}

	public IEnumerable<SelectRule> GetSelectRules()
	{
		return Rules.Where((RuleBase x) => x.RuleName == "select").Cast<SelectRule>();
	}

	public IEnumerable<StatisticRule> GetStatisticRules()
	{
		return Rules.Where((RuleBase x) => x.RuleName == "stat").Cast<StatisticRule>();
	}

	public virtual TElement Construct<TElement>() where TElement : ElementBase, new()
	{
		TElement val = new TElement();
		PropertyInfo[] properties = typeof(ElementBase).GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			PropertyInfo property = typeof(TElement).GetProperty(propertyInfo.Name, propertyInfo.PropertyType);
			if (property?.GetSetMethod() != null && propertyInfo.GetIndexParameters().Length == 0 && property.GetIndexParameters().Length == 0)
			{
				property.SetValue(val, property.GetValue(this, null), null);
			}
		}
		return val;
	}

	public virtual TElement ConstructFrom<TElement, TFromElement>() where TElement : ElementBase, new()
	{
		TElement val = new TElement();
		PropertyInfo[] properties = typeof(TFromElement).GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			PropertyInfo property = typeof(TElement).GetProperty(propertyInfo.Name, propertyInfo.PropertyType);
			if (property?.GetSetMethod() != null && propertyInfo.GetIndexParameters().Length == 0 && property.GetIndexParameters().Length == 0)
			{
				property.SetValue(val, property.GetValue(this, null), null);
			}
		}
		return val;
	}

	public bool ContainsRuleElement(ElementBase element)
	{
		if (RuleElements.Contains(element))
		{
			return true;
		}
		bool result = RuleElements.Contains(element);
		foreach (ElementBase ruleElement in RuleElements)
		{
			if (ruleElement.ContainsRuleElement(element))
			{
				result = true;
			}
		}
		return result;
	}

	public override string ToString()
	{
		return Name + " (" + Type + ")";
	}

	public string GetAlternateName()
	{
		if (!SheetDescription.HasAlternateName)
		{
			return Name;
		}
		return SheetDescription.AlternateName;
	}
}
