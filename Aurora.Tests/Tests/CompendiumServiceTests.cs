namespace Aurora.App.Services
{
    public sealed class ContentDatabaseService
    {
        public string? DatabasePath => null;
    }

    public sealed class CharacterService
    {
        public Task PreloadAsync() => Task.CompletedTask;
    }
}

namespace Aurora.Tests.Tests
{
    using Aurora.App.Services;

    public sealed class CompendiumServiceTests
    {
        [Fact]
        public void FilterAppliesSpellCastingTimeToSpellEntriesOnly()
        {
            var service = new CompendiumService(new ContentDatabaseService(), new CharacterService());
            CompendiumEntryModel[] entries =
            [
                Entry("Bless", "Spell", "1 action"),
                Entry("Healing Word", "Spell", "1 bonus action"),
                Entry("Potion of Healing", "Item", "1 bonus action")
            ];

            IReadOnlyList<CompendiumEntryModel> filtered = service.Filter(
                entries,
                query: null,
                type: "All",
                source: "All",
                spellLevel: "All",
                spellSchool: "All",
                spellClass: "All",
                spellCastingTime: "bonus-action",
                itemRarity: "All",
                itemAttunement: "All",
                creatureType: "All",
                creatureSize: "All",
                creatureChallenge: "All",
                restrictedSources: null);

            filtered.Select(entry => entry.Name).Should().Equal("Healing Word");
        }

        private static CompendiumEntryModel Entry(string name, string type, string castingTime) =>
            new(
                Id: $"ID_TEST_{name.Replace(" ", "_").ToUpperInvariant()}",
                Name: name,
                Type: type,
                Source: "Test Source",
                Summary: string.Empty,
                DescriptionHtml: string.Empty,
                SearchText: name,
                SpellLevel: type == "Spell" ? 1 : null,
                SpellSchool: type == "Spell" ? "Evocation" : string.Empty,
                SpellClasses: type == "Spell" ? ["Cleric"] : [],
                ItemRarity: string.Empty,
                RequiresAttunement: false,
                DisplayWeight: string.Empty,
                DisplayPrice: string.Empty,
                ItemDamage: string.Empty,
                ItemRange: string.Empty,
                ItemProperties: string.Empty,
                CreatureType: string.Empty,
                CreatureSize: string.Empty,
                ChallengeText: string.Empty,
                SpellCastingTime: castingTime,
                SpellRange: string.Empty,
                SpellComponents: string.Empty,
                SpellDuration: string.Empty,
                SpellIsConcentration: false,
                SpellIsRitual: false,
                HasComputedDetail: false);
    }
}
