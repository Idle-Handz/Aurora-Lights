namespace Aurora.Components.Models;

public sealed record SidekickChoiceOption(string Value, string Label);
public sealed record SidekickChoiceRow(string Key, string Label, string Value, IReadOnlyList<SidekickChoiceOption> Options, string Group);
public sealed record SidekickBaseField(string Key, string Label, string Value);
public sealed record SidekickSpellChoiceRow(string Key, string Label, string Name, string Description, bool HasSelection);
public sealed record SidekickChoiceChange(string Key, string Value);
public sealed record SidekickPendingChoice(string Key, string Label, string Group);
public sealed record SidekickHitPointRow(int Level, string Method, int? Roll, string Summary);
public sealed class SidekickHitPointsModel
{
    public int BaseLevel { get; init; } = 1;
    public string Method { get; init; } = "Average";
    public int DieSize { get; init; }
    public string Help { get; init; } = "";
    public IReadOnlyList<SidekickHitPointRow> Gains { get; init; } = [];
}
public sealed class SidekickChoicesModel
{
    public IReadOnlyList<SidekickPendingChoice> PendingChoices { get; init; } = [];
    public SidekickHitPointsModel? HitPoints { get; init; }
    public IReadOnlyList<SidekickBaseField> BaseFields { get; init; } = [];
    public IReadOnlyList<SidekickChoiceRow> Rows { get; init; } = [];
    public IReadOnlyList<SidekickSpellChoiceRow> Spells { get; init; } = [];
    public IReadOnlyList<string> Notices { get; init; } = [];
    public string Summary { get; init; } = "";
}
