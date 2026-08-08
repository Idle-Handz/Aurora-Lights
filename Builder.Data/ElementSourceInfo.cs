namespace Builder.Data;

public class ElementSourceInfo
{
	public string Source { get; set; }

	public string SourceId { get; set; }

	public ElementBase SourceElement { get; private set; }

	public bool HasSourceElement => SourceElement != null;

	public string Page { get; set; }

	public string OverrideUrl { get; set; }

	public ElementSourceInfo()
		: this(null)
	{
	}

	public ElementSourceInfo(string source)
	{
		Source = source;
	}

	public void SetSourceElement(ElementBase sourceElement)
	{
		SourceElement = sourceElement;
		Source = sourceElement.Name;
		SourceId = sourceElement.Id;
	}
}
