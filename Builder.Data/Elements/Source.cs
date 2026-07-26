using System;
using System.Collections.Generic;

namespace Builder.Data.Elements;

public class Source : ElementBase
{
	public override bool AllowMultipleElements => true;

	public string Url { get; set; }

	public string ImageUrl { get; set; }

	public string ErrataUrl { get; set; }

	public string Author { get; set; }

	public string AuthorUrl { get; set; }

	public string AuthorAbbreviation { get; set; }

	public string Abbreviation { get; set; }

	[Obsolete]
	public bool IsPreview { get; set; }

	[Obsolete]
	public bool IsHomebrew { get; set; }

	public List<string> AlternativeNames { get; } = new List<string>();

	public string GroupName { get; set; }

	public bool IsUndefinedContext { get; set; }

	public string ReleaseDate { get; set; }

	public bool IsOfficialContent { get; set; }

	public bool IsThirdPartyContent { get; set; }

	public bool IsHomebrewContent { get; set; }

	public bool IsCoreContent { get; set; }

	public bool IsSupplementContent { get; set; }

	public bool IsPlaytestContent { get; set; }

	public bool IsAdventureLeagueContent { get; set; }

	public bool IsLegal { get; set; }

	public bool IsIncomplete { get; set; }

	public string IncompleteMessage { get; set; }

	public bool IsWorkInProgress { get; set; }

	public bool HasSourceUrl => !string.IsNullOrWhiteSpace(Url);

	public bool HasErrataUrl => !string.IsNullOrWhiteSpace(ErrataUrl);

	public bool HasAuthorUrl => !string.IsNullOrWhiteSpace(AuthorUrl);

	public bool HasImageUrl => !string.IsNullOrWhiteSpace(ImageUrl);

	public bool HasGroupName => !string.IsNullOrWhiteSpace(GroupName);

	public bool HasReleaseDate => !string.IsNullOrWhiteSpace(ReleaseDate);

	public string InformationMessage { get; set; }

	public bool HasInformationMessage => !string.IsNullOrWhiteSpace(InformationMessage);

	public string InformationUrl { get; set; }

	public bool HasInformationUrl => !string.IsNullOrWhiteSpace(InformationUrl);
}
