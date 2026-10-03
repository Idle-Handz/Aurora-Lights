using Builder.Data;
using Builder.Presentation.Services;
using Aurora.Content.Contracts;

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
            new ElementBase("Staff of Flowers", "Magic Item", "Xanathar", id),
            new ElementBase("Staff of Flowers", "Magic Item", "Xanathar", id)
        };
        ElementProvenance.SetContentFilePath(elements[0], dmg);
        ElementProvenance.SetContentFilePath(elements[1], xgte);
        var surviving = elements.Where(e => !LocalCorrectionDocument.IsSuppressedFromFile(e.Id,
            ElementProvenance.GetContentFilePath(e), dmg, [id])).ToArray();
        ElementProvenance.GetContentFilePath(surviving.Should().ContainSingle().Subject).Should().Be(xgte);
        LocalCorrectionDocument.IsSuppressedFromFile(id, null, dmg, [id]).Should().BeFalse();
        LocalCorrectionDocument.IsSuppressedFromFile("ID_OTHER", dmg, dmg, [id]).Should().BeFalse();
    }
}
