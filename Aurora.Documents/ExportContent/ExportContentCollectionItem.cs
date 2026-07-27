namespace Aurora.Documents.ExportContent;

public class ExportContentCollectionItem
{
	public string Name { get; }

	public string Description { get; }

	public ExportContentCollectionItem(string name, string description)
	{
		Name = name;
		Description = description;
	}
}
