using System.Xml;
using System.Xml.Linq;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Strings;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class BuildPendingSnapshotEditsTests
{
    private const string MainClassId = "ID_PENDING_CLASS";
    private const string MulticlassId = "ID_PENDING_MULTICLASS";
    private const string ExtraId = "ID_PENDING_EXTRA";
    private const string LanguageOptionId = "ID_WOTC_TCOE_OPTION_CUSTOMIZED_LANGUAGE";

    [Theory]
    [InlineData("option")]
    [InlineData("hp")]
    [InlineData("level-up")]
    [InlineData("level-down")]
    [InlineData("custom-add")]
    [InlineData("custom-remove")]
    [InlineData("multiclass-new")]
    [InlineData("multiclass-level")]
    public Task BuildMutationsPreservePendingEditableFieldsInSnapshotAndFile(string operation) =>
        WithCharacter(async (manager, tab, _) =>
        {
            if (operation == "level-down")
                (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();
            if (operation == "custom-remove")
                (await BuildService.AddCustomFeatureAsync(tab, ExtraId)).Should().BeNull();
            if (operation == "multiclass-level")
                (await BuildService.AddMulticlassLevelAsync(tab, MulticlassId)).Should().BeNull();

            SetPendingEdits(tab.Snapshot!);
            tab.IsDirty = true;
            int levelBefore = manager.Character.Level;

            string? error = operation switch
            {
                "option" => await BuildService.SetCustomLanguageOptionAsync(tab, true),
                "hp" => await BuildService.SetHpMethodAsync(tab, HpMethod.Average),
                "level-up" => (await BuildService.LevelUpMainAsync(tab)).Error,
                "level-down" => await BuildService.LevelDownAsync(tab),
                "custom-add" => await BuildService.AddCustomFeatureAsync(tab, ExtraId),
                "custom-remove" => await BuildService.RemoveCustomFeatureAsync(tab, ExtraId),
                _ => await BuildService.AddMulticlassLevelAsync(tab, MulticlassId)
            };

            error.Should().BeNull();
            AssertPendingEdits(tab, operation == "level-down" ? 250 : 7000);
            if (operation is "level-up" or "multiclass-new" or "multiclass-level")
                tab.Snapshot!.Level.Should().Be(levelBefore + 1);
            else if (operation == "level-down")
                tab.Snapshot!.Level.Should().Be(levelBefore - 1);
            else if (operation == "option")
                manager.ContainsOption(LanguageOptionId).Should().BeTrue();
            else if (operation == "hp")
                manager.ContainsAverageHitPointsOption().Should().BeTrue();
            else if (operation.StartsWith("custom-", StringComparison.Ordinal))
                tab.File.LoadCustomFeatures().Contains(ExtraId).Should().Be(operation == "custom-add");
        });

    [Fact]
    public Task LevelUpRetainsTheEngineExperienceIncreaseInsteadOfRestoringTheOldSnapshot() =>
        WithCharacter(async (manager, tab, _) =>
        {
            tab.Snapshot!.Name = "Pending name";
            tab.Snapshot.Experience = 5;

            (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();

            tab.Snapshot!.Name.Should().Be("Pending name");
            tab.Snapshot.Level.Should().Be(2);
            tab.Snapshot.Experience.Should().Be(300);
            XDocument.Load(tab.File.FilePath).Root!.Element("build")!.Element("input")!
                .Element("experience")!.Value.Should().Be("300");
        });

    [Theory]
    [InlineData("Alignment", "ID_PENDING_ALIGNMENT", "New alignment")]
    [InlineData("Deity", "ID_PENDING_DEITY", "New deity")]
    public Task SelectingNarrativeRuleKeepsPendingTextButUsesTheNewSelection(
        string type, string elementId, string expected) =>
        WithCharacter(async (manager, tab, _) =>
        {
            SetPendingEdits(tab.Snapshot!);
            tab.Snapshot!.Alignment = "Pending alignment";
            tab.Snapshot.Deity = "Pending deity";
            var rule = manager.SelectionRules.Single(r => r.Attributes.Type == type);

            var errors = await BuildService.ApplySelectionAndSaveAsync(
                rule, elementId, 1, tab, tab.File);

            errors.Should().BeEmpty();
            AssertPendingEdits(tab);
            (type == "Alignment" ? tab.Snapshot!.Alignment : tab.Snapshot!.Deity).Should().Be(expected);
            (type == "Alignment" ? manager.Character.Alignment : manager.Character.Deity).Should().Be(expected);
            (type == "Alignment" ? tab.Snapshot.Deity : tab.Snapshot.Alignment)
                .Should().Be(type == "Alignment" ? "Pending deity" : "Pending alignment");
        });

    [Fact]
    public Task ManualSaveTransfersAlignmentAndDeityNarrativeEditsToTheLiveCharacter() =>
        WithCharacter(async (manager, tab, _) =>
        {
            tab.Snapshot!.Alignment = "Personal alignment";
            tab.Snapshot.Deity = "Personal faith";

            (await BuildService.SaveTabAsync(tab)).Should().BeNull();

            manager.Character.Alignment.Should().Be("Personal alignment");
            manager.Character.Deity.Should().Be("Personal faith");
        });

    [Fact]
    public Task PendingEditsSurviveATabSwapBeforeTheBuildMutation() =>
        WithCharacter(async (manager, tab, directory) =>
        {
            var secondPath = Path.Combine(directory, "second.dnd5e");
            File.Copy(tab.File.FilePath, secondPath);
            var second = new CharacterTab(new CharacterFile(secondPath));
            SetPendingEdits(tab.Snapshot!);
            tab.IsDirty = true;

            using (await CharacterContext.EnterAsync(second)) { }
            Action flushInactiveTab = () => BuildService.FlushPendingSnapshotEdits(tab);
            flushInactiveTab.Should().Throw<InvalidOperationException>();
            (await BuildService.SetCustomLanguageOptionAsync(tab, true)).Should().BeNull();

            AssertPendingEdits(tab);
            XDocument.Load(secondPath).Root!.Element("build")!.Element("input")!
                .Element("name")!.Value.Should().Be("Original name");
        });

    [Fact]
    public Task ClearingATrinketSurvivesTheSnapshotRefresh() =>
        WithCharacter(async (manager, tab, _) =>
        {
            manager.Character.Trinket.Content = "Old trinket";
            BuildService.ResnapTab(tab);
            tab.Snapshot!.Trinket = "";

            (await BuildService.SetCustomLanguageOptionAsync(tab, true)).Should().BeNull();

            tab.Snapshot!.Trinket.Should().BeEmpty();
            XDocument.Load(tab.File.FilePath).Root!.Element("build")!.Element("input")!
                .Element("background-trinket")!.Value.Should().BeEmpty();
        });

    private static void SetPendingEdits(CharacterSnapshot snap)
    {
        snap.Name = "Pending name";
        snap.PlayerName = "Pending player";
        snap.Experience = 7000;
        snap.Gender = "Pending gender";
        snap.Age = "Pending age";
        snap.Height = "Pending height";
        snap.Weight = "Pending weight";
        snap.Eyes = "Pending eyes";
        snap.Hair = "Pending hair";
        snap.Skin = "Pending skin";
        snap.Backstory = "Pending backstory\nSecond line";
        snap.Organisation = "Pending organisation";
        snap.Allies = "Pending allies";
        snap.Trinket = "Pending trinket";
        snap.Notes1 = "Pending left notes";
        snap.Notes2 = "Pending right notes";
        snap.InventoryEquipmentText = "Pending equipment";
        snap.InventoryTreasureText = "Pending treasure";
        snap.InventoryQuestText = "Pending quest";
        snap.CoinCopper = 11;
        snap.CoinSilver = 12;
        snap.CoinElectrum = 13;
        snap.CoinGold = 14;
        snap.CoinPlatinum = 15;
    }

    private static void AssertPendingEdits(CharacterTab tab, int expectedExperience = 7000)
    {
        var expected = new CharacterSnapshot();
        SetPendingEdits(expected);
        expected.Experience = expectedExperience;
        foreach (string property in new[]
        {
            "Name", "PlayerName", "Experience", "Gender", "Age", "Height", "Weight", "Eyes", "Hair", "Skin",
            "Backstory", "Organisation", "Allies", "Trinket", "Notes1", "Notes2", "InventoryEquipmentText",
            "InventoryTreasureText", "InventoryQuestText", "CoinCopper", "CoinSilver", "CoinElectrum", "CoinGold", "CoinPlatinum"
        })
        {
            var info = typeof(CharacterSnapshot).GetProperty(property)!;
            info.GetValue(tab.Snapshot).Should().Be(info.GetValue(expected), property + " must survive re-snapshotting");
        }

        var build = XDocument.Load(tab.File.FilePath).Root!.Element("build")!;
        var input = build.Element("input")!;
        var appearance = build.Element("appearance")!;
        foreach (var pair in new Dictionary<string, string>
        {
            ["name"] = expected.Name, ["player-name"] = expected.PlayerName, ["experience"] = expectedExperience.ToString(),
            ["gender"] = expected.Gender, ["backstory"] = expected.Backstory,
            ["background-trinket"] = expected.Trinket, ["quest"] = expected.InventoryQuestText
        }) input.Element(pair.Key)!.Value.Should().Be(pair.Value, pair.Key + " must be saved");
        foreach (var pair in new Dictionary<string, string>
        {
            ["age"] = expected.Age, ["height"] = expected.Height, ["weight"] = expected.Weight,
            ["eyes"] = expected.Eyes, ["hair"] = expected.Hair, ["skin"] = expected.Skin
        }) appearance.Element(pair.Key)!.Value.Should().Be(pair.Value, pair.Key + " must be saved");
        input.Element("organization")!.Element("name")!.Value.Should().Be(expected.Organisation);
        input.Element("organization")!.Element("allies")!.Value.Should().Be(expected.Allies);
        input.Element("notes")!.Elements("note").Single(n => (string?)n.Attribute("column") == "left")
            .Value.Should().Be(expected.Notes1);
        input.Element("notes")!.Elements("note").Single(n => (string?)n.Attribute("column") == "right")
            .Value.Should().Be(expected.Notes2);
        foreach (var pair in new Dictionary<string, string>
        {
            ["copper"] = "11", ["silver"] = "12", ["electrum"] = "13", ["gold"] = "14", ["platinum"] = "15",
            ["equipment"] = expected.InventoryEquipmentText, ["treasure"] = expected.InventoryTreasureText
        }) input.Element("currency")!.Element(pair.Key)!.Value.Should().Be(pair.Value);
    }

    private static async Task WithCharacter(Func<CharacterManager, CharacterTab, string, Task> test)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var catalog = DataManager.Current.ElementsCollection;
        var originals = catalog.ToArray();
        var previousHandler = SelectionRuleExpanderContext.Current;
        var previousSpells = SpellcastingSectionContext.Current;
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        var manager = CharacterManager.Current;
        string directory = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "pending-edits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await manager.New(false);
            catalog.Clear();
            for (int number = 1; number <= 4; number++)
            {
                string rules = number == 1
                    ? "<select type='Class' name='Class' /><select type='Alignment' name='Alignment' optional='true' /><select type='Deity' name='Deity' optional='true' />"
                    : $"<select type='Multiclass' name='Multiclass' level='{number}' requirements='ID_INTERNAL_MULTICLASS_LEVEL_{number}' />";
                var level = Parse($"<element name='{number}' type='Level' source='Internal' id='ID_PENDING_LEVEL_{number}'><rules>{rules}</rules></element>")
                    .Construct<LevelElement>();
                level.Level = number;
                level.RequiredExperience = (number - 1) * 300;
                level.ElementSetters.Add(new("Level", number.ToString()));
                catalog.Add(level);
                catalog.Add(Parse($"<element name='Multiclass level {number}' type='Grants' source='Internal' id='ID_INTERNAL_MULTICLASS_LEVEL_{number}' />").Construct<Grants>());
            }
            var mainClass = Parse($"<element name='Main class' type='Class' source='Test' id='{MainClassId}' />").Construct<Class>();
            mainClass.HitDice = "d8";
            catalog.Add(mainClass);
            var multiclass = Parse($"<element name='Second class' type='Multiclass' source='Test' id='{MulticlassId}' />").Construct<Multiclass>();
            multiclass.HitDice = "d6";
            catalog.Add(multiclass);
            catalog.Add(Parse($"<element name='Extra' type='Feat' source='Test' id='{ExtraId}' />").Construct<Feat>());
            catalog.Add(Parse("<element name='New alignment' type='Alignment' source='Test' id='ID_PENDING_ALIGNMENT' />"));
            catalog.Add(Parse("<element name='New deity' type='Deity' source='Test' id='ID_PENDING_DEITY' />").Construct<Deity>());
            foreach (string id in new[] { LanguageOptionId, InternalOptions.AllowAverageHitPoints, InternalOptions.AllowMulticlassing })
                catalog.Add(Parse($"<element name='{id}' type='Option' source='Internal' id='{id}' />").Construct<Option>());
            var prerequisite = Parse("<element name='Multiclass prerequisite' type='Grants' source='Internal' id='ID_INTERNAL_GRANTS_MULTICLASSING_PREREQUISITE' />").Construct<Grants>();
            catalog.Add(prerequisite);

            manager.RegisterElement(catalog.GetElement("ID_PENDING_LEVEL_1"));
            SelectionRuleExpanderContext.Current.SetRegisteredElement(
                manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), MainClassId);
            manager.RegisterElement(prerequisite);
            manager.RegisterElement(catalog.GetElement(InternalOptions.AllowMulticlassing));
            manager.Character.Name = "Original name";
            var file = new CharacterFile(Path.Combine(directory, "character.dnd5e"));
            file.Save(manager.Character).Should().BeTrue();
            var tab = new CharacterTab(file)
            {
                Character = manager.Character,
                Snapshot = CharacterSnapshot.From(manager.Character)
            };
            CharacterContext.ClaimAfterLoad(tab);
            await test(manager, tab, directory);
        }
        finally
        {
            await CharacterContext.InvalidateAsync();
            await manager.New(false);
            catalog.Clear();
            foreach (var element in originals) catalog.Add(element);
            SelectionRuleExpanderContext.Current = previousHandler;
            SpellcastingSectionContext.Current = previousSpells;
            Directory.Delete(directory, true);
        }
    }

    private static ElementBase Parse(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return new ElementParser().ParseElement(document.DocumentElement!);
    }
}
