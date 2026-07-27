using System.Collections.Generic;
using Aurora.Documents.ExportContent.Equipment;
using Aurora.Documents.Sheets;
using Aurora.Documents.Writers.Base;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Aurora.Documents.Writers;

public sealed class EquipmentPageWriter : CharacterSheetDocumentWriterBase
{
	public const int MaximumAdventuringGearCount = 40;

	public const int MaximumMagicItemCount = 20;

	public const int MaximumValuablesCount = 10;

	public const int MaximumStorageCount = 2;

	public const int MaximumItemsPerStorageCount = 10;

	private int _currentItemIndex;

	private int _currentMagicItemIndex;

	private int _currentValuableIndex;

	public EquipmentPageWriter(CharacterSheetConfiguration configuration, PdfStamper stamper)
		: base(configuration, stamper)
	{
		_currentItemIndex = 0;
		_currentMagicItemIndex = 0;
		_currentValuableIndex = 0;
	}

	public void Write(EquipmentExportContent exportContent)
	{
		foreach (InventoryItemExportContent item in exportContent.AdventuringGear)
		{
			if (_currentItemIndex == 40)
			{
				break;
			}
			WriteItem(item, _currentItemIndex);
			_currentItemIndex++;
		}
		foreach (InventoryItemExportContent magicItem in exportContent.MagicItems)
		{
			if (_currentMagicItemIndex == 20)
			{
				if (AllowOverflow())
				{
					WriteOverflowingMagicItems(exportContent, _currentMagicItemIndex);
				}
				break;
			}
			WriteMagicItem(magicItem, _currentMagicItemIndex);
			_currentMagicItemIndex++;
		}
		foreach (InventoryItemExportContent valuable in exportContent.Valuables)
		{
			if (_currentValuableIndex == 10)
			{
				if (AllowOverflow())
				{
					WriteOverflowingValuableItems(exportContent, _currentValuableIndex);
				}
				break;
			}
			WriteValuableItem(valuable, _currentValuableIndex);
			_currentValuableIndex++;
		}
		Dictionary<string, string> collection = new Dictionary<string, string>
		{
			{ "equipment_page_attunement_current", exportContent.AttunementCurrent },
			{ "equipment_page_attunement_max", exportContent.AttunementMaximum },
			{
				"equipment_page_coins_cp",
				exportContent.Coinage.Copper
			},
			{
				"equipment_page_coins_sp",
				exportContent.Coinage.Silver
			},
			{
				"equipment_page_coins_ep",
				exportContent.Coinage.Electrum
			},
			{
				"equipment_page_coins_gp",
				exportContent.Coinage.Gold
			},
			{
				"equipment_page_coins_pp",
				exportContent.Coinage.Platinum
			},
			{ "equipment_page_weight_capacity", exportContent.CarryingCapacity },
			{ "equipment_page_weight_drag", exportContent.DragCapacity },
			{ "equipment_page_weight_carried", exportContent.WeightCarried }
		};
		StampCollection(collection);
		int num = 0;
		foreach (StoredItemsExportContent storageLocation in exportContent.StorageLocations)
		{
			if (num == 2)
			{
				break;
			}
			Stamp($"equipment_page_vehicle_{num + 1}_name", storageLocation.Name);
			int num2 = 0;
			foreach (InventoryItemExportContent item2 in storageLocation.Items)
			{
				if (num2 == 10)
				{
					break;
				}
				WriteStorageItem(item2, num, num2);
				num2++;
			}
			num++;
		}
		Stamp("equipment_page_additional_treasure", exportContent.AdditionalTreasure, setFontSize: true, 8.2f);
		Stamp("equipment_page_quest_items", exportContent.QuestItems, setFontSize: true, 8.2f);
		if (base.Configuration.IncludeFormatting)
		{
			ReplaceAreaField("equipment_page_magic_items", exportContent.AttunedMagicItems, 7f);
			return;
		}
		string content = base.DescriptionConverter.GeneratePlainDescription(exportContent.AttunedMagicItems);
		Stamp("equipment_page_magic_items", content, setFontSize: true, 8.2f);
	}

	private void WriteItem(InventoryItemExportContent item, int itemIndex)
	{
		Stamp($"equipment_page_gear_name.{itemIndex}", item.IsEquipped ? ("[" + item.Name + "]") : item.Name);
		Stamp($"equipment_page_gear_count.{itemIndex}", item.Amount);
		Stamp($"equipment_page_gear_weight.{itemIndex}", item.Weight);
	}

	private void WriteMagicItem(InventoryItemExportContent item, int itemIndex)
	{
		Stamp($"equipment_page_magic_gear_name.{itemIndex}", item.IsEquipped ? ("[" + item.Name + "]") : item.Name);
		Stamp($"equipment_page_magic_gear_count.{itemIndex}", item.Amount);
		Stamp($"equipment_page_magic_gear_weight.{itemIndex}", item.Weight);
	}

	private void WriteValuableItem(InventoryItemExportContent item, int itemIndex)
	{
		Stamp($"equipment_page_valuable_name.{itemIndex}", item.IsEquipped ? ("[" + item.Name + "]") : item.Name);
		Stamp($"equipment_page_valuable_count.{itemIndex}", item.Amount);
		Stamp($"equipment_page_valuable_weight.{itemIndex}", item.Weight);
	}

	private void WriteStorageItem(InventoryItemExportContent item, int storageIndex, int itemIndex)
	{
		Stamp($"equipment_page_vehicle_{storageIndex + 1}_cargo_name.{itemIndex}", item.Name);
		Stamp($"equipment_page_vehicle_{storageIndex + 1}_cargo_count.{itemIndex}", item.Amount);
		Stamp($"equipment_page_vehicle_{storageIndex + 1}_cargo_weight.{itemIndex}", item.Weight);
	}

	private bool AllowOverflow()
	{
		return _currentItemIndex + 1 < 40;
	}

	private void WriteOverflowingMagicItems(EquipmentExportContent exportContent, int startingIndex)
	{
		_currentItemIndex++;
		if (AllowOverflow())
		{
			WriteAdventuringGearSeparator("Magic Items — Continued".ToUpperInvariant(), _currentItemIndex);
			_currentItemIndex++;
		}
		for (int i = startingIndex; i < exportContent.MagicItems.Count; i++)
		{
			if (_currentItemIndex == 40)
			{
				break;
			}
			WriteItem(exportContent.MagicItems[i], _currentItemIndex);
			_currentItemIndex++;
		}
	}

	private void WriteOverflowingValuableItems(EquipmentExportContent exportContent, int startingIndex)
	{
		_currentItemIndex++;
		if (AllowOverflow())
		{
			WriteAdventuringGearSeparator("Valuables Items — Continued".ToUpperInvariant(), _currentItemIndex);
			_currentItemIndex++;
		}
		for (int i = startingIndex; i < exportContent.Valuables.Count; i++)
		{
			if (_currentItemIndex == 40)
			{
				break;
			}
			WriteItem(exportContent.Valuables[i], _currentItemIndex);
			_currentItemIndex++;
		}
	}

	private void WriteAdventuringGearSeparator(string name, int index)
	{
		SetTextColor($"equipment_page_gear_name.{index}", BaseColor.DARK_GRAY);
		SetTextColor($"equipment_page_gear_count.{index}", BaseColor.DARK_GRAY);
		SetTextColor($"equipment_page_gear_weight.{index}", BaseColor.DARK_GRAY);
		Stamp($"equipment_page_gear_name.{index}", name, setFontSize: true, 6f);
		Stamp($"equipment_page_gear_count.{index}", "#", setFontSize: true, 6f);
		Stamp($"equipment_page_gear_weight.{index}", "lb.", setFontSize: true, 6f);
	}
}
