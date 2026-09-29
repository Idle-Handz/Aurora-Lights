namespace Aurora.Tests.Tests
{
    using Aurora.App.Services;

    public sealed class CompendiumServiceTests
    {
        private static CompendiumService NewService()
        {
            Helpers.TestApplicationContextInstaller.EnsureInstalled();
            return new CompendiumService(new ContentDatabaseService(), new CharacterService());
        }

        /// <summary>
        /// Filter takes thirteen loose filter strings. Naming only what a test varies keeps each
        /// case about one behaviour, and stops a test passing because two adjacent arguments were
        /// transposed.
        /// </summary>
        private static IReadOnlyList<CompendiumEntryModel> Filter(
            CompendiumService service,
            IEnumerable<CompendiumEntryModel> entries,
            string? query = null,
            string? type = "All",
            string? source = "All",
            string? spellLevel = "All",
            string? spellSchool = "All",
            string? spellClass = "All",
            string? spellCastingTime = "All",
            string? itemRarity = "All",
            string? itemAttunement = "All",
            string? creatureType = "All",
            string? creatureSize = "All",
            string? creatureChallenge = "All",
            ISet<string>? restrictedSources = null) =>
            service.Filter(entries, query, type, source, spellLevel, spellSchool, spellClass,
                spellCastingTime, itemRarity, itemAttunement, creatureType, creatureSize,
                creatureChallenge, restrictedSources);

        [Fact]
        public void FilterAppliesSpellCastingTimeToSpellEntriesOnly()
        {
            var service = NewService();
            CompendiumEntryModel[] entries =
            [
                Entry("Bless", "Spell", castingTime: "1 action"),
                Entry("Healing Word", "Spell", castingTime: "1 bonus action"),
                Entry("Potion of Healing", "Item", castingTime: "1 bonus action")
            ];

            Filter(service, entries, spellCastingTime: "bonus-action")
                .Select(entry => entry.Name).Should().Equal("Healing Word");
        }

        /// <summary>
        /// Choosing a facet that only one kind of entry has removes the other kinds rather than
        /// leaving them unfiltered. Changing that silently rewrites every list in the browser.
        /// </summary>
        [Fact]
        public void FilterNarrowsToTheKindEachFacetDescribes()
        {
            var service = NewService();
            CompendiumEntryModel[] entries =
            [
                Entry("Bless", "Spell"),
                Entry("Potion of Healing", "Item", rarity: "Common"),
                Entry("Drake Companion", "Companion", creatureType: "Dragon")
            ];

            Filter(service, entries, spellLevel: "1").Select(e => e.Name).Should().Equal("Bless");
            Filter(service, entries, spellSchool: "Evocation").Select(e => e.Name).Should().Equal("Bless");
            Filter(service, entries, spellClass: "Cleric").Select(e => e.Name).Should().Equal("Bless");
            Filter(service, entries, itemRarity: "Common").Select(e => e.Name).Should().Equal("Potion of Healing");
            Filter(service, entries, creatureType: "Dragon").Select(e => e.Name).Should().Equal("Drake Companion");
        }

        [Fact]
        public void FilterTreatsAllAndBlankAsNoFilterAtAll()
        {
            var service = NewService();
            CompendiumEntryModel[] entries = [Entry("Bless", "Spell"), Entry("Potion", "Item")];

            Filter(service, entries).Should().HaveCount(2);
            Filter(service, entries, type: null, spellLevel: "", spellSchool: "   ").Should().HaveCount(2);
        }

        [Fact]
        public void FilterRemovesEntriesFromRestrictedSources()
        {
            var service = NewService();
            CompendiumEntryModel[] entries =
            [
                Entry("Bless", "Spell", source: "Player's Handbook"),
                Entry("Hex", "Spell", source: "Homebrew Book")
            ];

            Filter(service, entries, restrictedSources: new HashSet<string> { "Homebrew Book" })
                .Select(e => e.Name).Should().Equal("Bless");
        }

        /// <summary>
        /// Content spells the same book with a typographic apostrophe, a straight one, or the
        /// mojibake an older encoding produced. A reader choosing one spelling must still see them.
        /// </summary>
        [Fact]
        public void FilterMatchesASourceAcrossApostropheSpellings()
        {
            var service = NewService();
            CompendiumEntryModel[] entries =
            [
                Entry("Bless", "Spell", source: "Player’s Handbook"),
                Entry("Hex", "Spell", source: "Homebrew Book")
            ];

            Filter(service, entries, source: "Player's Handbook")
                .Select(e => e.Name).Should().Equal("Bless");
        }

        [Fact]
        public void GetSourcesCollapsesApostropheSpellingsIntoOneChoice()
        {
            var service = NewService();
            CompendiumEntryModel[] entries =
            [
                Entry("Bless", "Spell", source: "Player's Handbook"),
                Entry("Aid", "Spell", source: "Player’s Handbook"),
                Entry("Hex", "Spell", source: "Player's Handbook")
            ];

            service.GetSources(entries).Should().Equal("All", "Player's Handbook");
        }

        /// <summary>
        /// The query is normalized before matching, so an entry's search key has to be normalized
        /// the same way. Enrichment used to upper-case it instead, which left a typographic
        /// apostrophe in place and made every possessive spell name unsearchable once enriched.
        /// </summary>
        [Fact]
        public void SearchMatchesEntriesHoldingATypographicApostrophe()
        {
            var service = NewService();
            CompendiumEntryModel[] entries =
            [
                Entry("Tasha’s Hideous Laughter", "Spell", searchText: "Tasha’s Hideous Laughter")
            ];

            Filter(service, entries, query: "Tasha's").Should().HaveCount(1);
            Filter(service, entries, query: "tasha’s hideous").Should().HaveCount(1);
        }

        /// <summary>
        /// Enrichment rebuilds an entry with a with-expression, which does not re-run the search
        /// key's initializer. Pairing the two is the only thing keeping an enriched entry findable.
        /// </summary>
        [Fact]
        public void ReplacingSearchTextKeepsItsKeyNormalized()
        {
            var service = NewService();
            CompendiumEntryModel enriched =
                Entry("Tasha's Hideous Laughter", "Spell")
                    .WithSearchText("Tasha’s Hideous Laughter, Enchantment, Bard");

            enriched.SearchKey.Should().Be(
                CompendiumService.NormalizeSearchKey(enriched.SearchText));
            Filter(service, [enriched], query: "Tasha's").Should().HaveCount(1);
        }

        /// <summary>
        /// Whichever path built an entry, its search text comes from the entry's own fields, so a
        /// companion is findable by its stats and a spell by its casting time without each builder
        /// repeating the list. Five places used to spell it out, and they had already drifted.
        /// </summary>
        [Fact]
        public void SearchTextComesFromTheEntrysOwnFields()
        {
            var service = NewService();
            CompendiumEntryModel drake = Entry("Drake Companion", "Companion", creatureType: "Dragon") with
            {
                CompanionSpeed = "40 ft., fly 80 ft.",
                CompanionSenses = "darkvision 60 ft.",
                ChallengeText = "2"
            };

            CompendiumEntryModel searchable = drake.WithSearchTextFrom("a plain description");

            Filter(service, [searchable], query: "darkvision").Should().HaveCount(1);
            Filter(service, [searchable], query: "fly 80").Should().HaveCount(1);
            Filter(service, [searchable], query: "Dragon").Should().HaveCount(1);
            Filter(service, [searchable], query: "plain description").Should().HaveCount(1);
            Filter(service, [searchable], query: "nothing here").Should().BeEmpty();
        }

        /// <summary>
        /// The prose a caller passes is extra, never a replacement: rebuilding must not drop the
        /// fields, and must not keep stale text from a previous build either.
        /// </summary>
        [Fact]
        public void RebuildingSearchTextReplacesTheProseButKeepsTheFields()
        {
            var service = NewService();
            CompendiumEntryModel first = Entry("Bless", "Spell").WithSearchTextFrom("first description");
            CompendiumEntryModel second = first.WithSearchTextFrom("second description");

            Filter(service, [second], query: "second description").Should().HaveCount(1);
            Filter(service, [second], query: "first description").Should().BeEmpty();
            Filter(service, [second], query: "Evocation").Should().HaveCount(1);
        }

        [Fact]
        public void NormalizeSearchKeyFoldsEveryApostropheSpellingTogether()
        {
            string straight = CompendiumService.NormalizeSearchKey("Player's Handbook");

            CompendiumService.NormalizeSearchKey("Player’s Handbook").Should().Be(straight);
            CompendiumService.NormalizeSearchKey("Player‘s Handbook").Should().Be(straight);
            CompendiumService.NormalizeSearchKey("Playerʼs Handbook").Should().Be(straight);
            CompendiumService.NormalizeSearchKey("  Player's Handbook  ").Should().Be(straight);
            CompendiumService.NormalizeSearchKey(null).Should().BeEmpty();
        }

        [Fact]
        public void GetTypesOffersAllFirst()
        {
            var service = NewService();
            CompendiumEntryModel[] entries = [Entry("Potion", "Item"), Entry("Bless", "Spell")];

            service.GetTypes(entries).Should().StartWith("All");
            service.GetTypes(entries).Should().Contain(["Spell", "Item"]);
        }

        private static CompendiumEntryModel Entry(
            string name,
            string type,
            string castingTime = "",
            string source = "Test Source",
            string rarity = "",
            string creatureType = "",
            string? searchText = null) =>
            new(
                Id: $"ID_TEST_{name.Replace(" ", "_").ToUpperInvariant()}",
                Name: name,
                Type: type,
                Source: source,
                Summary: string.Empty,
                DescriptionHtml: string.Empty,
                SearchText: searchText ?? name,
                SpellLevel: type == "Spell" ? 1 : null,
                SpellSchool: type == "Spell" ? "Evocation" : string.Empty,
                SpellClasses: type == "Spell" ? ["Cleric"] : [],
                ItemRarity: rarity,
                RequiresAttunement: false,
                DisplayWeight: string.Empty,
                DisplayPrice: string.Empty,
                ItemDamage: string.Empty,
                ItemRange: string.Empty,
                ItemProperties: string.Empty,
                CreatureType: creatureType,
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
