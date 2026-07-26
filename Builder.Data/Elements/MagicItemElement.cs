namespace Builder.Data.Elements;

public class MagicItemElement : Item
{
	public override bool AllowMultipleElements => true;

	public bool OverrideCost { get; set; }

	public bool OverrideWeight { get; set; }

	public int MaximumCharges { get; set; }
}
