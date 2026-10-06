using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class ShopCatalogFilterTests
{
    private static ShopItemModel Item(
        string name,
        string category,
        string type = "Item",
        long priceCopper = 0,
        string rarity = "",
        string source = "System Reference Document",
        string subtype = "",
        decimal weight = 0m)
    {
        var (shelf, department) = ShopTaxonomy.Classify(type, category);
        return new ShopItemModel(
            Id: "ID_" + name.ToUpperInvariant().Replace(' ', '_'),
            Name: name,
            Type: type,
            Source: source,
            Category: shelf,
            Department: department,
            Subtype: subtype,
            Rarity: ShopTaxonomy.NormalizeRarity(rarity),
            UnitPriceCopper: priceCopper,
            DisplayWeight: weight == 0 ? "—" : $"{weight} lb.",
            WeightPounds: weight,
            Summary: string.Empty,
            RequiresAttunement: false,
            IsStackable: false,
            IsTemplate: false,
            PriceFollowsBase: false,
            SearchKey: ShopCatalogFilter.BuildSearchKey(name, shelf, department, subtype, rarity, type));
    }

    private static List<ShopItemModel> Stock() =>
    [
        Item("Longsword", "Weapons", "Weapon", 1500, subtype: "Martial Melee", weight: 3),
        Item("Dagger", "Weapons", "Weapon", 200, subtype: "Simple Melee", weight: 1),
        Item("Longbow", "Weapons", "Weapon", 5000, subtype: "Martial Ranged", weight: 2),
        Item("Chain Mail", "Armor", "Armor", 7500, subtype: "Heavy Armor", weight: 55),
        Item("Rope, Hempen (50 feet)", "Adventuring Gear", priceCopper: 100, weight: 10),
        Item("Alchemist’s Supplies", "Tools", priceCopper: 5000, weight: 8),
        Item("Potion of Healing", "Potions", "Magic Item", 5000, "Common", weight: 0.5m),
        Item("Ring of Protection", "Rings", "Magic Item", 0, "Rare"),
        Item("Staff of Power", "Staffs", "Magic Item", 0, "Very rare"),
        Item("Mystery Box", "Curios", "Item", 300, source: "Homebrew"),
    ];

    private static IReadOnlyList<ShopItemModel> Run(ShopFilterState state, long wallet = long.MaxValue) =>
        ShopCatalogFilter.Apply(ShopCatalogFilter.ApplyScopedFilters(Stock(), state, wallet), state);

    [Fact]
    public void NoFilters_ListsEverythingByName()
    {
        var result = Run(new ShopFilterState());

        result.Should().HaveCount(10);
        result.Select(item => item.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Search_RequiresEveryTerm_InAnyOrder_AndIgnoresCase()
    {
        Run(new ShopFilterState { Query = "sword long" }).Select(item => item.Name).Should().Equal("Longsword");
        Run(new ShopFilterState { Query = "LONG" }).Select(item => item.Name).Should().Equal("Longbow", "Longsword");
    }

    [Fact]
    public void Search_MatchesCategoryAndRarityText_NotJustNames()
    {
        Run(new ShopFilterState { Query = "martial ranged" }).Select(item => item.Name).Should().Equal("Longbow");
        Run(new ShopFilterState { Query = "staffs" }).Select(item => item.Name).Should().Equal("Staff of Power");
    }

    [Fact]
    public void Search_FoldsTypographicApostrophes()
    {
        Run(new ShopFilterState { Query = "alchemist's" }).Select(item => item.Name)
            .Should().Equal("Alchemist’s Supplies");
    }

    [Fact]
    public void Department_AndShelf_AndSubtype_NarrowTheList()
    {
        Run(new ShopFilterState { Department = ShopTaxonomy.WeaponsAndArmor }).Should().HaveCount(4);
        Run(new ShopFilterState { Department = ShopTaxonomy.WeaponsAndArmor, Category = "Weapons" }).Should().HaveCount(3);
        Run(new ShopFilterState { Category = "Weapons", Subtype = "Martial Melee" })
            .Select(item => item.Name).Should().Equal("Longsword");
    }

    [Fact]
    public void Rarity_FiltersExactly_AndNormalisesCasingFromTheContent()
    {
        Run(new ShopFilterState { Rarity = "Very Rare" }).Select(item => item.Name).Should().Equal("Staff of Power");
        Run(new ShopFilterState { Rarity = ShopCatalogFilter.MundaneRarity }).Should().HaveCount(7);
    }

    [Fact]
    public void Source_FiltersToOneSource()
    {
        Run(new ShopFilterState { Source = "Homebrew" }).Select(item => item.Name).Should().Equal("Mystery Box");
    }

    [Fact]
    public void PricedOnly_DropsItemsWithNoListedPrice()
    {
        var result = Run(new ShopFilterState { PricedOnly = true });

        result.Should().NotContain(item => item.Name == "Ring of Protection");
        result.Should().OnlyContain(item => item.HasPrice);
    }

    [Fact]
    public void AffordableOnly_KeepsPricedItemsWithinTheWallet()
    {
        var result = Run(new ShopFilterState { AffordableOnly = true }, wallet: 1500);

        result.Select(item => item.Name).Should().BeEquivalentTo(
            "Longsword", "Dagger", "Rope, Hempen (50 feet)", "Mystery Box");
    }

    [Fact]
    public void Sort_ByPrice_PutsUnpricedItemsLastInBothDirections()
    {
        var ascending = Run(new ShopFilterState { Sort = ShopSort.PriceLowToHigh });
        var descending = Run(new ShopFilterState { Sort = ShopSort.PriceHighToLow });

        ascending.Take(3).Select(item => item.UnitPriceCopper).Should().Equal(100, 200, 300);
        ascending.TakeLast(2).Select(item => item.Name).Should().BeEquivalentTo("Ring of Protection", "Staff of Power");
        descending.First().Name.Should().Be("Chain Mail");
        descending.TakeLast(2).Select(item => item.UnitPriceCopper).Should().OnlyContain(price => price == 0);
    }

    [Fact]
    public void Sort_ByRarity_ListsMundaneFirstThenAscendingRarity()
    {
        var result = Run(new ShopFilterState { Sort = ShopSort.Rarity });

        result.Last().Name.Should().Be("Staff of Power");
        result[^2].Name.Should().Be("Ring of Protection");
        result[^3].Name.Should().Be("Potion of Healing");
        result.First().Rarity.Should().BeEmpty();
    }

    [Fact]
    public void Sort_ByWeight_IsAscending()
    {
        Run(new ShopFilterState { Sort = ShopSort.Weight }).Select(item => item.WeightPounds)
            .Should().BeInAscendingOrder();
    }

    [Fact]
    public void BuildDepartments_CountsMatchesAndFollowsShopOrder()
    {
        var departments = ShopCatalogFilter.BuildDepartments(Stock());

        departments.Select(d => d.Name).Should().Equal(
            ShopTaxonomy.WeaponsAndArmor,
            ShopTaxonomy.AdventuringGear,
            ShopTaxonomy.ToolsAndInstruments,
            ShopTaxonomy.PotionsAndScrolls,
            ShopTaxonomy.MagicItems,
            ShopTaxonomy.Other);
        departments[0].Count.Should().Be(4);
        departments[0].Shelves.Select(s => s.Name).Should().Equal("Weapons", "Armor");
        departments[4].Shelves.Select(s => s.Name).Should().Equal("Rings", "Staffs");
    }

    [Fact]
    public void BuildDepartments_ReflectsASearchBecauseTheCallerScopesFirst()
    {
        var scoped = ShopCatalogFilter.ApplyScopedFilters(Stock(), new ShopFilterState { Query = "potion" }, long.MaxValue);

        var departments = ShopCatalogFilter.BuildDepartments(scoped);

        departments.Should().ContainSingle().Which.Name.Should().Be(ShopTaxonomy.PotionsAndScrolls);
    }

    [Fact]
    public void BuildSubtypes_ListsOnlyTheSubtypesOnTheShelf()
    {
        var weapons = Stock().Where(item => item.Category == "Weapons");

        ShopCatalogFilter.BuildSubtypes(weapons).Select(s => s.Name)
            .Should().Equal("Martial Melee", "Martial Ranged", "Simple Melee");
        ShopCatalogFilter.BuildSubtypes(Stock().Where(item => item.Category == "Rings")).Should().BeEmpty();
    }

    [Fact]
    public void BuildRarities_ListsMundaneThenRankOrder()
    {
        ShopCatalogFilter.BuildRarities(Stock()).Should().Equal("Mundane", "Common", "Rare", "Very Rare");
    }

    [Fact]
    public void BuildSources_DeduplicatesBySourceKey()
    {
        ShopCatalogFilter.BuildSources(Stock()).Should().Equal("Homebrew", "System Reference Document");
    }

    // ── Taxonomy ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Item", "Adventuring Gear", "Adventuring Gear", ShopTaxonomy.AdventuringGear)]
    [InlineData("Weapon", "Weapons", "Weapons", ShopTaxonomy.WeaponsAndArmor)]
    [InlineData("Weapon", "", "Weapons", ShopTaxonomy.WeaponsAndArmor)]
    [InlineData("Armor", null, "Armor", ShopTaxonomy.WeaponsAndArmor)]
    [InlineData("Magic Item", "Ammunition", "Ammunition", ShopTaxonomy.WeaponsAndArmor)]
    [InlineData("Magic Item", "Spell Scrolls", "Spell Scrolls", ShopTaxonomy.PotionsAndScrolls)]
    [InlineData("Magic Item", "Wondrous Items", "Wondrous Items", ShopTaxonomy.MagicItems)]
    [InlineData("Item", "Equipment Packs", "Equipment Packs", ShopTaxonomy.AdventuringGear)]
    [InlineData("Item", "tools", "Tools", ShopTaxonomy.ToolsAndInstruments)]
    [InlineData("Item", "Treasure", "Treasure", ShopTaxonomy.Treasure)]
    public void Classify_PlacesKnownCategories(string type, string? category, string shelf, string department)
    {
        ShopTaxonomy.Classify(type, category).Should().Be((shelf, department));
    }

    [Fact]
    public void Classify_KeepsAnUnfamiliarCategoryOnAShelfOfItsOwn()
    {
        ShopTaxonomy.Classify("Item", "Siege Engines").Should().Be(("Siege Engines", ShopTaxonomy.Other));
        ShopTaxonomy.Classify("Magic Item", "Relics").Should().Be(("Relics", ShopTaxonomy.MagicItems));
        ShopTaxonomy.Classify("Item", "").Should().Be(("Miscellaneous", ShopTaxonomy.Other));
    }

    [Fact]
    public void ShelfRank_FollowsLegacyOrder_AndPutsUnknownShelvesLast()
    {
        ShopTaxonomy.ShelfRank("Weapons").Should().BeLessThan(ShopTaxonomy.ShelfRank("Armor"));
        ShopTaxonomy.ShelfRank("Adventuring Gear").Should().BeLessThan(ShopTaxonomy.ShelfRank("Tools"));
        ShopTaxonomy.ShelfRank("Siege Engines").Should().BeGreaterThan(ShopTaxonomy.ShelfRank("Supernatural Gifts"));
    }

    [Theory]
    [InlineData("Weapon", "", new[] { "ID_INTERNAL_WEAPON_CATEGORY_MARTIAL_MELEE", "ID_INTERNAL_DAMAGE_TYPE_SLASHING" }, "Martial Melee")]
    [InlineData("Weapon", "", new[] { "ID_INTERNAL_WEAPON_CATEGORY_SIMPLE_RANGED" }, "Simple Ranged")]
    [InlineData("Weapon", "", new[] { "Martial", "Ranged" }, "Martial Ranged")]
    [InlineData("Weapon", "", new[] { "ID_INTERNAL_WEAPON_CATEGORY_MARTIAL_MELEE", "Simple" }, "")]
    [InlineData("Weapon", "", new string[0], "")]
    [InlineData("Item", "Gaming Set", new string[0], "Gaming Sets")]
    [InlineData("Item", "Tool", new string[0], "")]
    public void ClassifySubtype_ReadsWeaponCategoryFromSupports(string type, string itemType, string[] supports, string expected)
    {
        ShopTaxonomy.ClassifySubtype(type, itemType, supports, null).Should().Be(expected);
    }

    [Theory]
    [InlineData("Light", "", "Light Armor")]
    [InlineData("Medium", "", "Medium Armor")]
    [InlineData("Heavy", "", "Heavy Armor")]
    [InlineData("Shield", "", "Shields")]
    [InlineData("", "Shield", "Shields")]
    [InlineData("", "", "")]
    public void ClassifySubtype_ReadsArmorGroup(string group, string itemType, string expected)
    {
        string[] groups = group.Length == 0 ? Array.Empty<string>() : new[] { group };

        ShopTaxonomy.ClassifySubtype("Armor", itemType, null, groups).Should().Be(expected);
    }

    [Theory]
    [InlineData("Very rare", "Very Rare")]
    [InlineData("  uncommon ", "Uncommon")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("Mythic", "Unknown")]
    [InlineData("Rarity Varies", "Varies")]
    [InlineData("Artificer Infusion", "Infusion")]
    public void NormalizeRarity_UsesOneSpelling(string? raw, string expected)
    {
        ShopTaxonomy.NormalizeRarity(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("Vert Rare", "Very Rare")]
    [InlineData("Lgendary", "Legendary")]
    [InlineData("unommon", "Uncommon")]
    public void NormalizeRarity_ReadsAOneEditTypoAsTheRarityItMeant(string typo, string expected)
    {
        ShopTaxonomy.NormalizeRarity(typo).Should().Be(expected);
        ShopTaxonomy.RarityRank(typo).Should().Be(ShopTaxonomy.RarityRank(expected));
    }

    [Fact]
    public void BuildRarities_NeverOffersATypo_AndItsItemFiltersAsTheRealRarity()
    {
        var stock = new List<ShopItemModel>
        {
            Item("Cloak One", "Wondrous Items", "Magic Item", 0, "Vert Rare"),
            Item("Staff Two", "Staffs", "Magic Item", 0, "Very Rare"),
            Item("Blade Three", "Magic Weapons", "Weapon", 0, "Lgendary"),
            Item("Boots Four", "Wondrous Items", "Magic Item", 0, "unommon"),
            Item("Rod Five", "Rods", "Magic Item", 0, "Mythic"),
        };

        ShopCatalogFilter.BuildRarities(stock).Should().Equal("Uncommon", "Very Rare", "Legendary", "Unknown");

        var state = new ShopFilterState { Rarity = "Very Rare" };
        ShopCatalogFilter.Apply(ShopCatalogFilter.ApplyScopedFilters(stock, state, long.MaxValue), state)
            .Select(item => item.Name).Should().Equal("Cloak One", "Staff Two");
    }

    [Fact]
    public void BuildRarities_GroupsOddValuesIntoVariesInfusionAndUnknownAfterTheRealOnes()
    {
        var stock = new List<ShopItemModel>
        {
            Item("Plain Sword", "Weapons", "Weapon", 1500),
            Item("Ring A", "Rings", "Magic Item", 0, "Rare"),
            Item("Potion B", "Potions", "Magic Item", 0, "Rarity varies by potion type"),
            Item("Wand C", "Wands", "Magic Item", 0, "Rare, Very Rare, or Legendary"),
            Item("Infusion D", "Infusions", "Magic Item", 0, "Artificer Infusion"),
            Item("Infusion E", "Infusions", "Magic Item", 0, "Infusion"),
            Item("Curio F", "Curios", "Magic Item", 0, "Unknown"),
            Item("Relic G", "Relics", "Magic Item", 0, "Mythic"),
        };

        ShopCatalogFilter.BuildRarities(stock).Should().Equal("Mundane", "Rare", "Varies", "Infusion", "Unknown");

        IEnumerable<string> Named(string rarity)
        {
            var state = new ShopFilterState { Rarity = rarity };
            return ShopCatalogFilter.Apply(ShopCatalogFilter.ApplyScopedFilters(stock, state, long.MaxValue), state)
                .Select(item => item.Name);
        }

        Named("Varies").Should().Equal("Potion B", "Wand C");
        Named("Infusion").Should().Equal("Infusion D", "Infusion E");
        Named("Unknown").Should().Equal("Curio F", "Relic G");
    }

    [Fact]
    public void RarityRank_OrdersMundaneFirstThenCommonToArtifact()
    {
        string?[] ladder = ["", "Common", "Uncommon", "Rare", "Very rare", "Legendary", "Artifact", "Mythic"];

        ladder.Select(ShopTaxonomy.RarityRank).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }
}
