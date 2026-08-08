namespace Builder.Data.Elements;

public class Deity : ElementBase
{
	public override bool AllowMultipleElements => true;

	public string Alignment { get; set; }

	public string Gender { get; set; }

	public string Symbol { get; set; }

	public string Domains { get; set; }
}
