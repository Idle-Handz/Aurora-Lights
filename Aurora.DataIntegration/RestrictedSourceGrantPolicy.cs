using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Sources;

namespace Aurora.App.Services;

/// <summary>
/// Keeps content a character restricts out of its automatic grants. Without this, restricting a
/// source hides its options but rules in other books still hand out its elements — a character that
/// restricts a weapon supplement would still be granted that supplement's weapon proficiencies.
/// Clearing the restriction gives them back, because grants are re-evaluated on every reprocess.
/// Aurora Legacy registers no policy and is unaffected.
/// </summary>
public sealed class RestrictedSourceGrantPolicy : IGrantPolicy
{
    private SourcesManager? _sources;
    private HashSet<string> _sourceNames = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _elementIds = new(StringComparer.Ordinal);

    public bool IsSuppressed(ElementBase granted, GrantRule rule)
    {
        SourcesManager? sources = CharacterManager.Current?.SourcesManager;
        if (sources is null)
            return false;
        if (!ReferenceEquals(sources, _sources))
            Attach(sources);
        if (_sourceNames.Count == 0 && _elementIds.Count == 0)
            return false;

        return _elementIds.Contains(granted.Id)
            || (!string.IsNullOrWhiteSpace(granted.Source) && _sourceNames.Contains(granted.Source));
    }

    private void Attach(SourcesManager sources)
    {
        if (_sources is not null)
            _sources.SourceRestrictionsApplied -= OnRestrictionsApplied;
        _sources = sources;
        sources.SourceRestrictionsApplied += OnRestrictionsApplied;
        Refresh();
    }

    private void OnRestrictionsApplied(object? sender, EventArgs e) => Refresh();

    // Read once per restriction change: this is consulted for every grant of every reprocess.
    private void Refresh()
    {
        if (_sources is null) return;
        _sourceNames = new HashSet<string>(_sources.GetRestrictedSources().Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);
        _elementIds = new HashSet<string>(_sources.GetRestrictedElementIds(), StringComparer.Ordinal);
    }
}
