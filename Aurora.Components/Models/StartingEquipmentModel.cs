namespace Aurora.Components.Models;

/// <summary>Parsed starting equipment block for a class or background element.</summary>
public sealed class StartingEquipmentBlock
{
    public static readonly StartingEquipmentBlock Empty = new();

    /// <summary>
    /// Top-level gold alternative (class only).
    /// When present, the player may take this instead of all equipment choices.
    /// </summary>
    public GoldAlternative? GoldAlternative { get; init; }

    /// <summary>A/B choice groups (class equipment choices).</summary>
    public IReadOnlyList<EquipmentChoice> Choices { get; init; } = [];

    /// <summary>Fixed items granted unconditionally (typically background equipment).</summary>
    public IReadOnlyList<EquipmentItem> FixedItems { get; init; } = [];

    /// <summary>Fixed gold granted unconditionally in GP (e.g. a background's "15 gp").</summary>
    public int FixedGold { get; init; }

    public bool HasContent =>
        GoldAlternative != null || Choices.Count > 0 || FixedItems.Count > 0 || FixedGold > 0;
}

/// <summary>
/// Top-level gold alternative — player takes this instead of all equipment choices.
/// Supports both 2014-style dice rolls and 2024-style fixed amounts.
/// </summary>
public sealed class GoldAlternative
{
    /// <summary>2014-style dice expression, e.g. "5d4".</summary>
    public string? Roll { get; init; }

    /// <summary>Multiplier applied to the roll result, e.g. 10 for "5d4 × 10 gp".</summary>
    public int Multiplier { get; init; } = 1;

    /// <summary>2024-style fixed amount in GP.</summary>
    public int? Amount { get; init; }

    public bool IsRolled => Roll != null;
}

/// <summary>One A/B choice line within a class's starting equipment.</summary>
public sealed class EquipmentChoice
{
    /// <summary>The options the player chooses between (typically two: "a" and "b").</summary>
    public IReadOnlyList<EquipmentOption> Options { get; init; } = [];
}

/// <summary>One option within an equipment choice (e.g. the "a" or "b" side).</summary>
public sealed class EquipmentOption
{
    /// <summary>Display label, e.g. "a" or "b".</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Items granted when this option is chosen.</summary>
    public IReadOnlyList<EquipmentItem> Items { get; init; } = [];
}

/// <summary>
/// A single item grant — either a specific item by element ID,
/// or an open category selection resolved in a follow-up picker.
/// </summary>
public sealed class EquipmentItem
{
    /// <summary>Specific item element ID. Null when this is a category selection.</summary>
    public string? Id { get; init; }

    /// <summary>
    /// Open category name for deferred selection, e.g. "Martial Weapon".
    /// Null when a specific Id is set.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>Number of this item granted. Defaults to 1.</summary>
    public int Count { get; init; } = 1;

    /// <summary>
    /// Optional display name override applied as <c>AlternativeName</c> when the item is
    /// added to inventory. Use this when the underlying element is a generic stand-in
    /// (e.g. a Vial renamed to "Incense") rather than a purpose-built element.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>True when the player must choose a specific item from a category.</summary>
    public bool IsCategory => !string.IsNullOrEmpty(Category);
}
