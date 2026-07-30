using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Aurora.Tests.Tests;

public sealed class SourceRestrictionsEditorTests : BunitContext
{
    [Fact]
    public void Broad_toggle_reports_mixed_state_and_enables_the_category()
    {
        SourceRestrictionCategoryToggle? requestedToggle = null;
        SourceRestrictionGroupModel[] groups =
        [
            new(
                "third-party",
                "Third Party",
                "",
                AllowUnchecking: true,
                IsChecked: null,
                Sources:
                [
                    new("source-1", "Enabled source", true, true, false, SourceRestrictionCategory.ThirdParty),
                    new("source-2", "Disabled source", false, true, false, SourceRestrictionCategory.ThirdParty)
                ])
        ];

        var cut = Render<SourceRestrictionsEditor>(parameters => parameters
            .Add(component => component.Groups, groups)
            .Add(
                component => component.OnToggleCategory,
                EventCallback.Factory.Create<SourceRestrictionCategoryToggle>(
                    this,
                    toggle => requestedToggle = toggle)));

        var toggle = cut.FindAll("button.source-restrictions-category")
            .Single(button => button.TextContent.Contains("3rd party", StringComparison.Ordinal));

        toggle.GetAttribute("aria-checked").Should().Be("mixed");
        toggle.TextContent.Should().Contain("1 / 2");

        toggle.Click();

        requestedToggle.Should().Be(new SourceRestrictionCategoryToggle(
            SourceRestrictionCategory.ThirdParty,
            IsEnabled: true));
    }

    [Fact]
    public void Broad_toggle_excludes_locked_core_sources_from_its_count()
    {
        SourceRestrictionGroupModel[] groups =
        [
            new(
                "official",
                "Wizards of the Coast",
                "",
                AllowUnchecking: true,
                IsChecked: null,
                Sources:
                [
                    new("core", "Core", true, false, true, SourceRestrictionCategory.Official5E),
                    new("optional", "Optional", false, true, false, SourceRestrictionCategory.Official5E)
                ])
        ];

        var cut = Render<SourceRestrictionsEditor>(parameters => parameters
            .Add(component => component.Groups, groups));

        var toggle = cut.FindAll("button.source-restrictions-category")
            .Single(button => string.Equals(
                button.QuerySelector("strong")?.TextContent,
                "5e official",
                StringComparison.Ordinal));

        toggle.GetAttribute("aria-checked").Should().Be("false");
        toggle.TextContent.Should().Contain("0 / 1");
    }
}
