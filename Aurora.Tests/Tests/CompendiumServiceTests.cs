namespace Aurora.Tests.Tests
{
    using Aurora.App.Services;
    using Aurora.Components.Models;

    public sealed class CompendiumServiceTests
    {
        // Element loading lives on the DataManager singleton, so every service in the process has
        // to share the one CharacterService that owns it; a second one would try to initialize
        // global state that is already loaded. The caches under test are per-instance, so each test
        // still gets its own CompendiumService and cannot see another test's invalidations.
        private static readonly ContentDatabaseService SharedContentDb = new();
        private static readonly CharacterService SharedCharacters = new();

        private static bool _warmed;

        private static CompendiumService NewService()
        {
            Helpers.TestApplicationContextInstaller.EnsureInstalled();
            if (!_warmed)
            {
                _warmed = true;
                // When a content database is absent but an earlier test already populated the
                // element singleton, the first load reports that it kept the working elements and
                // throws to say so. It marks itself initialized first, so the next call succeeds -
                // absorb that one report rather than letting test order decide who receives it.
                try { SharedCharacters.PreloadAsync().GetAwaiter().GetResult(); }
                catch (InvalidDataException) { }
            }

            return new CompendiumService(SharedContentDb, SharedCharacters);
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
            CompendiumFilter.Filter(entries, query, type, source, spellLevel, spellSchool, spellClass,
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

            CompendiumFilter.GetSources(entries).Should().Equal("All", "Player's Handbook");
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
                CompendiumFilter.NormalizeSearchKey(enriched.SearchText));
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
            string straight = CompendiumFilter.NormalizeSearchKey("Player's Handbook");

            CompendiumFilter.NormalizeSearchKey("Player’s Handbook").Should().Be(straight);
            CompendiumFilter.NormalizeSearchKey("Player‘s Handbook").Should().Be(straight);
            CompendiumFilter.NormalizeSearchKey("Playerʼs Handbook").Should().Be(straight);
            CompendiumFilter.NormalizeSearchKey("  Player's Handbook  ").Should().Be(straight);
            CompendiumFilter.NormalizeSearchKey(null).Should().BeEmpty();
        }

        /// <summary>
        /// The catalog is cached, so a refresh has to be able to throw it away. This is the shape of
        /// bug that bit the character graph: state that outlived the content it was built from.
        /// </summary>
        [Fact]
        public async Task TheCatalogIsReusedUntilSomethingInvalidatesIt()
        {
            var service = NewService();

            IReadOnlyList<CompendiumEntryModel> first = await service.BuildCatalogAsync();
            IReadOnlyList<CompendiumEntryModel> again = await service.BuildCatalogAsync();
            again.Should().BeSameAs(first, "a second read must not rebuild the catalog");

            service.InvalidateCache(rebuildInBackground: false);
            IReadOnlyList<CompendiumEntryModel> afterRefresh = await service.BuildCatalogAsync();
            afterRefresh.Should().NotBeSameAs(first, "invalidating must discard the catalog, not reuse it");
        }

        [Fact]
        public async Task ConcurrentReadersShareOneBuild()
        {
            var service = NewService();

            IReadOnlyList<CompendiumEntryModel>[] results = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => service.BuildCatalogAsync()));

            results.Should().AllBeEquivalentTo(results[0]);
            results.Distinct().Should().ContainSingle("eight readers must not each build their own catalog");
        }

        /// <summary>
        /// A build that began before a refresh must not install its result afterwards - that is a
        /// catalog describing content the app no longer has, which is the shape of the bug that bit
        /// the character graph. Both outcomes hand back a fresh list, so the only way to see the
        /// difference is to count assemblies: a discarded build is repeated, an installed one is not.
        /// </summary>
        [Fact]
        public async Task ABuildRunningAcrossARefreshIsDiscardedAndRepeated()
        {
            var service = NewService();
            await service.BuildCatalogAsync();

            service.InvalidateCache(rebuildInBackground: false);
            int before = service.CatalogBuildCount;

            // Refresh repeatedly while the build runs rather than at one instant: a warm build
            // takes a couple of hundred milliseconds, and a single well-timed invalidation is a
            // race the test would lose as often as it won.
            Task<IReadOnlyList<CompendiumEntryModel>> inFlight = service.BuildCatalogAsync();
            DateTime until = DateTime.UtcNow.AddMilliseconds(60);
            while (DateTime.UtcNow < until)
            {
                service.InvalidateCache(rebuildInBackground: false);
                await Task.Delay(2);
            }
            await inFlight;

            service.CatalogBuildCount.Should().BeGreaterThan(before + 1,
                "a build spanning a refresh must be thrown away and run again, not installed");
            (await service.BuildCatalogAsync()).Should().NotBeNull();
        }

        /// <summary>
        /// Enriched details are cached separately and describe the same content, so they have to go
        /// when the catalog does. A detail kept across a refresh is the character-graph bug again.
        /// </summary>
        [Fact]
        public async Task EnrichedDetailIsRecomputedAfterARefresh()
        {
            var service = NewService();
            IReadOnlyList<CompendiumEntryModel> catalog = await service.BuildCatalogAsync();
            CompendiumEntryModel subject = catalog.First(entry => !entry.HasComputedDetail);

            CompendiumEntryModel enriched = await service.EnrichEntryAsync(subject);
            CompendiumEntryModel cached = await service.EnrichEntryAsync(subject);
            cached.Should().BeSameAs(enriched, "a second read must come from the detail cache");

            service.InvalidateCache(rebuildInBackground: false);
            CompendiumEntryModel afterRefresh = await service.EnrichEntryAsync(subject);
            afterRefresh.Should().NotBeSameAs(enriched, "invalidating must clear cached detail too");
        }

        [Fact]
        public void GetTypesOffersAllFirst()
        {
            var service = NewService();
            CompendiumEntryModel[] entries = [Entry("Potion", "Item"), Entry("Bless", "Spell")];

            CompendiumFilter.GetTypes(entries).Should().StartWith("All");
            CompendiumFilter.GetTypes(entries).Should().Contain(["Spell", "Item"]);
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
