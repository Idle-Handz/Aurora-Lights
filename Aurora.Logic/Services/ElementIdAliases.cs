using Builder.Data;

namespace Builder.Presentation.Services;

/// <summary>
/// Forwarding addresses for element ids a saved character may still name.
///
/// Content that renames an element leaves every character saved against the old id referring to
/// something that no longer exists, and the loss is silent: the element simply does not come back.
/// An alias lets the author say where the meaning went.
///
/// A forwarding address is only consulted when the saved id resolves to nothing. An id that still
/// exists is never redirected, because a live identity outranks an alias - a character naming a
/// Player's Handbook feature must keep getting the Player's Handbook feature even if some book
/// later forwards that id somewhere else.
///
/// This cannot recover an id that exists but is no longer granted along the character's saved path.
/// That is a different loss, and an alias is the wrong tool for it.
/// </summary>
public static class ElementIdAliases
{
    private static IReadOnlyDictionary<string, string> forwarding =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Replaces the known aliases, normally right after a catalog is loaded.</summary>
    public static void Set(IEnumerable<KeyValuePair<string, string>> aliases)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (saved, target) in aliases)
        {
            if (string.IsNullOrWhiteSpace(saved) || string.IsNullOrWhiteSpace(target)) continue;
            if (string.Equals(saved, target, StringComparison.Ordinal)) continue;
            map[saved.Trim()] = target.Trim();
        }
        forwarding = map;
    }

    public static void Clear() => forwarding = new Dictionary<string, string>(StringComparer.Ordinal);

    public static int Count => forwarding.Count;

    /// <summary>The id an old reference now points at, following a short chain if one exists.</summary>
    public static bool TryGetTarget(string? savedId, out string targetId)
    {
        targetId = "";
        if (string.IsNullOrWhiteSpace(savedId)) return false;
        string current = savedId.Trim();
        // A rename of a rename is ordinary; a cycle is not, so give up rather than spin.
        for (int hop = 0; hop < 8; hop++)
        {
            if (!forwarding.TryGetValue(current, out string? next)) return hop > 0;
            if (string.Equals(next, savedId.Trim(), StringComparison.Ordinal)) return false;
            current = targetId = next;
        }
        return false;
    }

    /// <summary>
    /// The element a saved id means now: itself when it still exists, otherwise whatever it was
    /// renamed to. Null when neither resolves.
    /// </summary>
    public static ElementBase? Resolve(ElementBaseCollection collection, string? savedId)
    {
        if (collection == null || string.IsNullOrWhiteSpace(savedId)) return null;
        var element = collection.GetElement(savedId);
        if (element != null) return element;
        return TryGetTarget(savedId, out string target) ? collection.GetElement(target) : null;
    }
}
