using System.Collections.Generic;

namespace Aurora.Documents.ExportContent.Equipment;

public class StoredItemsExportContent
{
	public string Name { get; set; }

	public List<InventoryItemExportContent> Items { get; set; }

	public StoredItemsExportContent(string name = "")
	{
		Name = name;
		Items = new List<InventoryItemExportContent>();
	}
}
