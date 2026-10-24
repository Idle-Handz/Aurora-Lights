using Builder.Presentation.Models;
using Builder.Presentation.Models.Helpers;

namespace Aurora.Reflections.Services;

/// <summary>
/// Keeps Session-page attack reminders backed by the character's saved attack list.
/// The source index is the index in <see cref="Character.AttacksSection"/>, including
/// rows that are not currently displayed.
/// </summary>
public static class AttackReminderCharacterService
{
    public static int EnsureWeaponAttackEntry(Character character, string equipmentIdentifier)
    {
        if (string.IsNullOrWhiteSpace(equipmentIdentifier))
            return -1;

        for (int index = 0; index < character.AttacksSection.Items.Count; index++)
        {
            var existing = character.AttacksSection.Items[index];
            if (!string.Equals(
                    existing.EquipmentItem?.Identifier,
                    equipmentIdentifier,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            existing.IsDisplayed = true;
            existing.UpdateCalculations();
            return index;
        }

        var equipment = character.Inventory.Items.FirstOrDefault(item =>
            string.Equals(item.Identifier, equipmentIdentifier, StringComparison.OrdinalIgnoreCase));
        if (equipment is null)
            return -1;

        var attack = new AttackSectionItem(equipment);
        character.AttacksSection.Items.Add(attack);
        return character.AttacksSection.Items.Count - 1;
    }

    public static int AddAttackEntry(Character character, CustomAttackReminder reminder)
    {
        var attack = new AttackSectionItem(reminder.Name);
        ApplyEdits(attack, reminder);
        character.AttacksSection.Items.Add(attack);
        return character.AttacksSection.Items.Count - 1;
    }

    public static bool ApplyEdits(Character character, int sourceIndex, CustomAttackReminder reminder)
    {
        if (sourceIndex < 0 || sourceIndex >= character.AttacksSection.Items.Count)
            return false;

        ApplyEdits(character.AttacksSection.Items[sourceIndex], reminder);
        return true;
    }

    private static void ApplyEdits(AttackSectionItem attack, CustomAttackReminder reminder)
    {
        attack.Name.Content = reminder.Name;
        attack.Attack.Content = reminder.Attack;
        attack.Damage.Content = reminder.Damage;
        attack.Range.Content = reminder.Range;
        attack.IsDisplayed = true;
    }
}
