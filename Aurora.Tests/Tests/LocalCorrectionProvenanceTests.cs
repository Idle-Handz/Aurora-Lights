using Aurora.Importer;
using Builder.Data;
using Builder.Data.Files;

namespace Aurora.Tests.Tests;

public sealed class LocalCorrectionProvenanceTests
{
    [Fact]
    public void RenamingDmgStaff_DoesNotSuppressTheLegitimateXgteDefinition()
    {
        string root = Path.GetTempPath();
        string dmg = Path.Combine(root, "core", "items-staffs.xml");
        string xgte = Path.Combine(root, "supplements", "items-wondrous.xml");
        const string id = "ID_WOTC_XGTE_MAGIC_ITEM_STAFF_OF_FLOWERS";
        // Both malformed and legitimate definitions even claim the same name/source.
        var elements = new[] {
            new ElementBase("Staff of Flowers", "Magic Item", "Xanathar", id) { ContentFilePath = dmg },
            new ElementBase("Staff of Flowers", "Magic Item", "Xanathar", id) { ContentFilePath = xgte }
        };
        var surviving = elements.Where(e => !LocalCorrectionDocument.IsSuppressedFromFile(e.Id,
            e.ContentFilePath, dmg, [id])).ToArray();
        surviving.Should().ContainSingle().Which.ContentFilePath.Should().Be(xgte);
        LocalCorrectionDocument.IsSuppressedFromFile(id, null, dmg, [id]).Should().BeFalse();
        LocalCorrectionDocument.IsSuppressedFromFile("ID_OTHER", dmg, dmg, [id]).Should().BeFalse();
    }

    [Fact]
    public void CatalogProvenance_ResolvesPrimaryAndAdditionalRootsWithWindowsSeparators()
    {
        string work = Path.Combine(Path.GetTempPath(), "aurora-provenance-" + Guid.NewGuid().ToString("N"));
        string primary = Path.Combine(work, "primary"), secondary = Path.Combine(work, "Extra Books");
        try
        {
            foreach (string root in new[] { primary, secondary })
            {
                Directory.CreateDirectory(Path.Combine(root, "core"));
                File.WriteAllText(Path.Combine(root, "core", "item.xml"), "<elements/>");
            }
            AuroraContentImporter.ResolveSourceFilePath([primary, secondary], "core\\item.xml")
                .Should().Be(Path.Combine(primary, "core", "item.xml"));
            AuroraContentImporter.ResolveSourceFilePath([primary, secondary], "additional-1-extra-books\\core\\item.xml")
                .Should().Be(Path.Combine(secondary, "core", "item.xml"));
            AuroraContentImporter.ResolveSourceFilePath([primary, secondary], "../outside.xml").Should().BeNull();
        }
        finally { Directory.Delete(work, recursive: true); }
    }
}
