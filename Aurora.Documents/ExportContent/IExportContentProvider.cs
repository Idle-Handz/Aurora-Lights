using Aurora.Documents.ExportContent.Equipment;
using Aurora.Documents.ExportContent.Notes;

namespace Aurora.Documents.ExportContent;

public interface IExportContentProvider
{
	EquipmentExportContent GetEquipmentContent();

	NotesExportContent GetNotesContent();
}
