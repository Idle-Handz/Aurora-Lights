using System.Text;
using System.Xml.Linq;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class CharacterLoadDiagnosticsTests
{
    private static CharacterLoadValidation.MissingElement Missing(string id, string path,
        SavedElementSource? source = null, int count = 1) => new(id, count, path) { SavedSource = source };

    [Fact]
    public void ReportSeparatesMissingSourcesFromAvailableButUnrestoredDefinitions()
    {
        var marker = new ElementBase("Background with a Feat", "Grants", "Internal", "MARKER");
        ElementProvenance.SetContentFilePath(marker, "/content/AuroraLegacy/core/internal.xml");
        string report = CharacterLoadDiagnostics.Describe([
            Missing("CLAW", "Class > Simple Weapons > Claw", new("Ryoko's Guide", "items-weapons.xml", false), 2),
            Missing("SAI", "Class > Martial Weapons > Sai", new("Ryoko's Guide", "items-weapons.xml", false)),
            Missing("ORIGIN_TOUGH", "Farmer > Origin Feat (Tough)", new("Player's Handbook (2024)", "background-farmer.xml", true)),
            Missing("MARKER", "Farmer > Origin Feat (Tough) > Background with a Feat")
        ], [marker]);

        report.Should().Contain("4 entries; 5 missing occurrences.")
            .And.Contain("Missing definitions: 3 entries.")
            .And.Contain("Available definitions not restored: 1 entry.")
            .And.Contain("Ryoko's Guide (saved source): 2 entries")
            .And.Contain("Internal (current catalog): 1 entry")
            .And.Contain("Local customization references: 1 entry")
            .And.Contain("Local customization — Player's Handbook (2024)")
            .And.Contain("Source file: background-farmer.xml")
            .And.Contain("CLAW — missing 2; saved path: Class > Simple Weapons > Claw");
        report.Should().NotContain("/content/");
    }

    [Fact]
    public void LegacySaveDoesNotBorrowItsParentsSourceOrGuessFromItsId()
    {
        var build = XElement.Parse("""
            <build><elements><element id="PARENT" name="Farmer" type="Background">
              <element id="ID_WOTC_PHB24_UNKNOWN" name="Unknown child" type="Feat"/>
            </element></elements><sum><element id="PARENT" source-book="Parent Book"/></sum></build>
            """);
        var missing = CharacterLoadValidation.FindMissing(build, ["PARENT"]);
        missing.Should().ContainSingle().Which.SavedSource.Should().BeNull();
        var report = CharacterLoadDiagnostics.Describe(missing, []);
        report.Should().Contain("Source unknown").And.Contain("Older saves may lack source metadata")
            .And.NotContain("Parent Book").And.NotContain("Player's Handbook");
    }

    [Fact]
    public void SourceMetadataSurvivesPartialSaveAndAnotherLoad()
    {
        var saved = XElement.Parse("""
            <build><elements><element id="ORIGIN" name="Origin Feat (Tough)" type="Background Feature"/></elements>
              <sum><element id="ORIGIN" source-book="Player's Handbook (2024)" source-file="background-farmer.xml" source-local="true"/></sum>
            </build>
            """);
        var missing = CharacterLoadValidation.FindMissing(saved, []);
        var recovery = new UnresolvedCharacterBuild(saved, missing.Select(m => m.Id), []);
        var rebuilt = XElement.Parse("<build><elements/><sum/></build>");
        recovery.MergeInto(rebuilt, []);
        recovery.MergeInto(rebuilt, []);
        var reloaded = CharacterLoadValidation.FindMissing(rebuilt, []);
        reloaded.Should().ContainSingle().Which.SavedSource
            .Should().Be(new SavedElementSource("Player's Handbook (2024)", "background-farmer.xml", true));
        CharacterLoadDiagnostics.Describe(reloaded, []).Should().Contain("Local customization — Player's Handbook (2024)");
    }

    [Fact]
    public async Task CharacterSerializerRecordsPortableSourceMetadata()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var previousHandler = SelectionRuleExpanderContext.Current;
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        var manager = CharacterManager.Current;
        await manager.New(false);
        var element = new ElementBase("Local feat", "Option", "Local Book", "ID_SOURCE_LOG_TEST");
        ElementProvenance.SetContentFilePath(element, @"C:\Private\Alice\custom\user\local\custom-feat.xml");
        DataManager.Current.ElementsCollection.Add(element);
        try
        {
            manager.RegisterElement(element.Construct<Builder.Data.Elements.Option>());
            var file = new CharacterFile(Path.Combine(Path.GetTempPath(), "source-log-test.dnd5e"));
            string xml = Encoding.UTF8.GetString(file.SerializeCharacter(manager.Character)).TrimStart('\uFEFF');
            var build = XDocument.Parse(xml).Root!.Element("build")!;
            var summary = build.Element("sum")!.Elements("element").Single(n => (string?)n.Attribute("id") == element.Id);
            SavedElementSource.Read(summary).Should().Be(new SavedElementSource("Local Book", "custom-feat.xml", true));
            summary.ToString().Should().NotContain("Private").And.NotContain("Alice");
            CharacterLoadValidation.FindMissing(build, manager.GetElements().Where(e => e.Id != element.Id).Select(e => e.Id))
                .Single(m => m.Id == element.Id).SavedSource.Should().Be(SavedElementSource.Read(summary));
        }
        finally
        {
            await manager.New(false);
            DataManager.Current.ElementsCollection.Remove(element);
            SelectionRuleExpanderContext.Current = previousHandler;
        }
    }

    [Fact]
    public void AliasToAnInstalledDefinitionIsReportedAsAvailable()
    {
        using var scope = ElementIdAliases.BeginScope();
        ElementIdAliases.Set([new("OLD", "NEW")]);
        var current = new ElementBase("Renamed feat", "Feat", "Current Book", "NEW");
        var report = CharacterLoadDiagnostics.Describe([Missing("OLD", "Class > Feat")], [current]);
        report.Should().Contain("Available definitions not restored").And.Contain("Resolves to: NEW")
            .And.Contain("Current Book (current catalog)").And.NotContain("Missing definitions:");
    }

    [Fact]
    public void DirectDefinitionTakesPrecedenceOverAnAlias()
    {
        using var scope = ElementIdAliases.BeginScope();
        ElementIdAliases.Set([new("OLD", "NEW")]);
        var report = CharacterLoadDiagnostics.Describe([Missing("OLD", "Feat")], [
            new ElementBase("Original feat", "Feat", "Original Book", "OLD"),
            new ElementBase("Other feat", "Feat", "Other Book", "NEW")]);
        report.Should().Contain("Original Book").And.NotContain("Other Book").And.NotContain("Resolves to:");
    }

    [Fact]
    public void ChangedLocalOriginRemainsVisibleAlongsideTheCurrentSource()
    {
        var current = new ElementBase("Farmer", "Background", "Published Book", "FARMER");
        var report = CharacterLoadDiagnostics.Describe([
            Missing("FARMER", "Background", new("Local Book", "farmer.xml", true))], [current]);
        report.Should().Contain("Published Book (current catalog)")
            .And.Contain("Saved origin: Local customization — Local Book; file: farmer.xml");
    }

    [Fact]
    public void ConflictingSavedSourcesAreUnknownRatherThanArbitrarilyAttributed()
    {
        var build = XElement.Parse("""
            <build><elements/><sum>
              <element id="FEAT" source-book="Book A"/>
              <element id="FEAT" source-book="Book B"/>
            </sum></build>
            """);
        var missing = CharacterLoadValidation.FindMissing(build, []);
        missing.Should().ContainSingle().Which.Count.Should().Be(2);
        missing[0].SavedSource.Should().BeNull();
    }
}
