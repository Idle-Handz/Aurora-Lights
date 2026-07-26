using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Builder.Core.Logging;
using Builder.Data.Rules;
using DynamicExpresso;

namespace Builder.Data;

public class ElementBaseCollection : ObservableCollection<ElementBase>
{
	public string Name { get; set; }

	public ElementBaseCollection()
	{
	}

	public ElementBaseCollection(IEnumerable<ElementBase> elements)
	{
		if (elements == null)
		{
			throw new ArgumentNullException("elements");
		}
		foreach (ElementBase element in elements)
		{
			Add(element);
		}
	}

	public void AddRange(IEnumerable<ElementBase> elements)
	{
		foreach (ElementBase element in elements)
		{
			Add(element);
		}
	}

	public bool ContainsType(string type)
	{
		return this.Select((ElementBase x) => x.Type).Contains(type);
	}

	public bool ContainsRuleElement(ElementBase element, GrantRule rule)
	{
		return Contains(element);
	}

	public ElementBaseCollection WithoutRuleParent(GrantRule rule)
	{
		ElementBase elementBase = this.FirstOrDefault((ElementBase x) => x.ElementHeader.Equals(rule.ElementHeader));
		if (elementBase == null)
		{
			return this;
		}
		ElementBaseCollection elementBaseCollection = new ElementBaseCollection(this);
		foreach (ElementBase ruleElement in elementBase.RuleElements)
		{
			elementBaseCollection.Remove(ruleElement);
		}
		return elementBaseCollection;
	}

	public ElementBaseCollection WithoutParentGrants(GrantRule rule)
	{
		ElementBase elementBase = this.FirstOrDefault((ElementBase element) => element.ElementHeader == rule.ElementHeader);
		if (elementBase == null)
		{
			return this;
		}
		ElementBaseCollection elementBaseCollection = new ElementBaseCollection(this);
		foreach (ElementBase ruleElement in elementBase.RuleElements)
		{
			elementBaseCollection.Remove(ruleElement);
		}
		return elementBaseCollection;
	}

	public bool HasElement(string id)
	{
		return this.Any((ElementBase x) => x.Id == id);
	}

	public ElementBase GetElement(string id)
	{
		ElementBase elementBase = this.FirstOrDefault((ElementBase x) => x.Id.Equals(id));
		if (elementBase != null)
		{
			return elementBase;
		}
		return null;
	}

	public ElementBase GetFresh(string id)
	{
		if (!HasElement(id))
		{
			return null;
		}
		return GetElement(id).Construct<ElementBase>();
	}

	public ElementBase GetFresh<TElement>(string id) where TElement : ElementBase, new()
	{
		return HasElement(id) ? GetElement(id).Construct<TElement>() : null;
	}

	public void RemoveElement(string id)
	{
		if (HasElement(id))
		{
			Remove(GetElement(id));
		}
	}

	[Obsolete]
	public ElementBaseCollection GetSupportedElements(SelectRule rule)
	{
		return GetSupportedElements(rule, rule.Attributes.ContainsSupports() && !rule.Attributes.Supports.Contains("||") && rule.Attributes.Supports.Contains("|"));
	}

	[Obsolete]
	private ElementBaseCollection GetSupportedElements(SelectRule rule, bool supportsElementId)
	{
		ElementBaseCollection elementBaseCollection = new ElementBaseCollection(this.Where((ElementBase element) => element.Type == rule.Attributes.Type));
		_ = rule.Attributes.Requirements;
		if (rule.Attributes.ContainsSupports())
		{
			List<string> list = new List<string>();
			foreach (Match item in Regex.Matches(rule.Attributes.Supports, "([-a-zA-Z \\w]+)").Cast<Match>())
			{
				if (!list.Contains(item.Value))
				{
					list.Add(item.Value);
				}
			}
			string text;
			if (supportsElementId)
			{
				text = list.Aggregate(rule.Attributes.Supports, (string text2, string replacement) => text2.Replace(replacement, "x.Id.Equals(\"" + replacement + "\")"));
				text = text.Replace("|", "||");
			}
			else
			{
				text = list.Aggregate(rule.Attributes.Supports, (string text2, string replacement) => text2.Replace(replacement, "x.Supports.Contains(\"" + replacement + "\")"));
			}
			Interpreter interpreter = new Interpreter();
			interpreter.EnableAssignment(AssignmentOperators.None);
			Logger.Debug($"interpreting the {text} with {rule}");
			Expression<Func<ElementBase, bool>> predicate = interpreter.ParseAsExpression<Func<ElementBase, bool>>(text, new string[1] { "x" });
			return new ElementBaseCollection(elementBaseCollection.AsQueryable().Where(predicate));
		}
		return new ElementBaseCollection(elementBaseCollection);
	}

	[Obsolete]
	public bool MeetsRuleRequirements(string requirements)
	{
		if (string.IsNullOrWhiteSpace(requirements))
		{
			return true;
		}
		List<string> list = new List<string>();
		foreach (Match item in Regex.Matches(requirements, "([-a-zA-Z \\w]+)").Cast<Match>())
		{
			if (!list.Contains(item.Value))
			{
				list.Add(item.Value);
			}
		}
		string text = requirements;
		foreach (string item2 in list)
		{
			text = text.Replace(item2, "x.Contains(\"" + item2 + "\")");
		}
		string text2 = string.Join(" ", this.Select((ElementBase x) => x.Id));
		Interpreter interpreter = new Interpreter();
		interpreter.EnableAssignment(AssignmentOperators.None);
		return (bool)interpreter.Parse(text, new Parameter("x", typeof(string))).Invoke(text2);
	}

	[Obsolete]
	private ElementBaseCollection FilterRequirements(ElementBaseCollection elements, string requirementString)
	{
		List<string> list = new List<string>();
		foreach (Match item in Regex.Matches(requirementString, "([-a-zA-Z \\w]+)").Cast<Match>())
		{
			if (!list.Contains(item.Value))
			{
				list.Add(item.Value);
			}
		}
		string text = list.Aggregate(requirementString, (string text2, string replacement) => text2.Replace(replacement, "x.Id.Equals(\"" + replacement + "\")"));
		text = text.Replace("|", "||");
		Interpreter interpreter = new Interpreter();
		interpreter.EnableAssignment(AssignmentOperators.None);
		Logger.Debug("interpreting the " + text);
		Expression<Func<ElementBase, bool>> predicate = interpreter.ParseAsExpression<Func<ElementBase, bool>>(text, new string[1] { "x" });
		return new ElementBaseCollection(elements.AsQueryable().Where(predicate));
	}
}
