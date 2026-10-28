namespace Aurora.Components.Models;

public sealed record CreatureSlotModel(int Level, int Maximum, int Used);
public sealed record CreatureResourceModel(string Id, string Name, int Maximum, int Used, string Recovery, bool CanRemove, bool IsOverride);
public sealed record CreatureDetailModel(string Label, string Value, bool Wide);
public sealed record CreatureAction(string Kind, int Amount = 0, string Key = "", bool Active = false);
public sealed class CreatureManagementModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string? PortraitSource { get; init; }
    public string Notes { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public IReadOnlyList<SidekickPendingChoice> PendingChoices { get; init; } = [];
    public string ArmorClass { get; init; } = "";
    public int? MaximumHp { get; init; }
    public int CurrentHp { get; init; }
    public int TemporaryHp { get; init; }
    public int HitDice { get; init; }
    public int HitDiceUsed { get; init; }
    public int Exhaustion { get; init; }
    public IReadOnlyList<string> Conditions { get; init; } = [];
    public IReadOnlyList<string> AvailableConditions { get; init; } = [];
    public IReadOnlyList<CreatureSlotModel> Slots { get; init; } = [];
    public string? SlotError { get; init; }
    public string SlotRecovery { get; init; } = "Long rest";
    public IReadOnlyList<CreatureResourceModel> Resources { get; init; } = [];
    public IReadOnlyList<CreatureDetailModel> Details { get; init; } = [];
}
