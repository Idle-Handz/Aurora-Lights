using Aurora.Components.Models;
using Aurora.Web.Services;

namespace Aurora.Tests.Tests;

public sealed class WebMagicStatePersistenceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"web-magic-{Guid.NewGuid():N}.dnd5e");

    [Theory]
    [InlineData(0, 1, 4, 1)]
    [InlineData(1, 1, 4, 0)]
    [InlineData(0, 4, 4, 4)]
    [InlineData(4, 4, 4, 3)]
    [InlineData(4, 2, 4, 2)]
    [InlineData(5, 1, 0, 0)]
    [InlineData(0, 0, 4, 0)]
    [InlineData(0, 5, 4, 0)]
    public void SlotsUseOneBasedButtonsAndRoundTripWithinCapacity(int used, int clicked, int total, int expected)
    {
        File.WriteAllText(_path, "<character/>");
        var model = Model(total);
        model.SpellLevels[0].UsedSlots = used;
        WebMagicStatePersistence.ToggleSlot(model, 1, clicked);
        WebMagicStatePersistence.Save(_path, model);
        var loaded = Model(total);
        WebMagicStatePersistence.Load(_path, loaded);
        loaded.SpellLevels[0].UsedSlots.Should().Be(expected);
    }

    [Theory]
    [InlineData(-8, 0)]
    [InlineData(20, 4)]
    public void RestoredInvalidCountsAreClampedBeforeTheyCanBeSaved(int saved, int expected)
    {
        File.WriteAllText(_path, $"<character><session><spellslots><slot level='1' used='{saved}'/></spellslots></session></character>");
        var model = Model();
        WebMagicStatePersistence.Load(_path, model);
        model.SpellLevels[0].UsedSlots.Should().Be(expected);
        WebMagicStatePersistence.Save(_path, model);
        var loaded = Model();
        WebMagicStatePersistence.Load(_path, loaded);
        loaded.SpellLevels[0].UsedSlots.Should().Be(expected);
    }

    [Fact]
    public void SameSpellInIndependentProfilesRetainsSeparatePreparationThroughSaveCopyAndReload()
    {
        File.WriteAllText(_path, "<character/>");
        var model = Model();
        model.Sections[0].SpellLevels[0].Spells[0].IsPrepared = true;
        model.Sections[1].SpellLevels[0].Spells[0].IsPrepared = false;
        WebMagicStatePersistence.Save(_path, model);
        var restored = Model();
        WebMagicStatePersistence.Load(_path, restored);
        restored.Sections[0].PreparedCount.Should().Be(1);
        restored.Sections[1].PreparedCount.Should().Be(0);
        var rebuilt = Model();
        WebMagicStatePersistence.Copy(restored, rebuilt);
        rebuilt.Sections[0].SpellLevels[0].Spells[0].IsPrepared.Should().BeTrue();
        rebuilt.Sections[1].SpellLevels[0].Spells[0].IsPrepared.Should().BeFalse();
        rebuilt.Sections[0].SpellLevels[0].Spells[0].IsPrepared = false;
        rebuilt.Sections[1].SpellLevels[0].Spells[0].IsPrepared = true;
        WebMagicStatePersistence.Save(_path, rebuilt);
        WebMagicStatePersistence.Load(_path, restored);
        restored.Sections[0].PreparedCount.Should().Be(0);
        restored.Sections[1].PreparedCount.Should().Be(1);
    }

    [Fact]
    public void MissingSourceInOldFilesOnlyMatchesAnUnambiguousProfile()
    {
        File.WriteAllText(_path, "<character><build><magic><spellcasting name='Wizard'><spells><spell id='ID_SHARED' prepared='true'/></spells></spellcasting></magic></build></character>");
        var model = Model();
        model.Sections[1].Label = "Wizard";
        WebMagicStatePersistence.Load(_path, model);
        model.Sections.Should().OnlyContain(section => section.PreparedCount == 0);
        model.Sections = [model.Sections[0]];
        WebMagicStatePersistence.Load(_path, model);
        model.Sections[0].PreparedCount.Should().Be(1);
    }

    private static MagicOverviewModel Model(int total = 4) => new()
    {
        HasSpellcasting = true,
        Sections = new[] { ("WIZARD|Wizard", "Wizard", "Intelligence"), ("CLERIC|Cleric", "Cleric", "Wisdom") }
            .Select(profile => new MagicSpellcastingSectionModel
            {
                Id = profile.Item1, Label = profile.Item2, SpellcastingAbility = profile.Item3,
                IsPreparedCaster = true, MaxPrepared = 2,
                SpellLevels = [new MagicSpellLevelModel(1, [new("ID_SHARED", "Shared Spell", false, false)], total, 0)]
            }).ToList()
    };

    public void Dispose() => File.Delete(_path);
}
