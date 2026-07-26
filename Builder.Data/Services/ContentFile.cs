namespace Builder.Data.Services;

public abstract class ContentFile
{
	public ContentFileInformation Information { get; }

	protected ContentFile()
	{
		Information = new ContentFileInformation();
	}
}
