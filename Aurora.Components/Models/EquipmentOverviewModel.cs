namespace Aurora.Components.Models;

public sealed class EquipmentOverviewModel
{
    public IReadOnlyList<EquipmentAttackModel> Attacks { get; init; } = [];
    public IReadOnlyList<EquipmentGearSlotModel> GearSlots { get; init; } = [];
    public IReadOnlyList<EquipmentInventoryItemModel> InventoryItems { get; init; } = [];
    public long Copper { get; init; }
    public long Silver { get; init; }
    public long Electrum { get; init; }
    public long Gold { get; init; }
    public long Platinum { get; init; }
    public int AttunedCount { get; init; }
    public int AttunedMax { get; init; }
    public string EquipmentNotes { get; init; } = string.Empty;
    public string TreasureNotes { get; init; } = string.Empty;
    public string QuestNotes { get; init; } = string.Empty;
}

public sealed record EquipmentAttackModel(string Name, string Attack, string Damage, string Range);

public sealed record EquipmentGearSlotModel(string Id, string Label, string? EquippedName, string? EquippedIdentifier = null);

public sealed record EquipmentInventoryItemModel(
    string Identifier,
    string Name,
    int Amount,
    bool IsStackable,
    bool IsEquipped,
    string EquippedLocation,
    bool IsAttunable,
    bool IsAttuned,
    string DisplayWeight,
    string DisplayPrice,
    bool IsEquippable,
    bool HasVersatile = false,
    bool CanExtract = false);

public sealed record EquipmentCoinChangeModel(string CoinId, long Value);

public sealed record EquipmentAmountChangeModel(string Identifier, int Amount);

public sealed record EquipmentNoteChangeModel(string NoteId, string Value);

public sealed record EquipmentItemEditResult(string AlternativeName, string Notes);
