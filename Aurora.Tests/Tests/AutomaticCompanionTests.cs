using Aurora.App.Services;
using Aurora.Content;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class AutomaticCompanionTests
{
    [Fact]
    public async Task UnlocksDefaultsKeepsBeastChoicesAndPreservesDrakeAcrossLevelChangesAndReload()
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
            File.WriteAllText(Path.Combine(custom, "automatic-companions.xml"), Content);
            await ContentImport.ImportAsync(custom, Path.Combine(custom, ContentDatabaseService.DatabaseFileName));
            var service = new CharacterService();
            await service.PreloadAsync();
            await manager.New(true);
            manager.Character.Name = "Default companion regression";
            var handler = SelectionRuleExpanderContext.Current;
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), "ID_AUTO_CLASS");
            var file = new CharacterFile(Path.Combine(root, "automatic.dnd5e"));
            file.Save(manager.Character).Should().BeTrue();
            var tab = new CharacterTab(file) { Character = manager.Character };
            CharacterContext.ClaimAfterLoad(tab);
            BuildService.ResnapTab(tab);
            tab.Snapshot!.Companions.Should().BeEmpty();

            (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();
            tab.Snapshot!.Companions.Should().BeEmpty("the companion features unlock at level 3");
            (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();
            tab.Snapshot!.Companions.Select(c => c.TemplateId).Should().BeEquivalentTo("ID_AUTO_DEFENDER", "ID_AUTO_DRAKE_SMALL");
            var beastRule = manager.SelectionRules.Single(r => r.Attributes.Name == "Beast choice");
            handler.GetRegisteredElement(beastRule).Should().BeNull("even a single available beast needs an explicit choice without a default");
            foreach (string label in new[] { "Optional pet", "Missing pet", "Wrong supports", "Unmet creature", "Other choice" })
                handler.GetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Name == label)).Should().BeNull(label);

            var drake = manager.Character.Companions.Single(c => c.Element.Id == "ID_AUTO_DRAKE_SMALL");
            string drakeId = drake.InstanceId;
            drake.CompanionName.Content = "Ember";
            drake.Portrait.Content = "ember.png";
            var profile = SidekickService.LoadAll(tab).Single(p => p.Id == drakeId);
            profile.Set("name", "Ember");
            profile.Set("hp", "40");
            profile.Set("notes", "Keeps these notes while growing.");
            profile.Set("custom-additions", "Likes collecting coins.");
            profile.Set("portrait", "data:image/png;base64," + CreaturePortraitTests.Png);
            profile.Overrides.Add("portrait");
            (await SidekickService.SaveAsync(tab, profile)).Succeeded.Should().BeTrue();
            (await SidekickService.MutateAsync(tab, drakeId, p =>
            {
                CreatureCombat.Damage(p, 6);
                CreatureCombat.SetCondition(p, "Poisoned", true);
            })).Succeeded.Should().BeTrue();

            var sidekick = SidekickService.Create("sidekick-template:expert");
            sidekick.FollowOwnerLevel = false;
            sidekick.Set("portrait", "data:image/png;base64," + CreaturePortraitTests.Png);
            sidekick.Set("notes", "Separate sidekick notes.");
            SidekickChoices.Change(sidekick, new("base:hp", "13"));
            SidekickChoices.Change(sidekick, new("base:hit-dice", "2d8"));
            SidekickChoices.Change(sidekick, new("hp:method", "Roll"));
            sidekick.Set("level", "4");
            SidekickChoices.Change(sidekick, new("base:dexterity", "14"));
            SidekickChoices.Change(sidekick, new("asi:4:0", "Dexterity"));
            SidekickChoices.Change(sidekick, new("asi:4:1", "Dexterity"));
            SidekickChoices.Change(sidekick, new("skill:0", "Stealth"));
            SidekickChoices.Change(sidekick, new("expertise:3:0", "Stealth"));
            (await SidekickService.SaveAsync(tab, sidekick, isNew: true)).Succeeded.Should().BeTrue();

            var automatic = SidekickService.Create("sidekick-template:spellcaster");
            SidekickChoices.Change(automatic, new("base:hp", "13"));
            SidekickChoices.Change(automatic, new("base:hit-dice", "2d8"));
            SidekickChoices.Change(automatic, new("hp:method", "Roll"));
            automatic.Set("ac", "22"); automatic.Set("notes", "Follows this owner.");
            (await SidekickService.SaveAsync(tab, automatic, isNew: true)).Succeeded.Should().BeTrue();
            automatic = SidekickService.LoadAll(tab).Single(p => p.Id == automatic.Id);
            automatic.Get("level").Should().Be("3");
            var staleEditor = automatic.Copy();
            var recordedRolls = automatic.Choices!.HitPoints!.Gains.ToDictionary(g => g.Key, g => g.Value.Roll);

            // A deliberate alternative survives subsequent normalization and reload.
            var defenderRule = manager.SelectionRules.Single(r => r.Attributes.Name == "Steel Defender");
            (await BuildService.ApplySelectionAndSaveAsync(defenderRule, "ID_AUTO_ALTERNATE", 1, tab, file)).Should().BeEmpty();
            for (int i = 0; i < 3; i++) BuildService.NormalizeSelectionState();
            manager.Character.Companions.Should().HaveCount(2);
            manager.Character.Companions.Should().Contain(c => c.Element.Id == "ID_AUTO_ALTERNATE");

            while (manager.Character.Level < 7) (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();
            AssertDrake("ID_AUTO_DRAKE_MEDIUM");
            (await SidekickService.SaveAsync(tab, staleEditor)).Succeeded.Should().BeFalse("owner progression invalidates an older editor");
            while (manager.Character.Level < 15) (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();
            AssertDrake("ID_AUTO_DRAKE_LARGE");
            (await BuildService.LevelDownAsync(tab)).Should().BeNull();
            AssertDrake("ID_AUTO_DRAKE_MEDIUM");

            await CharacterContext.InvalidateAsync();
            (await service.LoadCharacterAsync(file)).Success.Should().BeTrue();
            tab.Character = manager.Character;
            CharacterContext.ClaimAfterLoad(tab);
            BuildService.ResnapTab(tab);
            AssertDrake("ID_AUTO_DRAKE_MEDIUM");
            manager.Character.Companions.Should().Contain(c => c.Element.Id == "ID_AUTO_ALTERNATE");

            // Older characters with the feature but an unfilled default gain it on load.
            defenderRule = manager.SelectionRules.Single(r => r.Attributes.Name == "Steel Defender");
            manager.UnregisterElement((Builder.Data.ElementBase)handler.GetRegisteredElement(defenderRule));
            handler.ClearRegisteredElement(defenderRule);
            file.Save(manager.Character).Should().BeTrue();
            await CharacterContext.InvalidateAsync();
            (await service.LoadCharacterAsync(file)).Success.Should().BeTrue();
            tab.Character = manager.Character;
            CharacterContext.ClaimAfterLoad(tab);
            BuildService.ResnapTab(tab);
            manager.Character.Companions.Should().Contain(c => c.Element.Id == "ID_AUTO_DEFENDER");
            manager.Character.Companions.Should().HaveCount(2);
            handler.GetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Name == "Beast choice")).Should().BeNull();

            while (manager.Character.Level > 2) (await BuildService.LevelDownAsync(tab)).Should().BeNull();
            tab.Snapshot!.Companions.Should().BeEmpty("losing the features removes their companions");
            (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();
            manager.Character.Companions.Should().HaveCount(2);
            manager.Character.Companions.Single(c => c.Element.Id == "ID_AUTO_DRAKE_SMALL").InstanceId
                .Should().NotBe(drakeId, "losing the feature discards its retired companion identity");

            void AssertDrake(string templateId)
            {
                var current = manager.Character.Companions.Single(c => c.Element.Id == templateId);
                current.InstanceId.Should().Be(drakeId);
                current.CompanionName.Content.Should().Be("Ember");
                current.Portrait.Content.Should().Be("ember.png");
                manager.Character.Companions.Should().HaveCount(2);
                tab.Snapshot!.Companions.Should().Contain(c => c.TemplateId == templateId && c.InstanceId == drakeId);
                var saved = SidekickService.LoadAll(tab).Single(p => p.Id == drakeId);
                saved.TemplateId.Should().Be(templateId);
                saved.Name.Should().Be("Ember");
                saved.Get("notes").Should().Be("Keeps these notes while growing.");
                saved.Get("custom-additions").Should().Be("Likes collecting coins.");
                saved.Get("portrait").Should().Be("data:image/png;base64," + CreaturePortraitTests.Png);
                var savedSidekick = SidekickService.LoadAll(tab).Single(p => p.Id == sidekick.Id);
                savedSidekick.Get("notes").Should().Be("Separate sidekick notes.");
                savedSidekick.Get("portrait").Should().Be(sidekick.Get("portrait"));
                savedSidekick.Get("dexterity").Should().Be("16");
                savedSidekick.Get("skills").Should().Contain("Stealth +7");
                savedSidekick.Choices!.Classes["Expert"].Improvements[4].Should().Equal("Dexterity", "Dexterity");
                savedSidekick.Get("hp").Should().Be(sidekick.Get("hp"));
                savedSidekick.Get("hit-dice").Should().Be("5d8");
                savedSidekick.Choices.HitPoints!.Gains.Should().HaveCount(3);
                savedSidekick.Choices.HitPoints.Gains.OrderBy(g => g.Key).Select(g => g.Value.Roll)
                    .Should().Equal(sidekick.Choices!.HitPoints!.Gains.OrderBy(g => g.Key).Select(g => g.Value.Roll));
                int level = manager.Character.Level;
                int proficiency = 2 + (level - 1) / 4;
                savedSidekick.FollowOwnerLevel.Should().BeFalse();
                savedSidekick.Get("level").Should().Be("4");
                var following = SidekickService.LoadAll(tab).Single(p => p.Id == automatic.Id);
                following.FollowOwnerLevel.Should().BeTrue();
                following.Get("level").Should().Be(level.ToString());
                following.Get("proficiency").Should().Be(proficiency.ToString());
                var pendingChoices = CreatureManagementService.Model(following).PendingChoices;
                pendingChoices.Should().Contain(c => c.Key == "asi:4:0").And.Contain(c => c.Key == "spell:0");
                pendingChoices.Where(c => c.Key.StartsWith("asi:")).Should().OnlyContain(c => int.Parse(c.Key.Split(':')[1]) <= level);
                following.Get("ac").Should().Be("22"); following.Get("notes").Should().Be("Follows this owner.");
                following.Get("hit-dice").Should().Be((level + 1) + "d8");
                following.Get("hp").Should().Be((13 + following.Choices!.HitPoints!.Gains.Where(g => g.Key <= level).Sum(g => g.Value.Roll!.Value)).ToString());
                foreach (var (at, roll) in recordedRolls) following.Choices!.HitPoints!.Gains[at].Roll.Should().Be(roll);
                foreach (var (at, gain) in following.Choices!.HitPoints!.Gains) recordedRolls[at] = gain.Roll;
                var repeated = SidekickService.LoadAll(tab).Single(p => p.Id == automatic.Id);
                repeated.Get("hp").Should().Be(following.Get("hp"));
                saved.Get("level").Should().Be(level.ToString());
                saved.Get("proficiency").Should().Be(proficiency.ToString());
                saved.Get("ac").Should().Be((14 + proficiency).ToString());
                saved.Get("hp").Should().Be("40", "the custom maximum remains fixed");
                saved.LevelDefaults["hp"].Should().Be((5 + 5 * level).ToString());
                saved.Get("hit-dice").Should().Be(level + "d10");
                saved.Get("speed").Should().Contain("fly 40 ft.");
                saved.Get("actions").Should().Contain("+" + (3 + proficiency) + " to hit");
                saved.Get("owner-features").Should().Contain(level >= 15 ? "Greater bond" : "Mature bond");
                saved.Get("owner-features").Should().NotContain(level >= 15 ? "Mature bond" : "Greater bond");
                SidekickService.LoadAll(tab).Single(p => p.IsCompanion && p.Id != drakeId).Get("owner-features").Should().NotContain("bond");
                CreatureCombat.Resources(saved).Should().ContainSingle(r => r.Name == "Repair" && r.Max == 3);
                CreatureCombat.CurrentHp(saved).Should().Be(34);
                saved.Combat.Conditions.Should().Equal("Poisoned");
            }
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
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewlyDefaultedCompanionsCanBeEditedImmediatelyAndKeepTheirIdentityOnReload(bool legacyCollection)
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
            File.WriteAllText(Path.Combine(custom, "automatic-companions.xml"), Content);
            await ContentImport.ImportAsync(custom, Path.Combine(custom, ContentDatabaseService.DatabaseFileName));
            var service = new CharacterService();
            await service.PreloadAsync();
            await manager.New(true);
            manager.Character.Name = "Default companion regression";
            var handler = SelectionRuleExpanderContext.Current;
            handler.SetRegisteredElement(manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), "ID_AUTO_CLASS");
            var file = new CharacterFile(Path.Combine(root, "automatic.dnd5e"));
            file.Save(manager.Character).Should().BeTrue();
            var tab = new CharacterTab(file) { Character = manager.Character };
            CharacterContext.ClaimAfterLoad(tab);
            BuildService.ResnapTab(tab);
            (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();
            (await BuildService.LevelUpMainAsync(tab)).Error.Should().BeNull();
            var drake = manager.Character.Companions.Single(c => c.Element.Id == "ID_AUTO_DRAKE_SMALL");
            string drakeId = drake.InstanceId;
            drake.CompanionName.Content = "Ember";
            drake.Portrait.Content = "ember.png";

            // Save an older character whose existing companion is recorded but whose
            // predefined defender has not yet been selected by the current loader.
            var rule = manager.SelectionRules.Single(r => r.Attributes.Name == "Steel Defender");
            manager.UnregisterElement((Builder.Data.ElementBase)handler.GetRegisteredElement(rule));
            handler.ClearRegisteredElement(rule);
            file.Save(manager.Character).Should().BeTrue();
            var saved = System.Xml.Linq.XDocument.Load(file.FilePath);
            var collection = saved.Root!.Element("build")!.Element("companions")!;
            collection.Elements("companion").Should().ContainSingle();
            string originalDrake = collection.Elements("companion").Single().ToString();
            if (legacyCollection)
            {
                collection.Remove();
                saved.Save(file.FilePath);
                file.InitializeDisplayPropertiesFromFilePath();
            }

            await Reload();
            manager.Character.Companions.Should().HaveCount(2);
            var defender = manager.Character.Companions.Single(c => c.Element.Id == "ID_AUTO_DEFENDER");
            string defenderId = defender.InstanceId;
            var profile = SidekickService.LoadAll(tab).Single(p => p.Id == drakeId);
            profile.Set("notes", "An existing companion can still be edited.");
            var result = await SidekickService.SaveAsync(tab, profile);
            result.Succeeded.Should().BeTrue(result.Message);
            profile = SidekickService.LoadAll(tab).Single(p => p.Id == defenderId);
            profile.Set("name", "Cog"); profile.Set("hp", "42");
            profile.Set("notes", "Keep this newly added defender.");
            profile.Set("portrait", "data:image/png;base64," + CreaturePortraitTests.Png);
            result = await SidekickService.SaveAsync(tab, profile);
            result.Succeeded.Should().BeTrue(result.Message);
            for (int i = 0; i < 2; i++)
            {
                result = await SidekickService.MutateAsync(tab, defenderId, p =>
                {
                    CreatureCombat.Damage(p, 3);
                    CreatureCombat.SetCondition(p, "Poisoned", true);
                });
                result.Succeeded.Should().BeTrue(result.Message);
            }
            saved = System.Xml.Linq.XDocument.Load(file.FilePath);
            var records = saved.Root!.Element("build")!.Element("companions")!.Elements("companion").ToList();
            records.Should().HaveCount(2, "repeated companion edits must not duplicate identities");
            records.Select(n => (string?)n.Attribute("instance")).Should().BeEquivalentTo(drakeId, defenderId);
            if (!legacyCollection)
                records.Single(n => (string?)n.Attribute("instance") == drakeId).ToString().Should().Be(originalDrake);

            // No full character save occurs between the first companion edit and reopen.
            await Reload();
            manager.Character.Companions.Should().HaveCount(2);
            manager.Character.Companions.Single(c => c.Element.Id == "ID_AUTO_DEFENDER").InstanceId.Should().Be(defenderId);
            drake = manager.Character.Companions.Single(c => c.InstanceId == drakeId);
            drake.CompanionName.Content.Should().Be("Ember"); drake.Portrait.Content.Should().Be("ember.png");
            SidekickService.LoadAll(tab).Single(p => p.Id == drakeId).Get("notes").Should().Be("An existing companion can still be edited.");
            profile = SidekickService.LoadAll(tab).Single(p => p.Id == defenderId);
            profile.Name.Should().Be("Cog"); profile.Get("hp").Should().Be("42");
            profile.Get("notes").Should().Be("Keep this newly added defender.");
            profile.Get("portrait").Should().Be("data:image/png;base64," + CreaturePortraitTests.Png);
            CreatureCombat.CurrentHp(profile).Should().Be(36);
            profile.Combat.Conditions.Should().Equal("Poisoned");

            async Task Reload()
            {
                await CharacterContext.InvalidateAsync();
                (await service.LoadCharacterAsync(file)).Success.Should().BeTrue();
                tab.Character = manager.Character;
                CharacterContext.ClaimAfterLoad(tab);
                BuildService.ResnapTab(tab);
            }
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
            Directory.Delete(root, true);
        }
    }

    private static string Creature(string id, string name, string supports, string requirements = "")
    {
        bool drake = id.StartsWith("DRAKE");
        string hp = drake ? "5 + five times your keeper level (hit dice [d10s] equal to your keeper level)" : "20";
        string extraSetters = drake ? """
            <set name="proficiency">0</set><set name="actions">ID_AUTO_BITE,ID_AUTO_REPAIR</set>
            <set name="saves">Dex +1 plus PB, Wis +2 plus PB</set>
            """ : "";
        string stats = drake ? """
            <stat name="companion:ac" value="14" /><stat name="companion:ac" value="proficiency" />
            <stat name="companion:proficiency" value="proficiency" />
            <stat name="companion:hp:max" value="5" />
            <stat name="companion:hp:max" value="level:keeper" /><stat name="companion:hp:max" value="level:keeper" />
            <stat name="companion:hp:max" value="level:keeper" /><stat name="companion:hp:max" value="level:keeper" />
            <stat name="companion:hp:max" value="level:keeper" />
            <stat name="companion:speed" value="40" /><stat name="companion:speed:fly" value="40" level="7" />
            """ : """<stat name="companion:hp:max" value="20" /><stat name="companion:ac" value="15" />""";
        return $$"""
        <element name="{{name}}" type="Companion" source="Test" id="ID_AUTO_{{id}}">
          <supports>{{supports}}</supports>{{requirements}}
          <setters>
            <set name="strength">16</set><set name="dexterity">12</set><set name="constitution">14</set>
            <set name="intelligence">10</set><set name="wisdom">14</set><set name="charisma">10</set>
            <set name="size">Medium</set><set name="type">Construct</set><set name="alignment">Unaligned</set>
            <set name="challenge">0</set><set name="ac">15</set><set name="hp">{{hp}}</set><set name="speed">40 ft.</set>
            {{extraSetters}}
          </setters>
          <rules>{{stats}}</rules>
        </element>
        """;
    }

    private static string Content => """
        <elements>
          <element name="Keeper" type="Class" source="Test" id="ID_AUTO_CLASS">
            <setters><set name="hd">d8</set></setters>
            <rules>
              <grant type="Class Feature" id="ID_AUTO_FEATURE" level="3" />
              <grant type="Class Feature" id="ID_AUTO_MATURE" level="7" />
              <grant type="Class Feature" id="ID_AUTO_GREATER" level="15" />
            </rules>
          </element>
          <element name="Companion features" type="Class Feature" source="Test" id="ID_AUTO_FEATURE">
            <rules>
              <select type="Companion" name="Steel Defender" supports="Defender" default="ID_AUTO_DEFENDER" />
              <select type="Companion" name="Drake Companion" supports="Drake" default="ID_AUTO_DRAKE_SMALL" requirements="!ID_AUTO_MATURE" />
              <select type="Companion" name="Drake Companion" supports="Drake" default="ID_AUTO_DRAKE_MEDIUM" requirements="ID_AUTO_MATURE,!ID_AUTO_GREATER" />
              <select type="Companion" name="Drake Companion" supports="Drake" default="ID_AUTO_DRAKE_LARGE" requirements="ID_AUTO_GREATER" />
              <select type="Companion" name="Beast choice" supports="Beast" />
              <select type="Companion" name="Optional pet" supports="Beast" default="ID_AUTO_BEAST" optional="true" />
              <select type="Companion" name="Missing pet" default="ID_AUTO_MISSING" />
              <select type="Companion" name="Wrong supports" supports="Other" default="ID_AUTO_BEAST" />
              <select type="Companion" name="Unmet creature" default="ID_AUTO_UNAVAILABLE" />
              <select type="Language" name="Other choice" default="ID_AUTO_LANGUAGE" />
            </rules>
          </element>
          <element name="Mature" type="Class Feature" source="Test" id="ID_AUTO_MATURE">
            <description>Your drake gains a stronger bond.</description>
            <sheet><description>Mature bond with your drake.</description><description level="15">Greater bond with your drake.</description></sheet>
          </element>
          <element name="Bite" type="Companion Action" source="Test" id="ID_AUTO_BITE">
            <sheet><description>Melee Weapon Attack: +3 plus PB to hit. Hit: 1d6 plus PB damage.</description></sheet>
          </element>
          <element name="Repair" type="Companion Action" source="Test" id="ID_AUTO_REPAIR">
            <sheet usage="3/Day"><description>Restore 2d8 + PB hit points.</description></sheet>
          </element>
          <element name="Greater" type="Class Feature" source="Test" id="ID_AUTO_GREATER" />
          <element name="Language" type="Language" source="Test" id="ID_AUTO_LANGUAGE" />
        """
        + Creature("DEFENDER", "Steel Defender", "Defender")
        + Creature("ALTERNATE", "Custom Defender", "Defender")
        + Creature("DRAKE_SMALL", "Drake Companion", "Drake")
        + Creature("DRAKE_MEDIUM", "Drake Companion", "Drake")
        + Creature("DRAKE_LARGE", "Drake Companion", "Drake")
        + Creature("BEAST", "Beast", "Beast")
        + Creature("UNAVAILABLE", "Unavailable", "Other", "<requirements>ID_AUTO_NOT_OWNED</requirements>")
        + "</elements>";
}
