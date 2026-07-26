namespace Builder.Data.Rules;

public class SelectionRuleListItem
{
	public int ID { get; set; }

	public string Text { get; set; }

	public SelectionRuleListItem(int id, string text)
	{
		ID = id;
		Text = text;
	}

	public override string ToString()
	{
		return $"({ID}) {Text}";
	}
}
