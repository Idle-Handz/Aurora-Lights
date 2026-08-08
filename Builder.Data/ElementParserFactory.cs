using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Builder.Data.Rules.Parsers;

namespace Builder.Data;

public static class ElementParserFactory
{
	public static IEnumerable<ElementParser> GetParsers()
	{
		return (from parser in Assembly.GetAssembly(typeof(ElementParser)).GetTypes()
			where parser.IsClass && !parser.IsAbstract && parser.IsSubclassOf(typeof(ElementParser))
			select (ElementParser)Activator.CreateInstance(parser)).ToList();
	}

	public static IEnumerable<RuleParser> GetRuleParsers()
	{
		return (from parser in Assembly.GetAssembly(typeof(RuleParser)).GetTypes()
			where parser.IsClass && !parser.IsAbstract && parser.IsSubclassOf(typeof(RuleParser))
			select (RuleParser)Activator.CreateInstance(parser)).ToList();
	}

	public static IEnumerable<RuleParser> GetImplementations()
	{
		return (from parser in Assembly.GetAssembly(typeof(RuleParser)).GetTypes()
			where parser.IsClass && !parser.IsAbstract && parser.IsSubclassOf(typeof(RuleParser))
			select (RuleParser)Activator.CreateInstance(parser)).ToList();
	}
}
