using Aurora.Reflections.Services;
using Aurora.Tests.Helpers;
using Builder.Data.Elements;
using Builder.Data.Strings;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class NativeSpellSlotPoolTests : IAsyncLifetime
{
    public Task InitializeAsync() => ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(5, new[] { 4, 3 })]
    [InlineData(6, new[] { 4, 3, 2 })]
    public async Task ThirdCasterContributionsRoundDownAtTheirOwnClassLevel(int levels, int[] expected)
    {
        using var fixture = new SyntheticContentScope("""
            <elements>
              <element name="Fixture third caster" type="Multiclass" source="Test" id="ID_TEST_THIRD_CLASS">
                <setters><set name="hd">d8</set></setters>
                <multiclass id="ID_TEST_THIRD_MULTICLASS" />
                <spellcasting name="Fixture third caster" ability="Intelligence"><list>Wizard</list></spellcasting>
                <rules>
                  <grant type="Grants" id="ID_INTERNAL_GRANT_MULTICLASS" />
                  <grant type="Grants" id="ID_INTERNAL_GRANT_MULTICLASS_SPELLCASTING_SLOTS_THIRD" />
                  <stat name="fixture third caster:spellcasting:slots:1" value="2" level="3" />
                </rules>
              </element>
            </elements>
            """);
        await BuildCaster("Wizard", 3);
        AddClass("Fixture third caster", levels);
        CharacterManager.Current.Status.HasMulticlassSpellSlots.Should().BeTrue();
        CharacterSnapshot.From(CharacterManager.Current.Character).SpellSlotPools.Single().Levels
            .Select(l => l.TotalSlots).Should().Equal(expected);
    }

    [Theory]
    [InlineData("Cleric", 3, new[] { 4, 3, 3 })]
    [InlineData("Paladin", 4, new[] { 4, 3, 2 })]
    public async Task CombinedSlotsUseCasterProgressionWithoutExpandingClassSpellLists(
        string secondClass, int levels, int[] expected)
    {
        await BuildCaster("Wizard", 3);
        AddClass(secondClass, levels);
        var snapshot = CharacterSnapshot.From(CharacterManager.Current.Character);
        snapshot.SpellSlotPools.Should().ContainSingle().Which.Levels.Select(l => l.TotalSlots).Should().Equal(expected);
        snapshot.SpellcastingSections.Single(s => s.Name == "Wizard").SpellLevels
            .Select(l => l.Level).Should().NotBeEmpty().And.OnlyContain(l => l <= 2);
        snapshot.SpellcastingSections.Single(s => s.Name == secondClass).SpellLevels
            .Select(l => l.Level).Should().NotBeEmpty().And.OnlyContain(l => l <= (secondClass == "Paladin" ? 1 : 2));

        string filePath = Path.Combine(Path.GetTempPath(), $"aurora-slot-pools-{Guid.NewGuid():N}.dnd5e");
        try
        {
            var file = new CharacterFile(filePath);
            file.Save(CharacterManager.Current.Character);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            (await file.Load()).Success.Should().BeTrue();
            CharacterSnapshot.From(CharacterManager.Current.Character).SpellSlotPools.Single().Levels
                .Select(l => l.TotalSlots).Should().Equal(expected);
        }
        finally { File.Delete(filePath); }
    }

    [Fact]
    public async Task PactSlotsRemainSeparateFromTwoRegularCastersAndRecoverIndependently()
    {
        await BuildCaster("Wizard", 3);
        AddClass("Cleric", 3);
        AddClass("Warlock", 3);
        var manager = CharacterManager.Current;
        var pools = CharacterSnapshot.From(manager.Character).SpellSlotPools;
        pools.Single(p => !p.RecoversOnShortRest).Levels.Select(l => l.TotalSlots).Should().Equal(4, 3, 3);
        pools.Single(p => p.RecoversOnShortRest).Levels.Should().Equal(new SpellSlotCapacity(2, 2));
        var warlock = manager.GetSpellcastingInformations().Single(i => i.Name == "Warlock" && !i.IsExtension);
        SpellSlotPoolResolver.ForProfile(manager, warlock).Should().Equal([new SpellSlotCapacity(2, 2)],
            "the PDF must use the same Pact pool instead of the multiclass table");

        var state = new SessionState();
        state.EnsureSpellSlotPools(pools);
        state.ToggleSlot(SpellSlotPoolResolver.SpellcastingId, 2, 3, 3);
        state.ToggleSlot(SpellSlotPoolResolver.PactMagicId, 2, 2, 2);
        state.ShortRest();
        state.UsedSlots(SpellSlotPoolResolver.SpellcastingId, 2, 3).Should().Be(3);
        state.UsedSlots(SpellSlotPoolResolver.PactMagicId, 2, 2).Should().Be(0);
        state.LongRest(20);
        state.UsedSlots(SpellSlotPoolResolver.SpellcastingId, 2, 3).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyUsageMigratesOnceAndBothPoolsSurviveSidecarReload(bool pactOnly)
    {
        await BuildCaster(pactOnly ? "Warlock" : "Wizard", 3);
        if (!pactOnly) AddClass("Warlock", 3);
        var pools = CharacterSnapshot.From(CharacterManager.Current.Character).SpellSlotPools;
        var state = new SessionState { SpellSlotsUsed = new() { [2] = 1 } };
        state.EnsureSpellSlotPools(pools);
        state.UsedSlots(pactOnly ? SpellSlotPoolResolver.PactMagicId : SpellSlotPoolResolver.SpellcastingId, 2, 2)
            .Should().Be(1);
        state.EnsureSpellSlotPools(pools);
        state.PactSpellSlotsUsed[2] = 2;
        string filePath = Path.Combine(Path.GetTempPath(), $"aurora-slot-session-{Guid.NewGuid():N}.dnd5e");
        try
        {
            File.WriteAllText(filePath, "<character/>");
            SessionStore.SaveRequired(filePath, state);
            var loaded = SessionStore.Load(filePath, out bool corrupted);
            corrupted.Should().BeFalse();
            loaded.Should().BeEquivalentTo(state);
            loaded.ShortRest();
            loaded.PactSpellSlotsUsed.Should().BeEmpty();
            if (!pactOnly) loaded.SpellSlotsUsed[2].Should().Be(1);
        }
        finally { SessionStore.Delete(filePath); File.Delete(filePath); }
    }

    private static async Task BuildCaster(string name, int levels)
    {
        ContentFixture.SkipIfUnavailable();
        var manager = CharacterManager.Current;
        await manager.New(true);
        foreach (var ability in manager.Character.Abilities.GetCollection()) ability.BaseScore = 15;
        manager.RegisterElement(DataManager.Current.ElementsCollection.GetElement(InternalOptions.AllowMulticlassing));
        var element = DataManager.Current.ElementsCollection.Single(e => e.Type == "Class" && e.Name == name);
        SelectionRuleExpanderContext.Current!.SetRegisteredElement(
            manager.SelectionRules.Single(r => r.Attributes.Type == "Class"), element.Id);
        for (int level = 1; level < levels; level++) manager.LevelUpMain();
        manager.ReprocessCharacter();
    }

    private static void AddClass(string name, int levels)
    {
        var manager = CharacterManager.Current;
        manager.NewMulticlass();
        var element = DataManager.Current.ElementsCollection.Single(e => e.Type == "Multiclass" && e.Name == name);
        var rule = manager.SelectionRules.Where(r => r.Attributes.Type == "Multiclass")
            .OrderByDescending(r => r.Attributes.RequiredLevel).First();
        SelectionRuleExpanderContext.Current!.SetRegisteredElement(rule, element.Id);
        var multiclass = (Multiclass)manager.ClassProgressionManagers.Last().ClassElement;
        for (int level = 1; level < levels; level++) manager.LevelUpMulti(multiclass);
        manager.ReprocessCharacter();
    }
}
