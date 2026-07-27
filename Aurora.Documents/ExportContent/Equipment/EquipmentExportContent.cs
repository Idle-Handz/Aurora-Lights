using System.Collections.Generic;

namespace Aurora.Documents.ExportContent.Equipment;

public class EquipmentExportContent
{
	public List<InventoryItemExportContent> AdventuringGear { get; set; }

	public List<InventoryItemExportContent> MagicItems { get; set; }

	public string AttunementCurrent { get; set; }

	public string AttunementMaximum { get; set; }

	public List<InventoryItemExportContent> Valuables { get; set; }

	public CoinageExportContent Coinage { get; set; }

	public string WeightCarried { get; set; }

	public string CarryingCapacity { get; set; }

	public string DragCapacity { get; set; }

	public string AdditionalTreasure { get; set; }

	public List<StoredItemsExportContent> StorageLocations { get; }

	public string AttunedMagicItems { get; set; }

	public string QuestItems { get; set; }

	public EquipmentExportContent()
	{
		AdventuringGear = new List<InventoryItemExportContent>();
		MagicItems = new List<InventoryItemExportContent>();
		Valuables = new List<InventoryItemExportContent>();
		Coinage = new CoinageExportContent();
		StorageLocations = new List<StoredItemsExportContent>();
		AttunedMagicItems = "";
		AttunementCurrent = "";
		AttunementMaximum = "";
	}
}
