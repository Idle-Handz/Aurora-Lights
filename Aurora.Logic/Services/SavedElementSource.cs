using Builder.Data;
using System.Xml;
using System.Xml.Linq;

namespace Builder.Presentation.Services;

/// <summary>
/// Diagnostic origin carried by a saved element. This is not an identity, a source restriction,
/// or permission to load a file. Only a filename is saved, never a device's absolute path.
/// </summary>
public sealed record SavedElementSource(string? Book, string? FileName, bool IsLocalCustomization)
{
    public static SavedElementSource Capture(ElementBase element, ElementBase? catalogDefinition = null)
    {
        // Some granted instances are constructed copies without the runtime provenance side table.
        // An exact catalog identity can supply that filename; a same-name element cannot.
        if (catalogDefinition?.Id != element.Id) catalogDefinition = null;
        string path = (ElementProvenance.GetContentFilePath(element)
            ?? ElementProvenance.GetContentFilePath(catalogDefinition) ?? "").Replace('\\', '/');
        bool local = ("/" + path.TrimStart('/')).Contains("/user/local/", StringComparison.OrdinalIgnoreCase);
        return new(Clean(element.Source) ?? Clean(catalogDefinition?.Source), FileOnly(path), local);
    }

    public void WriteTo(XmlNode node)
    {
        if (node is not XmlElement element) return;
        if (Clean(Book) is { } book) element.SetAttribute("source-book", book);
        if (FileOnly(FileName) is { } file) element.SetAttribute("source-file", file);
        if (IsLocalCustomization) element.SetAttribute("source-local", "true");
    }

    public static SavedElementSource? Read(XElement node)
    {
        string? book = Clean((string?)node.Attribute("source-book"));
        string? file = FileOnly((string?)node.Attribute("source-file"));
        bool local = string.Equals((string?)node.Attribute("source-local"), "true", StringComparison.OrdinalIgnoreCase);
        return book is null && file is null && !local ? null : new(book, file, local);
    }

    private static string? FileOnly(string? path) => Clean(path?.Replace('\\', '/').Split('/').Last());
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value)
        ? null : value.ReplaceLineEndings(" ").Trim();
}
