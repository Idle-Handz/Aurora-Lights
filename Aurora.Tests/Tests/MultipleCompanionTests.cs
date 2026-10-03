using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class MultipleCompanionTests
{
    [Fact]
    public async Task ExtrasAndSelectedCompanionsKeepIndependentStatsAndSurviveReopenAndRemoval()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        string originalRoot = settings.DocumentsRootDirectory;
        bool originalSeeded = settings.SourcePreferencesSeeded;
        var catalog = DataManager.Current.ElementsCollection;
        var originals = catalog.ToArray();
        var originalHandler = SelectionRuleExpanderContext.Current;
        var originalSpells = SpellcastingSectionContext.Current;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        string custom = Path.Combine(root, "custom");
        Directory.CreateDirectory(custom);
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        var manager = CharacterManager.Current;
        try
        {
            await manager.New(false);
            settings.DocumentsRootDirectory = root;
            settings.SourcePreferencesSeeded = true;
            DataManager.Current.InitializeDirectories();
            File.WriteAllText(Path.Combine(custom, "companions.xml"), """
                <elements>
                  <element name="Keeper" type="Class" source="Test" id="ID_TEST_KEEPER">
                    <setters><set name="hd">d8</set></setters>
                    <rules>
                      <select type="Companion" name="Pet" />
                      <stat name="companion:ac" value="4" />
                    </rules>
                  </element>
                  <element name="Training" type="Companion Feature" source="Test" id="ID_TEST_TRAINING">
                    <rules><stat name="companion:ac" value="1" /></rules>
                  </element>
                """ + Creature("CAT", 14, 5, 30) + Creature("OX", 8, 19, 40) + "</elements>");
            await ContentImport.ImportAsync(custom, Path.Combine(custom, ContentDatabaseService.DatabaseFileName));
            var service = new CharacterService();
            await service.PreloadAsync();
            catalog.GetElement("ID_TEST_KEEPER").Should().NotBeNull(service.ElementLoadSummary + " " + service.ElementLoadFailureReason);
            await manager.New(true);
            manager.Character.Name = "Companion regression";
            var handler = SelectionRuleExpanderContext.Current;
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), "ID_TEST_KEEPER");
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Name == "Pet"), "ID_TEST_CAT");
            manager.ReprocessCharacter();
            manager.Character.Companions.Should().ContainSingle();
            manager.Character.Companion.Statistics.ArmorClass.Should().Be(16); // 10 + Dex 2 + keeper 4

            EquipmentService.GetCustomFeatureCategories().Should().Contain("Companions");
            EquipmentService.SearchCustomFeatures("Companions", "OX").Select(r => r.Id).Should().Equal("ID_TEST_OX");
            var file = new CharacterFile(Path.Combine(root, "companions.dnd5e"));
            file.Save(manager.Character).Should().BeTrue();
            var tab = new CharacterTab(file) { Character = manager.Character };
            CharacterContext.ClaimAfterLoad(tab);
            EquipmentService.SearchCustomFeatures("Companions", "CAT", await BuildService.GetCustomFeatureOwnedIdsAsync(tab))
                .Should().Contain(r => r.Id == "ID_TEST_CAT", "an existing class companion does not prevent adopting another pet");
            foreach (var id in new[] { "ID_TEST_OX", "ID_TEST_CAT", "ID_TEST_CAT" })
                (await BuildService.AddCustomFeatureAsync(tab, id)).Should().BeNull();
            var trained = manager.Character.Companions.Last().Element;
            handler.SetRegisteredElement(trained.GetSelectRules().Single(), "ID_TEST_TRAINING");
            manager.ReprocessCharacter();
            tab.Snapshot = CharacterSnapshot.From(manager.Character);
            AssertCompanions(manager);
            tab.Snapshot!.Companions.Should().HaveCount(4);
            tab.Snapshot.Companions.Select(c => c.ArmorClass).Should().BeEquivalentTo(new[] { "16", "9", "12", "13" });
            BuildService.GetCustomFeatures(tab).Should().HaveCount(3);

            for (int i = 0; i < manager.Character.Companions.Count; i++)
                manager.Character.Companions[i].CompanionName.Content = "Pet " + i;
            file.Save(manager.Character).Should().BeTrue();
            await CharacterContext.InvalidateAsync();
            var loaded = await service.LoadCharacterAsync(file);
            loaded.Success.Should().BeTrue(loaded.Message);
            AssertCompanions(manager);
            manager.Character.Companions.Select(c => c.CompanionName.Content).Should().Equal("Pet 0", "Pet 1", "Pet 2", "Pet 3");
            BuildService.ReapplyCustomFeatures(file);
            BuildService.ReapplyCustomFeatures(file);
            AssertCompanions(manager);

            tab.Character = manager.Character;
            CharacterContext.ClaimAfterLoad(tab);
            (await BuildService.RemoveCustomFeatureAsync(tab, "ID_TEST_CAT")).Should().BeNull();
            manager.Character.Companions.Should().HaveCount(3);
            manager.Character.Companions.Single(c => c.Element.Aquisition.WasSelected).Statistics.ArmorClass.Should().Be(16);
            manager.Character.Companions.Count(c => c.Element.Id == "ID_TEST_CAT").Should().Be(2);
            (await BuildService.RemoveCustomFeatureAsync(tab, "ID_TEST_CAT")).Should().BeNull();
            manager.Character.Companions.Should().HaveCount(2);
            manager.Character.Companions.Single(c => c.Element.Id == "ID_TEST_CAT").Element.Aquisition.WasSelected.Should().BeTrue();
            handler = SelectionRuleExpanderContext.Current;
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Name == "Pet"), "ID_TEST_OX");
            manager.ReprocessCharacter();
            manager.Character.Companions.Should().HaveCount(2);
            manager.Character.Companions.Select(c => c.Statistics.ArmorClass).Should().BeEquivalentTo(new[] { 9, 13 });
            file.Save(manager.Character).Should().BeTrue();
            await CharacterContext.InvalidateAsync();
            (await service.LoadCharacterAsync(file)).Success.Should().BeTrue();
            manager.Character.Companions.Should().HaveCount(2);
            manager.Character.Companions.Select(c => c.Statistics.ArmorClass).Should().BeEquivalentTo(new[] { 9, 13 });
            manager.Character.Companions.Should().OnlyContain(c => c.Element is CompanionElement);
            tab.Character = manager.Character;
            CharacterContext.ClaimAfterLoad(tab);
            (await BuildService.RemoveCustomFeatureAsync(tab, "ID_TEST_OX")).Should().BeNull();
            manager.Character.Companions.Should().ContainSingle();
            manager.UnregisterElement(manager.Character.Companion.Element);
            manager.Character.Companions.Should().BeEmpty();
            manager.Status.HasCompanion.Should().BeFalse();
            CharacterSnapshot.From(manager.Character).HasCompanion.Should().BeFalse();
        }
        finally
        {
            await CharacterContext.InvalidateAsync();
            await manager.New(false);
            catalog.Clear();
            foreach (var element in originals) catalog.Add(element);
            settings.DocumentsRootDirectory = originalRoot;
            settings.SourcePreferencesSeeded = originalSeeded;
            DataManager.Current.InitializeDirectories();
            SelectionRuleExpanderContext.Current = originalHandler;
            SpellcastingSectionContext.Current = originalSpells;
            DbElementLoader.ResetCaches();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void AssertCompanions(CharacterManager manager)
    {
        manager.Character.Companions.Should().HaveCount(4);
        manager.GetElements().OfType<CompanionElement>().Should().HaveCount(4);
        manager.Character.Companions.Select(c => c.Statistics.ArmorClass).Should().BeEquivalentTo(new[] { 16, 9, 12, 13 });
        manager.Character.Companions.Select(c => c.Statistics.MaxHp).Should().BeEquivalentTo(new[] { 5, 19, 5, 5 });
        manager.Character.Companions.Select(c => c.Statistics.Speed).Should().BeEquivalentTo(new[] { 30, 40, 30, 30 });
    }

    private static string Creature(string name, int dexterity, int hp, int speed) => $$"""
        <element name="{{name}}" type="Companion" source="Test" id="ID_TEST_{{name}}">
          <setters>
            <set name="strength">10</set><set name="dexterity">{{dexterity}}</set><set name="constitution">10</set>
            <set name="intelligence">2</set><set name="wisdom">10</set><set name="charisma">6</set>
            <set name="size">Medium</set><set name="type">Beast</set><set name="alignment">Unaligned</set>
            <set name="challenge">0</set><set name="ac">10</set><set name="hp">{{hp}}</set><set name="speed">{{speed}}</set>
          </setters>
          <rules>
            <select name="Training" type="Companion Feature" optional="true" />
            <stat name="companion:ac" value="10" />
            <stat name="companion:ac" value="companion:dexterity:modifier" />
            <stat name="companion:hp:max" value="{{hp}}" />
            <stat name="companion:speed" value="{{speed}}" />
          </rules>
        </element>
        """;

}
