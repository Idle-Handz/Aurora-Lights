using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;
using System.Text;
using System.Text.RegularExpressions;

namespace Builder.Presentation.Services;

/// <summary>
/// Creates inventory entries while preserving Aurora's base-item plus magic-item
/// composition model for magic weapons and armor.
/// </summary>
public static class InventoryItemFactory
{
    private sealed record IndexedComposedName(string NormalizedName, ElementBase BaseItem);

    private sealed record IndexedItemNames(
        string NormalizedName,
        IReadOnlyList<IndexedComposedName> ComposedNames);

    private sealed class InventorySearchIndex
    {
        private readonly IReadOnlyDictionary<string, IndexedItemNames> _itemsById;

        public InventorySearchIndex(IReadOnlyDictionary<string, IndexedItemNames> itemsById)
        {
            _itemsById = itemsById;
        }

        public bool TryGet(ElementBase element, out IndexedItemNames names) =>
            _itemsById.TryGetValue(element.Id, out names!);
    }

    private static readonly object SearchIndexGate = new();
    private static InventorySearchIndex? _searchIndex;
    private static Task<InventorySearchIndex>? _searchIndexBuildTask;
    private static int _searchIndexGeneration;

    public enum TemplateKind
    {
        Armor,
        Weapon,
    }

    public static TemplateKind? GetTemplateKind(ElementBase element)
    {
        if (element is not Item item ||
            (!item.Type.Equals("Magic Item", StringComparison.OrdinalIgnoreCase) &&
             !item.Type.Equals("Item", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (item.ElementSetters.ContainsSetter("weapon"))
            return TemplateKind.Weapon;
        if (item.ElementSetters.ContainsSetter("armor"))
            return TemplateKind.Armor;
        return null;
    }

    public static IReadOnlyList<Item> GetCompatibleBaseItems(
        CharacterInventory inventory,
        ElementBase element)
    {
        if (element is not Item item)
            return [];

        try
        {
            return GetTemplateKind(item) switch
            {
                TemplateKind.Weapon => ResolveCompatibleWeapons(inventory, item)
                    .Cast<Item>()
                    .OrderBy(candidate => candidate.Name)
                    .ToList(),
                TemplateKind.Armor => ResolveCompatibleArmor(inventory, item)
                    .Cast<Item>()
                    .OrderBy(candidate => candidate.Name)
                    .ToList(),
                _ => [],
            };
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Builds and caches normalized item names plus the display names produced by applying
    /// magic armor/weapon templates to compatible bases. Call this after content loading so
    /// picker searches do not repeat support-expression evaluation for every keystroke.
    /// </summary>
    public static async Task PrecomputeSearchIndexAsync(
        CancellationToken cancellationToken = default)
    {
        Task<InventorySearchIndex> buildTask = GetOrStartSearchIndexBuild();
        await buildTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Discards precomputed item names after the element catalog changes.</summary>
    public static void InvalidateSearchIndex()
    {
        lock (SearchIndexGate)
        {
            _searchIndexGeneration++;
            _searchIndex = null;
            _searchIndexBuildTask = null;
        }
    }

    /// <summary>
    /// Creates a reusable predicate for one query. Query normalization and composed-template
    /// lookup are performed once rather than once per element.
    /// </summary>
    public static Func<ElementBase, bool> CreateSearchPredicate(
        CharacterInventory inventory,
        string query,
        Func<ElementBase, bool>? allowsBaseItem = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        if (string.IsNullOrWhiteSpace(query))
            return static _ => true;

        var normalizedQuery = NormalizeSearchText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return element =>
                element.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        var terms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        InventorySearchIndex index = GetSearchIndex();

        return element =>
        {
            if (index.TryGet(element, out IndexedItemNames? names))
            {
                if (MatchesNormalizedSearchTerms(names.NormalizedName, terms))
                    return true;

                return names.ComposedNames.Any(candidate =>
                    (allowsBaseItem?.Invoke(candidate.BaseItem) ?? true) &&
                    MatchesNormalizedSearchTerms(candidate.NormalizedName, terms));
            }

            // Elements added after the current catalog snapshot are uncommon, but keep the
            // direct API correct until the next invalidation/rebuild.
            if (MatchesSearchTerms(element.Name, terms))
                return true;

            if (element is not Item template || GetTemplateKind(template) is null)
                return false;

            return GetCompatibleBaseItems(inventory, template)
                .Where(baseItem => allowsBaseItem?.Invoke(baseItem) ?? true)
                .Any(baseItem => MatchesSearchTerms(
                    ComposeDisplayName(baseItem, template),
                    terms));
        };
    }

    /// <summary>
    /// Matches inventory-picker searches against both an element's stored name and
    /// the display names produced by applying a magic template to compatible bases.
    /// Punctuation is treated as spacing so queries such as "Shield +1" match the
    /// stored template name "Shield, +1".
    /// </summary>
    public static bool MatchesSearch(
        CharacterInventory inventory,
        ElementBase element,
        string query,
        Func<ElementBase, bool>? allowsBaseItem = null)
    {
        try
        {
            return CreateSearchPredicate(inventory, query, allowsBaseItem)(element);
        }
        catch
        {
            return false;
        }
    }

    public static RefactoredEquipmentItem? Create(
        CharacterInventory inventory,
        ElementBase element,
        string? baseElementId = null)
    {
        if (element is not Item item)
            return null;

        if (GetTemplateKind(item) is null)
            return new RefactoredEquipmentItem(item);

        if (string.IsNullOrWhiteSpace(baseElementId))
            return null;

        var baseItem = GetCompatibleBaseItems(inventory, item)
            .FirstOrDefault(candidate =>
                candidate.Id.Equals(baseElementId, StringComparison.OrdinalIgnoreCase));

        return baseItem is null
            ? null
            : new RefactoredEquipmentItem(baseItem, item);
    }

    public static RefactoredEquipmentItem CreateForLoadCompatibility(
        CharacterInventory inventory,
        Item item)
    {
        if (GetTemplateKind(item) is null)
            return new RefactoredEquipmentItem(item);

        var candidates = GetCompatibleBaseItems(inventory, item);
        return candidates.Count == 1
            ? new RefactoredEquipmentItem(candidates[0], item)
            : new RefactoredEquipmentItem(item);
    }

    private static IEnumerable<WeaponElement> ResolveCompatibleWeapons(
        CharacterInventory inventory,
        Item magicItem)
    {
        var supportExpression = GetSetterValue(magicItem, "weapon");
        return string.IsNullOrWhiteSpace(supportExpression)
            ? []
            : inventory.GetSupportedWeaponElements(supportExpression);
    }

    private static IEnumerable<ArmorElement> ResolveCompatibleArmor(
        CharacterInventory inventory,
        Item magicItem)
    {
        var supportExpression = GetSetterValue(magicItem, "armor");
        return string.IsNullOrWhiteSpace(supportExpression)
            ? []
            : inventory.GetSupportedArmorElements(supportExpression);
    }

    private static string? GetSetterValue(Item item, string setterName) =>
        item.ElementSetters.ContainsSetter(setterName)
            ? item.ElementSetters.GetSetter(setterName)?.Value
            : null;

    private static bool MatchesSearchTerms(string? candidate, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        return MatchesNormalizedSearchTerms(NormalizeSearchText(candidate), terms);
    }

    private static bool MatchesNormalizedSearchTerms(
        string normalizedCandidate,
        IReadOnlyList<string> terms)
    {
        return terms.All(term =>
            normalizedCandidate.Contains(term, StringComparison.Ordinal));
    }

    private static InventorySearchIndex GetSearchIndex()
    {
        InventorySearchIndex? current = Volatile.Read(ref _searchIndex);
        if (current is not null)
            return current;

        return GetOrStartSearchIndexBuild().GetAwaiter().GetResult();
    }

    private static Task<InventorySearchIndex> GetOrStartSearchIndexBuild()
    {
        lock (SearchIndexGate)
        {
            if (_searchIndex is not null)
                return Task.FromResult(_searchIndex);

            if (_searchIndexBuildTask is not null)
                return _searchIndexBuildTask;

            var elements = DataManager.Current.ElementsCollection.ToList();
            int generation = _searchIndexGeneration;
            Task<InventorySearchIndex> buildTask = Task.Run(() => BuildSearchIndex(elements));
            _searchIndexBuildTask = buildTask;

            _ = buildTask.ContinueWith(
                completed => CompleteSearchIndexBuild(completed, buildTask, generation),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return buildTask;
        }
    }

    private static InventorySearchIndex BuildSearchIndex(IReadOnlyList<ElementBase> elements)
    {
        var inventory = new CharacterInventory();
        var indexedItems = new Dictionary<string, IndexedItemNames>(StringComparer.OrdinalIgnoreCase);
        var compatibleBasesBySupport =
            new Dictionary<string, IReadOnlyList<Item>>(StringComparer.OrdinalIgnoreCase);

        foreach (ElementBase element in elements)
        {
            var composedNames = new List<IndexedComposedName>();
            if (element is Item template && GetTemplateKind(template) is TemplateKind templateKind)
            {
                string supportExpression = templateKind == TemplateKind.Weapon
                    ? GetSetterValue(template, "weapon") ?? string.Empty
                    : GetSetterValue(template, "armor") ?? string.Empty;
                string supportCacheKey = $"{templateKind}\0{supportExpression}";

                if (!compatibleBasesBySupport.TryGetValue(supportCacheKey, out var compatibleBases))
                {
                    compatibleBases = GetCompatibleBaseItems(inventory, template);
                    compatibleBasesBySupport[supportCacheKey] = compatibleBases;
                }

                foreach (Item baseItem in compatibleBases)
                {
                    try
                    {
                        string displayName = ComposeDisplayName(baseItem, template);
                        composedNames.Add(new IndexedComposedName(
                            NormalizeSearchText(displayName),
                            baseItem));
                    }
                    catch
                    {
                        // A malformed custom base/template pair must not poison the shared index.
                    }
                }
            }

            indexedItems[element.Id] = new IndexedItemNames(
                NormalizeSearchText(element.Name),
                composedNames);
        }

        return new InventorySearchIndex(indexedItems);
    }

    private static string ComposeDisplayName(Item baseItem, Item template)
    {
        if (string.IsNullOrWhiteSpace(template.NameFormat))
            return template.Name;

        string displayName = template.NameFormat;
        foreach (Match match in Regex.Matches(template.NameFormat, "\\$\\((.*?)\\)"))
        {
            displayName = match.Groups[1].Value switch
            {
                "parent" => displayName.Replace(match.Value, baseItem.Name),
                "enhancement" => displayName.Replace(match.Value, template.Enhancement),
                _ => displayName,
            };
        }

        foreach (Match match in Regex.Matches(template.NameFormat, "{{(.*?)}}"))
        {
            displayName = match.Groups[1].Value.Trim() switch
            {
                "parent" => displayName.Replace(match.Value, baseItem.Name),
                "enhancement" => displayName.Replace(match.Value, template.Enhancement),
                _ => displayName,
            };
        }

        return displayName;
    }

    private static void CompleteSearchIndexBuild(
        Task<InventorySearchIndex> completed,
        Task<InventorySearchIndex> expectedTask,
        int generation)
    {
        lock (SearchIndexGate)
        {
            if (generation != _searchIndexGeneration ||
                !ReferenceEquals(_searchIndexBuildTask, expectedTask))
            {
                return;
            }

            if (completed.Status == TaskStatus.RanToCompletion)
                _searchIndex = completed.Result;

            _searchIndexBuildTask = null;
        }
    }

    private static string NormalizeSearchText(string value)
    {
        var normalized = new StringBuilder(value.Length);
        var needsSpace = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (needsSpace && normalized.Length > 0)
                    normalized.Append(' ');

                normalized.Append(char.ToLowerInvariant(character));
                needsSpace = false;
            }
            else
            {
                needsSpace = true;
            }
        }

        return normalized.ToString();
    }
}
