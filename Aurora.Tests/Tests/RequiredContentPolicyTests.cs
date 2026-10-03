using Builder.Data;
using Builder.Presentation.Services.Sources;
using Builder.Data.Elements;
using Builder.Presentation.Models.Sources;
using Aurora.Components.Models;
using Aurora.Tests.Helpers;
using Builder.Presentation.Services.Data;

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

    [Theory]
    [InlineData("Renamed Essentials", "ID_SOURCE_AURORA_LEGACY_ESSENTIALS")]
    [InlineData(" Aurora Essentials ", "ID_TEST_ESSENTIALS")]
    [InlineData(" Internal ", "ID_TEST_INTERNAL")]
    [InlineData("CORE", "ID_TEST_CORE")]
    public void RequiredSourcesCannotBeUnlockedOrSetIndeterminate(string name, string id)
    {
        var item = new SourceItem(MakeSource(name, id));
        item.AllowUnchecking = true;
        item.IsChecked = false;
        item.IsChecked.Should().BeTrue();
        item.IsChecked = null;
        item.AllowUnchecking.Should().BeFalse();
        item.IsChecked.Should().BeTrue();
    }

    [Fact]
    public void BulkDisableKeepsRequiredChildrenAndReportsMixedState()
    {
        var group = new SourcesGroup("Mixed sources");
        var required = new SourceItem(MakeSource("Aurora Legacy Essentials", "ID_ESSENTIALS"));
        var optional = new SourceItem(MakeSource("Player's Handbook", "ID_PHB"));
        group.Sources.Add(required);
        group.Sources.Add(optional);
        required.SetParent(group);
        optional.SetParent(group);
        group.IsChecked = true;

        group.IsChecked = false;

        required.IsChecked.Should().BeTrue();
        optional.IsChecked.Should().BeFalse();
        group.IsChecked.Should().BeNull();
        group.Underline.Should().Be("1/2 Sources Included");
        group.IsChecked = true;
        group.Sources.Should().OnlyContain(item => item.IsChecked == true);
    }

    [Fact]
    public void LockedGroupsEnforceEnabledChildrenEvenThroughDirectCalls()
    {
        var group = new SourcesGroup("Required builder sources", allowUnchecking: false);
        var item = new SourceItem(MakeSource("Infrastructure", "ID_INFRA"));
        item.IsChecked = false;
        group.Sources.Add(item);
        item.SetParent(group);
        item.IsChecked.Should().BeTrue();

        item.AllowUnchecking = true;
        item.IsChecked = false;
        group.IsChecked = false;
        group.IsChecked = null;

        item.AllowUnchecking.Should().BeFalse();
        item.IsChecked.Should().BeTrue();
        group.IsChecked.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettingsExposeAllRequiredSourcesAndIgnoreStaleRestrictions(bool declareSystemSources)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var catalog = DataManager.Current.ElementsCollection;
        var originalCatalog = catalog.ToList();
        var settings = Builder.Presentation.ApplicationContext.Current.Settings;
        string originalDefaults = settings.DefaultSourceRestrictions;
        try
        {
            catalog.Clear();
            catalog.Add(MakeSource("Aurora Legacy Essentials", "ID_SOURCE_AURORA_LEGACY_ESSENTIALS"));
            catalog.Add(MakeSource("Aurora Essentials", "ID_ESSENTIALS_ALIAS"));
            if (declareSystemSources)
            {
                catalog.Add(MakeSource("Internal", "ID_INTERNAL"));
                catalog.Add(MakeSource("Core", "ID_CORE"));
            }
            catalog.Add(new ElementBase { ElementHeader = new ElementHeader("Infrastructure", "Internal", "Internal", "ID_SYSTEM") });
            foreach (string name in new[] { "Player's Handbook", "Dungeon Master's Guide", "Monster Manual" })
                catalog.Add(MakeSource(name, name, rulebook: true));

            var manager = new SourcesManager();
            var required = manager.SourceGroups.Single(group => group.Name == SourcesManager.RequiredGroupName);
            required.AllowUnchecking.Should().BeFalse();
            required.Sources.Select(item => item.Source.Name).Should().BeEquivalentTo(
                "Aurora Legacy Essentials", "Aurora Essentials", "Internal", "Core");

            // Old character/default restrictions and bulk toggles must never disable infrastructure.
            settings.DefaultSourceRestrictions = string.Join(",", manager.SourceItems.Select(item => item.Source.Id));
            manager.LoadDefaults();
            foreach (var group in manager.SourceGroups) group.IsChecked = false;
            manager.ApplyRestrictions();
            manager.StoreDefaults();

            required.IsChecked.Should().BeTrue();
            required.Sources.Should().OnlyContain(item => item.IsChecked == true && !item.AllowUnchecking);
            manager.RestrictedSources.Select(item => item.Source.Name).Should().BeEquivalentTo(
                "Player's Handbook", "Dungeon Master's Guide", "Monster Manual");
            settings.DefaultSourceRestrictions.Split(',').Should().BeEquivalentTo(
                "Player's Handbook", "Dungeon Master's Guide", "Monster Manual");
            var visible = SourceRestrictionModelMapper.ToGroupModels(manager)
                .Single(group => group.Name == SourcesManager.RequiredGroupName);
            visible.IsChecked.Should().BeTrue();
            visible.Sources.Should().HaveCount(4).And.OnlyContain(item => item.IsChecked == true && !item.AllowUnchecking);

            manager.ClearRestrictions();
            manager.RestrictedSources.Should().BeEmpty();
            manager.SourceGroups.SelectMany(group => group.Sources).Should().OnlyContain(item => item.IsChecked == true);
        }
        finally
        {
            catalog.Clear();
            catalog.AddRange(originalCatalog);
            settings.DefaultSourceRestrictions = originalDefaults;
        }
    }

    private static Source MakeSource(string name, string id, bool rulebook = false) => new()
    {
        ElementHeader = new ElementHeader(name, "Source", "Core", id),
        IsOfficialContent = rulebook,
        IsCoreContent = rulebook
    };
}
