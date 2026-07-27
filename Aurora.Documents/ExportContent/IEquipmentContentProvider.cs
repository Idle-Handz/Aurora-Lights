using Aurora.Documents.ExportContent.Equipment;

namespace Aurora.Documents.ExportContent;

public interface IEquipmentContentProvider
{
	EquipmentExportContent GetEquipmentContent();
}
