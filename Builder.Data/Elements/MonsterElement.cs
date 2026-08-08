namespace Builder.Data.Elements;

public class MonsterElement : ElementBase
{
	public int Strength { get; set; }

	public int Dexterity { get; set; }

	public int Constitution { get; set; }

	public int Intelligence { get; set; }

	public int Wisdom { get; set; }

	public int Charisma { get; set; }

	public string Size { get; set; }

	public string MonsterType { get; set; }

	public string Alignment { get; set; }

	public int Ac { get; set; }

	public int Hp { get; set; }

	public string Hd { get; set; }

	public Speed Speed { get; set; }

	public MonsterElement()
	{
		Speed = new Speed();
	}
}
