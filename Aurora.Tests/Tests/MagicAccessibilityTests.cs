using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class MagicAccessibilityTests : BunitContext
{
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
}
