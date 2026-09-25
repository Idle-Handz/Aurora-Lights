using Aurora.App.Services;
using Builder.Presentation.Models;
using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

/// <summary>
/// A character that loses a saved pick used to be reported in a toast carrying every element id with
/// its full saved path, followed by the startup diagnostics — several hundred characters the user
/// had to scroll past to learn that three proficiencies were gone. The toast now says how much was
/// lost and what it was called; the log keeps the rest.
/// </summary>
public sealed class PartialLoadReportTests
{
    private static CharacterFile.LoadResult Partial(params CharacterLoadValidation.MissingElement[] missing) =>
        new(false, "Saved character elements could not be restored: " + string.Join("; ", missing.Select(m => m.Id)), missing);

    private static CharacterLoadValidation.MissingElement Missing(string id, string path) => new(id, 1, path);

    [Fact]
    public void ItNamesWhatWasLostRatherThanItsIds()
    {
        string message = PartialLoadReport.Describe(Partial(
            Missing("ID_PROFICIENCY_SKILL_ARCANA", "Additional Proficiency, Skill Proficiency (Arcana) > Arcana"),
            Missing("ID_WOTC_DMG_PROFICIENCY_WEAPON_MODERN_FIREARMS_RIFLE",
                "1 > Class > Weapon Proficiency (Martial Weapons) > Weapon Proficiency (Rifle)")));

        message.Should().Contain("2 saved picks could not be restored");
        message.Should().Contain("Arcana").And.Contain("Weapon Proficiency (Rifle)");
        message.Should().NotContain("ID_", "an element id means nothing to the person reading it");
        message.Should().Contain("Console");
        message.Length.Should().BeLessThan(220, "it has to be readable at a glance");
    }

    [Fact]
    public void OneLostPickIsCountedInTheSingular()
    {
        PartialLoadReport.Describe(Partial(Missing("ID_X", "1 > Class > Weapon Proficiency (Laster Pistol)")))
            .Should().Contain("1 saved pick could not be restored")
            .And.Contain("Weapon Proficiency (Laster Pistol)");
    }

    [Fact]
    public void ALongListIsCutShortWithACount()
    {
        var message = PartialLoadReport.Describe(Partial(
            Missing("ID_A", "a > Alpha"), Missing("ID_B", "b > Bravo"),
            Missing("ID_C", "c > Charlie"), Missing("ID_D", "d > Delta"),
            Missing("ID_E", "e > Echo")));

        message.Should().Contain("5 saved picks");
        message.Should().Contain("Alpha, Bravo, Charlie, and 2 more");
        message.Should().NotContain("Echo");
    }

    [Fact]
    public void APathWithNoUsableNameFallsBackToTheCount()
    {
        // The validator writes this when a character's summary records no origin for the pick.
        var message = PartialLoadReport.Describe(Partial(
            Missing("ID_A", "saved character summary (origin unavailable)")));

        message.Should().Contain("1 saved pick could not be restored.");
        message.Should().NotContain("origin unavailable");
    }

    [Fact]
    public void AFailureWithNothingStructuredStillReportsItsMessage()
    {
        PartialLoadReport.Describe(new CharacterFile.LoadResult(false, "Something else went wrong"))
            .Should().Contain("Something else went wrong");
    }
}
