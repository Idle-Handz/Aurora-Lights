using Aurora.Reflections.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation.Models;

namespace Aurora.Tests.Tests;

public sealed class AttackReminderCharacterServiceTests
{
    public AttackReminderCharacterServiceTests() => TestApplicationContextInstaller.EnsureInstalled();

    [Fact]
    public void AddAttackEntry_CreatesDisplayedSavedAttackWithEditableValues()
    {
        var character = new Character();
        var reminder = new CustomAttackReminder
        {
            Name = "Improvised Flame",
            Attack = "+5 vs AC",
            Damage = "1d6+3 fire",
            Range = "20/60",
        };

        int sourceIndex = AttackReminderCharacterService.AddAttackEntry(character, reminder);

        sourceIndex.Should().Be(0);
        character.AttacksSection.Items.Should().ContainSingle();
        var attack = character.AttacksSection.Items[sourceIndex];
        attack.IsDisplayed.Should().BeTrue();
        attack.Name.Content.Should().Be("Improvised Flame");
        attack.Attack.Content.Should().Be("+5 vs AC");
        attack.Damage.Content.Should().Be("1d6+3 fire");
        attack.Range.Content.Should().Be("20/60");
    }

    [Fact]
    public void ApplyEdits_UpdatesEveryDisplayedReminderField()
    {
        var character = new Character();
        int sourceIndex = AttackReminderCharacterService.AddAttackEntry(
            character,
            new CustomAttackReminder { Name = "Claw", Attack = "+4", Damage = "1d6+2", Range = "5 ft" });

        bool updated = AttackReminderCharacterService.ApplyEdits(
            character,
            sourceIndex,
            new CustomAttackReminder { Name = "Raging Claw", Attack = "+6", Damage = "1d6+4", Range = "10 ft" });

        updated.Should().BeTrue();
        var attack = character.AttacksSection.Items[sourceIndex];
        attack.Name.Content.Should().Be("Raging Claw");
        attack.Attack.Content.Should().Be("+6");
        attack.Damage.Content.Should().Be("1d6+4");
        attack.Range.Content.Should().Be("10 ft");
    }
}
