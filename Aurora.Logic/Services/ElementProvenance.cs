using Builder.Data;
using System.Runtime.CompilerServices;

namespace Builder.Presentation.Services;

/// <summary>
/// Where an element's definition was loaded from, kept beside the element rather than on it. It is
/// loading bookkeeping — used to decide which file a correction supersedes and to report provenance —
/// not part of the element's data, and it never reaches a character file. Entries disappear with the
/// element they describe.
/// </summary>
public static class ElementProvenance
{
    private static readonly ConditionalWeakTable<ElementBase, string> ContentFilePaths = new();

    /// <summary>The content file this element was loaded from, or null when it was not recorded.</summary>
    public static string? GetContentFilePath(ElementBase? element) =>
        element is not null && ContentFilePaths.TryGetValue(element, out string? path) ? path : null;

    public static void SetContentFilePath(ElementBase? element, string? contentFilePath)
    {
        if (element is null) return;
        ContentFilePaths.Remove(element);
        if (!string.IsNullOrWhiteSpace(contentFilePath))
            ContentFilePaths.Add(element, contentFilePath);
    }

    /// <summary>
    /// Carries provenance onto a fresh instance of the same definition, so copies made while building
    /// a character still know where their definition came from.
    /// </summary>
    public static void CopyTo(ElementBase? source, ElementBase? copy) =>
        SetContentFilePath(copy, GetContentFilePath(source));
}
