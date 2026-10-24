using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Fixture-driven parity checks for representative legacy Aurora build flows.
/// These are not UI automation tests; they normalize the current builder state so
/// saved legacy .dnd5e baselines can be compared against the same snapshot shape later.
/// </summary>
public sealed class LegacyParityScenarioTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public LegacyParityScenarioTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public static IEnumerable<object[]> ScenarioFiles()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ParityScenarios");
        return Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).StartsWith("2024-", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new object[] { path });
    }

    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public async Task Scenario_ExposesExpectedChoicesAndSurvivesRoundTrip(string scenarioPath)
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var scenario = CharacterParityScenario.Load(scenarioPath);
        // Variant Human is no longer bundled. Keep its legacy selector coverage
        // through original synthetic imported mechanics, without restoring book prose.
        using var importedContent = scenario.SeedElementIds.Contains("ID_RACE_VARIANT_HUMAN_VARIANT")
            ? new SyntheticContentScope("""
                <elements>
                  <element name="Imported test human option" type="Race Variant" source="Tests" id="ID_RACE_VARIANT_HUMAN_VARIANT">
                    <supports>Human</supports>
                    <rules>
                      <select type="Racial Trait" name="Ability Score Increase (imported test option)" supports="Parity test ability choices" number="2" />
                      <select type="Feat" name="Imported test feat choice" />
                    </rules>
                  </element>
                  <element name="Test strength choice" type="Racial Trait" source="Tests" id="ID_RACE_VARIANT_HUMAN_VARIANT_STRENGTH">
                    <supports>Parity test ability choices</supports>
                    <rules><stat name="strength" value="1" /></rules>
                  </element>
                  <element name="Test dexterity choice" type="Racial Trait" source="Tests" id="ID_RACE_VARIANT_HUMAN_VARIANT_DEXTERITY">
                    <supports>Parity test ability choices</supports>
                    <rules><stat name="dexterity" value="1" /></rules>
                  </element>
                </elements>
                """)
            : null;
        if (!EnsureScenarioElementsAvailable(scenario)) return;

        var snapshot = await BuildScenarioAsync(scenario);

        snapshot.RegisteredElements.Select(e => e.Id)
            .Should().Contain(scenario.ExpectedRegisteredElementIds,
                because: $"{scenario.Name} should preserve its seed selections");

        if (scenario.ExpectedSpellcastingNames.Length > 0)
        {
            snapshot.Spellcasting.Select(s => s.Name)
                .Should().Contain(scenario.ExpectedSpellcastingNames,
                    because: $"{scenario.Name} should expose the expected spellcasting profiles");
        }

        foreach (var expectation in scenario.ExpectedSelectionRules)
            AssertRuleExpectation(scenario, snapshot, expectation);

        if (scenario.AssertRoundTrip)
            await AssertRoundTripAsync(scenario, snapshot);
    }

    [BundledContentFact("ID_WOTC_PHB24_CLASS_DRUID", "ID_WOTC_PHB24_BACKGROUND_ACOLYTE")]
    public Task Optional2024DruidAcolyteScenario() => Scenario_ExposesExpectedChoicesAndSurvivesRoundTrip(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ParityScenarios", "2024-druid-acolyte.json"));

    private bool EnsureScenarioElementsAvailable(CharacterParityScenario scenario)
    {
        var missing = scenario.SeedElementIds
            .Where(id => !DataManager.Current.ElementsCollection.Any(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count == 0) return true;

        throw new InvalidOperationException($"Required scenario {scenario.Name} is missing: {string.Join(", ", missing)}");
    }

    private static async Task<CharacterParitySnapshot> BuildScenarioAsync(CharacterParityScenario scenario)
    {
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var character = await CharacterManager.Current.New(initializeFirstLevel: true);
        ApplyBaseAbilityScores(character, scenario.BaseAbilityScores);

        foreach (var elementId in scenario.SeedElementIds)
        {
            var element = DataManager.Current.ElementsCollection
                .First(e => e.Id.Equals(elementId, StringComparison.OrdinalIgnoreCase));
            CharacterManager.Current.RegisterElement(element);
        }

        CharacterManager.Current.ReprocessCharacter();
        return CharacterParitySnapshotter.Capture();
    }

    private static void ApplyBaseAbilityScores(
        Character character,
        IReadOnlyDictionary<string, int>? baseScores)
    {
        if (baseScores is null || baseScores.Count == 0)
            return;

        foreach (var (ability, score) in baseScores)
        {
            switch (ability.ToLowerInvariant())
            {
                case "strength":
                    character.Abilities.Strength.BaseScore = score;
                    break;
                case "dexterity":
                    character.Abilities.Dexterity.BaseScore = score;
                    break;
                case "constitution":
                    character.Abilities.Constitution.BaseScore = score;
                    break;
                case "intelligence":
                    character.Abilities.Intelligence.BaseScore = score;
                    break;
                case "wisdom":
                    character.Abilities.Wisdom.BaseScore = score;
                    break;
                case "charisma":
                    character.Abilities.Charisma.BaseScore = score;
                    break;
            }
        }

        character.Abilities.CalculateAvailablePoints();
    }

    private static void AssertRuleExpectation(
        CharacterParityScenario scenario,
        CharacterParitySnapshot snapshot,
        CharacterParityRuleExpectation expectation)
    {
        var matches = snapshot.SelectionRules.Where(rule =>
            (expectation.Type is null || rule.Type.Equals(expectation.Type, StringComparison.OrdinalIgnoreCase)) &&
            (expectation.NameContains is null || rule.Name.Contains(expectation.NameContains, StringComparison.OrdinalIgnoreCase)) &&
            (expectation.OwnerId is null || rule.OwnerId.Equals(expectation.OwnerId, StringComparison.OrdinalIgnoreCase)) &&
            (expectation.Bucket is null || rule.Bucket.Equals(expectation.Bucket, StringComparison.OrdinalIgnoreCase)) &&
            (expectation.RequiredLevel is null || rule.RequiredLevel == expectation.RequiredLevel.Value) &&
            (expectation.OptionalFlavor is null || rule.OptionalFlavor == expectation.OptionalFlavor.Value))
            .ToList();

        matches.Should().NotBeEmpty(
            because: $"{scenario.Name} should expose rule '{Describe(expectation)}'");

        if (expectation.MinOptions is int minOptions)
        {
            matches.Max(rule => rule.OptionCount).Should().BeGreaterThanOrEqualTo(minOptions,
                because: $"{scenario.Name} rule '{Describe(expectation)}' should have selectable options");
        }

        if (expectation.ExpectedSelectedCount is int expectedSelectedCount)
        {
            matches.Should().Contain(rule =>
                    rule.SelectedIds.Count(id => !string.IsNullOrWhiteSpace(id)) == expectedSelectedCount,
                because: $"{scenario.Name} rule '{Describe(expectation)}' should preserve its expected selection state");
        }

        if (expectation.ExpectedOptionIds.Length > 0)
        {
            matches.SelectMany(rule => rule.OptionIds)
                .Should().Contain(expectation.ExpectedOptionIds,
                    because: $"{scenario.Name} rule '{Describe(expectation)}' should include known legacy options");
        }
    }

    private static async Task AssertRoundTripAsync(
        CharacterParityScenario scenario,
        CharacterParitySnapshot original)
    {
        var character = CharacterManager.Current.Character
            ?? throw new InvalidOperationException("No current character is loaded.");

        var bytes = CharacterManager.Current.File.SerializeCharacter(character);
        bytes.Should().NotBeNullOrEmpty();

        var tempPath = Path.Combine(Path.GetTempPath(), $"aurora_parity_{Guid.NewGuid():N}.dnd5e");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);
            SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();

            var reloaded = CharacterParitySnapshotter.Capture();
            reloaded.RegisteredElements.Select(e => e.Id)
                .Should().Contain(original.RegisteredElements.Select(e => e.Id),
                    because: $"{scenario.Name} should preserve registered elements after save/load");

            reloaded.Spellcasting.Select(s => s.Name)
                .Should().Contain(scenario.ExpectedSpellcastingNames,
                    because: $"{scenario.Name} should preserve expected spellcasting profiles after save/load");

            foreach (var expectation in scenario.ExpectedSelectionRules)
                AssertRuleExpectation(scenario, reloaded, expectation);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static string Describe(CharacterParityRuleExpectation expectation)
        => string.Join(" / ", new[]
        {
            expectation.Bucket,
            expectation.Type,
            expectation.NameContains,
            expectation.OwnerId
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
