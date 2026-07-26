using System.Linq;
using System.Xml;
using Builder.Core.Logging;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class SpellElementParser : ElementParser
{
	private const string LevelSetter = "level";

	private const string MagicSchoolSetter = "school";

	private const string CastingTimeSetter = "time";

	private const string DurationSetter = "duration";

	private const string RangeSetter = "range";

	private const string HasVerbalComponentSetter = "hasVerbalComponent";

	private const string HasSomaticComponentSetter = "hasSomaticComponent";

	private const string HasMaterialComponentSetter = "hasMaterialComponent";

	private const string MaterialComponentSetter = "materialComponent";

	private const string HasRoyaltyComponentSetter = "hasRoyaltyComponent";

	private const string RoyaltyComponentSetter = "royaltyComponent";

	private const string IsConcentrationSetter = "isConcentration";

	private const string IsRitualSetter = "isRitual";

	public override string ParserType => "Spell";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Spell spell = base.ParseElement(elementNode).Construct<Spell>();
		string[] requiredSetters = new string[5] { "level", "school", "time", "duration", "range" };
		ValidateElementSetters(spell, requiredSetters);
		spell.Level = spell.ElementSetters.GetSetter("level").ValueAsInteger();
		ElementSetters.Setter setter = spell.ElementSetters.GetSetter("school");
		spell.MagicSchool = setter.Value;
		if (setter.HasAdditionalAttributes && setter.AdditionalAttributes.ContainsKey("addition"))
		{
			spell.MagicSchoolAddition = spell.ElementSetters.GetSetter("school").AdditionalAttributes["addition"];
			spell.MagicSchoolAdditions.AddRange(from x in spell.MagicSchoolAddition.Split(',')
				select x.Trim());
		}
		spell.CastingTime = spell.ElementSetters.GetSetter("time").Value;
		spell.Duration = spell.ElementSetters.GetSetter("duration").Value;
		spell.Range = spell.ElementSetters.GetSetter("range").Value;
		spell.HasVerbalComponent = spell.ElementSetters.GetSetter("hasVerbalComponent")?.ValueAsBool() ?? false;
		spell.HasSomaticComponent = spell.ElementSetters.GetSetter("hasSomaticComponent")?.ValueAsBool() ?? false;
		spell.HasMaterialComponent = spell.ElementSetters.GetSetter("hasMaterialComponent")?.ValueAsBool() ?? false;
		spell.MaterialComponent = spell.ElementSetters.GetSetter("materialComponent")?.Value ?? string.Empty;
		spell.HasRoyaltyComponent = spell.ElementSetters.GetSetter("hasRoyaltyComponent")?.ValueAsBool() ?? false;
		spell.RoyaltyComponent = spell.ElementSetters.GetSetter("royaltyComponent")?.ValueAsInteger() ?? 0;
		spell.IsConcentration = spell.ElementSetters.GetSetter("isConcentration")?.ValueAsBool() ?? false;
		spell.IsRitual = spell.ElementSetters.GetSetter("isRitual")?.ValueAsBool() ?? false;
		spell.Supports.Add(spell.Level.ToString());
		spell.Supports.Add(spell.MagicSchool);
		spell.Supports.Add(spell.MagicSchool.ToLowerInvariant());
		if (spell.IsRitual)
		{
			spell.Supports.Add("Ritual");
		}
		foreach (string magicSchoolAddition in spell.MagicSchoolAdditions)
		{
			spell.Keywords.Add(magicSchoolAddition);
		}
		if (spell.CastingTime.Contains("bonus action"))
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_BONUS_ACTION");
			spell.Keywords.Add("1 bonus action");
		}
		else if (spell.CastingTime.Contains("reaction"))
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_REACTION");
			spell.Keywords.Add("1 reaction");
		}
		else if (spell.CastingTime.Contains("action"))
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_ACTION");
			spell.Keywords.Add("1 action");
		}
		if (spell.HasVerbalComponent)
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_VERBAL");
		}
		if (spell.HasSomaticComponent)
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_SOMATIC");
		}
		if (spell.HasMaterialComponent)
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_MATERIAL");
		}
		if (spell.HasRoyaltyComponent)
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_ROYALTY");
		}
		if (spell.IsRitual)
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_RITUAL");
		}
		if (spell.IsConcentration)
		{
			spell.Supports.Add("ID_INTERNAL_SUPPORT_CONCENTRATION");
		}
		if (spell.Description.Contains("At Higher Levels"))
		{
			spell.Keywords.Add("at higher levels");
		}
		if (string.IsNullOrWhiteSpace(spell.CastingTime))
		{
			Logger.Warning(spell.Name + " has no content in setter 'time'");
		}
		if (string.IsNullOrWhiteSpace(spell.Duration))
		{
			Logger.Warning(spell.Name + " has no content in setter 'duration'");
		}
		if (string.IsNullOrWhiteSpace(spell.Range))
		{
			Logger.Warning(spell.Name + " has no content in setter 'range'");
		}
		return spell;
	}
}
