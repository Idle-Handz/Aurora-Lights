using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Finding #1: registration stamps acquisition onto SHARED DataManager element singletons. These
/// tests verify the fix — PrepareForCharacterLoad now clears element acquisition so stale state from a
/// previously-loaded character no longer bleeds into the next within a single app session.
/// </summary>
public sealed class SharedElementStateTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    public SharedElementStateTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SharedElementAcquisition_IsClearedByPrepareForCharacterLoad()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elements = DataManager.Current.ElementsCollection;
        var multiclass = elements.FirstOrDefault(e => e.Type == "Multiclass");
        if (multiclass is null) { _output.WriteLine("[SKIP] no Multiclass element."); return; }

        // Clean baseline on the shared singleton.
        multiclass.Aquisition = new AquisitionInfo();

        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        await cm.New(initializeFirstLevel: true);

        // Stamp the shared element as if it had been selected (what the multiclass flow does).
        multiclass.Aquisition.SelectedBy(
            new Builder.Data.Rules.SelectRule(multiclass.ElementHeader));
        multiclass.Aquisition.WasSelected.Should().BeTrue("we just stamped it for character A");

        // Prepare for a different character — this must now scrub the stale stamp off the shared element.
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await cm.New(initializeFirstLevel: true);

        _output.WriteLine(
            $"After New() + PrepareForCharacterLoad: WasSelected={multiclass.Aquisition.WasSelected}, " +
            $"WasGranted={multiclass.Aquisition.WasGranted}");

        multiclass.Aquisition.WasSelected.Should().BeFalse(
            "PrepareForCharacterLoad clears shared element acquisition, so character A's stamp does "
            + "not bleed into character B");
    }

    [Fact]
    public async Task LoadCharacter_ClearsStaleAcquisitionOnUnrelatedElements()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var path = ContentFixture.FindFirstCharacterFile();
        if (path is null) { _output.WriteLine("[SKIP] no character file found."); return; }

        // A Multiclass element is very unlikely to be used by a basic test character, so it's a good
        // "unrelated element" probe: stamp it, then load a real character and see if the stamp clears.
        var probe = DataManager.Current.ElementsCollection.FirstOrDefault(e => e.Type == "Multiclass");
        if (probe is null) { _output.WriteLine("[SKIP] no Multiclass element."); return; }

        probe.Aquisition = new AquisitionInfo();
        probe.Aquisition.SelectedBy(new SelectRule(probe.ElementHeader));

        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await new CharacterFile(path).Load();

        _output.WriteLine($"After real Load(): probe.WasSelected={probe.Aquisition.WasSelected}");

        probe.Aquisition.WasSelected.Should().BeFalse(
            "a real character load (via PrepareForCharacterLoad) scrubs stale acquisition left on "
            + "shared elements by a previously-loaded character — no cross-load contamination");
    }
}
