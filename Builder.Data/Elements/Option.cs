namespace Builder.Data.Elements;

public class Option : ElementBase
{
	public override bool AllowMultipleElements => true;

	public bool IsInternal { get; set; }

	public bool IsDefault { get; set; }
}
