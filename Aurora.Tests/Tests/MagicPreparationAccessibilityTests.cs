using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;
using FluentAssertions;
using Xunit;

namespace Aurora.Tests.Tests;

public sealed class MagicPreparationAccessibilityTests : BunitContext
{
    [Fact]
    public void Prepared_spell_control_is_named_and_level_heading_is_a_native_toggle_button()
    {
        var spell = new MagicSpellListEntryModel("shield", "Shield", 1, "Test", false, false);
        var model = new MagicOverviewModel
        {
            HasSpellcasting = true,
            Sections =
            [
                new MagicSpellcastingSectionModel
                {
                    Id = "wizard",
                    Label = "Wizard",
                    IsPreparedCaster = true,
                    MaxPrepared = 2,
                    SpellLevels = [new MagicSpellLevelModel(1, [spell], 2, 0)]
                }
            ]
        };
        var component = Render<CharacterMagicWorkspace>(parameters => parameters.Add(component => component.Model, model));

        component.FindAll(".magic-view-tab").Single(tab => tab.TextContent.Contains("Prepared")).Click();
        component.Find("input.magic-spell-prep-check").GetAttribute("aria-label").Should().Be("Prepare Shield for Wizard (Test)");
        var toggle = component.Find("button.magic-level-toggle");
        toggle.GetAttribute("type").Should().Be("button");
        toggle.GetAttribute("aria-expanded").Should().Be("true");

        toggle.Click();

        component.Find("button.magic-level-toggle").GetAttribute("aria-expanded").Should().Be("false");
    }
}
