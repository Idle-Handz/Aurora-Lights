using Aurora.App.Services;

namespace Aurora.Tests.Tests;

public sealed class SessionAttackReminderServiceTests
{
    [Fact]
    public void Build_ShowsDefaultAttacksAndInventoryWeapons()
    {
        string[] selectedWeapons = ["main-weapon", "stowed-weapon"];
        SessionInventorySource[] inventory =
        [
            new("main-weapon", true, "Primary Hand"),
            new("stowed-weapon", false, "Backpack"),
            new("off-weapon", true, "Secondary Hand"),
        ];
        SessionAttackSource[] attacks =
        [
            new("Unarmed Strike", "+5 vs AC", "1+3 bludgeoning", "5 ft"),
            new("Longsword", "+5 vs AC", "1d8+3 slashing", "5 ft", "main-weapon"),
            new("Dagger", "+5 vs AC", "1d4+3 piercing", "20/60", "stowed-weapon"),
            new("Mace", "+5 vs AC", "1d6+3 bludgeoning", "5 ft", "off-weapon"),
        ];

        var result = SessionAttackReminderService.Build(attacks, inventory, selectedWeapons, []);

        result.Visible.Select(reminder => reminder.Name)
            .Should().BeEquivalentTo(["Unarmed Strike", "Longsword", "Dagger"]);
        result.AvailableWeapons.Select(reminder => reminder.Name)
            .Should().BeEquivalentTo(["Mace"]);
    }

    [Fact]
    public void Build_HidesRemovedDefaultAttacks()
    {
        var hidden = new SessionAttackSource("Claws", "+4 vs AC", "1d6+2 slashing", "5 ft");
        var hiddenKey = SessionAttackReminderService.Build(
            [hidden],
            [],
            [],
            []).Visible.Single().Key;

        var result = SessionAttackReminderService.Build(
            [hidden],
            [],
            [],
            [hiddenKey]);

        result.Visible.Should().BeEmpty();
    }

    [Fact]
    public void Build_AddsInventoryWeaponOptionsWhenNoAttackRowExists()
    {
        SessionInventorySource[] inventory =
        [
            new("blowgun", false, "Backpack", "Blowgun", true, "1 piercing", "25/100"),
            new("backpack", false, "Backpack", "Backpack", false),
        ];

        var result = SessionAttackReminderService.Build([], inventory, [], []);

        result.AvailableWeapons.Should().ContainSingle();
        result.AvailableWeapons[0].Name.Should().Be("Blowgun");
        result.AvailableWeapons[0].Damage.Should().Be("1 piercing");
        result.AvailableWeapons[0].Range.Should().Be("25/100");
    }

    [Fact]
    public void Build_DeduplicatesOnlyIdenticalRowsForTheSameWeapon()
    {
        SessionInventorySource[] inventory =
        [
            new("blade-1", true, "Primary Hand", "Longsword", true),
        ];
        SessionAttackSource[] attacks =
        [
            new("Longsword", "+5 vs AC", "1d8+3 slashing", "5 ft", "blade-1"),
            new("Longsword", "+5 vs AC", "1d8+3 slashing", "5 ft", "blade-1"),
            new("Longsword", "+5 vs AC", "1d10+3 slashing", "5 ft", "blade-1"),
        ];

        var result = SessionAttackReminderService.Build(attacks, inventory, [], []);

        result.AvailableWeapons.Select(reminder => reminder.Damage)
            .Should().BeEquivalentTo(["1d8+3 slashing", "1d10+3 slashing"]);
    }

    [Fact]
    public void Build_SelectsSpecificWeaponRowsByKeyAndLegacyIdentifierSelectsAllRows()
    {
        SessionInventorySource[] inventory =
        [
            new("blade-1", true, "Primary Hand", "Longsword", true),
        ];
        SessionAttackSource[] attacks =
        [
            new("Longsword", "+5 vs AC", "1d8+3 slashing", "5 ft", "blade-1"),
            new("Longsword", "+5 vs AC", "1d10+3 slashing", "5 ft", "blade-1"),
        ];

        var unselected = SessionAttackReminderService.Build(attacks, inventory, [], []);
        string oneHandedKey = unselected.AvailableWeapons
            .Single(reminder => reminder.Damage == "1d8+3 slashing")
            .Key;

        var rowSelected = SessionAttackReminderService.Build(attacks, inventory, [oneHandedKey], []);

        rowSelected.Visible.Select(reminder => reminder.Damage)
            .Should().BeEquivalentTo(["1d8+3 slashing"]);
        rowSelected.AvailableWeapons.Select(reminder => reminder.Damage)
            .Should().BeEquivalentTo(["1d10+3 slashing"]);

        var legacySelected = SessionAttackReminderService.Build(attacks, inventory, ["blade-1"], []);

        legacySelected.Visible.Select(reminder => reminder.Damage)
            .Should().BeEquivalentTo(["1d8+3 slashing", "1d10+3 slashing"]);
        legacySelected.AvailableWeapons.Should().BeEmpty();
    }

    [Fact]
    public void Build_ShowsCustomAttackReminders()
    {
        CustomAttackReminder[] customReminders =
        [
            new() { Id = "fire-blowgun", Name = "Blowgun of Flame", Attack = "+6 vs AC", Damage = "1 piercing + 1d6 fire", Range = "25/100" },
        ];

        var result = SessionAttackReminderService.Build([], [], [], [], customReminders);

        result.Visible.Should().ContainSingle();
        result.Visible[0].IsCustom.Should().BeTrue();
        result.Visible[0].CustomIdentifier.Should().Be("fire-blowgun");
        result.Visible[0].Name.Should().Be("Blowgun of Flame");
        result.Visible[0].Attack.Should().Be("+6 vs AC");
        result.Visible[0].Damage.Should().Be("1 piercing + 1d6 fire");
    }
}
