using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class CoinPurseTests
{
    private static CoinPurse Purse(long cp = 0, long sp = 0, long ep = 0, long gp = 0, long pp = 0) =>
        new(cp, sp, ep, gp, pp);

    [Fact]
    public void TotalCopper_CountsEveryDenomination()
    {
        Purse(cp: 3, sp: 2, ep: 1, gp: 4, pp: 5).TotalCopper.Should().Be(3 + 20 + 50 + 400 + 5000);
    }

    [Fact]
    public void TryPay_PaysFromMatchingCoinsWithoutChange()
    {
        Purse(gp: 20).TryPay(1500, out CoinPurse after).Should().BeTrue();
        after.Should().Be(Purse(gp: 5));
    }

    [Fact]
    public void TryPay_SpendsSmallCoinsBeforeLargeOnes()
    {
        // 25 cp + 8 sp + 2 gp for 1 gp: the copper and silver cover it, so both gold pieces stay.
        Purse(cp: 25, sp: 8, gp: 2).TryPay(100, out CoinPurse after).Should().BeTrue();
        after.TotalCopper.Should().Be(305 - 100);
        after.Gold.Should().Be(2, "gold is only broken when smaller coins cannot cover the price");
    }

    [Fact]
    public void TryPay_BreaksGoldOnlyWhenSmallerCoinsFallShort()
    {
        // 25 cp + 3 sp = 55 cp cannot cover 1 gp, so a gold piece is broken and 55 cp comes back
        // as 5 sp 5 cp; the smaller coins spent along the way are not returned.
        Purse(cp: 25, sp: 3, gp: 2).TryPay(100, out CoinPurse after).Should().BeTrue();
        after.Should().Be(Purse(cp: 5, sp: 5, gp: 1));
    }

    [Fact]
    public void TryPay_BreaksAGoldPieceIntoSilverAndCopperChange_NotElectrum()
    {
        // A 1 cp torch bought with gold: the change comes back as gp/sp/cp.
        Purse(gp: 5).TryPay(1, out CoinPurse after).Should().BeTrue();
        after.Should().Be(Purse(cp: 9, sp: 9, gp: 4));
    }

    [Fact]
    public void TryPay_BreaksAPlatinumPieceIntoGoldChange()
    {
        Purse(pp: 1).TryPay(250, out CoinPurse after).Should().BeTrue();
        after.Should().Be(Purse(gp: 7, sp: 5));
    }

    [Fact]
    public void TryPay_BreaksTheSmallestCoinThatCoversTheRemainder()
    {
        // 5 cp owed, holding 1 sp and 1 gp: the silver is broken, the gold is untouched.
        Purse(sp: 1, gp: 1).TryPay(5, out CoinPurse after).Should().BeTrue();
        after.Should().Be(Purse(cp: 5, gp: 1));
    }

    [Fact]
    public void TryPay_UsesElectrumWhenTheCharacterHoldsSome()
    {
        Purse(ep: 2).TryPay(100, out CoinPurse after).Should().BeTrue();
        after.Should().Be(Purse());
    }

    [Fact]
    public void TryPay_RefusesAPriceTheCharacterCannotMeet_AndLeavesTheCoinsAlone()
    {
        var purse = Purse(gp: 1, sp: 4);

        purse.TryPay(141, out CoinPurse after).Should().BeFalse();
        after.Should().Be(purse);
        purse.CanAfford(141).Should().BeFalse();
        purse.CanAfford(140).Should().BeTrue();
    }

    [Fact]
    public void TryPay_TreatsZeroAsFreeAndNegativeAsInvalid()
    {
        var purse = Purse(gp: 3);

        purse.TryPay(0, out CoinPurse free).Should().BeTrue();
        free.Should().Be(purse);

        purse.TryPay(-5, out CoinPurse invalid).Should().BeFalse();
        invalid.Should().Be(purse);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryPay_ConservesValueAndNeverMintsPlatinumOrNegativeCoins_AndElectrumOnlyWhenAllowed(bool useElectrum)
    {
        var options = new ShopCoinOptions(useElectrum);

        // Seeded so a failure reproduces; wide enough to hit every denomination mix.
        var random = new Random(20261005);
        for (int iteration = 0; iteration < 20_000; iteration++)
        {
            var purse = Purse(
                random.Next(0, 30), random.Next(0, 30), random.Next(0, 6),
                random.Next(0, 30), random.Next(0, 4));
            long cost = purse.TotalCopper == 0 ? 0 : random.NextInt64(0, purse.TotalCopper + 1);

            bool paid = purse.TryPay(cost, out CoinPurse after, options);

            // Built without braces: FluentAssertions runs a reason through string.Format.
            string context = $"paying {cost} cp from [{purse.Describe()}] gave [{after.Describe()}] (electrum {useElectrum})";
            paid.Should().BeTrue(context);
            after.TotalCopper.Should().Be(purse.TotalCopper - cost, context);
            after.Copper.Should().BeGreaterThanOrEqualTo(0, context);
            after.Silver.Should().BeGreaterThanOrEqualTo(0, context);
            after.Electrum.Should().BeGreaterThanOrEqualTo(0, context);
            after.Gold.Should().BeGreaterThanOrEqualTo(0, context);
            after.Platinum.Should().BeInRange(0, purse.Platinum, context);
            if (!useElectrum)
                after.Electrum.Should().BeLessThanOrEqualTo(purse.Electrum, context);
        }
    }

    [Fact]
    public void Receive_PaysOutInGoldSilverAndCopper()
    {
        Purse(gp: 1).Receive(1234).Should().Be(Purse(cp: 4, sp: 3, gp: 13));
        Purse(gp: 1).Receive(0).Should().Be(Purse(gp: 1));
        Purse(gp: 1).Receive(-9).Should().Be(Purse(gp: 1));
    }

    [Fact]
    public void Receive_WithoutElectrum_NeverHandsItOut_EvenWhereAFiftyCopperCoinFits()
    {
        Purse().Receive(75).Should().Be(Purse(cp: 5, sp: 7));
        Purse().Receive(75, new ShopCoinOptions(UseElectrum: false)).Should().Be(Purse(cp: 5, sp: 7));
    }

    [Fact]
    public void Receive_WithElectrum_HandsOutElectrumBetweenGoldAndSilver()
    {
        var withElectrum = new ShopCoinOptions(UseElectrum: true);

        Purse().Receive(75, withElectrum).Should().Be(Purse(cp: 5, sp: 2, ep: 1));
        Purse().Receive(49, withElectrum).Should().Be(Purse(cp: 9, sp: 4), "under 50 cp there is no electrum to give");
        Purse(gp: 1, ep: 2).Receive(1250, withElectrum).Should().Be(Purse(gp: 13, ep: 3));
        Purse(gp: 1).Receive(0, withElectrum).Should().Be(Purse(gp: 1));
    }

    [Fact]
    public void TryPay_WithElectrum_MakesChangeInElectrum_AsTheEnginesOwnWithdrawDoes()
    {
        // 5 gp for a 1 cp item. With electrum on, the 99 cp change is 1 ep, 4 sp, 9 cp: the same
        // coins the legacy Coinage.Withdraw returns, so a table that uses electrum sees no surprise.
        Purse(gp: 5).TryPay(1, out CoinPurse after, new ShopCoinOptions(UseElectrum: true)).Should().BeTrue();

        after.Should().Be(Purse(cp: 9, sp: 4, ep: 1, gp: 4));
    }

    [Fact]
    public void TryPay_DefaultOptionsAndExplicitNoElectrumAgree()
    {
        Purse(gp: 5).TryPay(1, out CoinPurse implicitDefault).Should().BeTrue();
        Purse(gp: 5).TryPay(1, out CoinPurse explicitOff, new ShopCoinOptions(UseElectrum: false)).Should().BeTrue();

        explicitOff.Should().Be(implicitDefault);
        explicitOff.Electrum.Should().Be(0);
    }

    [Fact]
    public void TryPay_SpendsElectrumTheCharacterHolds_WhicheverWayTheOptionIsSet()
    {
        Purse(ep: 2).TryPay(100, out CoinPurse ignoring, new ShopCoinOptions(UseElectrum: false)).Should().BeTrue();
        Purse(ep: 2).TryPay(100, out CoinPurse usingElectrum, new ShopCoinOptions(UseElectrum: true)).Should().BeTrue();

        ignoring.Should().Be(Purse());
        usingElectrum.Should().Be(Purse());
    }

    [Theory]
    [InlineData(0L, "0 cp")]
    [InlineData(7L, "7 cp")]
    [InlineData(10L, "1 sp")]
    [InlineData(100L, "1 gp")]
    [InlineData(1235L, "12 gp 3 sp 5 cp")]
    [InlineData(5_000_000L, "50,000 gp")]
    public void FormatCopper_BreaksAPriceIntoGoldSilverAndCopper(long copper, string expected)
    {
        CoinPurse.FormatCopper(copper).Should().Be(expected);
    }

    [Fact]
    public void DescribeChange_ListsOnlyTheCoinsThatMoved_LargestFirst()
    {
        string change = CoinPurse.DescribeChange(Purse(gp: 5), Purse(cp: 9, sp: 9, gp: 4));

        change.Should().Be("-1 gp, +9 sp, +9 cp");
        CoinPurse.DescribeChange(Purse(gp: 5), Purse(gp: 5)).Should().Be("no change");
    }

    [Fact]
    public void Describe_NamesWhatIsHeld()
    {
        Purse(cp: 2, gp: 12).Describe().Should().Be("12 gp 2 cp");
        Purse().Describe().Should().Be("no coins");
    }
}
