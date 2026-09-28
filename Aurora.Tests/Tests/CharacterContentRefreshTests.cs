using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class CharacterContentRefreshTests
{
    [Fact]
    public async Task RefreshThenReopenAndToggleArmorPreservesStatsAndSavedClassChoice()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var settings = ApplicationContext.Current.Settings;
        string originalRoot = settings.DocumentsRootDirectory;
        bool originalSeeded = settings.SourcePreferencesSeeded;
        var catalog = DataManager.Current.ElementsCollection;
        var originalElements = catalog.ToArray();
        var originalSelection = SelectionRuleExpanderContext.Current;
        var originalSpells = SpellcastingSectionContext.Current;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        string custom = Path.Combine(root, "custom");
        Directory.CreateDirectory(custom);
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        var manager = CharacterManager.Current;
        var service = new CharacterService();
        try
        {
            await manager.New(false);
            settings.DocumentsRootDirectory = root;
            settings.SourcePreferencesSeeded = true; // Do not persist global settings in this test.
            DataManager.Current.InitializeDirectories();
            File.WriteAllText(Path.Combine(custom, "refresh-test.xml"), """
                <elements>
                  <element name="Refresh Ranger" type="Class" source="Test" id="ID_TEST_REFRESH_CLASS">
                    <setters><set name="hd">d10</set></setters>
                    <rules><select type="Class Feature" name="Fighting Style" supports="Refresh Style" /></rules>
                  </element>
                  <element name="Refresh Style" type="Class Feature" source="Test" id="ID_TEST_REFRESH_STYLE">
                    <supports>Refresh Style</supports>
                    <rules><stat name="speed" value="30" /></rules>
                  </element>
                  <element name="Test Studded Leather" type="Armor" source="Test" id="ID_TEST_REFRESH_ARMOR">
                    <setters><set name="slot">armor</set><set name="armor">Light</set></setters>
                    <rules><stat name="ac:armored:armor" value="12" /></rules>
                  </element>
                </elements>
                """);
            await ContentImport.ImportAsync(custom, Path.Combine(custom, ContentDatabaseService.DatabaseFileName));
            await service.PreloadAsync();

            // Create a small saved character using the real engine and writer. Built-in
            // AC rules plus light armor and Dex 14 reproduce Lola's expected AC of 14.
            await manager.New(true);
            manager.Character.Name = "Refresh regression";
            manager.Character.Abilities.Dexterity.BaseScore = 14;
            var handler = SelectionRuleExpanderContext.Current;
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), "ID_TEST_REFRESH_CLASS");
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Name == "Fighting Style"), "ID_TEST_REFRESH_STYLE");
            EquipmentService.AddAndEquipToSlot(manager.Character, GearSlot.Armor, "ID_TEST_REFRESH_ARMOR").Should().BeTrue();
            manager.ReprocessCharacter();
            manager.Character.ArmorClass.Should().Be(14);
            var file = new CharacterFile(Path.Combine(root, "refresh.dnd5e"));
            file.Save(manager.Character).Should().BeTrue();
            byte[] originalSave = File.ReadAllBytes(file.FilePath);

            var loaded = await service.LoadCharacterAsync(file);
            loaded.Success.Should().BeTrue(loaded.Message);
            service.CurrentCharacter!.ArmorClass.Should().Be(14, "equipped armor must count immediately after load");
            var tabs = new CharacterTabService();
            CharacterContext.ClaimAfterLoad(tabs.OpenTab(file, service.CurrentCharacter!));
            service.IsPreloaded(file).Should().BeTrue();
            var oldBase = catalog.GetElement("ID_INTERNAL_GRANTS_CHARACTER_BASE");

            // This is the app sequence: close tab, replace the catalog, reopen the
            // same file through the preload fast path if the service still permits it.
            tabs.CloseAllTabs();
            await service.ReloadElementsAsync();
            catalog.GetElement("ID_INTERNAL_GRANTS_CHARACTER_BASE").Should().NotBeSameAs(oldBase);
            service.IsPreloaded(file).Should().BeFalse();
            service.CurrentCharacter.Should().BeNull();
            service.CurrentCharacterFile.Should().BeNull();
            CharacterContext.ActiveTab.Should().BeNull();
            if (!service.IsPreloaded(file))
            {
                loaded = await service.LoadCharacterAsync(file);
                loaded.Success.Should().BeTrue(loaded.Message);
            }
            var tab = tabs.OpenTab(file, service.CurrentCharacter!);
            CharacterContext.ClaimAfterLoad(tab);
            using (await CharacterContext.EnterAsync(tab))
            {
                var armor = tab.Character!.Inventory.EquippedArmor!;
                EquipmentService.UnequipSlot(tab.Character, GearSlot.Armor);
                manager.ReprocessCharacter();
                EquipmentService.EquipToSlot(tab.Character, GearSlot.Armor, armor.Identifier).Should().BeTrue();
                manager.ReprocessCharacter();
                manager.Character.ArmorClass.Should().Be(14, "refresh must not mix old and new AC grants");
                manager.GetElements().Count(e => e.Id == "ID_INTERNAL_GRANTS_CHARACTER_BASE").Should().Be(1);
                File.ReadAllBytes(file.FilePath).Should().Equal(originalSave, "refresh and reopen must not rewrite the save");
            }
            (await BuildService.SaveTabAsync(tab)).Should().BeNull();
            loaded = await service.LoadCharacterAsync(file);
            loaded.Success.Should().BeTrue(loaded.Message);
            var choice = manager.SelectionRules.Single(r => r.Attributes.Name == "Fighting Style");
            ((ElementBase)handler.GetRegisteredElement(choice)).Id.Should().Be("ID_TEST_REFRESH_STYLE");
            manager.GetElements().Count(e => e.Id == "ID_TEST_REFRESH_STYLE").Should().Be(1);
        }
        finally
        {
            await CharacterContext.InvalidateAsync();
            await manager.New(false);
            catalog.Clear();
            catalog.AddRange(originalElements);
            settings.DocumentsRootDirectory = originalRoot;
            settings.SourcePreferencesSeeded = originalSeeded;
            DataManager.Current.InitializeDirectories();
            SelectionRuleExpanderContext.Current = originalSelection;
            SpellcastingSectionContext.Current = originalSpells;
            DbElementLoader.ResetCaches();
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}

