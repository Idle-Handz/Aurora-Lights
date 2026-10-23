using System.Xml;
using Aurora.App.Services;
using Builder.Presentation;
using Builder.Presentation.Models;

namespace Aurora.Tests.Tests;

public sealed class CustomFeatureAutosaveTests
{
    private const string ExtraId = "ID_PENDING_EXTRA";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task AddAndRemoveRespectTheSavePolicyAndRetainPendingText(bool remove, bool autoSave) =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            if (remove) (await BuildService.AddCustomFeatureAsync(tab, ExtraId)).Should().BeNull();
            byte[] before = File.ReadAllBytes(tab.File.FilePath);
            tab.Snapshot!.Name = "Pending feature draft";
            tab.IsDirty = true;

            string? error = remove
                ? await BuildService.RemoveCustomFeatureAsync(tab, ExtraId, saveToFile: autoSave)
                : await BuildService.AddCustomFeatureAsync(tab, ExtraId, saveToFile: autoSave);

            error.Should().BeNull();
            tab.Snapshot.Name.Should().Be("Pending feature draft");
            BuildService.GetCustomFeatures(tab).Any(f => f.Id == ExtraId).Should().Be(!remove);
            if (!autoSave)
            {
                File.ReadAllBytes(tab.File.FilePath).Should().Equal(before);
                tab.IsDirty.Should().BeTrue();
                string exported = Path.Combine(directory, "feature-export.dnd5e");
                await BuildService.ExportCharacterAsync(tab, exported);
                var copy = new CharacterFile(exported);
                copy.LoadCustomFeatures().Contains(ExtraId).Should().Be(!remove);
                var document = new XmlDocument(); document.Load(exported);
                document.SelectSingleNode("/character/build/input/name")!.InnerText.Should().Be("Pending feature draft");
                File.ReadAllBytes(tab.File.FilePath).Should().Equal(before, "export must not commit the source draft");
                (await BuildService.SaveTabAsync(tab)).Should().BeNull();
            }
            tab.File.LoadCustomFeatures().Contains(ExtraId).Should().Be(!remove);
            var saved = new XmlDocument(); saved.Load(tab.File.FilePath);
            saved.SelectSingleNode("/character/build/input/name")!.InnerText.Should().Be("Pending feature draft");
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DeferredFeatureStateSurvivesTabSwitchesWithoutResurrectingRemovedFeatures(bool remove) =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (manager, tab, directory) =>
        {
            if (remove) (await BuildService.AddCustomFeatureAsync(tab, ExtraId)).Should().BeNull();
            byte[] before = File.ReadAllBytes(tab.File.FilePath);
            string secondPath = Path.Combine(directory, "second.dnd5e");
            File.Copy(tab.File.FilePath, secondPath);
            var second = new CharacterTab(new CharacterFile(secondPath));
            tab.Snapshot!.Name = "Tab-owned pending name";
            tab.IsDirty = true;
            (remove
                ? await BuildService.RemoveCustomFeatureAsync(tab, ExtraId, saveToFile: false)
                : await BuildService.AddCustomFeatureAsync(tab, ExtraId, saveToFile: false)).Should().BeNull();

            using (await CharacterContext.EnterAsync(second))
            {
                manager.GetElements().Any(e => e.Id == ExtraId).Should().Be(remove, "the other tab has its own saved build");
            }
            using (await CharacterContext.EnterAsync(tab))
            {
                manager.GetElements().Any(e => e.Id == ExtraId).Should().Be(!remove);
                BuildService.GetCustomFeatures(tab).Any(f => f.Id == ExtraId).Should().Be(!remove);
                tab.Snapshot.Name.Should().Be("Tab-owned pending name");
            }
            var exported = await BuildService.CaptureCharacterExportAsync(tab);
            (exported.SelectSingleNode($"/character/custom-features/feature[@id='{ExtraId}']") is not null).Should().Be(!remove);
            File.ReadAllBytes(tab.File.FilePath).Should().Equal(before);

            await CharacterContext.ReleaseAsync(tab);
            var reopened = new CharacterTab(new CharacterFile(tab.File.FilePath));
            using (await CharacterContext.EnterAsync(reopened))
                manager.GetElements().Any(e => e.Id == ExtraId).Should().Be(remove, "discard/reopen must use the saved build");
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task BackgroundRecoveryRetainsUnsavedFeatureChangesAndSessionState(bool remove, bool inactive) =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            if (remove) (await BuildService.AddCustomFeatureAsync(tab, ExtraId)).Should().BeNull();
            byte[] before = File.ReadAllBytes(tab.File.FilePath);
            tab.Snapshot!.Name = "Recovered feature draft";
            tab.Session.CurrentHp = 3;
            tab.Session.Inspiration = true;
            tab.IsDirty = true;
            (remove
                ? await BuildService.RemoveCustomFeatureAsync(tab, ExtraId, saveToFile: false)
                : await BuildService.AddCustomFeatureAsync(tab, ExtraId, saveToFile: false)).Should().BeNull();
            if (inactive)
            {
                string secondPath = Path.Combine(directory, "second.dnd5e");
                File.Copy(tab.File.FilePath, secondPath);
                using (await CharacterContext.EnterAsync(new CharacterTab(new CharacterFile(secondPath)))) { }
            }
            var recovery = new CharacterDraftRecovery(Path.Combine(directory, "journals"));
            recovery.Track(tab);
            recovery.CheckpointOnBackground();
            await recovery.FlushAsync();
            var interrupted = new CharacterDraftRecovery(Path.Combine(directory, "journals"));
            string recovered = interrupted.OpenCopy(interrupted.List().Single().Id, Path.Combine(directory, "copies"));
            var document = new XmlDocument(); document.Load(recovered);
            document.SelectSingleNode("/character/build/input/name")!.InnerText.Should().Be("Recovered feature draft");
            (document.SelectSingleNode($"/character/custom-features/feature[@id='{ExtraId}']") is not null).Should().Be(!remove);
            SessionStore.Load(recovered).CurrentHp.Should().Be(3);
            SessionStore.Load(recovered).Inspiration.Should().BeTrue();
            File.ReadAllBytes(tab.File.FilePath).Should().Equal(before);
            recovery.Forget(tab);
        });

    [Fact]
    public Task FailedSaveKeepsTheDeferredFeatureForRetry() =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (_, tab, directory) =>
        {
            string originalPath = tab.File.FilePath;
            byte[] before = File.ReadAllBytes(originalPath);
            (await BuildService.AddCustomFeatureAsync(tab, ExtraId, saveToFile: false)).Should().BeNull();
            tab.File.FilePath = Path.Combine(directory, "missing", "character.dnd5e");
            try
            {
                (await BuildService.SaveTabAsync(tab)).Should().NotBeNull();
                tab.IsDirty.Should().BeTrue();
                BuildService.GetCustomFeatures(tab).Should().Contain(f => f.Id == ExtraId);
            }
            finally { tab.File.FilePath = originalPath; }
            File.ReadAllBytes(originalPath).Should().Equal(before);
            (await BuildService.SaveTabAsync(tab)).Should().BeNull();
            tab.File.LoadCustomFeatures().Should().Contain(ExtraId);
        });
}
