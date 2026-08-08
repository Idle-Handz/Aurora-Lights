namespace Builder.Data;

public class ElementHeader
{
	public string Name { get; }

	public string Type { get; }

	public string Source { get; protected set; }

	public string Id { get; }

	public ElementHeader(string name, string type, string source, string id)
	{
		Name = name;
		Type = type;
		Source = source;
		Id = id;
	}

	public override string ToString()
	{
		return Name + " (" + Type + ")";
	}
}
