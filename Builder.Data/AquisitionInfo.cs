using Builder.Data.Rules;

namespace Builder.Data;

public class AquisitionInfo
{
	public bool WasGranted { get; set; }

	public GrantRule GrantRule { get; set; }

	public bool WasSelected { get; set; }

	public SelectRule SelectRule { get; set; }

	public string PrepareParent { get; set; }

	public void GrantedBy(GrantRule rule)
	{
		GrantRule = rule;
		WasGranted = true;
	}

	public void SelectedBy(SelectRule rule)
	{
		SelectRule = rule;
		WasSelected = true;
	}

	public void Clear()
	{
		WasGranted = false;
		WasSelected = false;
		GrantRule = null;
		SelectRule = null;
		PrepareParent = "";
	}

	public ElementHeader GetParentHeader()
	{
		if (WasGranted)
		{
			return GrantRule.ElementHeader;
		}
		if (WasSelected)
		{
			return SelectRule.ElementHeader;
		}
		return null;
	}
}
