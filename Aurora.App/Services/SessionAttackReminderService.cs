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
    string EquippedLocation,
    string Name = "",
    bool IsWeaponLike = false,
    string Damage = "",
    string Range = "");

public sealed record SessionAttackReminder(
    string Key,
    string Name,
    string Attack,
    string Damage,
    string Range,
    string? EquipmentIdentifier,
    bool IsWeapon,
    string? CustomIdentifier = null)
{
    public bool IsCustom => CustomIdentifier is not null;
    public bool IsDefault => !IsWeapon && !IsCustom;
}

public sealed record SessionAttackReminderSet(
    IReadOnlyList<SessionAttackReminder> Visible,
    IReadOnlyList<SessionAttackReminder> AvailableWeapons);

public static class SessionAttackReminderService
{
    public static SessionAttackReminderSet Build(
        IReadOnlyList<SessionAttackSource> attacks,
        IReadOnlyList<SessionInventorySource> inventoryItems,
        IReadOnlyCollection<string> selectedWeaponIds,
        IReadOnlyCollection<string> hiddenDefaultKeys,
        IReadOnlyList<CustomAttackReminder>? customAttackReminders = null)
    {
        var inventoryById = inventoryItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Identifier))
            .GroupBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var inventoryIdentifiers = inventoryById.Keys
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

        var customReminders = (customAttackReminders ?? [])
            .Where(reminder => !string.IsNullOrWhiteSpace(reminder.Name))
            .Select(ToCustomReminder)
            .ToList();

        var weaponOptions = reminders
            .Where(reminder => reminder.IsWeapon &&
                               reminder.EquipmentIdentifier is { } id &&
                               inventoryIdentifiers.Contains(id))
            .GroupBy(reminder => reminder.EquipmentIdentifier!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var weaponOptionIds = weaponOptions
            .Select(reminder => reminder.EquipmentIdentifier)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        weaponOptions.AddRange(inventoryById.Values
            .Where(item => item.IsWeaponLike && !weaponOptionIds.Contains(item.Identifier))
            .Select(ToInventoryWeaponReminder));

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
            defaultReminders.Concat(customReminders).Concat(selectedWeapons).ToList(),
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

    private static SessionAttackReminder ToCustomReminder(CustomAttackReminder reminder)
    {
        string id = string.IsNullOrWhiteSpace(reminder.Id)
            ? Guid.NewGuid().ToString("N")[..8]
            : reminder.Id;

        return new SessionAttackReminder(
            $"custom:{id}",
            reminder.Name,
            reminder.Attack,
            reminder.Damage,
            reminder.Range,
            null,
            false,
            id);
    }

    private static SessionAttackReminder ToInventoryWeaponReminder(SessionInventorySource item)
    {
        string key = $"weapon:{item.Identifier}";
        string name = string.IsNullOrWhiteSpace(item.Name) ? "Weapon" : item.Name;
        return new SessionAttackReminder(
            key,
            name,
            string.Empty,
            item.Damage,
            item.Range,
            item.Identifier,
            true);
    }

    private static string NormalizeKeyPart(string value) =>
        new(value
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch is ':' or '+' or '-' or '/')
            .ToArray());
}
