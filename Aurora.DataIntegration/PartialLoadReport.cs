using Builder.Data;
using Builder.Presentation.Models;
using Builder.Presentation.Services;

namespace Aurora.Reflections.Services;

/// <summary>
/// Shows actionable source names in the toast. Element identities and restore paths stay in the log.
/// </summary>
public static class PartialLoadReport
{
    private const string Warning = "⚠ Partial load: ";

    public static string Describe(CharacterFile.LoadResult result, IEnumerable<ElementBase> catalog)
    {
        if (result.Missing.Count == 0) return Warning + result.Message;

        var entries = CharacterLoadDiagnostics.Analyze(result.Missing, catalog);
        var unavailable = entries.Where(e => e.Definition is null).ToArray();
        if (unavailable.Length == 0)
            return "⚠ Some installed content could not be restored. See Console for details.";

        var sources = unavailable.Select(e => SourceName(e.Source))
            .Where(name => name is not null).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        bool unknown = unavailable.Any(e => SourceName(e.Source) is null);
        string message = sources.Length == 0
            ? "⚠ Required content is missing; its sources are unknown."
            : "⚠ Missing content from: " + ListSources(sources) + "."
              + (unknown ? " Other sources are unknown." : "");
        if (entries.Any(e => e.Definition is not null)) message += " Other restore issues remain.";
        return message + " See Console for details.";
    }

    private static string? SourceName(SavedElementSource? source)
    {
        string? book = string.IsNullOrWhiteSpace(source?.Book) ? null : source.Book;
        return source?.IsLocalCustomization == true
            ? (book is null ? "Local customizations" : book + " (local customization)")
            : book;
    }

    private static string ListSources(string[] sources)
    {
        var shown = sources.Take(3).Select(name => name.Length > 70 ? name[..69] + "…" : name).ToList();
        string Remaining() => sources.Length > shown.Count ? $", and {sources.Length - shown.Count} more" : "";
        while (shown.Count > 1 && string.Join(", ", shown).Length + Remaining().Length > 160)
            shown.RemoveAt(shown.Count - 1);
        return string.Join(", ", shown) + Remaining();
    }
}
