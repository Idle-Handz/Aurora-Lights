using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class ShopQuoteTests
{
    private static ShopItemModel Item(long price, bool template = false, bool followsBase = false) => new(
        "ID_X", "Thing", "Item", "SRD", "Adventuring Gear", ShopTaxonomy.AdventuringGear, "", "",
        price, "1 lb.", 1m, "", false, false, template, followsBase, "THING");

    private static ShopInventoryEntryModel Owned(long unit, int amount = 1) => new(
        "abc", "ID_X", "Thing", "SRD", "Adventuring Gear", ShopTaxonomy.AdventuringGear, "", "",
        amount, amount > 1, false, unit, "1 lb.", 1m, "THING");

    private static readonly CoinPurse TwentyGold = new(0, 0, 0, 20, 0);

    [Fact]
    public void Purchase_ThatCanBePaidForQuotesTheTotalAndTheCoinsLeft()
    {
        var quote = ShopQuotes.ForPurchase(Item(250), null, 3, null, TwentyGold);

        quote.UnitCopper.Should().Be(250);
        quote.TotalCopper.Should().Be(750);
        quote.CanBuy.Should().BeTrue();
        quote.Problem.Should().BeNull();
        quote.After.TotalCopper.Should().Be(2000 - 750);
    }

    [Fact]
    public void Purchase_TooDear_SaysHowFarShortTheCharacterIs()
    {
        var quote = ShopQuotes.ForPurchase(Item(3000), null, 1, null, TwentyGold);

        quote.CanBuy.Should().BeFalse();
        quote.CanAddFree.Should().BeTrue("adding for free never needs the money");
        quote.ShortCopper.Should().Be(1000);
        quote.Problem.Should().Be("You need 10 gp more.");
        quote.After.Should().Be(TwentyGold);
    }

    [Fact]
    public void Purchase_WithNoListedPrice_AsksForOne_UntilAnOverrideIsGiven()
    {
        var unpriced = ShopQuotes.ForPurchase(Item(0), null, 1, null, TwentyGold);
        unpriced.CanBuy.Should().BeFalse();
        unpriced.CanAddFree.Should().BeTrue();
        unpriced.Problem.Should().Contain("Set a price");

        var overridden = ShopQuotes.ForPurchase(Item(0), null, 1, 500, TwentyGold);
        overridden.CanBuy.Should().BeTrue();
        overridden.TotalCopper.Should().Be(500);
    }

    [Fact]
    public void Purchase_OfATemplate_NeedsABaseBeforeAnythingElse()
    {
        var template = Item(0, template: true);

        var quote = ShopQuotes.ForPurchase(template, null, 1, 500, TwentyGold);

        quote.NeedsBase.Should().BeTrue();
        quote.CanBuy.Should().BeFalse();
        quote.CanAddFree.Should().BeFalse();
        quote.Problem.Should().Be("Choose a base item first.");
    }

    [Fact]
    public void Purchase_OfATemplate_UsesThePriceOfTheChosenBase()
    {
        var template = Item(0, template: true, followsBase: true);
        var chain = new ShopBaseOptionModel("ID_CHAIN", "Chain Mail", 7500);

        var quote = ShopQuotes.ForPurchase(template, chain, 1, null, new CoinPurse(0, 0, 0, 100, 0));

        quote.NeedsBase.Should().BeFalse();
        quote.UnitCopper.Should().Be(7500);
        quote.CanBuy.Should().BeTrue();
        quote.After.TotalCopper.Should().Be(10_000 - 7500);
    }

    [Fact]
    public void Purchase_QuantityBelowOneStillQuotesOne()
    {
        ShopQuotes.ForPurchase(Item(100), null, 0, null, TwentyGold).TotalCopper.Should().Be(100);
    }

    [Fact]
    public void Sale_QuotesTheRateTimesTheValue_AndTheCoinsAfter()
    {
        var quote = ShopQuotes.ForSale(Owned(1500), 1, 50, null, TwentyGold);

        quote.ProceedsCopper.Should().Be(750);
        quote.CanSell.Should().BeTrue();
        quote.After.TotalCopper.Should().Be(2000 + 750);
        quote.Problem.Should().BeNull();
    }

    [Fact]
    public void Quotes_MakeTheirAfterPurseTheWayTheTransactionWill()
    {
        var electrum = new ShopCoinOptions(UseElectrum: true);
        var fiveGold = new CoinPurse(0, 0, 0, 5, 0);

        // A 1 cp item paid with gold; and a 750 cp sale.
        ShopQuotes.ForPurchase(Item(1), null, 1, null, fiveGold).After
            .Should().Be(new CoinPurse(9, 9, 0, 4, 0));
        ShopQuotes.ForPurchase(Item(1), null, 1, null, fiveGold, electrum).After
            .Should().Be(new CoinPurse(9, 4, 1, 4, 0));

        ShopQuotes.ForSale(Owned(1500), 1, 50, null, default).After.Should().Be(new CoinPurse(0, 5, 0, 7, 0));
        ShopQuotes.ForSale(Owned(1500), 1, 50, null, default, electrum).After.Should().Be(new CoinPurse(0, 0, 1, 7, 0));
    }

    [Fact]
    public void Sale_ClampsTheQuantityToWhatIsOwned()
    {
        ShopQuotes.ForSale(Owned(100, amount: 3), 50, 100, null, TwentyGold).ProceedsCopper.Should().Be(300);
    }

    [Fact]
    public void Sale_OfSomethingWorthNothing_AsksForAPrice()
    {
        var quote = ShopQuotes.ForSale(Owned(0), 1, 50, null, TwentyGold);

        quote.CanSell.Should().BeFalse();
        quote.Problem.Should().Contain("Set a price");
        quote.After.Should().Be(TwentyGold);

        ShopQuotes.ForSale(Owned(0), 1, 50, 900, TwentyGold).CanSell.Should().BeTrue();
    }
}
