using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

/// <summary>
/// Finding #4 groundwork: the app reads spell properties (Level, MagicSchool, IsRitual, …) via
/// <c>dynamic</c> + silent try/catch, which hides binder failures as wrong data. Those can be replaced
/// with a static cast to <see cref="Builder.Data.Elements.Spell"/> — but only safely if every element
/// whose Type == "Spell" is actually a Spell instance. This verifies that invariant against real content.
/// </summary>
public sealed class SpellTypeInvariantTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    public SpellTypeInvariantTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void EverySpellTypedElement_IsASpellInstance()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var spellTyped = DataManager.Current.ElementsCollection
            .Where(e => e.Type == "Spell")
            .ToList();
        spellTyped.Count.Should().BeGreaterThan(0, "the content has spells");

        var notSpell = spellTyped.Where(e => e is not Spell).ToList();
        _output.WriteLine($"Type==\"Spell\" elements: {spellTyped.Count}; not a Spell instance: {notSpell.Count}");
        foreach (var e in notSpell.Take(10))
            _output.WriteLine($"  NOT Spell: {e.Name} [{e.Id}] runtime={e.GetType().FullName}");

        notSpell.Should().BeEmpty(
            "every Type==\"Spell\" element must be a Builder.Data.Elements.Spell instance for a static "
            + "cast to safely replace the dynamic property reads");
    }
}
