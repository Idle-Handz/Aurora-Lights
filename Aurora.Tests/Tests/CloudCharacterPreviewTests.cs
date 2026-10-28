using System.Text;
using Aurora.Reflections.Services;
using Builder.Presentation.Services.Storage;

namespace Aurora.Tests.Tests;

public sealed class CloudCharacterPreviewTests
{
    private static readonly CharacterDocumentMetadata Metadata = new(new("drive-id"), "old-name.dnd5e", "4", null, null, null);

    [Fact]
    public void Preview_uses_saved_character_name_portrait_and_build_instead_of_filename()
    {
        var document = new CharacterDocument(Metadata, Encoding.UTF8.GetBytes("""
            <character><display-properties favorite="true">
              <name>Aria</name><level>5</level><race>Elf</race><class>Wizard</class>
              <portrait><base64>AQID</base64><local>C:\unavailable.png</local></portrait>
            </display-properties></character>
            """));
        var entry = CloudCharacterPreview.Read(document);
        entry.Id.Should().Be("drive-id");
        entry.DisplayName.Should().Be("Aria");
        entry.PrimaryText.Should().Be("Level 5 - Elf");
        entry.SecondaryText.Should().Be("Wizard");
        entry.PortraitDataUrl.Should().Be("data:image/png;base64,AQID");
        entry.IsFavorite.Should().BeTrue();
        entry.IsCloud.Should().BeTrue();
        entry.GroupName.Should().Be("Cloud Saves");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    public void Missing_or_bad_portrait_keeps_character_selectable(string portrait)
    {
        var document = new CharacterDocument(Metadata, Encoding.UTF8.GetBytes(
            $"<character><display-properties><portrait><base64>{portrait}</base64></portrait></display-properties></character>"));
        var entry = CloudCharacterPreview.Read(document);
        entry.DisplayName.Should().Be("old-name");
        entry.PortraitDataUrl.Should().BeNull();
        entry.IsCloud.Should().BeTrue();
    }
}
