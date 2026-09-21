using Aurora.Importer;
using Builder.Data;
using Builder.Presentation.Services.Sources;
using Builder.Data.Elements;
using Builder.Presentation.Models.Sources;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class RequiredContentPolicyTests
{
    [Theory]
    [InlineData("Internal", true)]
    [InlineData("Core", true)]
    [InlineData("Aurora Legacy Essentials", true)]
    [InlineData("aurora essentials", true)]
    [InlineData("Players Handbook 2024", false)]
    [InlineData("Dungeon Masters Guide", false)]
    [InlineData("Monster Manual", false)]
    public void InfrastructureIsRequiredButRulebooksRemainSelectable(string name, bool required)
        => RequiredContentPolicy.IsRequiredSource(name).Should().Be(required);

    [Fact]
    // The app no longer reads these flags; this guards the database side until the column goes.
    public void DisabledInfrastructureStaysEnabledInTheDatabase()
    {
        string path = Path.Combine(Path.GetTempPath(), "aurora-required-" + Guid.NewGuid() + ".sqlite");
        try
        {
            using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE content_packages(content_package_id INTEGER,package_key TEXT,package_name TEXT,
                    package_kind TEXT,precedence_rank INTEGER,is_enabled INTEGER);
                INSERT INTO content_packages VALUES(1,'core-ale-xml','ALE.xml','core',0,0),
                    (2,'core-players-handbook','Players Handbook','core',0,0);
                CREATE TABLE content_preparation_metadata(singleton_id,contract_version,catalog_policy,append_policy);
                INSERT INTO content_preparation_metadata VALUES(1,1,'unrestricted','materialized');
                """;
            command.ExecuteNonQuery();
            AuroraContentImporter.ReadEnabledPackageKeys(connection).Should().Equal("core-ale-xml");
            Action disable = () => AuroraContentImporter.SetPackageEnabled(path, 1, false);
            disable.Should().Throw<InvalidOperationException>().WithMessage("*must remain enabled*");
            AuroraContentImporter.SetPackageEnabled(path, 2, true);
            AuroraContentImporter.SetPackageEnabled(path, 2, false);
            AuroraContentImporter.ReadEnabledPackageKeys(connection).Should().Equal("core-ale-xml");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Aurora Legacy Essentials", true)]
    [InlineData("Internal", true)]
    [InlineData("Core", true)]
    [InlineData("Player’s Handbook (2024)", false)]
    public void CharacterRestrictionsCannotUncheckInfrastructure(string name, bool required)
    {
        var item = new SourceItem(new Source { ElementHeader = new ElementHeader(name, "Source", name, name) });
        item.SetIsChecked(false, true, true);
        item.AllowUnchecking.Should().Be(!required);
        item.IsChecked.Should().Be(required);
    }
}
