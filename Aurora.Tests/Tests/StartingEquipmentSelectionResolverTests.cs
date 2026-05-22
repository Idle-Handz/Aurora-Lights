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
}
