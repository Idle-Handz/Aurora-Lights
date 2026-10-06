using Aurora.App.Services;
using Aurora.Components.Models;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class ShopTransactionServiceTests : IAsyncLifetime
{
    private const string LongswordId = "ID_WOTC_PHB_WEAPON_LONGSWORD";
    private const string DaggerId = "ID_WOTC_PHB_WEAPON_DAGGER";
    private const string ChainMailId = "ID_WOTC_ARMOR_HEAVY_CHAIN_MAIL";
    private const string TorchId = "ID_WOTC_PHB_ITEM_TORCH";
    private const string ArmorPlusOneId = "ID_WOTC_DMG_MAGIC_ITEM_ARMOR_1";
    private const string PotionOfHealingId = "ID_WOTC_DMG_MAGIC_ITEM_POTION_OF_HEALING";

    private readonly ITestOutputHelper _output;

    public ShopTransactionServiceTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private Character CharacterWith(long cp = 0, long sp = 0, long ep = 0, long gp = 0, long pp = 0)
    {
        ContentFixture.SkipIfUnavailable(_output);
        var character = new Character();
        character.Inventory.Coins.Set(cp, sp, ep, gp, pp);
        return character;
    }

    private static CoinPurse Purse(Character character) => ShopCatalogService.GetPurse(character);

    private static ShopPurchaseRequest Buy(string id, int quantity = 1, string? baseId = null, long? price = null) =>
        new(id, baseId, quantity, price, Free: false);

    private static ShopPurchaseRequest Add(string id, int quantity = 1, string? baseId = null) =>
        new(id, baseId, quantity, null, Free: true);

    // ── Buying ────────────────────────────────────────────────────────────────

    [Fact]
    public void Purchase_TakesTheListedPriceFromThePurseAndAddsTheItem()
    {
        var character = CharacterWith(gp: 20);

        var result = ShopTransactionService.Purchase(character, Buy(LongswordId));

        result.Succeeded.Should().BeTrue(result.Message);
        result.AmountCopper.Should().Be(1500);
        result.Message.Should().Contain("Bought").And.Contain("Longsword").And.Contain("15 gp");
        Purse(character).Should().Be(new CoinPurse(0, 0, 0, 5, 0));
        result.Before.Should().Be(new CoinPurse(0, 0, 0, 20, 0));
        result.After.Should().Be(Purse(character));
        character.Inventory.Items.Should().ContainSingle(item => item.Item.Id == LongswordId);
    }

    [Fact]
    public void Purchase_MakesChangeInGoldSilverAndCopper()
    {
        var character = CharacterWith(gp: 5);

        // A 1 cp item bought with gold must not return electrum.
        const string cheapId = "ID_TEST_SHOP_ONE_COPPER";
        var cheap = new Item
        {
            ElementHeader = new ElementHeader("Copper Nail", "Item", "Test Source", cheapId),
            Category = "Adventuring Gear",
            Cost = 1,
            CurrencyAbbreviation = "cp",
        };
        DataManager.Current.ElementsCollection.Add(cheap);
        try
        {
            ShopTransactionService.Purchase(character, Buy(cheapId)).Succeeded.Should().BeTrue();
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(cheap);
        }

        Purse(character).Should().Be(new CoinPurse(Copper: 9, Silver: 9, Electrum: 0, Gold: 4, Platinum: 0));
    }

    [Fact]
    public void Purchase_MakesChangeInElectrum_OnlyWhenTheTableUsesIt()
    {
        const string cheapId = "ID_TEST_SHOP_ONE_COPPER_ELECTRUM";
        var cheap = new Item
        {
            ElementHeader = new ElementHeader("Copper Tack", "Item", "Test Source", cheapId),
            Category = "Adventuring Gear",
            Cost = 1,
            CurrencyAbbreviation = "cp",
        };
        DataManager.Current.ElementsCollection.Add(cheap);
        try
        {
            var ignoring = CharacterWith(gp: 5);
            ShopTransactionService.Purchase(ignoring, Buy(cheapId), new ShopCoinOptions(UseElectrum: false))
                .Succeeded.Should().BeTrue();
            Purse(ignoring).Should().Be(new CoinPurse(Copper: 9, Silver: 9, Electrum: 0, Gold: 4, Platinum: 0));

            var usingElectrum = CharacterWith(gp: 5);
            var result = ShopTransactionService.Purchase(usingElectrum, Buy(cheapId), new ShopCoinOptions(UseElectrum: true));
            result.Succeeded.Should().BeTrue();
            Purse(usingElectrum).Should().Be(new CoinPurse(Copper: 9, Silver: 4, Electrum: 1, Gold: 4, Platinum: 0));
            result.After.Should().Be(Purse(usingElectrum));
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(cheap);
        }
    }

    [Fact]
    public void Purchase_KeepsUnstackableItemsOneToARow_SoEachCanBeEquippedAlone()
    {
        var character = CharacterWith(gp: 10);

        var result = ShopTransactionService.Purchase(character, Buy(DaggerId, quantity: 2));

        result.Succeeded.Should().BeTrue(result.Message);
        Purse(character).Should().Be(new CoinPurse(0, 0, 0, 6, 0));
        var rows = character.Inventory.Items.Where(item => item.Item.Id == DaggerId).ToList();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.Amount == 1);
        rows.Select(row => row.Identifier).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Purchase_MergesStackableItemsIntoOneRow()
    {
        var character = CharacterWith(gp: 10);

        ShopTransactionService.Purchase(character, Buy(TorchId, quantity: 3)).Succeeded.Should().BeTrue();
        ShopTransactionService.Purchase(character, Buy(TorchId, quantity: 4)).Succeeded.Should().BeTrue();

        var torches = character.Inventory.Items.Where(item => item.Item.Id == TorchId).ToList();
        torches.Should().ContainSingle();
        torches[0].Amount.Should().Be(7);
        Purse(character).TotalCopper.Should().Be(1000 - 7);
    }

    [Fact]
    public void Purchase_DoesNotMergeIntoAStackTheCharacterRenamed()
    {
        var character = CharacterWith(gp: 10);
        ShopTransactionService.Purchase(character, Buy(TorchId, quantity: 2)).Succeeded.Should().BeTrue();
        character.Inventory.Items.Single().AlternativeName = "Lantern fuel";

        ShopTransactionService.Purchase(character, Buy(TorchId, quantity: 1)).Succeeded.Should().BeTrue();

        character.Inventory.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Purchase_RefusesWhenTheCharacterCannotAfford_AndChangesNothing()
    {
        var character = CharacterWith(gp: 1);

        var result = ShopTransactionService.Purchase(character, Buy(ChainMailId));

        result.Outcome.Should().Be(ShopOutcome.InsufficientFunds);
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("74 gp short");
        character.Inventory.Items.Should().BeEmpty();
        Purse(character).Should().Be(new CoinPurse(0, 0, 0, 1, 0));
    }

    [Fact]
    public void Purchase_Free_AddsTheItemWithoutTouchingThePurse()
    {
        var character = CharacterWith(gp: 1);

        var result = ShopTransactionService.Purchase(character, Add(ChainMailId));

        result.Succeeded.Should().BeTrue(result.Message);
        result.AmountCopper.Should().Be(0);
        result.Message.Should().StartWith("Added");
        Purse(character).Should().Be(new CoinPurse(0, 0, 0, 1, 0));
        character.Inventory.Items.Should().ContainSingle(item => item.Item.Id == ChainMailId);
    }

    [Fact]
    public void Purchase_RefusesAnItemWithNoListedPriceUnlessAPriceIsGiven()
    {
        var character = CharacterWith(gp: 100);
        const string amuletId = "ID_WOTC_DMG_MAGIC_ITEM_AMULET_OF_HEALTH";
        DataManager.Current.ElementsCollection.GetElement(amuletId).Should().NotBeNull("the SRD ships the Amulet of Health");

        var refused = ShopTransactionService.Purchase(character, Buy(amuletId));
        refused.Outcome.Should().Be(ShopOutcome.NoPrice);
        character.Inventory.Items.Should().BeEmpty();
        Purse(character).TotalCopper.Should().Be(10_000);

        var priced = ShopTransactionService.Purchase(character, Buy(amuletId, price: 2500));
        priced.Succeeded.Should().BeTrue(priced.Message);
        Purse(character).TotalCopper.Should().Be(10_000 - 2500);

        ShopTransactionService.Purchase(character, Buy(amuletId, price: 0)).Outcome
            .Should().Be(ShopOutcome.NoPrice, "a zero price is not a purchase; free adds are explicit");
    }

    [Fact]
    public void Purchase_AnOverridePriceReplacesTheListedPriceForTheWholeLot()
    {
        var character = CharacterWith(gp: 20);

        var result = ShopTransactionService.Purchase(character, Buy(DaggerId, quantity: 2, price: 150));

        result.Succeeded.Should().BeTrue(result.Message);
        result.AmountCopper.Should().Be(150);
        Purse(character).TotalCopper.Should().Be(2000 - 150);
    }

    [Fact]
    public void Purchase_RefusesAnUnknownItem()
    {
        var character = CharacterWith(gp: 20);

        var result = ShopTransactionService.Purchase(character, Buy("ID_DOES_NOT_EXIST"));

        result.Outcome.Should().Be(ShopOutcome.UnknownItem);
        Purse(character).TotalCopper.Should().Be(2000);
    }

    [Fact]
    public void Purchase_ClampsAbsurdQuantities()
    {
        var character = CharacterWith(pp: 100_000);

        var result = ShopTransactionService.Purchase(character, Buy(TorchId, quantity: int.MaxValue));

        result.Succeeded.Should().BeTrue(result.Message);
        result.Quantity.Should().Be(ShopTransactionService.MaxQuantity);
        character.Inventory.Items.Single().Amount.Should().Be(ShopTransactionService.MaxQuantity);
    }

    // ── Magic templates ───────────────────────────────────────────────────────

    [Fact]
    public void Purchase_OfATemplateNeedsABaseItem()
    {
        var character = CharacterWith(gp: 100);

        var result = ShopTransactionService.Purchase(character, Add(ArmorPlusOneId));

        result.Outcome.Should().Be(ShopOutcome.NeedsBaseItem);
        character.Inventory.Items.Should().BeEmpty();
    }

    [Fact]
    public void Purchase_OfATemplate_RefusesABaseItThatDoesNotFit()
    {
        var character = CharacterWith(gp: 100);

        var result = ShopTransactionService.Purchase(character, Add(ArmorPlusOneId, baseId: LongswordId));

        result.Outcome.Should().Be(ShopOutcome.IncompatibleBase);
        character.Inventory.Items.Should().BeEmpty();
    }

    [Fact]
    public void Purchase_OfATemplate_ComposesItOntoTheChosenBase()
    {
        var character = CharacterWith(gp: 100);

        var result = ShopTransactionService.Purchase(character, Buy(ArmorPlusOneId, baseId: ChainMailId, price: 5000));

        result.Succeeded.Should().BeTrue(result.Message);
        var armor = character.Inventory.Items.Should().ContainSingle().Subject;
        armor.Item.Id.Should().Be(ChainMailId);
        armor.AdornerItem.Should().NotBeNull();
        armor.AdornerItem!.Id.Should().Be(ArmorPlusOneId);
        result.Message.Should().Contain("Chain Mail");
        Purse(character).TotalCopper.Should().Be(10_000 - 5000);
    }

    [Fact]
    public void Purchase_OfATemplateThatFollowsItsBase_ChargesBasePlusItsOwnCost()
    {
        var character = CharacterWith(gp: 100);
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
            var result = ShopTransactionService.Purchase(character, Buy(templateId, baseId: LongswordId));

            result.Succeeded.Should().BeTrue(result.Message);
            result.AmountCopper.Should().Be(1500 + 1000);
            Purse(character).TotalCopper.Should().Be(10_000 - 2500);
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(template);
        }
    }

    // ── Selling ───────────────────────────────────────────────────────────────

    [Fact]
    public void Sell_PaysTheRateTimesTheListedPrice_AndRemovesTheItem()
    {
        var character = CharacterWith();
        EquipmentService.AddItem(character, LongswordId).Should().BeTrue();
        string identifier = character.Inventory.Items.Single().Identifier;

        var result = ShopTransactionService.Sell(character, new ShopSaleRequest(identifier, 1, 50, null));

        result.Succeeded.Should().BeTrue(result.Message);
        result.AmountCopper.Should().Be(750);
        result.Message.Should().Contain("Sold").And.Contain("Longsword").And.Contain("7 gp 5 sp");
        Purse(character).Should().Be(new CoinPurse(0, 5, 0, 7, 0));
        character.Inventory.Items.Should().BeEmpty();
    }

    [Fact]
    public void Sell_PaysInElectrum_OnlyWhenTheTableUsesIt()
    {
        var ignoring = CharacterWith();
        EquipmentService.AddItem(ignoring, LongswordId).Should().BeTrue();
        ShopTransactionService.Sell(
            ignoring, new ShopSaleRequest(ignoring.Inventory.Items.Single().Identifier, 1, 50, null))
            .Succeeded.Should().BeTrue();
        Purse(ignoring).Should().Be(new CoinPurse(0, 5, 0, 7, 0));

        var usingElectrum = CharacterWith();
        EquipmentService.AddItem(usingElectrum, LongswordId).Should().BeTrue();
        ShopTransactionService.Sell(
            usingElectrum, new ShopSaleRequest(usingElectrum.Inventory.Items.Single().Identifier, 1, 50, null),
            new ShopCoinOptions(UseElectrum: true))
            .Succeeded.Should().BeTrue();
        Purse(usingElectrum).Should().Be(new CoinPurse(0, 0, 1, 7, 0));
    }

    [Fact]
    public void Sell_PartOfAStackLeavesTheRest()
    {
        var character = CharacterWith(gp: 10);
        ShopTransactionService.Purchase(character, Buy(TorchId, quantity: 10)).Succeeded.Should().BeTrue();
        string identifier = character.Inventory.Items.Single().Identifier;
        long afterBuying = Purse(character).TotalCopper;

        var result = ShopTransactionService.Sell(character, new ShopSaleRequest(identifier, 4, 100, null));

        result.Succeeded.Should().BeTrue(result.Message);
        character.Inventory.Items.Single().Amount.Should().Be(6);
        Purse(character).TotalCopper.Should().Be(afterBuying + 4);
    }

    [Fact]
    public void Sell_AtFullRateReturnsWhatABuyCost()
    {
        var character = CharacterWith(gp: 30);
        long start = Purse(character).TotalCopper;
        ShopTransactionService.Purchase(character, Buy(LongswordId)).Succeeded.Should().BeTrue();
        string identifier = character.Inventory.Items.Single().Identifier;

        ShopTransactionService.Sell(character, new ShopSaleRequest(identifier, 1, 100, null)).Succeeded.Should().BeTrue();

        Purse(character).TotalCopper.Should().Be(start);
    }

    [Fact]
    public void Sell_RefusesAnItemWithNoListedValueUnlessAPriceIsGiven()
    {
        var character = CharacterWith();
        EquipmentService.AddItem(character, ArmorPlusOneId, 1, ChainMailId).Should().BeTrue();
        string identifier = character.Inventory.Items.Single().Identifier;

        var refused = ShopTransactionService.Sell(character, new ShopSaleRequest(identifier, 1, 50, null));
        refused.Outcome.Should().Be(ShopOutcome.NoPrice);
        character.Inventory.Items.Should().ContainSingle();
        Purse(character).TotalCopper.Should().Be(0);

        var sold = ShopTransactionService.Sell(character, new ShopSaleRequest(identifier, 1, 50, 12_000));
        sold.Succeeded.Should().BeTrue(sold.Message);
        Purse(character).Should().Be(new CoinPurse(0, 0, 0, 120, 0));
        character.Inventory.Items.Should().BeEmpty();
    }

    [Fact]
    public void Sell_AnEquippedItemUnequipsItFirst()
    {
        var character = CharacterWith();
        EquipmentService.AddAndEquipToSlot(character, GearSlot.Armor, ChainMailId).Should().BeTrue();
        character.Inventory.EquippedArmor.Should().NotBeNull();
        string identifier = character.Inventory.Items.Single().Identifier;

        var result = ShopTransactionService.Sell(character, new ShopSaleRequest(identifier, 1, 50, null));

        result.Succeeded.Should().BeTrue(result.Message);
        character.Inventory.EquippedArmor.Should().BeNull();
        character.Inventory.Items.Should().BeEmpty();
        Purse(character).TotalCopper.Should().Be(3750);
    }

    [Fact]
    public void Sell_RefusesAnItemThatIsNotOwned()
    {
        var character = CharacterWith();

        var result = ShopTransactionService.Sell(character, new ShopSaleRequest("nope", 1, 50, null));

        result.Outcome.Should().Be(ShopOutcome.NotOwned);
    }

    [Fact]
    public void Sell_ClampsTheQuantityToWhatIsOwned()
    {
        var character = CharacterWith(gp: 10);
        ShopTransactionService.Purchase(character, Buy(TorchId, quantity: 3)).Succeeded.Should().BeTrue();
        string identifier = character.Inventory.Items.Single().Identifier;

        var result = ShopTransactionService.Sell(character, new ShopSaleRequest(identifier, 50, 100, null));

        result.Succeeded.Should().BeTrue(result.Message);
        result.Quantity.Should().Be(3);
        character.Inventory.Items.Should().BeEmpty();
    }

    [Fact]
    public void Transactions_KeepTheCarriedWeightCurrent()
    {
        var character = CharacterWith(gp: 100);

        ShopTransactionService.Purchase(character, Buy(ChainMailId)).Succeeded.Should().BeTrue();
        // The total includes the coins' weight, so compare against the armor's 55 lb.
        decimal withArmor = character.Inventory.EquipmentWeight;
        string identifier = character.Inventory.Items.Single().Identifier;
        ShopTransactionService.Sell(character, new ShopSaleRequest(identifier, 1, 50, null)).Succeeded.Should().BeTrue();

        withArmor.Should().BeGreaterThanOrEqualTo(55);
        character.Inventory.EquipmentWeight.Should().BeLessThan(55);
    }

    [Fact]
    public void Purchase_OfAPriceyPotionWorksEndToEndThroughTheCatalogRules()
    {
        var character = CharacterWith(gp: 60);

        var result = ShopTransactionService.Purchase(character, Buy(PotionOfHealingId, quantity: 1));

        result.Succeeded.Should().BeTrue(result.Message);
        Purse(character).TotalCopper.Should().Be(6000 - 5000);
    }
}
