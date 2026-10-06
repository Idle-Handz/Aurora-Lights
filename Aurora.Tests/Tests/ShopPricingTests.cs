using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class ShopPricingTests
{
    [Theory]
    [InlineData(5, "cp", 5L)]
    [InlineData(5, "SP", 50L)]
    [InlineData(5, "ep", 250L)]
    [InlineData(5, "gp", 500L)]
    [InlineData(5, "pp", 5000L)]
    [InlineData(2, "gold", 200L)]
    [InlineData(2, " silver ", 20L)]
    public void ToCopper_ConvertsEachDenomination(int cost, string currency, long expected)
    {
        ShopPricing.ToCopper(cost, currency).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "gp")]
    [InlineData(-3, "gp")]
    [InlineData(10, null)]
    [InlineData(10, "")]
    [InlineData(10, "zorkmids")]
    public void ToCopper_IsZeroWhenThereIsNoUsablePrice(int cost, string? currency)
    {
        ShopPricing.ToCopper(cost, currency).Should().Be(0);
    }

    [Fact]
    public void UnitPrice_IsTheBaseItemsCostForAPlainItem()
    {
        ShopPricing.UnitPrice(1500).Should().Be(1500);
    }

    [Fact]
    public void UnitPrice_AddsAMagicTemplatesCostToTheBase()
    {
        ShopPricing.UnitPrice(baseCopper: 1500, adornerCopper: 50_000).Should().Be(51_500);
    }

    [Fact]
    public void UnitPrice_LetsAnOverridingTemplateCarryTheWholePrice()
    {
        ShopPricing.UnitPrice(1500, adornerCopper: 0, adornerOverridesCost: true).Should().Be(0);
        ShopPricing.UnitPrice(1500, adornerCopper: 50_000, adornerOverridesCost: true).Should().Be(50_000);
    }

    [Fact]
    public void PurchaseTotal_MultipliesByQuantityUnlessOverridden()
    {
        ShopPricing.PurchaseTotal(unitCopper: 25, quantity: 4).Should().Be(100);
        ShopPricing.PurchaseTotal(25, 4, overrideTotalCopper: 60).Should().Be(60);
        ShopPricing.PurchaseTotal(25, 0).Should().Be(25, "a quantity below one still buys one");
    }

    [Theory]
    [InlineData(100L, 1, 50, 50L)]
    [InlineData(100L, 3, 50, 150L)]
    [InlineData(5L, 1, 50, 2L)]        // 2.5 cp rounds down to a whole copper piece
    [InlineData(100L, 1, 100, 100L)]
    [InlineData(100L, 1, 0, 0L)]
    [InlineData(100L, 1, 250, 100L)]   // the rate is clamped to 100%
    public void SaleProceeds_AppliesTheRateAndRoundsDown(long unit, int quantity, int rate, long expected)
    {
        ShopPricing.SaleProceeds(unit, quantity, rate).Should().Be(expected);
    }

    [Fact]
    public void SaleProceeds_AnOverrideReplacesTheWholeFigure()
    {
        ShopPricing.SaleProceeds(100, 2, 50, overrideTotalCopper: 777).Should().Be(777);
    }

    [Theory]
    [InlineData("2.5", 250L)]
    [InlineData("0.01", 1L)]
    [InlineData(" 12 ", 1200L)]
    [InlineData("0", 0L)]
    public void ParseGold_ReadsGoldAsCopper(string text, long expected)
    {
        ShopPricing.ParseGold(text).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("99999999999")]
    public void ParseGold_RejectsBlankNegativeAndUnreadableText(string? text)
    {
        ShopPricing.ParseGold(text).Should().BeNull();
    }
}
