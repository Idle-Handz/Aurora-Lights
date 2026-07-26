namespace Builder.Data.Elements;

public class BackgroundFeature : ElementBase
{
	public override bool AllowMultipleElements => true;

	public bool IsPrimary { get; set; }
}
