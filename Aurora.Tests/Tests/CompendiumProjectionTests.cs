using System.Collections;
using System.Reflection;
using Aurora.Web.Services;

namespace Aurora.Tests.Tests;

public sealed class CompendiumProjectionTests
{
    [Fact]
    public void Search_type_origin_and_catalog_changes_refresh_the_cached_projection()
    {
        var page = new Aurora.Web.Components.Pages.Compendium();
        SetField(page, "_catalog", Catalog([
            new ContentCatalogEntry("needle-feat", "Needle feat", "Feat", "Source", "content.xml", ContentCatalogOrigin.Session),
            new ContentCatalogEntry("needle-race", "Needle race", "Race", "Source", "content.xml", ContentCatalogOrigin.Session),
            new ContentCatalogEntry("other-feat", "Other feat", "Feat", "Source", "content.xml", ContentCatalogOrigin.Session)
        ]));

        Projection(page).Select(entry => entry.Id).Should().Equal("needle-feat", "needle-race", "other-feat");
        SetField(page, "_searchText", "needle");
        Projection(page).Select(entry => entry.Id).Should().Equal("needle-feat", "needle-race");
        SetField(page, "_selectedType", "Feat");
        Projection(page).Select(entry => entry.Id).Should().Equal("needle-feat");
        SetField(page, "_searchText", string.Empty);
        Projection(page).Select(entry => entry.Id).Should().Equal("needle-feat", "other-feat");
        SetField(page, "_selectedOrigin", "Baseline");
        Projection(page).Should().BeEmpty();
        SetField(page, "_selectedOrigin", "session");
        Projection(page).Select(entry => entry.Id).Should().Equal("needle-feat", "other-feat");

        SetField(page, "_catalog", Catalog([
            new ContentCatalogEntry("replacement", "Replacement feat", "Feat", "Source", "new.xml", ContentCatalogOrigin.Session)
        ]));
        Projection(page).Select(entry => entry.Id).Should().Equal("replacement");
    }

    [Fact]
    public void Repeated_render_projections_enumerate_catalog_once()
    {
        var entries = new CountingEntries(Enumerable.Range(0, 10000).Select(index =>
            new ContentCatalogEntry($"ID_{index}", index % 2 == 0 ? "Needle" : "Other",
                "Feat", "Source", "content.xml", ContentCatalogOrigin.Session)).ToList());
        var page = new Aurora.Web.Components.Pages.Compendium();
        SetField(page, "_catalog", Catalog(entries));

        // The page reads the projection for its count, visible rows, and truncation notice.
        for (int render = 0; render < 2; render++)
            for (int read = 0; read < 3; read++)
                Projection(page).Count.Should().Be(10000);

        entries.Visits.Should().Be(10000);
    }

    private static MergedContentCatalog Catalog(IReadOnlyList<ContentCatalogEntry> entries) => new()
    {
        Elements = entries,
        ElementTypes = new Dictionary<string, int>(),
        Sources = new Dictionary<string, int>(),
        Origins = new Dictionary<ContentCatalogOrigin, int>()
    };

    private static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static IReadOnlyList<ContentCatalogEntry> Projection(object page) =>
        (IReadOnlyList<ContentCatalogEntry>)page.GetType()
            .GetProperty("_filteredElements", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(page)!;

    private sealed class CountingEntries(IReadOnlyList<ContentCatalogEntry> entries)
        : IReadOnlyList<ContentCatalogEntry>
    {
        public int Visits { get; private set; }
        public int Count => entries.Count;
        public ContentCatalogEntry this[int index] => entries[index];
        public IEnumerator<ContentCatalogEntry> GetEnumerator()
        {
            foreach (var entry in entries)
            {
                Visits++;
                yield return entry;
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
