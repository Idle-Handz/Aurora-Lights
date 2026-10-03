using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class CharacterLoadRegressionTests
{
    [Fact]
    public void IsolatedSettingsKeepTheirStorageLocationThroughLoadReloadAndReset()
    {
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "settings.json");
        try
        {
            var settings = new AppSettingsStore(path) { PlayerName = "Isolated", DocumentsRootDirectory = root };
            settings.Save();
            var loaded = AppSettingsStore.Load(path);
            loaded.DocumentsRootDirectory.Should().Be(root);
            loaded.PlayerName.Should().Be("Isolated");
            loaded.PlayerName = "Unsaved";
            loaded.Reload();
            loaded.PlayerName.Should().Be("Isolated");
            loaded.Reset();
            AppSettingsStore.Load(path).PlayerName.Should().BeEmpty();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RepeatedAbilityIncreaseKeepsEarlierRacialIncreaseAndIndependentSelections()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        var manager = CharacterManager.Current;
        await manager.New(false);
        SetProgressionLevel(manager);
        var intelligence = Parse("ID_LOAD_INT", "Ability Score Improvement", "<rules><stat name='intelligence' value='2' /></rules>")
            .Construct<AbilityScoreImprovement>();
        var dexterity = Parse("ID_LOAD_DEX", "Ability Score Improvement", "<rules><stat name='dexterity' value='1' /></rules>")
            .Construct<AbilityScoreImprovement>();
        dexterity.AllowDuplicate = true;
        var owners = Enumerable.Range(1, 3).Select(i => Parse($"ID_LOAD_OWNER_{i}", "Feat Feature",
            "<rules><select name='Increase' type='Ability Score Improvement' /></rules>").Construct<FeatFeature>()).ToArray();
        var catalog = DataManager.Current.ElementsCollection;
        catalog.Add(intelligence);
        catalog.Add(dexterity);
        var registrations = new Dictionary<string, object>();
        try
        {
            foreach (var owner in owners) manager.RegisterElement(owner);
            SelectionRuleRegistrationService.SetRegisteredElement(registrations, owners[0].GetSelectRules().Single(), intelligence.Id);
            SelectionRuleRegistrationService.SetRegisteredElement(registrations, owners[1].GetSelectRules().Single(), dexterity.Id);
            SelectionRuleRegistrationService.SetRegisteredElement(registrations, owners[2].GetSelectRules().Single(), dexterity.Id);
            manager.ReprocessCharacter();
            manager.GetElements().Should().Contain(intelligence);
            var repeated = manager.GetElements().Where(e => e.Id == dexterity.Id).ToArray();
            repeated.Should().HaveCount(2).And.OnlyContain(e => e is AbilityScoreImprovement);
            repeated[0].Aquisition.Should().NotBeSameAs(repeated[1].Aquisition);
            repeated[0].Rules.Should().NotBeSameAs(repeated[1].Rules);
            repeated.Select(e => e.Aquisition.GetParentHeader().Id).Should().BeEquivalentTo(owners.Skip(1).Select(e => e.Id));
            manager.UnregisterElement(owners[2]);
            manager.GetElements().Should().Contain(intelligence);
            manager.GetElements().Count(e => e.Id == dexterity.Id).Should().Be(1);
        }
        finally
        {
            await manager.New(false);
            catalog.Remove(intelligence);
            catalog.Remove(dexterity);
        }
    }

    [Fact]
    public async Task MutuallyExclusiveGrantsSeeChangesImmediatelyAndConverge()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        var manager = CharacterManager.Current;
        await manager.New(false);
        SetProgressionLevel(manager);
        var catalog = DataManager.Current.ElementsCollection;
        var first = Parse("ID_LOAD_OLD", "Proficiency", "<requirements>!ID_LOAD_NEW</requirements>");
        var second = Parse("ID_LOAD_NEW", "Proficiency", "<requirements>!ID_LOAD_OLD</requirements>");
        var owner = Parse("ID_LOAD_GRANTS", "Feat Feature",
            "<rules><grant type='Proficiency' id='ID_LOAD_OLD' /><grant type='Proficiency' id='ID_LOAD_NEW' /></rules>").Construct<FeatFeature>();
        catalog.Add(first);
        catalog.Add(second);
        var policy = GrantPolicyContext.Current;
        GrantPolicyContext.Current = null;
        try
        {
            manager.GetElements(); // Warm the cache before attaching the root.
            manager.RegisterElement(owner);
            for (int i = 0; i < 4; i++)
            {
                manager.GetElements().Should().Contain(first).And.NotContain(second);
                manager.ReprocessCharacter();
            }
            manager.UnregisterElement(owner);
            manager.GetElements().Should().NotContain(first).And.NotContain(second);
        }
        finally
        {
            await manager.New(false);
            catalog.Remove(first);
            catalog.Remove(second);
            GrantPolicyContext.Current = policy;
        }
    }

    [Fact]
    public async Task SavedDefaultsAndRenamedRowsRequireAnUnambiguousEligibleActiveChoice()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        var manager = CharacterManager.Current;
        await manager.New(false);
        SetProgressionLevel(manager);
        var option = Parse("ID_LOAD_DEFAULT", "Language", "<supports>Recovery</supports>");
        var wrong = Parse("ID_LOAD_WRONG", "Language", "<supports>Other</supports>");
        var owner = Parse("ID_LOAD_RECOVERY", "Feat Feature",
            "<rules><select name='Current label' type='Language' supports='Recovery' default='ID_LOAD_DEFAULT'/></rules>").Construct<FeatFeature>();
        var catalog = DataManager.Current.ElementsCollection;
        catalog.Add(option);
        catalog.Add(wrong);
        try
        {
            manager.RegisterElement(owner);
            var rule = owner.GetSelectRules().Single();
            SavedSelectionRecovery.FindRenamedRule(owner, "Language", option.Id, 1).Should().BeSameAs(rule);
            SavedSelectionRecovery.FindRenamedRule(owner, "Language", wrong.Id, 1).Should().BeNull();
            SavedSelectionRecovery.FindRenamedRule(owner, "Language", option.Id, 2).Should().BeNull();
            SavedSelectionRecovery.RestoreSavedDefaults(new HashSet<string> { option.Id }, new HashSet<Builder.Data.Rules.SelectRule> { rule })
                .Should().Be(0, "an unresolved explicit saved choice must not be replaced with a default");
            SavedSelectionRecovery.RestoreSavedDefaults(new HashSet<string> { wrong.Id }).Should().Be(0);
            SavedSelectionRecovery.RestoreSavedDefaults(new HashSet<string> { option.Id }).Should().Be(1);
            SavedSelectionRecovery.RestoreSavedDefaults(new HashSet<string> { option.Id }).Should().Be(0);
            manager.GetElements().Should().Contain(option);
            manager.UnregisterElement(owner);
            SavedSelectionRecovery.IsEligible(rule, option.Id).Should().BeFalse();
        }
        finally
        {
            await manager.New(false);
            catalog.Remove(option);
            catalog.Remove(wrong);
        }
    }

    [Fact]
    public void NormalizationKeepsIndependentItemChoiceOwners()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var first = Parse("ID_LOAD_PROXY", "Item", "<rules><select name='Companion' type='Companion'/></rules>").Construct<Item>();
        var second = Parse("ID_LOAD_PROXY", "Item", "<rules><select name='Companion' type='Companion'/></rules>").Construct<Item>();
        second.GetSelectRules().Single().RenewIdentifier();
        var progression = new ProgressionManager();
        progression.Elements.Add(first);
        progression.Elements.Add(second);
        progression.NormalizeDuplicateProgressionState().Should().Be(0);
        progression.Elements.Should().HaveCount(2);
    }

    private static void SetProgressionLevel(CharacterManager manager)
    {
        var progression = (ProgressionManager)typeof(CharacterManager).GetField("_progressionManager",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(manager)!;
        progression.ProgressionLevel = 1;
    }

    private static ElementBase Parse(string id, string type, string body)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"<element id='{id}' name='{id}' type='{type}' source='Internal'>{body}</element>");
        return new ElementParser().ParseElement(doc.DocumentElement!);
    }
}
