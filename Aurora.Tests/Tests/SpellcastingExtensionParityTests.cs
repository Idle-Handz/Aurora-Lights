using Aurora.Importer;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class SpellcastingExtensionParityTests : IAsyncLifetime
{
    private const string WarlockClassId = "ID_WOTC_PHB_CLASS_WARLOCK";
    private const string FiendPatronId = "ID_WOTC_PHB_ARCHETYPE_OTHERWORLDLY_PATRON_FIEND";
    private const string FireballId = "ID_PHB_SPELL_FIREBALL";

    private readonly ITestOutputHelper _output;
    public SpellcastingExtensionParityTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void SpellcastingExtendText_PreservesMultipleExtensionEntries()
    {
        string? extendText = SpellcastingExtensionText.JoinEntries(
        [
            "ID_PHB_SPELL_BURNING_HANDS",
            "ID_PHB_SPELL_COMMAND",
            "ID_PHB_SPELL_FIREBALL"
        ]);

        SpellcastingExtensionText.SplitEntries(extendText)
            .Should()
            .ContainInOrder("ID_PHB_SPELL_BURNING_HANDS", "ID_PHB_SPELL_COMMAND", FireballId);
    }

    [Fact]
    public async Task FiendWarlock_ExtendedProfileContributesExpandedSpells()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var elements = DataManager.Current.ElementsCollection;
        var warlock = elements.GetElement(WarlockClassId);
        var fiend = elements.GetElement(FiendPatronId);
        if (warlock is null || fiend is null)
        {
            _output.WriteLine("[SKIP] Warlock or Fiend patron not present.");
            return;
        }

        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var cm = CharacterManager.Current;
        await cm.New(initializeFirstLevel: true);
        cm.RegisterElement(warlock);
        cm.RegisterElement(fiend);
        cm.ReprocessCharacter();

        var warlockProfile = cm.GetSpellcastingInformations()
            .FirstOrDefault(info => !info.IsExtension && info.Name.Equals("Warlock", StringComparison.OrdinalIgnoreCase));

        warlockProfile.Should().NotBeNull("the base Warlock spellcasting profile must be active");
        string extensionDump = DumpExtensionExpressions(warlockProfile!);
        _output.WriteLine(extensionDump);

        extensionDump.Should().Contain(FireballId,
            "The Fiend's spellcasting extension should merge Fireball into the active Warlock profile");
    }

    private static string DumpExtensionExpressions(object spellcastingInformation)
    {
        var prop = spellcastingInformation.GetType().GetProperty("ExtendedSupportedSpellsExpressions");
        var values = prop?.GetValue(spellcastingInformation) as System.Collections.IEnumerable;
        if (values is null)
            return string.Empty;

        return string.Join(Environment.NewLine, values.Cast<object>().Select(DumpObject));
    }

    private static string DumpObject(object value)
    {
        var parts = value.GetType()
            .GetProperties()
            .Select(prop =>
            {
                object? propValue;
                try { propValue = prop.GetValue(value); }
                catch { return null; }
                return $"{prop.Name}={propValue}";
            })
            .Where(part => part is not null);

        return $"{value} {string.Join(" ", parts)}";
    }
}
