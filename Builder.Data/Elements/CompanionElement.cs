using System.Collections.Generic;

namespace Builder.Data.Elements;

public sealed class CompanionElement : ElementBase
{
	public int Strength { get; set; }

	public int Dexterity { get; set; }

	public int Constitution { get; set; }

	public int Intelligence { get; set; }

	public int Wisdom { get; set; }

	public int Charisma { get; set; }

	public int Proficiency { get; set; }

	public string ArmorClass { get; set; }

	public string HitPoints { get; set; }

	public string Speed { get; set; }

	public string Senses { get; set; }

	public string Languages { get; set; }

	public string CreatureType { get; set; }

	public string Size { get; set; }

	public string Alignment { get; set; }

	public string Challenge { get; set; }

	public string Experience { get; set; }

	public List<string> Traits { get; set; }

	public List<string> Actions { get; set; }

	public List<string> Reactions { get; set; }

	public string SavingThrows { get; set; }

	public string Skills { get; set; }

	public string DamageVulnerabilities { get; set; }

	public string DamageResistances { get; set; }

	public string DamageImmunities { get; set; }

	public string ConditionVulnerabilities { get; set; }

	public string ConditionResistances { get; set; }

	public string ConditionImmunities { get; set; }

	public bool HasSenses => !string.IsNullOrWhiteSpace(Senses);

	public bool HasLanguages => !string.IsNullOrWhiteSpace(Languages);

	public bool HasExperience => !string.IsNullOrWhiteSpace(Experience);

	public bool HasSavingThrows => !string.IsNullOrWhiteSpace(SavingThrows);

	public bool HasSkills => !string.IsNullOrWhiteSpace(Skills);

	public bool HasDamageVulnerabilities => !string.IsNullOrWhiteSpace(DamageVulnerabilities);

	public bool HasDamageResistances => !string.IsNullOrWhiteSpace(DamageResistances);

	public bool HasDamageImmunities => !string.IsNullOrWhiteSpace(DamageImmunities);

	public bool HasConditionVulnerabilities => !string.IsNullOrWhiteSpace(ConditionVulnerabilities);

	public bool HasConditionResistances => !string.IsNullOrWhiteSpace(ConditionResistances);

	public bool HasConditionImmunities => !string.IsNullOrWhiteSpace(ConditionImmunities);

	public CompanionElement()
	{
		Traits = new List<string>();
		Actions = new List<string>();
		Reactions = new List<string>();
	}
}
