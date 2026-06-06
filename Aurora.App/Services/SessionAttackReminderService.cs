namespace Aurora.App.Services;

public sealed record SessionAttackSource(
    string Name,
    string Attack,
    string Damage,
    string Range,
    string EquipmentIdentifier = "");

public sealed record SessionInventorySource(
    string Identifier,
    bool IsEquipped,
    string EquippedLocation);

public sealed record SessionAttackReminder(
    string Key,
    string Name,
    string Attack,
    string Damage,
    string Range,
    string? EquipmentIdentifier,
    bool IsWeapon)
{
    public bool IsDefault => !IsWeapon;
}

public sealed record SessionAttackReminderSet(
    IReadOnlyList<SessionAttackReminder> Visible,
    IReadOnlyList<SessionAttackReminder> AvailableWeapons);

public static class SessionAttackReminderService
{
    private static readonly string[] HandLocations =
    [
        "Primary Hand",
        "Secondary Hand",
        "Two-Handed",
        "Two-Handed (Versatile)"
    ];

    public static SessionAttackReminderSet Build(
        IReadOnlyList<SessionAttackSource> attacks,
        IReadOnlyList<SessionInventorySource> inventoryItems,
        IReadOnlyCollection<string> selectedWeaponIds,
        IReadOnlyCollection<string> hiddenDefaultKeys)
    {
        var onHandIdentifiers = inventoryItems
            .Where(item => item.IsEquipped &&
                           HandLocations.Contains(item.EquippedLocation, StringComparer.OrdinalIgnoreCase))
            .Select(item => item.Identifier)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var reminders = attacks
            .Where(attack => !string.IsNullOrWhiteSpace(attack.Name))
            .Select(attack => ToReminder(attack))
            .ToList();

        var defaultReminders = reminders
            .Where(reminder => reminder.IsDefault)
            .Where(reminder => !hiddenDefaultKeys.Contains(reminder.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var weaponOptions = reminders
            .Where(reminder => reminder.IsWeapon &&
                               reminder.EquipmentIdentifier is { } id &&
                               onHandIdentifiers.Contains(id))
            .GroupBy(reminder => reminder.EquipmentIdentifier!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var selectedWeaponSet = selectedWeaponIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedWeapons = weaponOptions
            .Where(reminder => reminder.EquipmentIdentifier is { } id && selectedWeaponSet.Contains(id))
            .ToList();

        var availableWeapons = weaponOptions
            .Where(reminder => reminder.EquipmentIdentifier is { } id && !selectedWeaponSet.Contains(id))
            .ToList();

        return new SessionAttackReminderSet(
            defaultReminders.Concat(selectedWeapons).ToList(),
            availableWeapons);
    }

    private static SessionAttackReminder ToReminder(SessionAttackSource attack)
    {
        var equipmentIdentifier = string.IsNullOrWhiteSpace(attack.EquipmentIdentifier)
            ? null
            : attack.EquipmentIdentifier;
        var isWeapon = equipmentIdentifier is not null;
        var key = isWeapon
            ? $"weapon:{equipmentIdentifier}"
            : $"default:{NormalizeKeyPart(attack.Name)}:{NormalizeKeyPart(attack.Attack)}:{NormalizeKeyPart(attack.Damage)}:{NormalizeKeyPart(attack.Range)}";

        return new SessionAttackReminder(
            key,
            attack.Name,
            attack.Attack,
            attack.Damage,
            attack.Range,
            equipmentIdentifier,
            isWeapon);
    }

    private static string NormalizeKeyPart(string value) =>
        new(value
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch is ':' or '+' or '-' or '/')
            .ToArray());
}
