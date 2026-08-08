using System.Xml;
using Builder.Core.Logging;
using Builder.Data.Elements;

namespace Builder.Data.ElementParsers;

public sealed class SourceParser : ElementParser
{
	public override string ParserType => "Source";

	public override ElementBase ParseElement(XmlNode elementNode)
	{
		Source source = base.ParseElement(elementNode).Construct<Source>();
		ValidateElementSetters(source, "url", "abbreviation");
		source.Url = source.ElementSetters.GetSetter("url")?.Value;
		source.Abbreviation = source.ElementSetters.GetSetter("abbreviation")?.Value;
		source.ImageUrl = source.ElementSetters.GetSetter("image")?.Value;
		source.ReleaseDate = source.ElementSetters.GetSetter("release")?.Value;
		source.ErrataUrl = source.ElementSetters.GetSetter("errata")?.Value;
		ElementSetters.Setter setter = source.ElementSetters.GetSetter("author");
		if (setter != null)
		{
			source.Author = setter.Value;
			source.AuthorAbbreviation = (setter.AdditionalAttributes.ContainsKey("abbreviation") ? setter.AdditionalAttributes["abbreviation"] : "");
			source.AuthorUrl = (setter.AdditionalAttributes.ContainsKey("url") ? setter.AdditionalAttributes["url"] : "");
		}
		ElementSetters.Setter setter2 = source.ElementSetters.GetSetter("alternative");
		if (setter2 != null)
		{
			source.AlternativeNames.Add(setter2.Value.Trim());
		}
		if (!source.SheetDescription.HasAlternateName)
		{
			source.SheetDescription.AlternateName = source.ElementSetters.GetSetter("alt")?.Value;
		}
		source.GroupName = source.ElementSetters.GetSetter("group")?.Value;
		source.IsOfficialContent = source.ElementSetters.GetSetter("official")?.ValueAsBool() ?? false;
		source.IsThirdPartyContent = source.ElementSetters.GetSetter("third-party")?.ValueAsBool() ?? false;
		source.IsHomebrewContent = source.ElementSetters.GetSetter("homebrew")?.ValueAsBool() ?? false;
		if (!source.IsOfficialContent && !source.IsThirdPartyContent && !source.IsHomebrewContent)
		{
			Logger.Warning($"missing context (official, third-party, homebrew) on {source}");
			source.IsUndefinedContext = true;
		}
		source.IsCoreContent = source.ElementSetters.GetSetter("core")?.ValueAsBool() ?? false;
		source.IsSupplementContent = source.ElementSetters.GetSetter("supplement")?.ValueAsBool() ?? false;
		source.IsPlaytestContent = source.ElementSetters.GetSetter("playtest")?.ValueAsBool() ?? false;
		if (!source.IsPlaytestContent)
		{
			source.IsPlaytestContent = source.ElementSetters.GetSetter("beta")?.ValueAsBool() ?? false;
		}
		source.IsAdventureLeagueContent = source.ElementSetters.GetSetter("adventure-league")?.ValueAsBool() ?? false;
		if (!source.IsAdventureLeagueContent)
		{
			source.IsAdventureLeagueContent = source.ElementSetters.GetSetter("league")?.ValueAsBool() ?? false;
		}
		if (source.IsOfficialContent)
		{
			source.IsLegal = source.ElementSetters.GetSetter("legal")?.ValueAsBool() ?? true;
		}
		else
		{
			source.IsLegal = false;
		}
		source.IsIncomplete = source.ElementSetters.GetSetter("incomplete")?.ValueAsBool() ?? false;
		source.IncompleteMessage = "Source Incomplete";
		source.IsWorkInProgress = source.ElementSetters.GetSetter("wip")?.ValueAsBool() ?? false;
		source.InformationMessage = source.ElementSetters.GetSetter("information")?.Value ?? string.Empty;
		if (source.HasInformationMessage && source.ElementSetters.GetSetter("information").HasAdditionalAttributes && source.ElementSetters.GetSetter("information").AdditionalAttributes.ContainsKey("url"))
		{
			source.InformationUrl = source.ElementSetters.GetSetter("information").AdditionalAttributes["url"];
		}
		source.IsPreview = source.IsPlaytestContent;
		source.IsHomebrew = source.IsHomebrewContent;
		return source;
	}
}
