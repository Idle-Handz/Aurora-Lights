using Builder.Presentation.Models.Sources;
using Builder.Presentation.Services.Sources;

namespace Aurora.Components.Models;

/// <summary>
/// Projects the engine's source groups into the models <c>SourceRestrictionsEditor</c> renders.
/// Shared so the per-character editor and the default-restrictions editor show the same tree.
/// </summary>
public static class SourceRestrictionModelMapper
{
    public static IReadOnlyList<SourceRestrictionGroupModel> ToGroupModels(SourcesManager sources) =>
        sources.SourceGroups.Select(ToGroupModel).ToList();

    public static SourceRestrictionGroupModel ToGroupModel(SourcesGroup group) =>
        new(
            group.Name,
            group.Name,
            group.Underline,
            group.AllowUnchecking,
            group.IsChecked,
            group.Sources.Select(item => new SourceRestrictionItemModel(
                item.Source.Id,
                item.Source.Name ?? string.Empty,
                item.IsChecked,
                item.AllowUnchecking,
                !item.AllowUnchecking,
                Classify(item))).ToList());

    public static SourceRestrictionCategory? Classify(SourceItem item) =>
        SourceRestrictionCategoryClassifier.Classify(
            item.Source.IsOfficialContent,
            item.Source.IsThirdPartyContent,
            item.Source.IsHomebrewContent,
            item.Source.Author,
            item.Source.Name,
            item.Source.ReleaseDate);
}
