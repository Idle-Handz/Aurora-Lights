using System.Collections.Generic;

namespace Aurora.Documents.ExportContent.Equipment;

public class VehicleExportContent
{
	public string Name { get; set; }

	public List<InventoryItemExportContent> Cargo { get; set; }

	public string WeightCarried { get; set; }

	public VehicleExportContent(string name = "")
	{
		Name = name;
		Cargo = new List<InventoryItemExportContent>();
	}
}
