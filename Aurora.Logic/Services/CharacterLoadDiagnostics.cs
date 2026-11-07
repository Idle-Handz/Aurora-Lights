using Builder.Data;
using System.Text;

namespace Builder.Presentation.Services;

/// <summary>Explains a partial load using exact catalog identities and recorded save provenance.</summary>
public static class CharacterLoadDiagnostics
{
    public static Entry[] Analyze(IReadOnlyList<CharacterLoadValidation.MissingElement> missing,
        IEnumerable<ElementBase> catalog)
    {
        var definitions = catalog.GroupBy(e => e.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        ElementBase? Resolve(string id)
        {
            if (definitions.TryGetValue(id, out var direct)) return direct;
            return ElementIdAliases.TryGetTarget(id, out string target)
                ? definitions.GetValueOrDefault(target) : null;
        }

        return missing.Select(entry =>
        {
            var definition = Resolve(entry.Id);
            return new Entry(entry, definition,
                definition is null ? entry.SavedSource : SavedElementSource.Capture(definition));
        }).ToArray();
    }

    public static string Describe(IReadOnlyList<CharacterLoadValidation.MissingElement> missing,
        IEnumerable<ElementBase> catalog)
    {
        var rows = Analyze(missing, catalog);
        var text = new StringBuilder("Saved character elements could not be restored.");
        text.AppendLine().AppendLine($"{Entries(rows.Length)}; {rows.Sum(r => r.Missing.Count)} missing occurrences.");
        int local = rows.Count(r => r.Source?.IsLocalCustomization == true || r.Missing.SavedSource?.IsLocalCustomization == true);
        if (local > 0) text.AppendLine($"Local customization references: {Entries(local)} (included below).");

        foreach (bool available in new[] { false, true })
        {
            var category = rows.Where(r => (r.Definition is not null) == available).ToArray();
            if (category.Length == 0) continue;
            text.AppendLine().AppendLine(available
                ? $"Available definitions not restored: {Entries(category.Length)}. Check saved grant paths, prerequisites, or changed content."
                : $"Missing definitions: {Entries(category.Length)}. Check installed content and local customizations.");
            foreach (var group in category.GroupBy(r => SourceLabel(r.Source), StringComparer.Ordinal)
                         .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                text.AppendLine($"  {group.Key} ({(available ? "current catalog" : "saved source")}): {Entries(group.Count())}");
                foreach (var row in group)
                {
                    text.AppendLine($"    {row.Missing.Id} — missing {row.Missing.Count}; saved path: {row.Missing.SavedPath}");
                    if (row.Source?.FileName is { } file) text.AppendLine($"      Source file: {file}");
                    if (row.Definition is { } definition && definition.Id != row.Missing.Id)
                        text.AppendLine($"      Resolves to: {definition.Id}");
                    if (available && row.Missing.SavedSource is { } saved && saved != row.Source)
                        text.AppendLine($"      Saved origin: {SourceLabel(saved)}; file: {saved.FileName ?? "unknown"}");
                }
            }
        }
        if (rows.Any(r => r.Source?.Book is null))
            text.AppendLine().AppendLine("Source unknown means no book was recorded or found; IDs and parent choices are not used to guess a source. Older saves may lack source metadata.");
        return text.ToString().TrimEnd();
    }

    private static string SourceLabel(SavedElementSource? source) =>
        (source?.IsLocalCustomization == true ? "Local customization — " : "") + (source?.Book ?? "Source unknown");

    private static string Entries(int count) => $"{count} {(count == 1 ? "entry" : "entries")}";

    public sealed record Entry(CharacterLoadValidation.MissingElement Missing, ElementBase? Definition, SavedElementSource? Source);
}
