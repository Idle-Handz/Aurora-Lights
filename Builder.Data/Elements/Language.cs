namespace Builder.Data.Elements;

public class Language : ElementBase
{
	public override bool AllowMultipleElements => true;

	public bool IsExotic { get; set; }

	public bool IsStandard { get; set; }

	public bool IsSecret { get; set; }

	public bool IsMonsterLanguage { get; set; }

	public string Speakers { get; set; }

	public string Script { get; set; }
}
