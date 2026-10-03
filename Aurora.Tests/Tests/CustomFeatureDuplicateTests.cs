using System.Xml;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.ElementParsers;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class CustomFeatureDuplicateTests
{
    [Theory]
    [InlineData("Companion")]
    [InlineData("Feat")]
    [InlineData("Language")]
    [InlineData("Test Specialty")]
    [InlineData("Ability Score Improvement")]
    public Task OptionalChoicesDoNotKeepBuildGuidanceOpen(string type) =>
        WithCharacter((manager, file, tab) =>
        {
            manager.Character.Abilities.Strength.BaseScore = 12;
            var owner = Parse($"""
                <element name="Optional Training" type="Feat Feature" source="Test" id="ID_EXTRAS_OPTIONAL_TRAINING">
                  <rules><select type="{type}" name="Optional Specialty" optional="true" /></rules>
                </element>
                """).Construct<FeatFeature>();
            Add(owner);
            manager.RegisterElement(owner);

            var data = BuildService.GetBuildData(preferClassFirst: false);
            data.Tabs.SelectMany(buildTab => buildTab.RuleGroups).SelectMany(group => group.Rules)
                .Concat(data.AsiEntries).Should().Contain(entry => entry.Label == "Optional Specialty" && entry.IsOptional);
            data.Tabs.Sum(buildTab => buildTab.UnresolvedCount).Should().Be(0);
            data.NextStep.Should().BeNull("an optional choice is not a required build step");
            BuildService.GetNextRequiredStep().Should().BeNull();

            manager.SelectionRules.Single(rule => rule.Attributes.Name == "Optional Specialty").Attributes.Optional = false;
            var requiredStep = BuildService.GetBuildData(preferClassFirst: false).NextStep;
            requiredStep.Should().NotBeNull();
            requiredStep!.StepLabel.Should().Be("Optional Specialty", "the same unfilled choice still needs guidance when required");
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PickerAndRegistrationRejectRepeatedExtrasWithoutChangingTheSave(bool companion) =>
        WithCharacter(async (manager, file, tab) =>
        {
            ElementBase target = companion ? Creature("PET") : Parse("""
                <element name="Alert" type="Feat" source="Test" id="ID_EXTRAS_ALERT" />
                """).Construct<Feat>();
            Add(target);
            var proxy = companion ? target : AddProxy(target);
            string category = companion ? "Companions" : "Additional Feat";

            // Many types allow several different definitions without allowing the same one twice.
            target.AllowMultipleElements.Should().BeTrue();
            target.AllowDuplicate.Should().BeFalse();
            EquipmentService.SearchCustomFeatures(category, "", await BuildService.GetCustomFeatureOwnedIdsAsync(tab))
                .Should().Contain(r => r.Id == proxy.Id);
            (await BuildService.AddCustomFeatureAsync(tab, proxy.Id)).Should().BeNull();
            byte[] saved = File.ReadAllBytes(file.FilePath);

            EquipmentService.SearchCustomFeatures(category, "", await BuildService.GetCustomFeatureOwnedIdsAsync(tab))
                .Should().NotContain(r => r.Id == proxy.Id);
            (await BuildService.AddCustomFeatureAsync(tab, proxy.Id)).Should().Contain("already on this character");
            File.ReadAllBytes(file.FilePath).Should().Equal(saved);
            file.LoadCustomFeatures().Should().Equal(target.Id);
            manager.GetElements().Count(e => e.Id == target.Id).Should().Be(1);

            BuildService.ReapplyCustomFeatures(file);
            BuildService.ReapplyCustomFeatures(file);
            manager.GetElements().Count(e => e.Id == target.Id).Should().Be(1);

            if (companion)
            {
                var otherPet = Creature("OTHER_PET");
                Add(otherPet);
                EquipmentService.SearchCustomFeatures(category, "", await BuildService.GetCustomFeatureOwnedIdsAsync(tab))
                    .Should().Contain(r => r.Id == otherPet.Id);
                (await BuildService.AddCustomFeatureAsync(tab, otherPet.Id)).Should().BeNull();
                manager.Character.Companions.Should().HaveCount(2,
                    "a restriction on repeating one definition is not a limit on the number of pets");
            }
        });

    [Fact]
    public Task ProgressionGrantsCannotBeAddedAgainOrRemovedThroughStaleExtras() =>
        WithCharacter(async (manager, file, tab) =>
        {
            var feat = Parse("""
                <element name="Alert" type="Feat" source="Test" id="ID_EXTRAS_ALERT">
                  <setters><set name="allow duplicate">false</set></setters>
                </element>
                """).Construct<Feat>();
            Add(feat);
            var proxy = AddProxy(feat);
            var owner = Parse("""
                <element name="Training" type="Feat Feature" source="Test" id="ID_EXTRAS_TRAINING">
                  <rules><grant type="Feat" id="ID_EXTRAS_ALERT" /></rules>
                </element>
                """).Construct<FeatFeature>();
            Add(owner);
            manager.RegisterElement(owner);
            var granted = manager.GetElements().Single(e => e.Id == feat.Id);
            granted.Aquisition.WasGranted.Should().BeTrue();

            EquipmentService.SearchCustomFeatures("Additional Feat", "", await BuildService.GetCustomFeatureOwnedIdsAsync(tab))
                .Should().NotContain(r => r.Id == proxy.Id);
            (await BuildService.AddCustomFeatureAsync(tab, proxy.Id)).Should().Contain("already on this character");
            file.LoadCustomFeatures().Should().BeEmpty();

            // Older saves may already list this grant as an Extra, including by proxy ID.
            file.SaveCustomFeatures([proxy.Id, feat.Id]);
            BuildService.ReapplyCustomFeatures(file);
            BuildService.ReapplyCustomFeatures(file);
            manager.GetElements().Where(e => e.Id == feat.Id).Should().ContainSingle().Which.Should().BeSameAs(granted);
            (await BuildService.RemoveCustomFeatureAsync(tab, proxy.Id)).Should().BeNull();
            (await BuildService.RemoveCustomFeatureAsync(tab, feat.Id)).Should().BeNull();
            file.LoadCustomFeatures().Should().BeEmpty();
            manager.GetElements().Where(e => e.Id == feat.Id).Should().ContainSingle().Which.Should().BeSameAs(granted);
            granted.Aquisition.WasGranted.Should().BeTrue();
        });

    [Fact]
    public Task RepeatableAbilityIncreasesStillStackAndRemoveIndividually() =>
        WithCharacter(async (manager, file, tab) =>
        {
            var asi = Parse("""
                <element name="Dexterity" type="Ability Score Improvement" source="Test" id="ID_EXTRAS_DEXTERITY">
                  <setters><set name="allow duplicate">true</set></setters>
                  <rules><stat name="dexterity" value="1" /></rules>
                </element>
                """).Construct<AbilityScoreImprovement>();
            Add(asi);
            var proxy = AddProxy(asi);
            int before = manager.Character.Abilities.Dexterity.AdditionalScore;
            (await BuildService.AddCustomFeatureAsync(tab, proxy.Id)).Should().BeNull();
            EquipmentService.SearchCustomFeatures("Additional Ability Score Improvement", "",
                    await BuildService.GetCustomFeatureOwnedIdsAsync(tab))
                .Should().Contain(r => r.Id == proxy.Id);
            (await BuildService.AddCustomFeatureAsync(tab, proxy.Id)).Should().BeNull();
            manager.Character.Abilities.Dexterity.AdditionalScore.Should().Be(before + 2);
            file.LoadCustomFeatures().Should().Equal(asi.Id, asi.Id);
            (await BuildService.RemoveCustomFeatureAsync(tab, asi.Id)).Should().BeNull();
            manager.Character.Abilities.Dexterity.AdditionalScore.Should().Be(before + 1);
            file.LoadCustomFeatures().Should().Equal(asi.Id);
        });

    private static ElementBase AddProxy(ElementBase target)
    {
        var proxy = Parse($"""
            <element name="Additional {target.Type}, {target.Name}" type="Item" source="Test" id="{target.Id}_PROXY">
              <setters><set name="allow duplicate">true</set><set name="stackable">true</set></setters>
              <rules><grant type="{target.Type}" name="{target.Id}" /></rules>
            </element>
            """).Construct<Item>();
        Add(proxy);
        return proxy;
    }

    private static CompanionElement Creature(string id)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"""
            <element name="{id}" type="Companion" source="Test" id="ID_EXTRAS_{id}">
              <setters>
                <set name="strength">10</set><set name="dexterity">10</set>
                <set name="constitution">10</set><set name="intelligence">2</set>
                <set name="wisdom">10</set><set name="charisma">5</set>
                <set name="type">Beast</set><set name="size">Medium</set>
                <set name="alignment">Unaligned</set><set name="challenge">0</set>
                <set name="ac">10</set><set name="hp">4</set><set name="speed">30</set>
                <set name="allow duplicate">false</set>
              </setters>
            </element>
            """);
        return (CompanionElement)new CompanionElementParser().ParseElement(doc.DocumentElement!);
    }

    private static ElementBase Parse(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return new ElementParser().ParseElement(doc.DocumentElement!);
    }

    private static void Add(ElementBase element) => DataManager.Current.ElementsCollection.Add(element);

    private static async Task WithCharacter(Func<CharacterManager, CharacterFile, CharacterTab, Task> test)
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
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "extras-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await manager.New(false);
            catalog.Clear();
            // The engine queries the upcoming level while resetting an active character.
            for (int number = 1; number <= 2; number++)
            {
                var level = new LevelElement
                {
                    ElementHeader = new($"Level {number}", "Level", "Internal", $"ID_EXTRAS_LEVEL_{number}"),
                    Level = number,
                    RequiredExperience = (number - 1) * 300,
                };
                level.ElementSetters.Add(new("Level", number.ToString()));
                Add(level);
                if (number == 1) manager.RegisterElement(level);
            }
            manager.Character.Name = "Extras regression";
            var file = new CharacterFile(Path.Combine(root, "extras.dnd5e"));
            file.Save(manager.Character).Should().BeTrue();
            var tab = new CharacterTab(file) { Character = manager.Character };
            CharacterContext.ClaimAfterLoad(tab);
            await test(manager, file, tab);
        }
        finally
        {
            await CharacterContext.InvalidateAsync();
            await manager.New(false);
            catalog.Clear();
            foreach (var element in originals) catalog.Add(element);
            SelectionRuleExpanderContext.Current = previousHandler;
            SpellcastingSectionContext.Current = previousSpells;
            Directory.Delete(root, true);
        }
    }
}
