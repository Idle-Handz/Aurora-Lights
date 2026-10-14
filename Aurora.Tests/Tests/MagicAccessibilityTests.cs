using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Aurora.Tests.Tests;

public sealed class MagicAccessibilityTests : BunitContext
{
    [Theory]
    [InlineData("All", "Search castable spells")]
    [InlineData("Known", "Search known spells")]
    [InlineData("Prepared — Wizard", "Search spells")]
    public void Every_spell_view_names_its_search_and_filter_controls(string tabName, string searchName)
    {
        var model = new MagicOverviewModel
        {
            HasSpellcasting = true,
            KnownSpellGroups =
            [
                new MagicKnownSpellGroupModel("Known spells",
                    [new MagicKnownSpellEntryModel("known-shield", "1st level", "Shield",
                        SpellLevel: 1, IsReadOnly: true, SpellId: "shield")], ReadOnlyGroup: true)
            ],
            Sections =
            [
                new MagicSpellcastingSectionModel
                {
                    Id = "wizard", Label = "Wizard", IsPreparedCaster = true, MaxPrepared = 2,
                    SpellLevels = [new MagicSpellLevelModel(1,
                        [new MagicSpellListEntryModel("shield", "Shield", 1, "Test", true, false)], 2, 0)]
                }
            ]
        };
        var cut = Render<CharacterMagicWorkspace>(parameters => parameters.Add(component => component.Model, model));

        cut.FindAll(".magic-view-tab").Single(tab => tab.TextContent.Trim() == tabName).Click();

        cut.Find("input.magic-filter-input").GetAttribute("aria-label").Should().Be(searchName);
        cut.FindAll("select.magic-filter-select").Select(select => select.GetAttribute("aria-label"))
            .Should().Equal("Spell level", "Spell source", "Spell school", "Spell casting time");
    }

    [Fact]
    public void Slot_buttons_expose_position_usage_and_toggle_callback()
    {
        MagicSlotToggleModel? clicked = null;
        var model = new MagicOverviewModel
        {
            HasSpellcasting = true,
            Sections =
            [
                new MagicSpellcastingSectionModel
                {
                    Id = "wizard", Label = "Wizard", IsPreparedCaster = true,
                    SpellLevels = [new MagicSpellLevelModel(1, [], 2, 1)]
                }
            ]
        };
        var cut = Render<CharacterMagicWorkspace>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.OnToggleSlot, (MagicSlotToggleModel value) => clicked = value));

        var slots = cut.FindAll("button.magic-slot-bubble");
        slots.Should().HaveCount(2);
        slots[0].GetAttribute("aria-label").Should().Be("1st level spell slot 1 of 2: used");
        slots[0].GetAttribute("aria-pressed").Should().Be("true");
        slots[1].GetAttribute("aria-label").Should().Be("1st level spell slot 2 of 2: available");
        slots[1].GetAttribute("aria-pressed").Should().Be("false");
        slots[1].Click();
        clicked.Should().Be(new MagicSlotToggleModel(1, 2));
    }

    [Fact]
    public void Duplicate_spell_names_identify_the_source_and_dispatch_the_exact_spell()
    {
        var changes = new List<MagicPreparedChangeModel>();
        var cut = RenderPrepared(parameters => parameters
            .Add(component => component.OnPreparedChanged, (MagicPreparedChangeModel change) => changes.Add(change)));

        var checkboxes = cut.FindAll("input.magic-spell-prep-check");
        checkboxes.Select(input => input.GetAttribute("aria-label")).Should().Equal(
            "Prepare Cure Wounds for Paladin (Player's Handbook)",
            "Prepare Cure Wounds for Paladin (Player's Handbook 2024)");
        checkboxes[1].Change(true);

        changes.Should().ContainSingle().Which.Should()
            .Be(new MagicPreparedChangeModel("cure-2024", true, "paladin", "Paladin"));
    }

    [Fact]
    public void Busy_preparation_remains_focusable_and_rejects_stale_native_changes()
    {
        var changes = new List<MagicPreparedChangeModel>();
        var cut = RenderPrepared(parameters => parameters
            .Add(component => component.OnPreparedChanged, (MagicPreparedChangeModel change) => changes.Add(change)));

        cut.Render(parameters => parameters.Add(component => component.Busy, true));
        var checkboxes = cut.FindAll("input.magic-spell-prep-check");
        foreach (var checkbox in checkboxes)
        {
            checkbox.HasAttribute("disabled").Should().BeFalse();
            checkbox.GetAttribute("aria-disabled").Should().Be("true");
        }
        // A queued browser change may still arrive after Busy changes. Binding must restore
        // the authoritative checked state even when the callback is rejected.
        checkboxes[1].Change(true);
        changes.Should().BeEmpty();
        cut.FindAll("input.magic-spell-prep-check")[1].HasAttribute("checked").Should().BeFalse();

        cut.Render(parameters => parameters.Add(component => component.Busy, false));
        cut.FindAll("input.magic-spell-prep-check")[1].GetAttribute("aria-disabled").Should().Be("false");
        cut.FindAll("input.magic-spell-prep-check")[1].Change(true);
        changes.Should().ContainSingle();
    }

    [Fact]
    public async Task Slow_preparation_blocks_a_second_change_before_the_parent_sets_Busy()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var model = PreparedModel();
        var cut = RenderPrepared(parameters => parameters
            .Add(component => component.OnPreparedChanged, async (MagicPreparedChangeModel change) =>
            {
                calls++;
                model.Sections[0].SpellLevels[0].Spells.Single(spell => spell.Id == change.SpellId).IsPrepared = change.Value;
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }), model);

        Task firstChange = cut.FindAll("input.magic-spell-prep-check")[0]
            .ChangeAsync(new ChangeEventArgs { Value = false });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cut.WaitForAssertion(() =>
            {
                var inputs = cut.FindAll("input.magic-spell-prep-check");
                inputs.Should().OnlyContain(input => !input.HasAttribute("disabled"));
                inputs.Should().OnlyContain(input => input.GetAttribute("aria-disabled") == "true");
            });
            await cut.FindAll("input.magic-spell-prep-check")[1].ChangeAsync(new ChangeEventArgs { Value = true })
                .WaitAsync(TimeSpan.FromSeconds(3));
            calls.Should().Be(1);
            cut.FindAll("input.magic-spell-prep-check")[1].HasAttribute("checked").Should().BeFalse();
        }
        finally { release.TrySetResult(); }
        await firstChange;

        cut.FindAll("input.magic-spell-prep-check")[0].HasAttribute("checked").Should().BeFalse();
        cut.FindAll("input.magic-spell-prep-check").Should()
            .OnlyContain(input => input.GetAttribute("aria-disabled") == "false");
    }

    [Fact]
    public void Reordering_same_named_spells_keeps_their_state_and_callback_identity()
    {
        var model = PreparedModel();
        var changes = new List<MagicPreparedChangeModel>();
        var cut = RenderPrepared(parameters => parameters
            .Add(component => component.OnPreparedChanged, (MagicPreparedChangeModel change) => changes.Add(change)), model);
        var section = model.Sections[0];
        section.SpellLevels = [new MagicSpellLevelModel(1, section.SpellLevels[0].Spells.Reverse().ToList(), 4, 0)];

        cut.Render(parameters => parameters.Add(component => component.Model, model));

        var moved = cut.FindAll("input.magic-spell-prep-check")[1];
        moved.GetAttribute("aria-label").Should().Be("Prepare Cure Wounds for Paladin (Player's Handbook)");
        moved.HasAttribute("checked").Should().BeTrue();
        var other = cut.FindAll("input.magic-spell-prep-check")[0];
        other.HasAttribute("checked").Should().BeFalse();
        other.Change(true);
        changes.Should().ContainSingle().Which.SpellId.Should().Be("cure-2024");
    }

    [Fact]
    public void Intrinsic_preparation_restrictions_still_disable_the_control()
    {
        var model = PreparedModel();
        model.Sections[0].PreparedCount = 2;
        model.Sections[0].SpellLevels[0].Spells[0].IsAlwaysPrepared = true;
        var cut = RenderPrepared(parameters => parameters
            .Add(component => component.OnPreparedChanged, (MagicPreparedChangeModel _) => { }), model);

        cut.FindAll("input.magic-spell-prep-check").Should()
            .OnlyContain(input => input.HasAttribute("disabled") && input.GetAttribute("aria-disabled") == "true");

        var noCallback = RenderPrepared(_ => { });
        noCallback.FindAll("input.magic-spell-prep-check").Should()
            .OnlyContain(input => input.HasAttribute("disabled"));
    }

    private IRenderedComponent<CharacterMagicWorkspace> RenderPrepared(
        Action<ComponentParameterCollectionBuilder<CharacterMagicWorkspace>> configure,
        MagicOverviewModel? model = null)
    {
        var cut = Render<CharacterMagicWorkspace>(parameters =>
        {
            parameters.Add(component => component.Model, model ?? PreparedModel());
            configure(parameters);
        });
        cut.FindAll(".magic-view-tab").Single(tab => tab.TextContent.Trim() == "Prepared — Paladin").Click();
        return cut;
    }

    private static MagicOverviewModel PreparedModel() => new()
    {
        HasSpellcasting = true,
        Sections =
        [
            new MagicSpellcastingSectionModel
            {
                Id = "paladin", Label = "Paladin", IsPreparedCaster = true, MaxPrepared = 2, PreparedCount = 1,
                SpellLevels = [new MagicSpellLevelModel(1,
                [
                    new MagicSpellListEntryModel("cure-2014", "Cure Wounds", 1, "Player's Handbook", true, false),
                    new MagicSpellListEntryModel("cure-2024", "Cure Wounds", 1, "Player's Handbook 2024", false, false)
                ], 4, 0)]
            }
        ]
    };
}
