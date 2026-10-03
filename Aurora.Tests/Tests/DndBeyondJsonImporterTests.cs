using Aurora.PdfImport;

namespace Aurora.Tests.Tests;

public sealed class DndBeyondJsonImporterTests
{
    [Theory]
    [InlineData("\"background\":null")]
    [InlineData("\"spells\":null")]
    [InlineData("\"spells\":{\"class\":null}")]
    [InlineData("\"classSpells\":null")]
    public void ParseJsonAcceptsNullOptionalSections(string section)
    {
        var sheet = DndBeyondJsonImporter.ParseJson("{\"name\":\"Test character\"," + section + "}");

        sheet.CharacterName.Should().Be("Test character");
        sheet.Background.Should().BeNull();
        sheet.Spells.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseJsonKeepsStartingClassRegardlessOfArrayOrder(bool startingFirst)
    {
        const string starting = """{"definition":{"name":"Fighter"},"level":1,"isStartingClass":true}""";
        const string secondary = """{"definition":{"name":"Wizard"},"level":5,"isStartingClass":false}""";
        string classes = startingFirst ? starting + "," + secondary : secondary + "," + starting;

        var sheet = DndBeyondJsonImporter.ParseJson("{\"classes\":[" + classes + "]}");

        sheet.ClassName.Should().Be("Fighter");
        sheet.ClassLevel.Should().Be(6);
    }
}
