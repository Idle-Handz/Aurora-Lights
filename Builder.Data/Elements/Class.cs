using System;

namespace Builder.Data.Elements;

public class Class : ElementBase
{
	public override bool AllowMultipleElements => false;

	public string Short { get; set; }

	public bool HasShort => !string.IsNullOrWhiteSpace(Short);

	public string StartingEquipment { get; set; }

	public bool HasStartingEquipment => !string.IsNullOrWhiteSpace(Short);

	public string HitDice { get; set; }

	[Obsolete]
	public string SpellcastingAbility { get; set; }

	[Obsolete]
	public bool IsSpellcaster { get; set; }

	public bool CanMulticlass { get; set; }

	public string MulticlassId { get; set; }
}
