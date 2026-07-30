using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Elements;

namespace Builder.Presentation.Services;

public sealed record BuildSourceRestrictionSnapshot(
    IReadOnlySet<string> ElementIds,
    IReadOnlySet<string> SourceNames)
{
    public static BuildSourceRestrictionSnapshot Empty { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static BuildSourceRestrictionSnapshot CaptureCurrent()
    {
        try
        {
            return Capture(CharacterManager.Current.SourcesManager);
        }
        catch
        {
            return Empty;
        }
    }

    public static BuildSourceRestrictionSnapshot Capture(ISourceRestrictionsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new BuildSourceRestrictionSnapshot(
            provider.GetRestrictedElements().ToHashSet(StringComparer.OrdinalIgnoreCase),
            provider.GetRestrictedSources().ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    public bool Allows(ElementBase element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return !ElementIds.Contains(element.Id)
            && !SourceNames.Contains(element.Source ?? string.Empty);
    }
}

/// <summary>
/// Authoritative selection-option query used by every client. It does not
/// mutate character selection state, and combines host-specific content
/// enrichment with the active character's source restrictions before
/// delegating option construction to the resolver.
/// </summary>
public static class BuildSelectionOptionQueryService
{
    public static IReadOnlyList<BuildSelectionOption> Query(
        SelectRule rule,
        int number = 1,
        BuildSelectionOptionResolverSettings? hostSettings = null,
        BuildSourceRestrictionSnapshot? sourceRestrictions = null)
    {
        ArgumentNullException.ThrowIfNull(rule);

        hostSettings ??= new BuildSelectionOptionResolverSettings();
        sourceRestrictions ??= BuildSourceRestrictionSnapshot.CaptureCurrent();

        var settings = new BuildSelectionOptionResolverSettings
        {
            SpellAccessMap = hostSettings.SpellAccessMap,
            SortMetadataSelector = hostSettings.SortMetadataSelector,
            ElementFallbackProvider = hostSettings.ElementFallbackProvider,
            ListFallbackProvider = hostSettings.ListFallbackProvider,
            RestrictedElementIds = Merge(
                hostSettings.RestrictedElementIds,
                sourceRestrictions.ElementIds),
            RestrictedSourceNames = Merge(
                hostSettings.RestrictedSourceNames,
                sourceRestrictions.SourceNames)
        };

        return BuildSelectionOptionResolver.ResolveOptions(rule, number, settings);
    }

    private static IReadOnlySet<string> Merge(
        IReadOnlySet<string>? first,
        IReadOnlySet<string> second)
    {
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (first is not null)
            merged.UnionWith(first);
        merged.UnionWith(second);
        return merged;
    }
}
