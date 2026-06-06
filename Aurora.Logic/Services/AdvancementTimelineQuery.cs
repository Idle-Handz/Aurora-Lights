using Builder.Data;
using Builder.Data.Elements;

namespace Builder.Presentation.Services;

/// <summary>
/// Builds a read-only, client-neutral view of the features granted at each class level.
/// It does not mutate progression state or replace the legacy advancement engine.
/// </summary>
public static class AdvancementTimelineQuery
{
    public static IReadOnlyList<ClassAdvancementTimeline> Build(CharacterManager manager)
    {
        if (manager is null) throw new ArgumentNullException(nameof(manager));
        return manager.ClassProgressionManagers.Select(BuildClassTimeline).ToList();
    }

    private static ClassAdvancementTimeline BuildClassTimeline(ClassProgressionManager manager)
    {
        var byLevel = Enumerable.Range(1, manager.ProgressionLevel)
            .ToDictionary(level => level, _ => new List<ElementBase>());
        var levelByElementId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var levelElement in manager.LevelElements)
        {
            int level = GetLevel(levelElement);
            if (level < 1 || level > manager.ProgressionLevel) continue;
            foreach (var element in WalkChildren(levelElement))
                if (!string.IsNullOrWhiteSpace(element.Id))
                    levelByElementId.TryAdd(element.Id, level);
        }

        foreach (var feature in manager.GetElements().Where(IsDisplayableClassFeature))
        {
            int level = ResolveFeatureLevel(feature, levelByElementId, manager.ProgressionLevel);
            if (!byLevel.TryGetValue(level, out var features)) continue;
            if (features.Any(existing => existing.Name == feature.Name)) continue;
            features.Add(feature);
        }

        int hitDie = 0;
        try { hitDie = manager.GetHitDieValue(); } catch { }

        return new ClassAdvancementTimeline(
            manager.ClassElement?.Name ?? "Unknown",
            manager.HD ?? "—",
            manager.IsMainClass,
            byLevel.Select(pair => new AdvancementLevelData(
                    pair.Key,
                    pair.Key == 1 && manager.IsMainClass ? hitDie : (hitDie / 2) + 1,
                    pair.Value))
                .ToList());
    }

    private static bool IsDisplayableClassFeature(ElementBase element) =>
        (element.Type == "Class Feature" || element.Type == "Archetype Feature") &&
        !string.IsNullOrWhiteSpace(element.Name) &&
        !element.Name.StartsWith("Ability Score Increase") &&
        !element.Name.StartsWith("Ability Score Improvement") &&
        !element.Name.Equals("Feat", StringComparison.OrdinalIgnoreCase);

    private static int GetLevel(ElementBase levelElement)
    {
        if (levelElement is LevelElement level) return level.Level;
        return int.TryParse(levelElement.ElementSetters.GetSetter("Level")?.Value, out int parsed)
            ? parsed
            : 0;
    }

    private static int ResolveFeatureLevel(
        ElementBase feature,
        IReadOnlyDictionary<string, int> levelByElementId,
        int maxLevel)
    {
        foreach (string id in GetAnchorIds(feature))
            if (levelByElementId.TryGetValue(id, out int level))
                return level;

        int requiredLevel = GetRequiredLevel(feature);
        return requiredLevel >= 1 && requiredLevel <= maxLevel ? requiredLevel : 1;
    }

    private static IEnumerable<string> GetAnchorIds(ElementBase feature)
    {
        if (!string.IsNullOrWhiteSpace(feature.Id)) yield return feature.Id;

        var parent = feature.Aquisition.GetParentHeader();
        if (!string.IsNullOrWhiteSpace(parent?.Id)) yield return parent.Id;

        if (feature.Aquisition.WasGranted &&
            !string.IsNullOrWhiteSpace(feature.Aquisition.GrantRule?.ElementHeader?.Id))
            yield return feature.Aquisition.GrantRule.ElementHeader.Id;

        if (feature.Aquisition.WasSelected &&
            !string.IsNullOrWhiteSpace(feature.Aquisition.SelectRule?.ElementHeader?.Id))
            yield return feature.Aquisition.SelectRule.ElementHeader.Id;
    }

    private static int GetRequiredLevel(ElementBase feature)
    {
        try
        {
            if (feature.Aquisition.WasGranted)
                return feature.Aquisition.GrantRule.Attributes.RequiredLevel;
            if (feature.Aquisition.WasSelected)
                return feature.Aquisition.SelectRule.Attributes.RequiredLevel;
        }
        catch { }
        return 0;
    }

    private static IEnumerable<ElementBase> WalkChildren(ElementBase parent)
    {
        yield return parent;
        foreach (var child in parent.RuleElements)
            foreach (var descendant in WalkChildren(child))
                yield return descendant;
    }
}

public sealed record AdvancementLevelData(int Level, int AverageHp, IReadOnlyList<ElementBase> Features);

public sealed record ClassAdvancementTimeline(
    string ClassName,
    string HitDie,
    bool IsMainClass,
    IReadOnlyList<AdvancementLevelData> Levels);
