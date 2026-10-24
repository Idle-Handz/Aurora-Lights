using Aurora.Reflections.Services;
using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class CompanionProgressionTests
{
    [Fact]
    public void ResolvesOwnerAndCreatureFormulaeWithoutRollingDiceOrLosingUnknownContent()
    {
        var values = new Dictionary<string, int>
        {
            ["proficiency"] = 4, ["intelligence:modifier"] = 3, ["level:artificer"] = 9,
            ["artificer:spellcasting:attack"] = 7, ["artificer:spellcasting:dc"] = 15,
            ["companion:ac"] = 17
        };
        string Resolve(string text) => CompanionProgression.ResolveText(text, values, "artificer");
        Resolve("Dex +1 plus PB, Perception +0 plus PB × 2").Should().Be("Dex +5, Perception +8");
        Resolve("passive Perception 10 plus PB×2").Should().Be("passive Perception 18");
        Resolve("your spell attack modifier to hit. Hit: 1d8 + PB force damage.")
            .Should().Be("+7 to hit. Hit: 1d8 + 4 force damage.");
        Resolve("your spell save DC; 2d8 plus PB; 1d4 + your Intelligence modifier")
            .Should().Be("DC 15; 2d8 + 4; 1d4 + 3");
        Resolve("2 + your Intelligence modifier + five times your artificer level").Should().Be("50");
        Resolve("{{companion:ac}}; $(companion:ac); {{homebrew:missing}}")
            .Should().Be("17; 17; {{homebrew:missing}}");
    }

    [Fact]
    public void BeastMasterBonusesUpdateAttackDamageAndAverageWithoutCompounding()
    {
        const string text = "Melee Weapon Attack: +4 to hit. Hit: 7 (2d4 + 2) piercing damage.";
        CompanionProgression.ApplyAttackBonuses(text, 3, 3)
            .Should().Be("Melee Weapon Attack: +7 to hit. Hit: 10 (2d4 + 5) piercing damage.");
        CompanionProgression.ApplyAttackBonuses("Hit: 1d6 slashing damage.", 0, 4)
            .Should().Be("Hit: 1d6 + 4 slashing damage.");
    }

    [Fact]
    public void LegacyBeastMasterProficientBonusesFollowOwnerWithoutDoublingDeclaredBonuses()
    {
        CompanionProgression.ApplyProficientBonus("Perception +3, Stealth +4", "Perception +3, Stealth +4", 4)
            .Should().Be("Perception +7, Stealth +8");
        CompanionProgression.ApplyProficientBonus("Perception +8", "Perception +3", 4).Should().Be("Perception +8");
    }

    [Fact]
    public void FeatureOverridesAndCustomAdditionsStayIndependentOfLevelDefaults()
    {
        CompanionSnapshot Snapshot(string feature) => new("Drake", "Dragon", "16", "20", "40", "+1", 2, [], [], [], [])
        {
            InstanceId = "companion-instance:test", TemplateId = "drake",
            DefaultFields = new Dictionary<string, string> { ["features"] = feature, ["notes"] = "", ["custom-additions"] = "" }
        };
        var profile = SidekickService.MergeCompanion(Snapshot("Small drake"), null);
        profile.Set("notes", "Likes silver coins");
        profile.Set("custom-additions", "Can speak Common");
        profile = SidekickService.MergeCompanion(Snapshot("Flying drake"), profile);
        profile.Get("features").Should().Be("Flying drake");
        profile.Get("notes").Should().Be("Likes silver coins");
        profile.Get("custom-additions").Should().Be("Can speak Common");
        profile.Set("features", "Custom creature features");
        profile = SidekickService.MergeCompanion(Snapshot("Large flying drake"), profile);
        profile.Get("features").Should().Be("Custom creature features");
        profile.LevelDefaults["features"].Should().Be("Large flying drake");
    }
}

public sealed class CreaturePortraitTests
{
    internal const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl1sAAAAASUVORK5CYII=";
    [Fact]
    public async Task StoresAValidatedPortablePortraitAndRejectsMarkupAndOversizedFiles()
    {
        await using var stream = new MemoryStream(Convert.FromBase64String(Png));
        string portrait = await CreaturePortrait.ReadAsync(stream);
        CreaturePortrait.DisplaySource(portrait).Should().Be("data:image/png;base64," + Png);
        CreaturePortrait.DisplaySource("https://example.com/image.png").Should().BeNull();
        CreaturePortrait.DisplaySource("data:image/svg+xml;base64," + Convert.ToBase64String("<svg onload='alert(1)'/>"u8.ToArray())).Should().BeNull();
        await using var invalid = new MemoryStream("<html>not an image</html>"u8.ToArray());
        await FluentActions.Awaiting(() => CreaturePortrait.ReadAsync(invalid)).Should().ThrowAsync<InvalidDataException>();
        await using var tooLarge = new MemoryStream(new byte[CreaturePortrait.MaximumBytes + 1]);
        await FluentActions.Awaiting(() => CreaturePortrait.ReadAsync(tooLarge)).Should().ThrowAsync<InvalidDataException>().WithMessage("*5 MB*");
    }

    [Fact]
    public void DraftPortraitChangesDoNotChangeTheOriginalBeforeSaving()
    {
        var profile = SidekickService.Create("sidekick-template:expert");
        profile.Set("portrait", "data:image/png;base64," + Png);
        var draft = profile.Copy();
        draft.Set("portrait", "");
        profile.Get("portrait").Should().NotBeEmpty();
        CreatureManagementService.Model(profile).PortraitSource.Should().Be(profile.Get("portrait"));
    }
}

public sealed class CreaturePortraitAndNotesCardTests : BunitContext
{
    [Fact]
    public void PortraitAndNotesArePerCreatureAndNotesWaitForSave()
    {
        string? saved = null;
        int editCount = 0;
        var model = new CreatureManagementModel { Id = "ember", Name = "Ember", Notes = "Original", PortraitSource = "data:image/png;base64," + CreaturePortraitTests.Png };
        var cut = Render<CreatureManagementCard>(p => p.Add(c => c.Model, model)
            .Add(c => c.OnSaveNotes, (string notes) => saved = notes).Add(c => c.OnEdit, () => editCount++));
        cut.Find("img").GetAttribute("alt").Should().Be("Portrait of Ember");
        cut.FindAll("button").Single(b => b.TextContent == "Change portrait").Click();
        editCount.Should().Be(1);
        cut.Find("textarea").Input("Remember <b>food</b>");
        saved.Should().BeNull();
        cut.Find(".creature-save-notes").Click();
        saved.Should().Be("Remember <b>food</b>");
        cut.Render(p => p.Add(c => c.Busy, true));
        cut.Find("textarea").GetAttribute("value").Should().Be("Remember <b>food</b>", "unrelated combat refreshes must not discard a notes draft");
        cut.Find("textarea").HasAttribute("disabled").Should().BeTrue();
        cut.Render(p => p.Add(c => c.Model, new CreatureManagementModel { Id = "rook", Notes = "Other notes" }));
        cut.Find("textarea").GetAttribute("value").Should().Be("Other notes");
    }
}
