using System;
using Builder.Core;

namespace Builder.Data.Files;

public class InformationSection : ObservableObject
{
	private string _displayName;

	private string _description;

	private string _author;

	private string _authorUrl;

	private Version _version;

	private string _revised;

	private string _updateFilename;

	private string _updateUrl;

	public string DisplayName
	{
		get
		{
			return _displayName;
		}
		set
		{
			SetProperty(ref _displayName, value, "DisplayName");
		}
	}

	public string Description
	{
		get
		{
			return _description;
		}
		set
		{
			SetProperty(ref _description, value, "Description");
		}
	}

	public string Author
	{
		get
		{
			return _author;
		}
		set
		{
			SetProperty(ref _author, value, "Author");
		}
	}

	public string AuthorUrl
	{
		get
		{
			return _authorUrl;
		}
		set
		{
			SetProperty(ref _authorUrl, value, "AuthorUrl");
		}
	}

	public Version Version
	{
		get
		{
			return _version;
		}
		set
		{
			SetProperty(ref _version, value, "Version");
		}
	}

	public string Revised
	{
		get
		{
			return _revised;
		}
		set
		{
			SetProperty(ref _revised, value, "Revised");
		}
	}

	public string UpdateFilename
	{
		get
		{
			return _updateFilename;
		}
		set
		{
			SetProperty(ref _updateFilename, value, "UpdateFilename");
		}
	}

	public string UpdateUrl
	{
		get
		{
			return _updateUrl;
		}
		set
		{
			SetProperty(ref _updateUrl, value, "UpdateUrl");
		}
	}

	public bool ContainsInfoNode { get; set; }

	public InformationSection()
	{
		_version = new Version();
	}
}
