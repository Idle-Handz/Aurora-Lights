using Builder.Presentation.Models;

namespace Aurora.App.Services;

/// <summary>
/// Turns a partial character load into something short enough to read in a toast. The full list of
/// ids with their saved paths, and the load diagnostics, belong in the log where they can be read
/// and copied.
/// </summary>
public static class PartialLoadReport
{
    private const string Warning = "⚠ Partial load: ";

    public static string Describe(CharacterFile.LoadResult result)
    {
        if (result.Missing.Count == 0) return Warning + result.Message;

        string what = result.Missing.Count == 1 ? "1 saved pick" : $"{result.Missing.Count} saved picks";
        var names = Names(result);
        string listed = names.Count == 0
            ? string.Empty
            : " — " + string.Join(", ", names.Take(3))
              + (names.Count > 3 ? $", and {names.Count - 3} more" : string.Empty);

        return Warning + $"{what} could not be restored{listed}. "
             + "The Console lists them in full; re-pick them and save to clear this.";
    }

    /// <summary>
    /// The last step of a saved path is what the user picked; everything before it is how the
    /// character reached it, which is detail for the log rather than for a toast.
    /// </summary>
    private static List<string> Names(CharacterFile.LoadResult result) => result.Missing
        .Select(missing => missing.SavedPath.Split('>').Last().Trim())
        .Where(name => name.Length > 0 && !name.StartsWith("saved character summary", StringComparison.Ordinal))
        .Distinct(StringComparer.Ordinal)
        .ToList();
}
