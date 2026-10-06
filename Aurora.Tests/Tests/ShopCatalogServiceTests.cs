using Aurora.App.Services;
using Aurora.Components.Models;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class ShopCatalogServiceTests : IAsyncLifetime
{
    private const string LongswordId = "ID_WOTC_PHB_WEAPON_LONGSWORD";
    private const string ChainMailId = "ID_WOTC_ARMOR_HEAVY_CHAIN_MAIL";
    private const string ShieldId = "ID_WOTC_GEAR_SHIELD";
    private const string ArmorPlusOneId = "ID_WOTC_DMG_MAGIC_ITEM_ARMOR_1";
    private const string PotionOfHealingId = "ID_WOTC_DMG_MAGIC_ITEM_POTION_OF_HEALING";

    private readonly ITestOutputHelper _output;

    public ShopCatalogServiceTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<IReadOnlyList<ShopItemModel>> LoadCatalogAsync()
    {
        using var service = new ShopCatalogService();
        return await service.GetCatalogAsync(BuildSourceRestrictionSnapshot.Empty);
    }

    [Fact]
    public async Task Catalog_ListsInventoryItemsAndLeavesOutTheEnginesHiddenProxies()
    {
        ContentFixture.SkipIfUnavailable(_output);

        var catalog = await LoadCatalogAsync();

        catalog.Should().NotBeEmpty();
        catalog.Should().NotContain(item => item.Name.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase));
        catalog.Should().OnlyContain(item => EquipmentService.IsInventoryItemType(item.Type));
        catalog.Select(item => item.Id).Should().OnlyHaveUniqueItems();

        var hidden = DataManager.Current.ElementsCollection.OfType<Item>().Where(item => item.HideFromInventory).Select(item => item.Id);
        catalog.Select(item => item.Id).Should().NotIntersectWith(hidden);
    }

    [Fact]
    public async Task Catalog_PlacesEveryItemOnAShelfInADepartment()
    {
        ContentFixture.SkipIfUnavailable(_output);

        var catalog = await LoadCatalogAsync();

        catalog.Should().OnlyContain(item => item.Category.Length > 0 && item.Department.Length > 0);
        var departments = ShopCatalogFilter.BuildDepartments(catalog);
        _output.WriteLine(string.Join(Environment.NewLine, departments.Select(d =>
            $"{d.Name} ({d.Count}): " + string.Join(", ", d.Shelves.Select(s => $"{s.Name} {s.Count}")))));

        departments.Select(d => d.Name).Should().Contain(
            [ShopTaxonomy.WeaponsAndArmor, ShopTaxonomy.AdventuringGear, ShopTaxonomy.MagicItems]);
        departments.Single(d => d.Name == ShopTaxonomy.WeaponsAndArmor).Shelves.Select(s => s.Name)
            .Should().Contain(["Weapons", "Armor"]);
    }

    [Fact]
    public async Task Catalog_DescribesAWeapon_FromTheContentsOwnFields()
    {
        ContentFixture.SkipIfUnavailable(_output);

        var longsword = (await LoadCatalogAsync()).Single(item => item.Id == LongswordId);

        longsword.Name.Should().Be("Longsword");
        longsword.Category.Should().Be("Weapons");
        longsword.Department.Should().Be(ShopTaxonomy.WeaponsAndArmor);
        longsword.Subtype.Should().Be("Martial Melee");
        longsword.UnitPriceCopper.Should().Be(1500);
        longsword.WeightPounds.Should().Be(3);
        longsword.Summary.Should().StartWith("1d8 slashing").And.Contain("Versatile");
        longsword.Rarity.Should().BeEmpty();
        longsword.IsTemplate.Should().BeFalse();
    }

    [Fact]
    public async Task Catalog_DescribesArmorAndShields()
    {
        ContentFixture.SkipIfUnavailable(_output);

        var catalog = await LoadCatalogAsync();
        var chainMail = catalog.Single(item => item.Id == ChainMailId);
        var shield = catalog.Single(item => item.Id == ShieldId);

        chainMail.Subtype.Should().Be("Heavy Armor");
        chainMail.UnitPriceCopper.Should().Be(7500);
        chainMail.Summary.Should().Be("AC 16 · Str 13 · Stealth disadvantage");
        shield.Subtype.Should().Be("Shields");
        shield.Summary.Should().StartWith("AC +2");
    }

    [Fact]
    public async Task Catalog_PricesPotionsButNotMostMagicItems()
    {
        ContentFixture.SkipIfUnavailable(_output);

        var catalog = await LoadCatalogAsync();
        var potion = catalog.Single(item => item.Id == PotionOfHealingId);

        potion.Department.Should().Be(ShopTaxonomy.PotionsAndScrolls);
        potion.Rarity.Should().Be("Common");
        potion.UnitPriceCopper.Should().Be(5000);

        var armorPlusOne = catalog.Single(item => item.Id == ArmorPlusOneId);
        armorPlusOne.IsTemplate.Should().BeTrue();
        armorPlusOne.HasPrice.Should().BeFalse();
        armorPlusOne.PriceFollowsBase.Should().BeFalse("it declares its own (zero) cost, which overrides the base item's");
    }

    [Fact]
    public async Task Catalog_NormalisesRarityCasingFromTheContent()
    {
        ContentFixture.SkipIfUnavailable(_output);

        var rarities = (await LoadCatalogAsync()).Select(item => item.Rarity).Distinct().ToList();

        rarities.Should().NotContain("Very rare");
        rarities.Should().OnlyContain(r => r.Length == 0
            || ShopTaxonomy.KnownRarities.Contains(r)
            || RarityRepair.Groups.Contains(r),
            "every rarity is a real one or one of the groups, never a free-form value");
    }

    [Fact]
    public async Task Catalog_HonoursSourceRestrictionsOnEveryRead()
    {
        ContentFixture.SkipIfUnavailable(_output);

        using var service = new ShopCatalogService();
        var all = await service.GetCatalogAsync(BuildSourceRestrictionSnapshot.Empty);
        all.Should().Contain(item => item.Id == LongswordId);

        var restrictedElement = new BuildSourceRestrictionSnapshot(
            new HashSet<string>([LongswordId], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        (await service.GetCatalogAsync(restrictedElement)).Should().NotContain(item => item.Id == LongswordId);

        string source = all.Single(item => item.Id == LongswordId).Source;
        var restrictedSource = new BuildSourceRestrictionSnapshot(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>([source], StringComparer.OrdinalIgnoreCase));
        (await service.GetCatalogAsync(restrictedSource)).Should().NotContain(item => item.Source == source);

        // The cache is not poisoned by a restricted read.
        (await service.GetCatalogAsync(BuildSourceRestrictionSnapshot.Empty)).Count.Should().Be(all.Count);
    }

    [Fact]
    public async Task Catalog_RebuildsAfterTheEngineReplacesItsElements()
    {
        ContentFixture.SkipIfUnavailable(_output);

        const string addedId = "ID_TEST_SHOP_LATE_ITEM";
        using var service = new ShopCatalogService();
        var before = await service.GetCatalogAsync(BuildSourceRestrictionSnapshot.Empty);
        before.Should().NotContain(item => item.Id == addedId);

        var added = new Item
        {
            ElementHeader = new ElementHeader("Late Lantern", "Item", "Test Source", addedId),
            Category = "Adventuring Gear",
            Cost = 5,
            CurrencyAbbreviation = "gp",
        };
        DataManager.Current.ElementsCollection.Add(added);
        try
        {
            var after = await service.GetCatalogAsync(BuildSourceRestrictionSnapshot.Empty);

            after.Should().ContainSingle(item => item.Id == addedId)
                .Which.UnitPriceCopper.Should().Be(500);
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(added);
        }
    }

    [Fact]
    public async Task Catalog_TreatsAnItemWithACostButNoCurrencyAsHavingNoListedPrice()
    {
        ContentFixture.SkipIfUnavailable(_output);

        const string oddId = "ID_TEST_SHOP_NO_CURRENCY";
        var odd = new Item
        {
            ElementHeader = new ElementHeader("Currencyless Trinket", "Item", "Test Source", oddId),
            Category = "Adventuring Gear",
            Cost = 12,
        };
        DataManager.Current.ElementsCollection.Add(odd);
        try
        {
            var model = (await LoadCatalogAsync()).Single(item => item.Id == oddId);

            model.HasPrice.Should().BeFalse();
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(odd);
        }
    }

    [Fact]
    public async Task Catalog_ListsARepeatedElementIdOnce_AsTheDeclarationTheEngineWouldReturn()
    {
        ContentFixture.SkipIfUnavailable(_output);

        // Real content repeats ids (generated spell scrolls do); a purchase can only name one of them.
        const string repeatedId = "ID_TEST_SHOP_REPEATED_ID";
        Item Twin(string source, int cost) => new()
        {
            ElementHeader = new ElementHeader("Twin Lantern", "Item", source, repeatedId),
            Category = "Adventuring Gear",
            Cost = cost,
            CurrencyAbbreviation = "gp",
        };
        var first = Twin("First Source", 5);
        var second = Twin("Second Source", 9);
        DataManager.Current.ElementsCollection.Add(first);
        DataManager.Current.ElementsCollection.Add(second);
        try
        {
            var catalog = await LoadCatalogAsync();

            catalog.Select(item => item.Id).Should().OnlyHaveUniqueItems();
            var listed = catalog.Should().ContainSingle(item => item.Id == repeatedId).Subject;
            listed.Source.Should().Be("First Source");
            listed.UnitPriceCopper.Should().Be(500);
            DataManager.Current.ElementsCollection.GetElement(repeatedId).Should().BeSameAs(first);
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(first);
            DataManager.Current.ElementsCollection.Remove(second);
        }
    }

    [Fact]
    public void Detail_ForAPlainItem_CarriesRulesTextAndStatsButNoBaseOptions()
    {
        ContentFixture.SkipIfUnavailable(_output);

        using var service = new ShopCatalogService();
        var detail = service.GetDetail(LongswordId, new Character());

        detail.Should().NotBeNull();
        detail!.Name.Should().Be("Longsword");
        detail.Stats.Should().Contain(stat => stat.Label == "Damage" && stat.Value == "1d8 slashing");
        detail.Stats.Should().Contain(stat => stat.Label == "Weight" && stat.Value == "3 lb.");
        detail.BaseOptions.Should().BeEmpty();
        (detail.DescriptionHtml.Length + detail.PlainDescription.Length).Should().BeGreaterThan(0);
        service.GetDetail("ID_DOES_NOT_EXIST", new Character()).Should().BeNull();
    }

    [Fact]
    public void Detail_ForAMagicArmorTemplate_ListsCompatibleBasesWithTheirPrices()
    {
        ContentFixture.SkipIfUnavailable(_output);

        using var service = new ShopCatalogService();
        var detail = service.GetDetail(ArmorPlusOneId, new Character());

        detail!.BaseOptions.Should().Contain(option => option.Id == ChainMailId);
        detail.BaseOptions.Should().NotContain(option => option.Id == ShieldId);
        // +1 armor declares a cost of its own, so the base item's price does not add to it.
        detail.BaseOptions.Should().OnlyContain(option => option.UnitPriceCopper == 0);
    }

    [Fact]
    public void Detail_PricesATemplateThatFollowsItsBase_AsBasePlusItsOwnCost()
    {
        ContentFixture.SkipIfUnavailable(_output);

        const string templateId = "ID_TEST_SHOP_PRICED_TEMPLATE";
        var template = new Item
        {
            ElementHeader = new ElementHeader("Gilded Weapon", "Magic Item", "Test Source", templateId),
            Cost = 10,
            CurrencyAbbreviation = "gp",
        };
        template.ElementSetters.Add(new ElementSetters.Setter("weapon", "ID_INTERNAL_WEAPON_CATEGORY_MARTIAL_MELEE"));
        DataManager.Current.ElementsCollection.Add(template);
        try
        {
            using var service = new ShopCatalogService();
            var detail = service.GetDetail(templateId, new Character());
            var model = ShopCatalogService.ToModel(template);

            model.IsTemplate.Should().BeTrue();
            model.PriceFollowsBase.Should().BeTrue();
            detail!.BaseOptions.Single(option => option.Id == LongswordId).UnitPriceCopper
                .Should().Be(1500 + 1000, "a 15 gp longsword plus the template's own 10 gp");
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(template);
        }
    }

    [Fact]
    public void Inventory_ListsOwnedItemsShelvedAndValuedLikeTheCatalog()
    {
        ContentFixture.SkipIfUnavailable(_output);

        var character = new Character();
        EquipmentService.AddItem(character, LongswordId).Should().BeTrue();
        EquipmentService.AddItem(character, ArmorPlusOneId, 1, ChainMailId).Should().BeTrue();

        var entries = ShopCatalogService.GetInventory(character);

        var sword = entries.Single(entry => entry.ElementId == LongswordId);
        sword.Category.Should().Be("Weapons");
        sword.UnitPriceCopper.Should().Be(1500);
        sword.Amount.Should().Be(1);

        var armor = entries.Single(entry => entry.ElementId == ArmorPlusOneId);
        armor.Category.Should().Be("Magic Armor");
        armor.Department.Should().Be(ShopTaxonomy.MagicItems);
        armor.UnitPriceCopper.Should().Be(0, "the +1 armor's own zero cost overrides the base's price");
        armor.Name.Should().Contain("Chain Mail");

        var owned = ShopCatalogService.GetOwnedCounts(character);
        owned[LongswordId].Should().Be(1);
        owned[ArmorPlusOneId].Should().Be(1);
        owned.Should().NotContainKey(ChainMailId);
    }

    [Fact]
    public void Inventory_TellsTwoDaggersApartByTheHandTheyAreIn()
    {
        ContentFixture.SkipIfUnavailable(_output);

        const string daggerId = "ID_WOTC_PHB_WEAPON_DAGGER";
        var character = new Character();
        EquipmentService.AddItem(character, daggerId, 3).Should().BeTrue();
        var rows = character.Inventory.Items.Where(row => row.Item.Id == daggerId).ToList();
        EquipmentService.EquipToSlot(character, GearSlot.MainHand, rows[0].Identifier).Should().BeTrue();
        EquipmentService.EquipToSlot(character, GearSlot.OffHand, rows[1].Identifier).Should().BeTrue();

        var entries = ShopCatalogService.GetInventory(character).ToDictionary(entry => entry.Identifier);

        entries[rows[0].Identifier].Name.Should().Be("Dagger (M)");
        entries[rows[1].Identifier].Name.Should().Be("Dagger (O)");
        entries[rows[2].Identifier].Name.Should().Be("Dagger");
    }

    [Fact]
    public void Purse_ReadsTheCharactersCoins()
    {
        var character = new Character();
        character.Inventory.Coins.Set(1, 2, 3, 4, 5);

        ShopCatalogService.GetPurse(character).Should().Be(new CoinPurse(1, 2, 3, 4, 5));
    }
}
