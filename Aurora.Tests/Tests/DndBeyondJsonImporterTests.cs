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
        sheet.ClassLevel.Should().Be(1);
        sheet.Classes.Should().BeEquivalentTo(new[]
        {
            new ParsedClass("Fighter", 1, null, true),
            new ParsedClass("Wizard", 5, null, false)
        });
        Action validate = () => CharacterImportSupport.EnsureSupported(sheet);
        validate.Should().NotThrow();
    }

    [Fact]
    public void SingleClassRetainsItsLevelAndRemainsSupported()
    {
        var sheet = DndBeyondJsonImporter.ParseJson("""{"classes":[{"definition":{"name":"Wizard"},"level":5,"isStartingClass":true}]}""");
        sheet.ClassLevel.Should().Be(5);
        Action validate = () => CharacterImportSupport.EnsureSupported(sheet);
        validate.Should().NotThrow();
    }

    [Fact]
    public void ClassSpellSourcesKeepIndependentPreparationForTheSameSpell()
    {
        var sheet = DndBeyondJsonImporter.ParseJson("""
            {"classes":[
              {"id":11,"definition":{"name":"Cleric"},"level":2,"isStartingClass":true},
              {"id":22,"definition":{"name":"Wizard"},"level":3}],
             "classSpells":[
              {"characterClassId":22,"spells":[{"definition":{"name":"Shared Spell","level":1},"prepared":false}]},
              {"characterClassId":11,"spells":[{"definition":{"name":"Shared Spell","level":1},"prepared":true}]}]}
            """);
        sheet.Spells.Should().HaveCount(2);
        sheet.Spells.Single(spell => spell.Source == "Cleric").IsPrepared.Should().BeTrue();
        sheet.Spells.Single(spell => spell.Source == "Wizard").IsPrepared.Should().BeFalse();
    }
}
