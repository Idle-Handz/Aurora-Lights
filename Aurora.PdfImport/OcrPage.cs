namespace Aurora.PdfImport;

/// <summary>
/// Platform-agnostic representation of OCR results for one PDF page.
/// Coordinates are in PDF points (1/72 inch), origin at top-left.
/// </summary>
public sealed record OcrPage(IReadOnlyList<OcrWord> Words, double WidthPt, double HeightPt);

/// <summary>A single word recognised by OCR with its bounding box.</summary>
public sealed record OcrWord(string Text, double Left, double Top, double Right, double Bottom);
