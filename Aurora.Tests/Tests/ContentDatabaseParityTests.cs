using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;
using System.Xml;

namespace Aurora.Tests.Tests;

public sealed class ContentDatabaseParityTests
{
    [Fact]
    public async Task PreparedAuditDetectsDatabaseDefinitionDriftWithoutPublishingTheSnapshot()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var manager = DataManager.Current;
        var primaryProperty = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        string? previousPrimary = manager.UserDocumentsCustomElementsDirectory;
        var extraDirectories = Builder.Presentation.ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        var previousExtra = extraDirectories.ToArray();
        var previousElements = manager.ElementsCollection.ToArray();
        string primary = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(primary);
        string database = Path.Combine(primary, ContentDatabaseService.DatabaseFileName);
        try
        {
            File.WriteAllText(Path.Combine(primary, "fixture.xml"), "<elements><element name='Parity' type='Feat' source='Test' id='ID_TEST_PARITY'><description><p>Original</p></description></element></elements>");
            await ContentImport.ImportAsync(primary, database);
            primaryProperty.SetValue(manager, primary);
            extraDirectories.Clear();
            var service = new ContentDatabaseParityService();

            var matched = await service.RunAsync();
            matched.Success.Should().BeTrue(matched.FailureReason);
            matched.Status.Should().Be(ContentDatabaseParityStatus.Healthy);

            using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE content_prepared_elements SET base_xml=replace(base_xml, 'Original', 'Changed') WHERE aurora_id='ID_TEST_PARITY'";
                command.ExecuteNonQuery().Should().Be(1);
            }

            var drifted = await service.RunAsync();
            drifted.Success.Should().BeTrue(drifted.FailureReason);
            drifted.DefinitionMismatchCount.Should().Be(1);
            drifted.DefinitionMismatchSample.Should().Equal("ID_TEST_PARITY");
            drifted.Status.Should().Be(ContentDatabaseParityStatus.Warning);
            manager.ElementsCollection.Should().Equal(previousElements);
        }
        finally
        {
            primaryProperty.SetValue(manager, previousPrimary);
            extraDirectories.Clear();
            extraDirectories.AddRange(previousExtra);
            SqliteConnection.ClearAllPools();
            Directory.Delete(primary, recursive: true);
        }
    }

    [Theory]
    [InlineData("<rules><grant type='Feat' id='FIRST'/></rules>", "<rules><grant type='Feat' id='SECOND'/></rules>")]
    [InlineData("<supports>First</supports>", "<supports>Second</supports>")]
    [InlineData("<setters><set name='value'>1</set></setters>", "<setters><set name='value'>2</set></setters>")]
    [InlineData("<description><p>First</p></description>", "<description><p>Second</p></description>")]
    [InlineData("<rules><select type='List' name='Choice'><item id='1'>First</item></select></rules>", "<rules><select type='List' name='Choice'><item id='1'>Second</item></select></rules>")]
    public void SameIdsAndCountsDoNotHideChangedDefinitions(string xmlBody, string dbBody)
    {
        var report = ContentDatabaseParityService.CompareSnapshots([Element(xmlBody)], [Element(dbBody)]);

        report.Success.Should().BeTrue();
        report.MissingInDatabaseCount.Should().Be(0);
        report.MissingInXmlCount.Should().Be(0);
        report.TypeMismatches.Should().BeEmpty();
        report.SourceMismatches.Should().BeEmpty();
        report.DefinitionMismatchCount.Should().Be(1);
        report.DefinitionMismatchSample.Should().Equal("ID_TEST_PARITY");
        report.TotalMismatchCount.Should().Be(1);
        report.Status.Should().Be(ContentDatabaseParityStatus.Warning);
    }

    [Fact]
    public void CosmeticXmlFormattingAndAttributeOrderDoNotReportDefinitionDrift()
    {
        var xml = Parse("""
            <element name="Parity" type="Feat" source="Test" id="ID_TEST_PARITY">
              <!-- author note -->
              <rules><grant type="Feat" id="FIRST" /></rules>
            </element>
            """);
        var db = Parse("<element id='ID_TEST_PARITY' source='Test' type='Feat' name='Parity'><rules><grant id='FIRST' type='Feat'/></rules></element>");

        var report = ContentDatabaseParityService.CompareSnapshots([xml], [db]);

        report.DefinitionMismatchCount.Should().Be(0);
        report.Status.Should().Be(ContentDatabaseParityStatus.Healthy);
    }

    [Fact]
    public void RuleOrderAndMultiplicityRemainSignificant()
    {
        var first = Element("<rules><grant type='Feat' id='FIRST'/><grant type='Feat' id='SECOND'/></rules>");
        var reversed = Element("<rules><grant type='Feat' id='SECOND'/><grant type='Feat' id='FIRST'/></rules>");
        var repeated = Element("<rules><grant type='Feat' id='FIRST'/><grant type='Feat' id='SECOND'/><grant type='Feat' id='SECOND'/></rules>");

        ContentDatabaseParityService.CompareSnapshots([first], [reversed]).DefinitionMismatchCount.Should().Be(1);
        ContentDatabaseParityService.CompareSnapshots([first], [repeated]).DefinitionMismatchCount.Should().Be(1);
    }

    [Fact]
    public void CaseOnlyIdentityDifferencesAreMissingDefinitions()
    {
        var upper = Element("");
        var lower = Parse("<element name='Parity' type='Feat' source='Test' id='id_test_parity'/>");

        var report = ContentDatabaseParityService.CompareSnapshots([upper], [lower]);

        report.MissingInDatabaseCount.Should().Be(1);
        report.MissingInXmlCount.Should().Be(1);
        report.Status.Should().Be(ContentDatabaseParityStatus.Warning);
    }

    [Fact]
    public async Task CancelledAuditPropagatesCancellationInsteadOfReportingFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ContentDatabaseParityService().RunAsync(cancellation.Token));
    }

    private static ElementBase Element(string body) =>
        Parse($"<element name='Parity' type='Feat' source='Test' id='ID_TEST_PARITY'>{body}</element>");

    private static ElementBase Parse(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return new ElementParser().ParseElement(document.DocumentElement!);
    }
}
