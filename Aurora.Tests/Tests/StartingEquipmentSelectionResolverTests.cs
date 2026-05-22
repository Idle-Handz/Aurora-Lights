using Aurora.Components.Models;

namespace Aurora.Tests.Tests;

public sealed class StartingEquipmentSelectionResolverTests
{
    [Fact]
    public void Resolve_WhenTakingClassEquipment_IncludesClassFixedGold()
    {
        var classBlock = new StartingEquipmentBlock
        {
            GoldAlternative = new GoldAlternative { Amount = 150 },
            FixedItems =
            [
                new EquipmentItem { Id = "ID_TEST_TOOL", Count = 1 },
            ],
            FixedGold = 16,
        };

        var result = StartingEquipmentSelectionResolver.Resolve(
            classBlock,
            StartingEquipmentBlock.Empty,
            [],
            [],
            new Dictionary<string, string>(),
            takeClassGold: false);

        result.Gold.Should().Be(16);
        result.Copper.Should().Be(0);
        result.TookRolledGold.Should().BeFalse();
        result.Items.Should().ContainSingle();
        result.Items[0].ElementId.Should().Be("ID_TEST_TOOL");
    }

    [Fact]
    public void Resolve_WhenTakingFixedClassGoldAlternative_ExcludesClassFixedGoldAndItems()
    {
        var classBlock = new StartingEquipmentBlock
        {
            GoldAlternative = new GoldAlternative { Amount = 150 },
            FixedItems =
            [
                new EquipmentItem { Id = "ID_TEST_TOOL", Count = 1 },
            ],
            FixedGold = 16,
        };

        var result = StartingEquipmentSelectionResolver.Resolve(
            classBlock,
            StartingEquipmentBlock.Empty,
            [],
            [],
            new Dictionary<string, string>(),
            takeClassGold: true);

        result.Gold.Should().Be(150);
        result.TookRolledGold.Should().BeFalse();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WhenTakingRolledClassGoldAlternative_SetsRolledFlagAndSkipsClassFixedGold()
    {
        var classBlock = new StartingEquipmentBlock
        {
            GoldAlternative = new GoldAlternative { Roll = "5d4", Multiplier = 10 },
            FixedItems =
            [
                new EquipmentItem { Id = "ID_TEST_TOOL", Count = 1 },
            ],
            FixedGold = 16,
        };
        var backgroundBlock = new StartingEquipmentBlock { FixedGold = 10 };

        var result = StartingEquipmentSelectionResolver.Resolve(
            classBlock,
            backgroundBlock,
            [],
            [],
            new Dictionary<string, string>(),
            takeClassGold: true);

        result.Gold.Should().Be(10);
        result.TookRolledGold.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WhenTakingBackgroundGoldAlternative_ExcludesBackgroundKit()
    {
        var backgroundBlock = new StartingEquipmentBlock
        {
            GoldAlternative = new GoldAlternative { Amount = 50 },
            FixedItems =
            [
                new EquipmentItem { Id = "ID_BACKGROUND_TOOL", Count = 1 },
            ],
            FixedCoins = new CoinGrant(Copper: 16),
        };

        var result = StartingEquipmentSelectionResolver.Resolve(
            StartingEquipmentBlock.Empty,
            backgroundBlock,
            [],
            [],
            new Dictionary<string, string>(),
            takeClassGold: false,
            takeBackgroundGold: true);

        result.Gold.Should().Be(50);
        result.Copper.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WhenChoiceHasCoins_IncludesSelectedOptionCoinsOnly()
    {
        var classBlock = new StartingEquipmentBlock
        {
            Choices =
            [
                new EquipmentChoice
                {
                    Options =
                    [
                        new EquipmentOption
                        {
                            Items = [new EquipmentItem { Id = "ID_OPTION_A" }],
                            Coins = new CoinGrant(Gold: 4),
                        },
                        new EquipmentOption
                        {
                            Items = [new EquipmentItem { Id = "ID_OPTION_B" }],
                            Coins = new CoinGrant(Gold: 11),
                        },
                    ],
                },
            ],
        };

        var result = StartingEquipmentSelectionResolver.Resolve(
            classBlock,
            StartingEquipmentBlock.Empty,
            [1],
            [],
            new Dictionary<string, string>(),
            takeClassGold: false);

        result.Gold.Should().Be(11);
        result.Items.Should().ContainSingle();
        result.Items[0].ElementId.Should().Be("ID_OPTION_B");
    }
}
