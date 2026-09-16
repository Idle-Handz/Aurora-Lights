using Aurora.Importer;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Models.Sources;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class RequiredContentPolicyTests
{
    [Theory]
    [InlineData("core-ale-xml", "ALE.xml", true)]
    [InlineData("core-internal-xml", "Internal.xml", true)]
    [InlineData("core:aurora-legacy-essentials", "Aurora Legacy Essentials", true)]
    [InlineData("core:core", "Core", true)]
    [InlineData("core-players-handbook-2024", "Players Handbook 2024", false)]
    [InlineData("core-dungeon-masters-guide", "Dungeon Masters Guide", false)]
    [InlineData("core-monster-manual", "Monster Manual", false)]
    public void InfrastructureIsRequiredButRulebooksRemainSelectable(string key, string name, bool required)
        => RequiredContentPolicy.IsRequiredPackage(key, name).Should().Be(required);

    [Fact]
    public void DisabledInfrastructureRemainsVisibleAndCannotBeDisabledThroughTheApi()
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
            var packages = AuroraContentImporter.GetPackages(path);
            packages.Single(p => p.Id == 1).IsEnabled.Should().BeTrue();
            packages.Single(p => p.Id == 1).IsRequired.Should().BeTrue();
            packages.Single(p => p.Id == 2).IsEnabled.Should().BeFalse();
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
