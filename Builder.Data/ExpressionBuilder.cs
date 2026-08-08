using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Builder.Core.Logging;
using Builder.Data.Rules;
using DynamicExpresso;

namespace Builder.Data;

[Obsolete]
public class ExpressionBuilder
{
	private readonly Interpreter _interpreter;

	public ExpressionBuilder()
	{
		_interpreter = new Interpreter();
	}

	public ElementBaseCollection BuildSupportedElementsLinqExpression(ElementBaseCollection elementsCollection, SelectRule rule, bool supportsElementId)
	{
		string supports = rule.Attributes.Supports;
		if (supports.Contains(","))
		{
			Logger.Warning($"unsupported characters in the supportstring for {rule}");
		}
		List<string> list = new List<string>();
		foreach (Match item in Regex.Matches(supports, "([-a-zA-Z \\w]+)").Cast<Match>())
		{
			if (!list.Contains(item.Value))
			{
				list.Add(item.Value);
			}
		}
		string text;
		if (!supports.Contains("||") && supports.Contains("|"))
		{
			text = list.Aggregate(supports, (string text2, string replacement) => text2.Replace(replacement, "x.Id.Equals(\"" + replacement + "\")"));
			text = text.Replace("|", "||");
		}
		else
		{
			text = list.Aggregate(supports, (string text2, string replacement) => text2.Replace(replacement, "x.Supports.Contains(\"" + replacement + "\")"));
		}
		Logger.Debug($"interpreting the {text} with {rule}");
		Expression<Func<ElementBase, bool>> predicate = _interpreter.ParseAsExpression<Func<ElementBase, bool>>(text, new string[1] { "x" });
		ElementBaseCollection elementBaseCollection = new ElementBaseCollection(elementsCollection.AsQueryable().Where(predicate));
		Logger.Info($"from the {elementsCollection.Count} elements provided, {elementBaseCollection.Count} are supported");
		return elementBaseCollection;
	}
}
