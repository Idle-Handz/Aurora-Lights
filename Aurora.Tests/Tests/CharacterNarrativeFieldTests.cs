using Aurora.Components.Shared;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class CharacterNarrativeFieldTests : BunitContext
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Input_publishes_the_final_draft_before_blur_without_requesting_autosave(int lines)
    {
        string draft = "Original";
        int commits = 0;
        var cut = Render<CharacterNarrativeField>(parameters => parameters
            .Add(component => component.Label, "Backstory")
            .Add(component => component.Value, draft)
            .Add(component => component.Lines, lines)
            .Add(component => component.ValueChanged, (string value) => draft = value)
            .Add(component => component.OnCommitted, () => commits++));

        var input = cut.Find(lines == 1 ? "input" : "textarea");
        input.Input("N");
        input.Input("New");
        input.Input("New final text");

        draft.Should().Be("New final text", "an explicit save reads the latest model before blur");
        commits.Should().Be(0, "typing must not perform a full save for each keystroke");
        cut.Find("label").GetAttribute("for").Should().Be(input.Id);

        input.Change("New final text");
        commits.Should().Be(1, "autosave is requested only when the edit is committed");
    }
}
