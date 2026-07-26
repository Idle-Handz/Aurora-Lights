namespace Builder.Data.Elements;

public class LevelElement : ElementBase
{
	public override bool AllowMultipleElements => true;

	public int RequiredExperience { get; set; }

	public int Level { get; set; }
}
