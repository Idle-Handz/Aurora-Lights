using Builder.Data;
using Builder.Presentation.Services.Sources;
using Builder.Data.Elements;
using Builder.Presentation.Models.Sources;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class RequiredContentPolicyTests
{
    [Theory]
    [InlineData("Internal", true)]
    [InlineData("Core", true)]
    [InlineData("Aurora Legacy Essentials", true)]
    [InlineData("aurora essentials", true)]
    [InlineData("Players Handbook 2024", false)]
    [InlineData("Dungeon Masters Guide", false)]
    [InlineData("Monster Manual", false)]
    public void InfrastructureIsRequiredButRulebooksRemainSelectable(string name, bool required)
        => RequiredContentPolicy.IsRequiredSource(name).Should().Be(required);

    [Theory]
    [InlineData("Aurora Legacy Essentials", true)]
    [InlineData("Internal", true)]
    [InlineData("Core", true)]
    [InlineData("Player’s Handbook (2024)", false)]
    public void CharacterRestrictionsCannotUncheckInfrastructure(string name, bool required)
    {
        var item = new SourceItem(new Source { ElementHeader = new ElementHeader(name, "Source", name, name) });
        item.SetIsChecked(false, true, true);
        item.AllowUnchecking.Should().Be(!required);
        item.IsChecked.Should().Be(required);
    }
}
