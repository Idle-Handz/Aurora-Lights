using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Builder.Data.Strings;

public class AuroraStatisticStrings
{
	private string _strength => "Strength";

	private string _dexterity => "Dexterity";

	private string _constitution => "Constitution";

	private string _intelligence => "Intelligence";

	private string _wisdom => "Wisdom";

	private string _charisma => "Charisma";

	private string _acrobatics => "Acrobatics";

	private string _animalHandling => "Animal Handling";

	private string _arcana => "Arcana";

	private string _athletics => "Athletics";

	private string _deception => "Deception";

	private string _history => "History";

	private string _insight => "Insight";

	private string _intimidation => "Intimidation";

	private string _investigation => "Investigation";

	private string _medicine => "Medicine";

	private string _nature => "Nature";

	private string _perception => "Perception";

	private string _performance => "Performance";

	private string _persuasion => "Persuasion";

	private string _religion => "Religion";

	private string _sleightOfHand => "Sleight of Hand";

	private string _stealth => "Stealth";

	private string _survival => "Survival";

	public string Strength => _strength.ToLowerInvariant() ?? "";

	public string Dexterity => _dexterity.ToLowerInvariant() ?? "";

	public string Constitution => _constitution.ToLowerInvariant() ?? "";

	public string Intelligence => _intelligence.ToLowerInvariant() ?? "";

	public string Wisdom => _wisdom.ToLowerInvariant() ?? "";

	public string Charisma => _charisma.ToLowerInvariant() ?? "";

	public string StrengthScore => Strength + ":score";

	public string DexterityScore => Dexterity + ":score";

	public string ConstitutionScore => Constitution + ":score";

	public string IntelligenceScore => Intelligence + ":score";

	public string WisdomScore => Wisdom + ":score";

	public string CharismaScore => Charisma + ":score";

	public string StrengthSet => StrengthScore + ":set";

	public string DexteritySet => DexterityScore + ":set";

	public string ConstitutionSet => ConstitutionScore + ":set";

	public string IntelligenceSet => IntelligenceScore + ":set";

	public string WisdomSet => WisdomScore + ":set";

	public string CharismaSet => CharismaScore + ":set";

	public string StrengthModifier => Strength + ":modifier";

	public string DexterityModifier => Dexterity + ":modifier";

	public string ConstitutionModifier => Constitution + ":modifier";

	public string IntelligenceModifier => Intelligence + ":modifier";

	public string WisdomModifier => Wisdom + ":modifier";

	public string CharismaModifier => Charisma + ":modifier";

	public string StrengthModifierHalf => StrengthModifier + ":half";

	public string DexterityModifierHalf => DexterityModifier + ":half";

	public string ConstitutionModifierHalf => ConstitutionModifier + ":half";

	public string IntelligenceModifierHalf => IntelligenceModifier + ":half";

	public string WisdomModifierHalf => WisdomModifier + ":half";

	public string CharismaModifierHalf => CharismaModifier + ":half";

	public string StrengthModifierHalfUp => StrengthModifierHalf + ":up";

	public string DexterityModifierHalfUp => DexterityModifierHalf + ":up";

	public string ConstitutionModifierHalfUp => ConstitutionModifierHalf + ":up";

	public string IntelligenceModifierHalfUp => IntelligenceModifierHalf + ":up";

	public string WisdomModifierHalfUp => WisdomModifierHalf + ":up";

	public string CharismaModifierHalfUp => CharismaModifierHalf + ":up";

	public string StrengthMaximum => Strength + ":max";

	public string DexterityMaximum => Dexterity + ":max";

	public string ConstitutionMaximum => Constitution + ":max";

	public string IntelligenceMaximum => Intelligence + ":max";

	public string WisdomMaximum => Wisdom + ":max";

	public string CharismaMaximum => Charisma + ":max";

	public string StrengthSave => Strength + ":save";

	public string DexteritySave => Dexterity + ":save";

	public string ConstitutionSave => Constitution + ":save";

	public string IntelligenceSave => Intelligence + ":save";

	public string WisdomSave => Wisdom + ":save";

	public string CharismaSave => Charisma + ":save";

	public string StrengthSaveProficiency => StrengthSave + ":proficiency";

	public string DexteritySaveProficiency => DexteritySave + ":proficiency";

	public string ConstitutionSaveProficiency => ConstitutionSave + ":proficiency";

	public string IntelligenceSaveProficiency => IntelligenceSave + ":proficiency";

	public string WisdomSaveProficiency => WisdomSave + ":proficiency";

	public string CharismaSaveProficiency => CharismaSave + ":proficiency";

	public string StrengthSaveMisc => StrengthSave + ":misc";

	public string DexteritySaveMisc => DexteritySave + ":misc";

	public string ConstitutionSaveMisc => ConstitutionSave + ":misc";

	public string IntelligenceSaveMisc => IntelligenceSave + ":misc";

	public string WisdomSaveMisc => WisdomSave + ":misc";

	public string CharismaSaveMisc => CharismaSave + ":misc";

	public string Acrobatics => _acrobatics.ToLowerInvariant() ?? "";

	public string AnimalHandling => _animalHandling.ToLowerInvariant() ?? "";

	public string Arcana => _arcana.ToLowerInvariant() ?? "";

	public string Athletics => _athletics.ToLowerInvariant() ?? "";

	public string Deception => _deception.ToLowerInvariant() ?? "";

	public string History => _history.ToLowerInvariant() ?? "";

	public string Insight => _insight.ToLowerInvariant() ?? "";

	public string Intimidation => _intimidation.ToLowerInvariant() ?? "";

	public string Investigation => _investigation.ToLowerInvariant() ?? "";

	public string Medicine => _medicine.ToLowerInvariant() ?? "";

	public string Nature => _nature.ToLowerInvariant() ?? "";

	public string Perception => _perception.ToLowerInvariant() ?? "";

	public string Performance => _performance.ToLowerInvariant() ?? "";

	public string Persuasion => _persuasion.ToLowerInvariant() ?? "";

	public string Religion => _religion.ToLowerInvariant() ?? "";

	public string SleightOfHand => _sleightOfHand.ToLowerInvariant() ?? "";

	public string Stealth => _stealth.ToLowerInvariant() ?? "";

	public string Survival => _survival.ToLowerInvariant() ?? "";

	public string AcrobaticsProficiency => Acrobatics + ":proficiency";

	public string AnimalHandlingProficiency => AnimalHandling + ":proficiency";

	public string ArcanaProficiency => Arcana + ":proficiency";

	public string AthleticsProficiency => Athletics + ":proficiency";

	public string DeceptionProficiency => Deception + ":proficiency";

	public string HistoryProficiency => History + ":proficiency";

	public string InsightProficiency => Insight + ":proficiency";

	public string IntimidationProficiency => Intimidation + ":proficiency";

	public string InvestigationProficiency => Investigation + ":proficiency";

	public string MedicineProficiency => Medicine + ":proficiency";

	public string NatureProficiency => Nature + ":proficiency";

	public string PerceptionProficiency => Perception + ":proficiency";

	public string PerformanceProficiency => Performance + ":proficiency";

	public string PersuasionProficiency => Persuasion + ":proficiency";

	public string ReligionProficiency => Religion + ":proficiency";

	public string SleightOfHandProficiency => SleightOfHand + ":proficiency";

	public string StealthProficiency => Stealth + ":proficiency";

	public string SurvivalProficiency => Survival + ":proficiency";

	public string AcrobaticsMisc => Acrobatics + ":misc";

	public string AnimalHandlingMisc => AnimalHandling + ":misc";

	public string ArcanaMisc => Arcana + ":misc";

	public string AthleticsMisc => Athletics + ":misc";

	public string DeceptionMisc => Deception + ":misc";

	public string HistoryMisc => History + ":misc";

	public string InsightMisc => Insight + ":misc";

	public string IntimidationMisc => Intimidation + ":misc";

	public string InvestigationMisc => Investigation + ":misc";

	public string MedicineMisc => Medicine + ":misc";

	public string NatureMisc => Nature + ":misc";

	public string PerceptionMisc => Perception + ":misc";

	public string PerformanceMisc => Performance + ":misc";

	public string PersuasionMisc => Persuasion + ":misc";

	public string ReligionMisc => Religion + ":misc";

	public string SleightOfHandMisc => SleightOfHand + ":misc";

	public string StealthMisc => Stealth + ":misc";

	public string SurvivalMisc => Survival + ":misc";

	public string AcrobaticsPassive => Acrobatics + ":passive";

	public string AnimalHandlingPassive => AnimalHandling + ":passive";

	public string ArcanaPassive => Arcana + ":passive";

	public string AthleticsPassive => Athletics + ":passive";

	public string DeceptionPassive => Deception + ":passive";

	public string HistoryPassive => History + ":passive";

	public string InsightPassive => Insight + ":passive";

	public string IntimidationPassive => Intimidation + ":passive";

	public string InvestigationPassive => Investigation + ":passive";

	public string MedicinePassive => Medicine + ":passive";

	public string NaturePassive => Nature + ":passive";

	public string PerceptionPassive => Perception + ":passive";

	public string PerformancePassive => Performance + ":passive";

	public string PersuasionPassive => Persuasion + ":passive";

	public string ReligionPassive => Religion + ":passive";

	public string SleightOfHandPassive => SleightOfHand + ":passive";

	public string StealthPassive => Stealth + ":passive";

	public string SurvivalPassive => Survival + ":passive";

	public string HitPoints => "hp";

	public string HitPointsStarting => "hp:starting";

	public string HitPointsTemp => "hp:temp";

	public string Level => "level";

	public string LevelHalf => "level:half";

	public string LevelHalfUp => "level:half:up";

	public string LevelQuarter => "level:quarter";

	public string LevelQuarterUp => "level:quarter:up";

	public string Proficiency => "proficiency";

	public string ProficiencyHalf => Proficiency + ":half";

	public string ProficiencyHalfDown => ProficiencyHalf + ":down";

	public string ProficiencyHalfUp => ProficiencyHalf + ":up";

	public string ArmorClass => "ac";

	public string Speed => "speed";

	public string SpeedFly => "speed:fly";

	public string SpeedClimb => "speed:climb";

	public string SpeedSwim => "speed:swim";

	public string SpeedBurrow => "speed:burrow";

	public string InnateSpeed => "innate " + Speed;

	public string InnateSpeedFly => "innate " + SpeedFly;

	public string InnateSpeedClimb => "innate speed:climb";

	public string InnateSpeedSwim => "innate speed:swim";

	public string InnateSpeedBurrow => "innate speed:burrow";

	public string InnateSpeedMisc => InnateSpeed + ":misc";

	public string InnateSpeedFlyMisc => InnateSpeedFly + ":misc";

	public string InnateSpeedClimbMisc => "innate speed:climb:misc";

	public string InnateSpeedSwimMisc => "innate speed:swim:misc";

	public string InnateSpeedBurrowMisc => "innate speed:burrow:misc";

	public string SpeedMisc => Speed + ":misc";

	public string SpeedFlyMisc => SpeedFly + ":misc";

	public string SpeedClimbMisc => "speed:climb:misc";

	public string SpeedSwimMisc => "speed:swim:misc";

	public string SpeedBurrowMisc => "speed:burrow:misc";

	public string Initiative => "initiative";

	public string InitiativeMisc => Initiative + ":misc";

	public string AttunementMax => "attunement:max";

	private string _spellcasting => "spellcasting";

	private string _multiclass => "multiclass";

	private string _slots => "slots";

	public string SpellcastingAttack => _spellcasting + ":attack";

	public string SpellcastingDC => _spellcasting + ":dc";

	public string SpellcastingSlot1 => _spellcasting + ":" + _slots + ":1";

	public string SpellcastingSlot2 => _spellcasting + ":" + _slots + ":2";

	public string SpellcastingSlot3 => _spellcasting + ":" + _slots + ":3";

	public string SpellcastingSlot4 => _spellcasting + ":" + _slots + ":4";

	public string SpellcastingSlot5 => _spellcasting + ":" + _slots + ":5";

	public string SpellcastingSlot6 => _spellcasting + ":" + _slots + ":6";

	public string SpellcastingSlot7 => _spellcasting + ":" + _slots + ":7";

	public string SpellcastingSlot8 => _spellcasting + ":" + _slots + ":8";

	public string SpellcastingSlot9 => _spellcasting + ":" + _slots + ":9";

	public string MulticlassSpellcastingSlot1 => _multiclass + ":" + _spellcasting + ":" + _slots + ":1";

	public string MulticlassSpellcastingSlot2 => _multiclass + ":" + _spellcasting + ":" + _slots + ":2";

	public string MulticlassSpellcastingSlot3 => _multiclass + ":" + _spellcasting + ":" + _slots + ":3";

	public string MulticlassSpellcastingSlot4 => _multiclass + ":" + _spellcasting + ":" + _slots + ":4";

	public string MulticlassSpellcastingSlot5 => _multiclass + ":" + _spellcasting + ":" + _slots + ":5";

	public string MulticlassSpellcastingSlot6 => _multiclass + ":" + _spellcasting + ":" + _slots + ":6";

	public string MulticlassSpellcastingSlot7 => _multiclass + ":" + _spellcasting + ":" + _slots + ":7";

	public string MulticlassSpellcastingSlot8 => _multiclass + ":" + _spellcasting + ":" + _slots + ":8";

	public string MulticlassSpellcastingSlot9 => _multiclass + ":" + _spellcasting + ":" + _slots + ":9";

	public string SpellcastingKnownSpells => _spellcasting + ":known spells";

	public string SpellcastingAttackStrength => SpellcastingAttack + ":" + _strength.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingAttackDexterity => SpellcastingAttack + ":" + _dexterity.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingAttackConstitution => SpellcastingAttack + ":" + _constitution.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingAttackIntelligence => SpellcastingAttack + ":" + _intelligence.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingAttackWisdom => SpellcastingAttack + ":" + _wisdom.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingAttackCharisma => SpellcastingAttack + ":" + _charisma.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingSaveDcStrength => SpellcastingDC + ":" + _strength.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingSaveDcDexterity => SpellcastingDC + ":" + _dexterity.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingSaveDcConstitution => SpellcastingDC + ":" + _constitution.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingSaveDcIntelligence => SpellcastingDC + ":" + _intelligence.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingSaveDcWisdom => SpellcastingDC + ":" + _wisdom.Substring(0, 3).ToLowerInvariant();

	public string SpellcastingSaveDcCharisma => SpellcastingDC + ":" + _charisma.Substring(0, 3).ToLowerInvariant();

	[Obsolete]
	public IEnumerable<string> GetStrings()
	{
		foreach (string item in (from x in typeof(AuroraStatisticStrings).GetProperties()
			select x.GetValue(this)).Cast<string>())
		{
			yield return item.ToLowerInvariant();
		}
	}
}
