namespace Aurora.Documents.ExportContent.Equipment;

public class InventoryItemExportContent
{
	public string ReferenceId { get; set; }

	public string Name { get; set; }

	public string Amount { get; set; }

	public string Weight { get; set; }

	public string IndividualPrice { get; set; }

	public bool IsEquipped { get; set; }

	public InventoryItemExportContent(string name = "")
	{
		Name = name;
	}
}
