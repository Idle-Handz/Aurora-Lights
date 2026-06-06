using Aurora.App.Services;

namespace Aurora.Tests.Tests;

public sealed class SessionAttackReminderServiceTests
{
    [Fact]
    public void Build_ShowsDefaultAttacksAndOnlySelectedOnHandWeapons()
    {
        string[] selectedWeapons = ["main-weapon", "stowed-weapon"];
        SessionInventorySource[] inventory =
        [
            new("main-weapon", true, "Primary Hand"),
            new("stowed-weapon", true, "Backpack"),
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
            .Should().BeEquivalentTo(["Unarmed Strike", "Longsword"]);
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
}
