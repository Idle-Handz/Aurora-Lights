using System.Xml;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class LanguageElementParser : ElementParser
{
	public override string ParserType => "Language";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Language language = base.ParseElement(elementNode).Construct<Language>();
		if (language.AttemptGetSetterValue("standard", out var setter))
		{
			language.IsStandard = setter.ValueAsBool();
			if (!language.Supports.Contains("Standard"))
			{
				language.Supports.Add("Standard");
			}
		}
		if (language.AttemptGetSetterValue("exotic", out var setter2))
		{
			language.IsExotic = setter2.ValueAsBool();
			if (!language.Supports.Contains("Exotic"))
			{
				language.Supports.Add("Exotic");
			}
		}
		if (language.AttemptGetSetterValue("secret", out var setter3))
		{
			language.IsSecret = setter3.ValueAsBool();
			if (!language.Supports.Contains("Secret"))
			{
				language.Supports.Add("Secret");
			}
		}
		if (language.AttemptGetSetterValue("monster", out var setter4))
		{
			language.IsMonsterLanguage = setter4.ValueAsBool();
			if (!language.Supports.Contains("Monster"))
			{
				language.Supports.Add("Monster");
			}
		}
		if (language.AttemptGetSetterValue("speakers", out var setter5))
		{
			language.Speakers = setter5.Value;
		}
		if (language.AttemptGetSetterValue("script", out var setter6))
		{
			language.Script = setter6.Value;
		}
		if (language.Supports.Contains("Starting") && !language.Supports.Contains("Standard"))
		{
			language.Supports.Add("Standard");
		}
		if (language.Supports.Contains("Exotic") && !language.Supports.Contains("ID_INTERNAL_SUPPORT_LANGUAGE_EXOTIC"))
		{
			language.Supports.Add("ID_INTERNAL_SUPPORT_LANGUAGE_EXOTIC");
		}
		if (language.Supports.Contains("Standard") || language.Supports.Contains("Exotic") || language.Supports.Contains("Secret"))
		{
			language.Supports.Add("Character");
		}
		return language;
	}
}
