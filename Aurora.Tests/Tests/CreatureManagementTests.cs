using Aurora.Reflections.Services;
using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class CreatureManagementTests
{
    [Fact]
    public void DamageUsesTemporaryHpFirstAndHealingCannotOverflowTheMaximum()
    {
        var profile = Creature();
        profile.Set("hp", "22 (4d8 + 4)");
        profile.Set("temporary-hp", "5");
        CreatureCombat.GrantTemporaryHp(profile, 3);
        CreatureCombat.Damage(profile, 3);
        CreatureCombat.CurrentHp(profile).Should().Be(22);
        CreatureCombat.TemporaryHp(profile).Should().Be(2);
        CreatureCombat.Damage(profile, 7);
        CreatureCombat.CurrentHp(profile).Should().Be(17);
        CreatureCombat.TemporaryHp(profile).Should().Be(0);
        CreatureCombat.Heal(profile, int.MaxValue);
        CreatureCombat.CurrentHp(profile).Should().Be(22);
        CreatureCombat.Damage(profile, int.MaxValue);
        CreatureCombat.CurrentHp(profile).Should().Be(0);
        profile.Set("hp", "special");
        var fields = new Dictionary<string, string>(profile.Fields);
        Action damage = () => CreatureCombat.Damage(profile, 5);
        damage.Should().Throw<InvalidOperationException>().WithMessage("*Maximum HP*");
        profile.Fields.Should().BeEquivalentTo(fields);
    }

    [Fact]
    public void RestsRespectRecoveryTypesAndKeepConditions()
    {
        var profile = Creature();
        profile.Set("current-hp", "5");
        profile.Set("temporary-hp", "3");
        profile.Set("spell-slots", "1st: 2; 3rd: 1");
        profile.Set("hit-dice", "5d8");
        CreatureCombat.SetCondition(profile, "Poisoned", true);
        CreatureCombat.SetCondition(profile, "Poisoned", true);
        profile.Combat.Exhaustion = 3;
        profile.Combat.Resources = [
            new() { Id = "short", Name = "Recharge", Max = 2, Used = 1, ResetOn = ResetOn.ShortRest },
            new() { Id = "long", Name = "Daily", Max = 1, Used = 1 },
            new() { Id = "manual", Name = "Charges", Max = 3, Used = 2, ResetOn = ResetOn.Manual }
        ];
        CreatureCombat.AdjustSlot(profile, 1, 10);
        profile.Combat.SpellSlotsUsed[1].Should().Be(2);
        CreatureCombat.SpendHitDie(profile, 4);
        CreatureCombat.CurrentHp(profile).Should().Be(9);
        profile.Combat.HitDiceUsed.Should().Be(1);
        profile.Combat.HitDiceUsed = 5;
        CreatureCombat.Rest(profile, false);
        CreatureCombat.CurrentHp(profile).Should().Be(9);
        CreatureCombat.TemporaryHp(profile).Should().Be(3);
        profile.Combat.Resources.Select(r => r.Used).Should().Equal(0, 1, 2);
        profile.Combat.SpellSlotsUsed[1].Should().Be(2);
        profile.Combat.Conditions.Should().Equal("Poisoned");
        CreatureCombat.Rest(profile, true);
        CreatureCombat.CurrentHp(profile).Should().Be(20);
        CreatureCombat.TemporaryHp(profile).Should().Be(0);
        profile.Combat.HitDiceUsed.Should().Be(3, "2014 long rests restore half the total, rounded down");
        profile.Combat.Exhaustion.Should().Be(2);
        profile.Combat.Resources.Select(r => r.Used).Should().Equal(0, 0, 2);
        profile.Combat.SpellSlotsUsed.Should().BeEmpty();
        profile.Combat.Conditions.Should().Equal("Poisoned");
        CreatureCombat.SetCondition(profile, "Poisoned", false);
        profile.Combat.Conditions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Short or long rest", false, 0)]
    [InlineData("Long rest", false, 1)]
    [InlineData("Long rest", true, 0)]
    [InlineData("Manual", true, 1)]
    public void SpellSlotsUseTheSelectedRecoveryRule(string recovery, bool longRest, int expectedUsed)
    {
        var profile = Creature();
        profile.Set("spell-slots", "1st: 1");
        profile.Set("slot-recovery", recovery);
        CreatureCombat.AdjustSlot(profile, 1, 1);
        CreatureCombat.Rest(profile, longRest);
        profile.Combat.SpellSlotsUsed.GetValueOrDefault(1).Should().Be(expectedUsed);
    }

    [Fact]
    public void WarriorResourcesFollowLevelAndCustomResourcesRemainIndependent()
    {
        var profile = SidekickService.Create("sidekick-template:warrior");
        profile.Set("hp", "20");
        profile.Set("level", "18");
        SidekickProgression.Refresh(profile);
        CreatureCombat.AdjustResource(profile, "sidekick:second-wind", 1);
        CreatureCombat.AdjustResource(profile, "sidekick:indomitable", 2);
        CreatureCombat.Rest(profile, false);
        CreatureCombat.Resources(profile).Single(r => r.Name == "Second Wind").Used.Should().Be(0);
        CreatureCombat.Resources(profile).Single(r => r.Name == "Indomitable").Used.Should().Be(2);
        profile.Set("level", "20");
        SidekickProgression.Refresh(profile);
        CreatureCombat.Resources(profile).Single(r => r.Name == "Second Wind").Max.Should().Be(2);
        CreatureManagementService.SetResource(profile, new CustomResource
            { Id = "sidekick:indomitable", Name = "Custom resilience", Max = 3, ResetOn = ResetOn.Manual });
        CreatureCombat.Rest(profile, true);
        CreatureCombat.Resources(profile).Single(r => r.Id == "sidekick:indomitable").Used.Should().Be(2);
        var copy = profile.Copy();
        copy.Combat.Conditions.Add("Prone");
        copy.Combat.Resources[0].Used = 0;
        profile.Combat.Conditions.Should().BeEmpty();
        profile.Combat.Resources[0].Used.Should().Be(2);
    }

    [Fact]
    public void CompanionDefaultsFollowTheSourceUntilOverridden()
    {
        var original = Snapshot("10", "12");
        var profile = SidekickService.MergeCompanion(original, null);
        profile.Set("ac", "19");
        profile.Set("name", "My companion");
        profile.Set("current-hp", "4");
        var updated = SidekickService.MergeCompanion(Snapshot("13", "30"), profile);
        updated.Get("ac").Should().Be("19");
        updated.Get("hp").Should().Be("30", "untouched stats follow the latest class calculation");
        updated.Get("current-hp").Should().Be("4");
        updated.Name.Should().Be("My companion");
        updated.LevelDefaults["ac"].Should().Be("13");
        updated.Overrides.Remove("ac");
        SidekickService.MergeCompanion(Snapshot("15", "32"), updated).Get("ac").Should().Be("15");
        original.DefaultFields["name"].Should().Be("Original");
    }

    [Fact]
    public void BadSlotDescriptionsStayVisibleWithoutInventingUsableSlots()
    {
        var profile = Creature();
        profile.Set("spell-slots", "homebrew slot pool");
        var model = CreatureManagementService.Model(profile);
        model.SlotError.Should().Contain("Edit spell slots");
        model.Slots.Should().BeEmpty();
        model.Details.Should().Contain(f => f.Value == "homebrew slot pool");
    }

    private static SidekickProfile Creature()
    {
        var profile = SidekickService.Create("sidekick-template:custom");
        profile.Set("hp", "20");
        return profile;
    }
    private static CompanionSnapshot Snapshot(string ac, string hp) => new("Original", "Beast", ac, hp, "30", "+0", 2, [], [], [], [])
    {
        InstanceId = "companion-instance:test", TemplateId = "template",
        DefaultFields = new Dictionary<string, string> { ["name"] = "Original", ["ac"] = ac, ["hp"] = hp }
    };
}

public sealed class CreatureManagementCardTests : BunitContext
{
    [Fact]
    public async Task ControlsSendTheEnteredAmountAndAwaitSaving()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CreatureAction? received = null;
        var cut = Render<CreatureManagementCard>(p => p.Add(c => c.Model, Model())
            .Add(c => c.OnAction, async (CreatureAction action) => { received = action; await pending.Task; }));
        cut.Find("input[type=number]").Input("7");
        var dispatch = cut.Find("[data-action=damage]").ClickAsync(new());
        try
        {
            received.Should().Be(new CreatureAction("damage", 7));
            dispatch.IsCompleted.Should().BeFalse();
        }
        finally { pending.SetResult(); await dispatch; }
        cut.Render(p => p.Add(c => c.Busy, true));
        cut.FindAll("button").Should().OnlyContain(b => b.HasAttribute("disabled"));
    }

    [Fact]
    public void ExhaustedSlotsAndHitDiceCannotBeSpentAndConditionsCanBeCleared()
    {
        var actions = new List<CreatureAction>();
        var model = Model();
        var cut = Render<CreatureManagementCard>(p => p.Add(c => c.Model, model)
            .Add(c => c.OnAction, (CreatureAction action) => actions.Add(action)));
        cut.Find("button[aria-label='Spend level 1 slot']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-action=hit-die]").HasAttribute("disabled").Should().BeTrue();
        cut.Find("button[aria-label='Clear Poisoned']").Click();
        actions.Should().ContainSingle().Which.Should().Be(new CreatureAction("condition", Key: "Poisoned", Active: false));
        cut.Find(".creature-details").TextContent.Should().Contain("Custom <text>");
        cut.FindAll("script").Should().BeEmpty();
    }

    private static CreatureManagementModel Model() => new()
    {
        Id = "pet", Name = "Rook", MaximumHp = 20, CurrentHp = 12, TemporaryHp = 3,
        HitDice = 2, HitDiceUsed = 2, Slots = [new(1, 2, 2)], Conditions = ["Poisoned"],
        AvailableConditions = CreatureCombat.Conditions, Details = [new("Notes", "Custom <text>", true)]
    };
}
