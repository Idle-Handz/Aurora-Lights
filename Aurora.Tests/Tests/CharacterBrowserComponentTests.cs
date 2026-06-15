using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Aurora.Tests.Tests;

public sealed class CharacterBrowserComponentTests : BunitContext
{
    [Fact]
    public void CharacterBrowser_filters_by_name_and_group()
    {
        CharacterBrowserEntry[] characters =
        [
            Entry("1", "Honesty", "Level 5 Sprite Cleric", group: "Active"),
            Entry("2", "Old Artificer", "Level 3 Human Artificer", group: "Old Characters"),
            Entry("3", "Archivist", "Level 1 Elf Wizard", group: "Archive")
        ];

        var cut = Render<CharacterBrowser>(parameters => parameters
            .Add(p => p.Characters, characters));

        cut.Find("input[type=search]").Input("old");

        cut.Markup.Should().Contain("Old Artificer");
        cut.Markup.Should().Contain("Old Characters");
        cut.Markup.Should().Contain("1 of 3");
        cut.Markup.Should().NotContain("Honesty");
        cut.Markup.Should().NotContain("Archivist");
    }

    [Fact]
    public void CharacterBrowser_exposes_group_and_character_group_actions()
    {
        string? renamedGroup = null;
        CharacterBrowserEntry? editedCharacter = null;
        CharacterBrowserEntry[] characters =
        [
            Entry("1", "Honesty", "Level 5 Sprite Cleric", group: "Active")
        ];

        var cut = Render<CharacterBrowser>(parameters => parameters
            .Add(p => p.Characters, characters)
            .Add(p => p.OnRenameGroup, EventCallback.Factory.Create<string>(this, group => renamedGroup = group))
            .Add(p => p.OnEditGroup, EventCallback.Factory.Create<CharacterBrowserEntry>(this, entry => editedCharacter = entry)));

        cut.Find("button.character-browser-section-action").Click();
        cut.Find("button.character-browser-group-btn").Click();

        renamedGroup.Should().Be("Active");
        editedCharacter.Should().Be(characters[0]);
    }

    [Fact]
    public void CharacterBrowser_respects_create_button_disabled_state()
    {
        bool created = false;

        var cut = Render<CharacterBrowser>(parameters => parameters
            .Add(p => p.Characters, Array.Empty<CharacterBrowserEntry>())
            .Add(p => p.ShowCreateButton, true)
            .Add(p => p.DisableCreateButton, true)
            .Add(p => p.OnCreate, EventCallback.Factory.Create(this, () => created = true)));

        var button = cut.Find("button.character-browser-create");

        button.HasAttribute("disabled").Should().BeTrue();
        created.Should().BeFalse();
    }

    [Fact]
    public void CharacterBrowser_uses_search_placeholder()
    {
        CharacterBrowserEntry[] characters =
        [
            Entry("1", "Honesty", "Level 5 Sprite Cleric")
        ];

        var cut = Render<CharacterBrowser>(parameters => parameters
            .Add(p => p.Characters, characters));

        cut.Find("input[type=search]")
            .GetAttribute("placeholder")
            .Should()
            .Be("Search characters\u2026");
    }

    private static CharacterBrowserEntry Entry(
        string id,
        string displayName,
        string primary,
        string? group = null,
        bool favorite = false) =>
        new(id, displayName, primary, GroupName: group, IsFavorite: favorite);
}
