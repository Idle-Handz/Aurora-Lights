using Aurora.Components.Models;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Sources;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// The engine lists sources from a snapshot of the catalog taken when the manager is constructed —
/// for the singleton, whenever something first touches CharacterManager.Current. Touch it before
/// content has loaded and every source is missing for the rest of the session: the Manage tab then
/// renders its Sources section (one group is always created) with nothing in it to switch off.
/// Refresh rebuilds the list in place, keeping the manager so its subscribers stay attached.
/// </summary>
public sealed class SourcesManagerRefreshTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public SourcesManagerRefreshTests(ITestOutputHelper output) => _output = output;

    public Task InitializeAsync() => ContentFixture.EnsureAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void AManagerBuiltBeforeContentLoadedListsNothingUntilItIsRefreshed()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        // Stand in for the singleton being touched during startup: build against an empty catalog.
        var elements = DataManager.Current.ElementsCollection;
        var saved = elements.ToArray();
        SourcesManager early;
        try
        {
            elements.Clear();
            early = new SourcesManager();
        }
        finally
        {
            foreach (var element in saved) elements.Add(element);
        }

        early.SourceItems.Should().BeEmpty("that is the state the Manage tab was showing");
        // One group is created unconditionally, which is why the Sources section still renders.
        early.SourceGroups.Should().NotBeEmpty();
        SourceRestrictionTreeBuilder.Build(SourceRestrictionModelMapper.ToGroupModels(early))
            .Should().BeEmpty("an empty snapshot leaves the tree with nothing to show");

        early.Refresh();

        early.SourceItems.Should().NotBeEmpty("the catalog has loaded since, so the list can be rebuilt");
        SourceRestrictionTreeBuilder.Build(SourceRestrictionModelMapper.ToGroupModels(early))
            .Should().NotBeEmpty();
    }

    [Fact]
    public void RefreshKeepsWhatTheCharacterHasRestricted()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var sources = new SourcesManager();
        var restrictable = sources.SourceGroups
            .Where(group => group.AllowUnchecking)
            .SelectMany(group => group.Sources)
            .FirstOrDefault();
        restrictable.Should().NotBeNull("the catalog must offer something that can be restricted");

        sources.Load([restrictable!.Source.Id]);
        sources.RestrictedSources.Select(item => item.Source.Id).Should().Equal(restrictable.Source.Id);

        sources.Refresh();

        // A content reload must not quietly give a character back a source it had switched off.
        sources.RestrictedSources.Select(item => item.Source.Id).Should().Equal(restrictable.Source.Id);
    }

    [Fact]
    public void RefreshKeepsSubscribersAttached()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var sources = new SourcesManager();
        int applied = 0;
        sources.SourceRestrictionsApplied += (_, _) => applied++;

        sources.Refresh();
        sources.ApplyRestrictions();

        applied.Should().BeGreaterThan(0, "the grant policy listens to this manager and is attached once");
    }
}
