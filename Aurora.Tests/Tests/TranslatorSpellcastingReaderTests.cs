using Aurora.Importer;
using Builder.Data;
using Microsoft.Data.Sqlite;
using System.Xml;

namespace Aurora.Tests.Tests;

public sealed class TranslatorSpellcastingReaderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Version11_PreservesDeclarationsThroughRuntimeParser(bool extension)
    {
        string xml = $"""
            <spellcasting name="Wizard" ability="Intelligence" all="true" extend="{extension.ToString().ToLowerInvariant()}" prepare="true" allowReplace="true">
              <list>First</list>
              <extend known="true">ID_TEST_ONE,ID_TEST_TWO</extend>
              <list known="true">Wizard,Evocation</list>
              <extend known="false">Wizard,Conjuration</extend>
            </spellcasting>
            """;
        using var connection = CreateDatabase(xml);
        XmlElement profile = TranslatorSpellcastingReader.ReadProfiles(connection)[1];
        var document = new XmlDocument();
        document.LoadXml("<elements><element name='Test' type='Class Feature' source='Test' id='ID_TEST' /></elements>");
        var node = (XmlElement)document.DocumentElement!.FirstChild!;
        node.AppendChild(document.ImportNode(profile, true));

        var spellcasting = new ElementParser().ParseElement(node).SpellcastingInformation;
        spellcasting.IsExtension.Should().Be(extension);
        spellcasting.AssignToAllSpellcastingClasses.Should().BeTrue();
        spellcasting.AllowSpellSwap.Should().BeTrue();
        spellcasting.InitialSupportedSpellsExpression.Supports.Should().Be("Wizard,Evocation");
        spellcasting.InitialSupportedSpellsExpression.Known.Should().BeTrue();
        spellcasting.ExtendedSupportedSpellsExpressions.Select(entry => entry.Supports)
            .Should().Equal("ID_TEST_ONE,ID_TEST_TWO", "Wizard,Conjuration");
        spellcasting.ExtendedSupportedSpellsExpressions.Select(entry => entry.Known)
            .Should().Equal(true, false);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<wrong />")]
    public void Version11_RejectsIncompleteProfiles(string? xml)
    {
        using var connection = CreateDatabase(xml);
        Action read = () => TranslatorSpellcastingReader.ReadProfiles(connection);
        read.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData("<spellcasting>")]
    [InlineData("<!DOCTYPE spellcasting [<!ENTITY value 'test'>]><spellcasting>&value;</spellcasting>")]
    public void Version11_RejectsMalformedOrDtdXml(string xml)
    {
        using var connection = CreateDatabase(xml);
        Action read = () => TranslatorSpellcastingReader.ReadProfiles(connection);
        read.Should().Throw<XmlException>();
    }

    private static SqliteConnection CreateDatabase(string? xml)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE spellcasting_profiles (owner_element_id INTEGER, raw_xml TEXT); INSERT INTO spellcasting_profiles VALUES (1, $xml);";
        command.Parameters.AddWithValue("$xml", (object?)xml ?? DBNull.Value);
        command.ExecuteNonQuery();
        return connection;
    }
}
