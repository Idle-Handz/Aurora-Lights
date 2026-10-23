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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RepeatableAbilityIncreasesStillStackAndRemoveIndividually(bool saveToFile) =>
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
            byte[] original = File.ReadAllBytes(file.FilePath);
            (await BuildService.AddCustomFeatureAsync(tab, proxy.Id, saveToFile)).Should().BeNull();
            EquipmentService.SearchCustomFeatures("Additional Ability Score Improvement", "",
                    await BuildService.GetCustomFeatureOwnedIdsAsync(tab))
                .Should().Contain(r => r.Id == proxy.Id);
            (await BuildService.AddCustomFeatureAsync(tab, proxy.Id, saveToFile)).Should().BeNull();
            manager.Character.Abilities.Dexterity.AdditionalScore.Should().Be(before + 2);
            BuildService.GetCustomFeatures(tab).Select(feature => feature.Id).Should().Equal(asi.Id, asi.Id);
            (await BuildService.RemoveCustomFeatureAsync(tab, asi.Id, saveToFile)).Should().BeNull();
            manager.Character.Abilities.Dexterity.AdditionalScore.Should().Be(before + 1);
            if (!saveToFile)
            {
                File.ReadAllBytes(file.FilePath).Should().Equal(original);
                (await BuildService.SaveTabAsync(tab)).Should().BeNull();
            }
            file.LoadCustomFeatures().Should().Equal(asi.Id);
        });

    [Fact]
    public Task SidekicksKeepIndependentEditsAcrossFullSavesAndRemoval() =>
        WithCharacter(async (manager, file, tab) =>
        {
            EquipmentService.GetCustomFeatureCategories().Should().Contain("Sidekicks");
            var templates = EquipmentService.SearchCustomFeatures("Sidekicks", "");
            templates.Should().HaveCount(4);
            var warrior = templates.Single(t => t.Name == "Sidekick - Warrior");
            var first = SidekickService.Create(warrior.Id);
            var second = SidekickService.Create(warrior.Id);
            first.Set("name", "Rook & <friend>");
            first.Set("hp", "22 (4d8 + 4)");
            first.Set("features", "A custom feature.\nSecond line <stays plain text>.");
            second.Set("name", "Robin");
            second.Set("hp", "17");
            var registered = manager.GetElements().Select(e => e.Id).ToArray();

            (await SidekickService.SaveAsync(tab, first, isNew: true)).Succeeded.Should().BeTrue();
            (await SidekickService.SaveAsync(tab, second, isNew: true)).Succeeded.Should().BeTrue();
            var draft = first.Copy();
            draft.Set("hp", "99");
            first.Get("hp").Should().Be("22 (4d8 + 4)", "editing a draft does not mutate its original");
            SidekickService.Load(file).First().Get("hp").Should().Be("22 (4d8 + 4)", "an unsaved draft does not touch disk");
            (await SidekickService.SaveAsync(tab, draft)).Succeeded.Should().BeTrue();
            tab.IsDirty = true;
            var anotherDraft = draft.Copy();
            anotherDraft.Set("notes", "Keep other pending character edits.");
            (await SidekickService.SaveAsync(tab, anotherDraft)).Succeeded.Should().BeTrue();
            tab.IsDirty.Should().BeTrue();

            System.Text.Encoding.UTF8.GetString(file.SerializeCharacter(manager.Character))
                .Should().Contain(first.Id).And.Contain(second.Id, "tab snapshots must retain newly saved sidekicks");

            file.Save(manager.Character).Should().BeTrue();
            var reopened = new CharacterFile(file.FilePath);
            var saved = SidekickService.Load(reopened);
            saved.Should().HaveCount(2);
            saved[0].Name.Should().Be(first.Name);
            saved[0].Get("features").Should().Be(first.Get("features"));
            saved[0].Get("hp").Should().Be("99");
            saved[1].Get("hp").Should().Be("17");
            saved.Select(s => s.Id).Should().OnlyHaveUniqueItems();
            BuildService.GetCustomFeatures(tab).Should().Contain(f => f.Id == first.Id && f.Type == "Sidekick");
            manager.GetElements().Select(e => e.Id).Should().Equal(registered, "sidekick features must not alter the player character");
            file.LoadCustomFeatures().Should().BeEmpty("sidekick instances are not player feature grants");

            (await BuildService.RemoveCustomFeatureAsync(tab, first.Id)).Should().BeNull();
            file.Save(manager.Character).Should().BeTrue();
            SidekickService.Load(file).Should().ContainSingle().Which.Id.Should().Be(second.Id);
            (await SidekickService.SaveAsync(tab, draft)).Succeeded.Should().BeFalse("a stale editor cannot resurrect a removed sidekick");
        });

    [Fact]
    public Task SidekickContentSeedsEditableFieldsWithoutChangingTheTemplate() =>
        WithCharacter(async (manager, file, tab) =>
        {
            var template = Creature("EXPERT");
            template.Supports.Add("Sidekick");
            template.ElementSetters.Add(new("sidekick role", "Expert"));
            template.ElementSetters.Add(new("saves", "Dex +4"));
            template.ElementSetters.Add(new("actions", "ID_EXTRAS_HELP"));
            template.Actions.Add("ID_EXTRAS_HELP");
            Add(template);
            Add(Parse("""
                <element name="Helpful" type="Companion Action" source="Test" id="ID_EXTRAS_HELP">
                  <description><p>Help an ally.</p></description>
                </element>
                """));
            EquipmentService.SearchCustomFeatures("Sidekicks", "EXPERT").Should().Contain(e => e.Id == template.Id);
            EquipmentService.SearchCustomFeatures("Companions", "EXPERT").Should().NotContain(e => e.Id == template.Id);
            var profile = SidekickService.Create(template.Id);
            profile.Get("role").Should().Be("Expert");
            profile.Choices!.Base["saves"].Should().Be("Dex +4", "the original stat block remains the calculation basis");
            profile.Get("saves").Should().Contain("Dexterity +4");
            profile.Get("actions").Should().Contain("Helpful").And.Contain("Help an ally.");
            profile.Set("strength", "18");
            (await SidekickService.SaveAsync(tab, profile, isNew: true)).Succeeded.Should().BeTrue();
            template.Strength.Should().Be(10);
            template.ElementSetters.GetSetter("strength").Value.Should().Be("10");
            // The saved copy is self-contained even if its content source is later removed.
            DataManager.Current.ElementsCollection.Remove(template);
            SidekickService.Load(file).Single().Get("strength").Should().Be("18");
        });

    [Fact]
    public Task SidekickProgressionAndOverridesSurviveFullSaveAndLegacyProfilesUpgradeSafely() =>
        WithCharacter(async (manager, file, tab) =>
        {
            var profile = SidekickService.Create("sidekick-template:spellcaster");
            profile.FollowOwnerLevel = false;
            profile.Set("level", "5");
            profile.Set("spellcasting-role", "Healer");
            profile.Set("wisdom", "16");
            profile.Set("spell-save-dc", "19");
            profile.Set("proficiency", "4");
            profile.Set("features", "Custom <healing> & support");
            (await SidekickService.SaveAsync(tab, profile, isNew: true)).Succeeded.Should().BeTrue();
            file.Save(manager.Character).Should().BeTrue();
            var loaded = SidekickService.Load(new CharacterFile(file.FilePath)).Single();
            loaded.ProgressionClass.Should().Be("Spellcaster");
            loaded.Get("spellcasting-ability").Should().Be("Wisdom");
            loaded.Get("spell-save-dc").Should().Be("19");
            loaded.Get("spell-attack").Should().Be("+7");
            loaded.Set("level", "9");
            (await SidekickService.SaveAsync(tab, loaded)).Succeeded.Should().BeTrue();
            loaded = SidekickService.Load(file).Single();
            loaded.Get("spell-save-dc").Should().Be("19", "saved overrides remain customized on a later level-up");
            loaded.Get("spell-attack").Should().Be("+7");
            loaded.Get("spell-slots").Should().Be("1st: 4; 2nd: 3; 3rd: 2");
            loaded.Get("features").Should().Be("Custom <healing> & support");
            SidekickProgression.UseLevelDefault(loaded, "spell-save-dc");
            loaded.Get("spell-save-dc").Should().Be("15");
            loaded.Set("level", "13");
            SidekickProgression.Refresh(loaded);
            loaded.Get("proficiency").Should().Be("4", "an override matching the level 9 default must survive reopening and later levels");

            // Simulate an older manually maintained sidekick, without progression metadata or generated fields.
            var document = new XmlDocument();
            document.Load(file.FilePath);
            var entry = (XmlElement)document.SelectSingleNode("/*/sidekicks/sidekick")!;
            entry.RemoveAttribute("progression");
            entry.RemoveChild(entry["level-defaults"]!);
            foreach (var key in loaded.LevelDefaults.Keys)
                if (entry.SelectSingleNode($"field[@name='{key}']") is { } field) entry.RemoveChild(field);
            document.Save(file.FilePath);
            var legacy = SidekickService.Load(new CharacterFile(file.FilePath)).Single();
            legacy.ProgressionClass.Should().Be("Spellcaster");
            legacy.Get("class-features").Should().Contain("Potent Cantrips");
            legacy.Get("spell-slots").Should().Be("1st: 4; 2nd: 3; 3rd: 2");
            legacy.Get("features").Should().Be("Custom <healing> & support");
        });

    [Fact]
    public Task ExistingSidekicksMatchOnOpeningWithoutRerollingOrRewritingRepeatedLoads() =>
        WithCharacter(async (manager, file, tab) =>
        {
            var profile = SidekickService.Create("sidekick-template:warrior");
            profile.FollowOwnerLevel = false;
            SidekickChoices.Change(profile, new("base:hp", "12"));
            SidekickChoices.Change(profile, new("base:hit-dice", "2d8"));
            SidekickChoices.Change(profile, new("hp:method", "Roll"));
            profile.Set("level", "4"); SidekickProgression.Refresh(profile);
            var rolls = profile.Choices!.HitPoints!.Gains.Values.Select(g => g.Roll).ToArray();
            (await SidekickService.SaveAsync(tab, profile, isNew: true)).Succeeded.Should().BeTrue();
            var document = new XmlDocument(); document.Load(file.FilePath);
            ((XmlElement)document.SelectSingleNode("/*/sidekicks/sidekick")!).RemoveAttribute("follow-owner-level");
            document.Save(file.FilePath); file.InitializeDisplayPropertiesFromFilePath();
            (await SidekickService.SynchronizeOwnerLevelsAsync(tab)).Succeeded.Should().BeTrue();
            var saved = SidekickService.Load(file).Single();
            saved.FollowOwnerLevel.Should().BeTrue(); saved.Get("level").Should().Be(manager.Character.Level.ToString());
            saved.Choices!.HitPoints!.Gains.Values.Select(g => g.Roll).Should().Equal(rolls);
            byte[] first = File.ReadAllBytes(file.FilePath);
            (await SidekickService.SynchronizeOwnerLevelsAsync(tab)).Succeeded.Should().BeTrue();
            File.ReadAllBytes(file.FilePath).Should().Equal(first, "a repeat page refresh must not write or reroll");
            saved.Revision.Should().Be(profile.Revision + 1);
        });

    [Fact]
    public Task SidekickValidationAndExternalChangesLeaveTheFileUntouched() =>
        WithCharacter(async (manager, file, tab) =>
        {
            var profile = SidekickService.Create("sidekick-template:custom");
            var before = File.ReadAllBytes(file.FilePath);
            profile.Set("level", "21");
            (await SidekickService.SaveAsync(tab, profile, isNew: true)).Succeeded.Should().BeFalse();
            File.ReadAllBytes(file.FilePath).Should().Equal(before);
            profile.Set("level", "3");
            File.AppendAllText(file.FilePath, " ");
            var externallyChanged = File.ReadAllBytes(file.FilePath);
            (await SidekickService.SaveAsync(tab, profile, isNew: true)).IsExternalChange.Should().BeTrue();
            File.ReadAllBytes(file.FilePath).Should().Equal(externallyChanged);
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
