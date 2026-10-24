using System.Collections.Generic;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Aurora.Documents.Helpers;

public static class PdfLayoutExtensions
{
    public sealed class FieldPosition
    {
        public int page { get; init; }
        public Rectangle position { get; init; }
    }

    // The fork returns five floats per widget, already adjusted for page rotation.
    public static IList<FieldPosition> GetWidgetPositions(this AcroFields fields, string name)
    {
        float[] values = fields.GetFieldPositions(name);
        if (values == null) return null;
        var positions = new List<FieldPosition>();
        for (int i = 0; i + 4 < values.Length; i += 5)
            positions.Add(new FieldPosition { page = (int)values[i], position = new Rectangle(values[i + 1], values[i + 2], values[i + 3], values[i + 4]) });
        return positions;
    }

    // Optional export values used to be accepted as null by the old engine.
    public static bool SetFieldValue(this AcroFields fields, string name, string value)
    {
        value ??= string.Empty;
        // PDF checkboxes use Off, not an arbitrary No/empty appearance name.
        if (fields.GetFieldType(name) == AcroFields.FIELD_TYPE_CHECKBOX && (value == "No" || value.Length == 0))
            value = "Off";
        return fields.SetField(name, value);
    }

    public static void SetSimpleColumn(this ColumnText column, Rectangle bounds) =>
        column.SetSimpleColumn(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
}
