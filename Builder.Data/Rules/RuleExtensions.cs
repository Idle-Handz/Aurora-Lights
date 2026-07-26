namespace Builder.Data.Rules;

public static class RuleExtensions
{
	public static bool ContainsPreparedSetter(this RuleBase rule)
	{
		if (rule == null)
		{
			return false;
		}
		if (rule.ContainsSetters())
		{
			return rule.Setters.ContainsSetter("prepared");
		}
		return false;
	}

	public static bool IsAlwaysPrepared(this RuleBase rule)
	{
		if (rule == null)
		{
			return false;
		}
		if (rule.ContainsSetters() && rule.Setters.ContainsSetter("prepared"))
		{
			return rule.Setters.GetSetter("prepared").ValueAsBool();
		}
		return false;
	}
}
