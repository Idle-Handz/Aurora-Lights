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
    private static readonly AsyncLocal<AliasScope?> Pending = new();

    private static AliasScope? ActiveScope
    {
        get
        {
            var scope = Pending.Value;
            // Queued notifications may carry the loading ExecutionContext past its lifetime.
            while (scope is { IsDisposed: true }) scope = scope.Previous;
            return scope;
        }
    }

    private static IReadOnlyDictionary<string, string> Current
    {
        get => ActiveScope?.Aliases ?? Volatile.Read(ref forwarding);
        set
        {
            if (ActiveScope is { } scope) scope.Aliases = value;
            else Volatile.Write(ref forwarding, value);
        }
    }

    /// <summary>
    /// Stages catalog aliases, including generated proxies, in the current asynchronous flow.
    /// Other readers keep the live catalog's aliases until the completed load is published.
    /// </summary>
    public static AliasScope BeginScope() => new();

    public sealed class AliasScope : IDisposable
    {
        private readonly AliasScope? _previous;
        private volatile bool _disposed;
        internal IReadOnlyDictionary<string, string> Aliases;
        internal bool IsDisposed => _disposed;
        internal AliasScope? Previous => _previous;

        internal AliasScope()
        {
            _previous = Pending.Value;
            Aliases = Current;
            Pending.Value = this;
        }

        /// <summary>Publishes a completed catalog and returns a rollback for its activation.</summary>
        public Action Publish()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var published = Aliases;
            var previous = Interlocked.Exchange(ref forwarding, published);
            // A later successful load must not be undone by an older activation's rollback.
            return () => Interlocked.CompareExchange(ref forwarding, previous, published);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Pending.Value = _previous;
            _disposed = true;
        }
    }

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
        Current = map;
    }

    public static void Clear() => Current = new Dictionary<string, string>(StringComparer.Ordinal);

    public static int Count => Current.Count;

    // Generated proxies are forwarding addresses for the same underlying definition.
    // Derive their old IDs only from explicit aliases; never infer equivalence by name.
    internal static void ForwardGeneratedIds(string targetId, Func<string, string?> generateId)
    {
        var additions = Current.Keys.Where(saved => TryGetTarget(saved, out string target) && target == targetId)
            .Select(saved => generateId(saved)).Where(id => id is not null).ToArray();
        if (additions.Length == 0) return;
        string? generatedTarget = generateId(targetId);
        if (generatedTarget is null) return;
        var map = new Dictionary<string, string>(Current, StringComparer.Ordinal);
        foreach (string? saved in additions)
            if (saved != generatedTarget) map.TryAdd(saved!, generatedTarget);
        Current = map;
    }

    /// <summary>The id an old reference now points at, following a short chain if one exists.</summary>
    public static bool TryGetTarget(string? savedId, out string targetId)
    {
        targetId = "";
        if (string.IsNullOrWhiteSpace(savedId)) return false;
        string current = savedId.Trim();
        var aliases = Current;
        // A rename of a rename is ordinary; a cycle is not, so give up rather than spin.
        for (int hop = 0; hop < 8; hop++)
        {
            if (!aliases.TryGetValue(current, out string? next)) return hop > 0;
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
        // A spell proxy is built when something first asks for its category, so a saved character
        // naming one directly misses until that happens. ResolveOrBuild builds it rather than
        // letting the element be reported as lost; only a proxy-shaped id can trigger that.
        var element = Data.SpellProxyCatalog.ResolveOrBuild(collection, savedId);
        if (element != null) return element;
        return TryGetTarget(savedId, out string target)
            ? Data.SpellProxyCatalog.ResolveOrBuild(collection, target) : null;
    }
}
