using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;
using FluentAssertions;
using Xunit;

namespace Aurora.Tests.Tests;

public sealed class EquipmentNoteDraftTests : BunitContext
{
    [Fact]
    public void Typing_updates_the_draft_without_committing_until_the_field_changes()
    {
        var drafts = new Dictionary<string, string>();
        int inputEvents = 0;
        int commits = 0;
        var component = Render<CharacterEquipmentWorkspace>(parameters => parameters
            .Add(component => component.Model, new EquipmentOverviewModel { EquipmentNotes = "Saved note" })
            .Add(component => component.NoteDrafts, drafts)
            .Add(component => component.OnNoteChanged, (EquipmentNoteChangeModel change) =>
            {
                drafts[change.NoteId] = change.Value;
                inputEvents++;
            })
            .Add(component => component.OnNoteCommitted, () => commits++));

        for (int index = 1; index <= 20; index++)
        {
            component.FindAll("textarea")[0].Input($"Note {index}");
        }

        inputEvents.Should().Be(20);
        commits.Should().Be(0);
        drafts["equipment"].Should().Be("Note 20");
        component.FindAll("textarea")[0].Change("Note 20");
        commits.Should().Be(1);
    }

    [Fact]
    public void Retained_draft_is_shown_instead_of_an_older_saved_note()
    {
        var component = Render<CharacterEquipmentWorkspace>(parameters => parameters
            .Add(component => component.Model, new EquipmentOverviewModel { EquipmentNotes = "Saved note" })
            .Add(component => component.NoteDrafts, new Dictionary<string, string> { ["equipment"] = "Retained draft" })
            .Add(component => component.OnNoteChanged, (EquipmentNoteChangeModel _) => { }));

        component.FindAll("textarea")[0].GetAttribute("value").Should().Be("Retained draft");
    }
}
