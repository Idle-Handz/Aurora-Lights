using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class ProgressionStateNormalizationTests : IAsyncLifetime
{
    private const string DruidClassId = "ID_WOTC_PHB24_CLASS_DRUID";

    private readonly ITestOutputHelper _output;

    public ProgressionStateNormalizationTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DuplicateGrantedFeature_NormalizationRemovesDuplicateSelectionRows()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var druid = DataManager.Current.ElementsCollection.GetElement(DruidClassId);
        if (druid is null)
        {
            _output.WriteLine("[SKIP] 2024 Druid content is not available.");
            return;
        }

        var manager = CharacterManager.Current;
        await manager.New(initializeFirstLevel: true);
        manager.RegisterElement(druid);
        for (int level = 2; level <= 5; level++)
            manager.LevelUpMain();
        manager.ReprocessCharacter();

        var druidManager = manager.ClassProgressionManagers.FirstOrDefault(m =>
            m.IsMainClass &&
            m.ClassElement?.Id.Equals(DruidClassId, StringComparison.OrdinalIgnoreCase) == true);
        if (druidManager is null)
        {
            _output.WriteLine("[SKIP] Druid class progression manager was not created.");
            return;
        }

        SelectRule? subclassRule = manager.SelectionRules.FirstOrDefault(rule =>
            rule.ElementHeader?.Type == "Class Feature" &&
            rule.Attributes.Name.Equals("Druid Subclass", StringComparison.OrdinalIgnoreCase));
        if (subclassRule is null)
        {
            _output.WriteLine("[SKIP] Druid Subclass selection rule is not available.");
            return;
        }

        ElementBase? owner = FindElement(druidManager.Elements, subclassRule.ElementHeader.Id);
        ElementBase? parent = FindParent(druidManager.Elements, subclassRule.ElementHeader.Id);
        if (owner is null || parent is null)
        {
            _output.WriteLine("[SKIP] Could not locate the Druid Subclass feature parent.");
            return;
        }

        ElementBase? duplicate = DataManager.Current.ElementsCollection.GetFresh(owner.Id);
        if (duplicate is null)
        {
            _output.WriteLine("[SKIP] Could not create a fresh Druid Subclass feature copy.");
            return;
        }

        if (owner.Aquisition.WasGranted && owner.Aquisition.GrantRule is not null)
            duplicate.Aquisition.GrantedBy(owner.Aquisition.GrantRule);

        parent.RuleElements.Add(duplicate);
        druidManager.ProcessExistingElements();

        CountMatchingSelectionRows(manager, subclassRule).Should().BeGreaterThan(1);
        manager.GetElements().Count(element => element.Id == owner.Id).Should().BeGreaterThan(1);

        int removed = manager.NormalizeDuplicateProgressionState();

        removed.Should().BeGreaterThan(0);
        CountMatchingSelectionRows(manager, subclassRule).Should().Be(1);
        manager.GetElements().Count(element => element.Id == owner.Id).Should().Be(1);
    }

    private static int CountMatchingSelectionRows(CharacterManager manager, SelectRule rule) =>
        manager.SelectionRules.Count(candidate =>
            candidate.ElementHeader?.Id == rule.ElementHeader.Id &&
            candidate.Attributes.Name == rule.Attributes.Name &&
            candidate.Attributes.Type == rule.Attributes.Type);

    private static ElementBase? FindElement(IEnumerable<ElementBase> roots, string id)
    {
        foreach (ElementBase root in roots)
        {
            if (root.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                return root;

            ElementBase? child = FindElement(root.RuleElements, id);
            if (child is not null)
                return child;
        }

        return null;
    }

    private static ElementBase? FindParent(IEnumerable<ElementBase> roots, string childId)
    {
        foreach (ElementBase root in roots)
        {
            if (root.RuleElements.Any(child => child.Id.Equals(childId, StringComparison.OrdinalIgnoreCase)))
                return root;

            ElementBase? parent = FindParent(root.RuleElements, childId);
            if (parent is not null)
                return parent;
        }

        return null;
    }
}
