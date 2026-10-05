using Builder.Data;
using Builder.Data.Elements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Builder.Presentation.Services.Data;

/// <summary>
/// Builds the "Additional &lt;list&gt; Spell" proxy items on demand instead of at content load.
///
/// Those proxies are a cross product of every spell and every spellcasting list. For the shipped
/// catalog that is 2,136 x 37 = 79,032 items, which measured at roughly eleven seconds and four
/// hundred megabytes of the twenty-two second content load - spent on one picker that most sessions
/// never open. The list names can be derived without building anything, so the categories stay
/// available while the items behind each one are built the first time something asks for them.
///
/// Materialized proxies are added to the catalog they were primed with, so everything that resolves
/// an element by id keeps working once a category is in. Priming happens at the end of content
/// post-processing and discards whatever the previous catalog had materialized.
/// </summary>
public static class SpellProxyCatalog
{
    private const string Infix = "_INTERNAL_ITEM_";
    private const string Suffix = "_SPELL_PROXY_";

    private static readonly object Gate = new object();
    private static ElementBaseCollection _catalog;
    private static List<string> _listNames = new List<string>();
    private static HashSet<string> _unavailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Materialized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set by the loader so a test or tool can keep the old all-at-once behavior.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Adopts a freshly post-processed catalog. Any proxies materialized against the previous one
    /// are forgotten along with it, so a content refresh cannot leave stale categories behind.
    /// </summary>
    /// <param name="unavailableIds">
    /// Identities the content database declares unavailable. Eager generation was followed by a
    /// sweep that removed these, so building a proxy later has to honour the same exclusion - a
    /// generated id must not make a deliberately unavailable identity usable again.
    /// </param>
    public static void Prime(ElementBaseCollection catalog, IEnumerable<string> listNames, IEnumerable<string> unavailableIds)
    {
        lock (Gate)
        {
            _catalog = catalog;
            _listNames = (listNames ?? Enumerable.Empty<string>()).ToList();
            _unavailable = new HashSet<string>(
                (unavailableIds ?? Enumerable.Empty<string>()).Select(id => (id ?? "").Trim()),
                StringComparer.OrdinalIgnoreCase);
            Materialized.Clear();
        }
    }

    public static void Reset()
    {
        lock (Gate)
        {
            _catalog = null;
            _listNames = new List<string>();
            _unavailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Materialized.Clear();
        }
    }

    // Loading prepares private elements first; retain the active proxy state if activation fails.
    internal static Action CaptureRestore()
    {
        lock (Gate)
        {
            var catalog = _catalog;
            var listNames = _listNames;
            var unavailable = _unavailable;
            var materialized = Materialized.ToArray();
            return () =>
            {
                lock (Gate)
                {
                    _catalog = catalog;
                    _listNames = listNames;
                    _unavailable = unavailable;
                    Materialized.Clear();
                    Materialized.UnionWith(materialized);
                }
            };
        }
    }

    /// <summary>The picker category each spellcasting list is offered under.</summary>
    public static string CategoryFor(string listName) =>
        string.IsNullOrWhiteSpace(listName) ? "Additional Spell" : $"Additional {listName} Spell";

    /// <summary>
    /// Every category the proxies would supply, whether or not it has been built. The picker needs
    /// these up front; building them is what waits.
    /// </summary>
    public static IReadOnlyList<string> Categories
    {
        get
        {
            lock (Gate)
                return _catalog == null ? Array.Empty<string>() : _listNames.Select(CategoryFor).ToList();
        }
    }

    /// <summary>
    /// Builds one category's proxies if they are not in the catalog yet. Returns false when this is
    /// not a spell-proxy category, so a caller can fall through to whatever else supplies it.
    /// </summary>
    public static bool EnsureCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return false;
        lock (Gate)
        {
            if (_catalog == null) return false;
            string listName = _listNames.FirstOrDefault(name =>
                string.Equals(CategoryFor(name), category, StringComparison.OrdinalIgnoreCase));
            if (listName == null) return false;
            return MaterializeLocked(listName);
        }
    }

    /// <summary>
    /// Builds whichever category would contain <paramref name="id"/>. A saved character can name a
    /// proxy directly - an "Additional Spell" item in its build tree - and that reference has to
    /// resolve on load even though nothing opened the picker.
    /// </summary>
    public static bool EnsureId(ElementBaseCollection collection, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOf(Suffix, StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        lock (Gate)
        {
            // Only the primed catalog may grow. A test or a projection passes its own collection,
            // and quietly adding generated content to it would be a surprise.
            if (_catalog == null || !ReferenceEquals(collection, _catalog)) return false;
            string listName = _listNames.FirstOrDefault(name =>
                id.IndexOf(Infix + TokenFor(name) + Suffix, StringComparison.OrdinalIgnoreCase) >= 0);
            if (listName == null) return false;
            return MaterializeLocked(listName);
        }
    }

    /// <summary>
    /// Looks an id up, building its proxy category first if nothing has it yet. Every load path that
    /// resolves a saved id against the catalog should come through here: a deferred proxy is absent
    /// until something asks, and a plain <c>GetElement</c> reports that absence as a lost element.
    /// </summary>
    public static ElementBase ResolveOrBuild(ElementBaseCollection collection, string id)
    {
        if (collection == null || string.IsNullOrWhiteSpace(id)) return null;
        ElementBase element = collection.GetElement(id);
        if (element != null) return element;
        return EnsureId(collection, id) ? collection.GetElement(id) : null;
    }

    /// <summary>The list name as it appears inside a generated proxy id.</summary>
    private static string TokenFor(string listName) => (listName ?? "").Replace(" ", "_");

    private static bool MaterializeLocked(string listName)
    {
        if (!Materialized.Add(listName)) return false;
        // The generator reads the catalog into lists before emitting, so adding to it afterwards
        // is safe. Spell and source rows are the same either way; only the list filter differs.
        var generated = new InternalElementsGenerator().GenerateInternalSpells(
            _catalog, name => string.Equals(name, listName, StringComparison.Ordinal));
        if (_unavailable.Count > 0)
            generated = generated.Where(e => !_unavailable.Contains((e.Id ?? "").Trim())).ToList();
        _catalog.AddRange(generated);
        Builder.Core.Logging.Logger.Info("materialized {0} spell proxies for {1}",
            (object) generated.Count, (object) CategoryFor(listName));
        return generated.Count > 0;
    }
}
