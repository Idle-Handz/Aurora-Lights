using Aurora.Tests.Helpers;
using Builder.Presentation.Models.Characters;

namespace Aurora.Tests.Tests;

public sealed class ImportedCharacterSummaryTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"aurora_summary_{Guid.NewGuid():N}");

    public ImportedCharacterSummaryTests() => Directory.CreateDirectory(_tempDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public void FromSavedFile_ReadsSanitizedCharacterFixtureMetadata()
    {
        string path = ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e");

        var summary = ImportedCharacterSummary.FromSavedFile(
            path,
            "Characters/prepared-paladin.dnd5e",
            "prepared-paladin.dnd5e",
            new FileInfo(path).Length);

        summary.DisplayName.Should().Be("Fixture Prepared Paladin");
        summary.GroupName.Should().Be("Test Fixtures");
        summary.Level.Should().Be("5");
        summary.CharacterClass.Should().Be("Paladin");
        summary.RelativePath.Should().Be("Characters/prepared-paladin.dnd5e");
    }

    [Fact]
    public void FromSavedFile_FallsBackToBuildInputAndFileName()
    {
        string path = Path.Combine(_tempDirectory, "fallback-character.dnd5e");
        File.WriteAllText(path, """
            <character version="1.0.3">
              <build>
                <input>
                  <name>Fallback Builder Name</name>
                  <player-name>Fixture Player</player-name>
                </input>
              </build>
            </character>
            """);

        var summary = ImportedCharacterSummary.FromSavedFile(
            path,
            "fallback-character.dnd5e",
            "fallback-character.dnd5e",
            new FileInfo(path).Length);

        summary.DisplayName.Should().Be("Fallback Builder Name");
        summary.PlayerName.Should().Be("Fixture Player");
        summary.Level.Should().Be("1");
        summary.GroupName.Should().BeEmpty();
        summary.Version.Should().Be("1.0.3");
    }
}
