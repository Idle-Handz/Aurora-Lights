using Builder.Presentation;

namespace Aurora.Tests.Helpers;

/// <summary>Optional corpus acceptance is reported as skipped, never as a successful early return.
/// The test harness uses an isolated install containing only this assembly's embedded catalog.</summary>
public sealed class BundledContentFactAttribute : FactAttribute
{
    public BundledContentFactAttribute(params string[] requiredIds)
    {
        var assembly = typeof(CharacterManager).Assembly;
        var remaining = requiredIds.ToHashSet(StringComparer.Ordinal);
        foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".xml")))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            string xml = reader.ReadToEnd();
            remaining.RemoveWhere(id => xml.Contains($"id=\"{id}\"", StringComparison.Ordinal));
            if (remaining.Count == 0) return;
        }
        Skip = "Optional corpus is not bundled: " + string.Join(", ", remaining);
    }
}
