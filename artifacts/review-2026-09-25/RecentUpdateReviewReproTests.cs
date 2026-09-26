using Aurora.App.Services;
using Aurora.Components.Models;
using Aurora.Components.Shared;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Bunit;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class RecentUpdateReviewSearchReproTests : BunitContext
{
    [Theory]
    [InlineData("Wizards of the Coast")]
    [InlineData("5e official")]
    public void MatchingAnAncestorShouldShowItsBooks(string query)
    {
        SourceRestrictionGroupModel[] groups =
        [
            new("wotc", "Wizards of the Coast", "", true, true,
                [new("phb", "Player's Handbook", true, true, false, SourceRestrictionCategory.Official5E)]),
            new("ua", "Unearthed Arcana", "", true, true,
                [new("ua-feats", "Unearthed Arcana: Feats", true, true, false, SourceRestrictionCategory.Official5E)])
        ];
        var cut = Render<SourceRestrictionsEditor>(p => p.Add(c => c.Groups, groups));
        cut.Find("input.source-restrictions-search").Input(query);
        cut.FindAll("button.source-restrictions-item").Select(row => row.TextContent.Trim())
            .Should().Contain("Player's Handbook", "a matching publisher or category should reveal its sources");
    }
}

public sealed class RecentUpdateReviewFallbackReproTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PreparedLoadFailureMustNotReplaceARetainedDefinitionWithRejectedXml()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var manager = DataManager.Current;
        var root = Path.Combine(Path.GetTempPath(), "AuroraReview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var property = typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!;
        var previousRoot = manager.UserDocumentsCustomElementsDirectory;
        var previousElements = manager.ElementsCollection.ToArray();
        var additional = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
        var previousAdditional = additional.ToArray();
        const string id = "ID_REVIEW_RETAINED";
        string Xml(string name) => $"<elements><element id='{id}' name='{name}' type='Proficiency' source='Review' /></elements>";
        var database = Path.Combine(root, ContentDatabaseService.DatabaseFileName);
        try
        {
            property.SetValue(manager, root);
            additional.Clear();
            DbElementLoader.ResetCaches();
            File.WriteAllText(Path.Combine(root, "a.xml"), Xml("Original retained"));
            await ContentImport.ImportAsync(root, database, skipUnusableContent: true);
            File.WriteAllText(Path.Combine(root, "a.xml"), Xml("Changed A"));
            File.WriteAllText(Path.Combine(root, "b.xml"), Xml("Conflicting B"));
            await ContentImport.ImportAsync(root, database, skipUnusableContent: true);
            ContentDatabaseReader.ReadSkippedContent(database).Should().Contain(s => s.Kind == "definition-collision");
            ContentDatabaseReader.ReadUnavailableIds(database).Should().BeEmpty();
            var prepared = new ElementBaseCollection();
            var success = await DbElementLoader.TryLoadSnapshotAsync(prepared);
            success.Success.Should().BeTrue(success.FailureReason);
            prepared.GetElement(id)!.Name.Should().Be("Original retained");

            // A new malformed user XML file is read at runtime without a database refresh.
            var user = Path.Combine(root, "user");
            Directory.CreateDirectory(user);
            File.WriteAllText(Path.Combine(user, "broken.xml"), "<elements><element");
            manager.ElementsCollection.Clear();
            var failed = await DbElementLoader.TryLoadAsync(manager.ElementsCollection);
            failed.Success.Should().BeFalse();
            output.WriteLine("Prepared load failure: " + failed.FailureReason);
            // Follow CharacterService.EnsureElementsLoadedAsync's cold-start fallback exactly.
            ContentDatabaseService.ValidateRawXmlFallback(failed.DatabasePath, failed.FailureReason);
            await manager.InitializeElementDataAsync();
            output.WriteLine("After fallback: " + manager.ElementsCollection.GetElement(id)?.Name);
            manager.ElementsCollection.GetElement(id)!.Name.Should().Be("Original retained",
                "falling back must not discard the importer's preserved working definition");
        }
        finally
        {
            property.SetValue(manager, previousRoot);
            additional.Clear();
            foreach (var directory in previousAdditional) additional.Add(directory);
            manager.ElementsCollection.Clear();
            manager.ElementsCollection.AddRange(previousElements);
            DbElementLoader.ResetCaches();
            Directory.Delete(root, recursive: true);
        }
    }
}
