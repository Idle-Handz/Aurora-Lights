using System;
using System.Collections.Generic;
using System.Linq;

namespace Builder.Data.Elements;

public class Spell : ElementBase
{
	public override bool AllowMultipleElements => true;

	public int Level { get; set; }

	public string MagicSchool { get; set; }

	public string MagicSchoolAddition { get; set; }

	public string CastingTime { get; set; }

	public string Duration { get; set; }

	public string Range { get; set; }

	public bool HasVerbalComponent { get; set; }

	public bool HasSomaticComponent { get; set; }

	public bool HasMaterialComponent { get; set; }

	public string MaterialComponent { get; set; }

	public bool HasRoyaltyComponent { get; set; }

	public int RoyaltyComponent { get; set; }

	public bool IsConcentration { get; set; }

	public bool IsRitual { get; set; }

	public List<string> MagicSchoolAdditions { get; set; } = new List<string>();

	public string Underline => GetShortDescription();

	public string GetShortDescription()
	{
		string text = ((Level == 0) ? (MagicSchool + " Cantrip") : ((Level < 0) ? (MagicSchool ?? "") : (Level switch
		{
			1 => "1st-level " + MagicSchool.ToLowerInvariant(),
			2 => "2nd-level " + MagicSchool.ToLowerInvariant(),
			3 => "3rd-level " + MagicSchool.ToLowerInvariant(),
			_ => $"{Level}th-level {MagicSchool.ToLowerInvariant()}",
		})));
		if (IsRitual || MagicSchoolAdditions.Any())
		{
			List<string> list = new List<string>();
			if (IsRitual)
			{
				list.Add("ritual");
			}
			list.AddRange(MagicSchoolAdditions);
			string text2 = string.Join(", ", list);
			text = text + " (" + text2.ToLowerInvariant() + ")";
		}
		return text;
	}

	public string GetComponentsString()
	{
		string text = "";
		if (HasVerbalComponent)
		{
			text += "V";
		}
		if (HasSomaticComponent)
		{
			text += (HasVerbalComponent ? ", S" : "S");
		}
		if (HasMaterialComponent)
		{
			text += ((HasVerbalComponent || HasSomaticComponent) ? (", M (" + MaterialComponent + ")") : ("M (" + MaterialComponent + ")"));
		}
		if (base.ElementSetters.ContainsSetter("hasRoyaltyComponent") && base.ElementSetters.GetSetter("hasRoyaltyComponent").ValueAsBool())
		{
			int num = Level;
			if (base.ElementSetters.ContainsSetter("royaltyComponent"))
			{
				num = base.ElementSetters.GetSetter("royaltyComponent").ValueAsInteger();
			}
			text = ((num <= 0) ? (text + "R") : (text + $"R ({num})"));
		}
		return text;
	}

	[Obsolete]
	public string GetMagicSchoolString()
	{
		if (string.IsNullOrWhiteSpace(MagicSchoolAddition))
		{
			return MagicSchool;
		}
		return MagicSchool + " (" + MagicSchoolAddition + ")";
	}
}
