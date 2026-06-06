using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class AdvancementTimelineQueryTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public AdvancementTimelineQueryTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FighterTimeline_GroupsFeaturesAtGrantedClassLevels_WithoutMutation()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var fighter = DataManager.Current.ElementsCollection.GetElement("ID_PHB_CLASS_FIGHTER");
        if (fighter is null)
        {
            _output.WriteLine("[SKIP] PHB Fighter element not found.");
            return;
        }

        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var manager = CharacterManager.Current;
        await manager.New(initializeFirstLevel: true);
        manager.RegisterElement(fighter);

        while (manager.Character.Level < 5 && manager.Status.CanLevelUp)
            manager.LevelUpMain();

        var progression = manager.ClassProgressionManagers.Single(m => m.IsMainClass);
        int elementCountBefore = progression.GetElements().Count;
        int levelCountBefore = progression.LevelElements.Count;

        var timeline = AdvancementTimelineQuery.Build(manager);

        timeline.Should().ContainSingle();
        timeline[0].ClassName.Should().Be("Fighter");
        timeline[0].Levels.Select(level => level.Level).Should().Equal(1, 2, 3, 4, 5);
        timeline[0].Levels.Single(level => level.Level == 2).Features
            .Should().Contain(feature => feature.Name == "Action Surge");
        timeline[0].Levels.Single(level => level.Level == 5).Features
            .Should().Contain(feature => feature.Name == "Extra Attack");

        progression.GetElements().Count.Should().Be(elementCountBefore);
        progression.LevelElements.Count.Should().Be(levelCountBefore);
    }
}
