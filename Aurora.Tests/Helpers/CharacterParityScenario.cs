using System.Text.Json;

namespace Aurora.Tests.Helpers;

public sealed record CharacterParityScenario
{
    public string Name { get; init; } = "";

    public string[] SeedElementIds { get; init; } = [];

    public Dictionary<string, int>? BaseAbilityScores { get; init; }

    public string[] ExpectedRegisteredElementIds { get; init; } = [];

    public string[] ExpectedSpellcastingNames { get; init; } = [];

    public CharacterParityRuleExpectation[] ExpectedSelectionRules { get; init; } = [];

    public bool AssertRoundTrip { get; init; } = true;

    public static CharacterParityScenario Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CharacterParityScenario>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Unable to read parity scenario fixture: {path}");
    }
}

public sealed record CharacterParityRuleExpectation
{
    public string? Type { get; init; }

    public string? NameContains { get; init; }

    public string? OwnerId { get; init; }

    public string? Bucket { get; init; }

    public int? MinOptions { get; init; }

    public int? RequiredLevel { get; init; }

    public string[] ExpectedOptionIds { get; init; } = [];

    public bool? OptionalFlavor { get; init; }
}
