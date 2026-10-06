using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class InventoryItemAdderTests : IAsyncLifetime
{
    private const string DaggerId = "ID_WOTC_PHB_WEAPON_DAGGER";
    private const string TorchId = "ID_WOTC_PHB_ITEM_TORCH";
    private const string ChainMailId = "ID_WOTC_ARMOR_HEAVY_CHAIN_MAIL";
    private const string ArmorPlusOneId = "ID_WOTC_DMG_MAGIC_ITEM_ARMOR_1";
    private const string DiplomatsPackId = "ID_WOTC_ITEM_DIPLOMATS_PACK";

    private readonly ITestOutputHelper _output;

    public InventoryItemAdderTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private Character NewCharacter()
    {
        ContentFixture.SkipIfUnavailable(_output);
        return new Character();
    }

    private static IEnumerable<Builder.Presentation.ViewModels.Shell.Items.RefactoredEquipmentItem> Rows(
        Character character, string elementId) =>
        character.Inventory.Items.Where(row => row.Item.Id == elementId);

    // ── Rows, stacks and the dual-wielding rule ──────────────────────────────

    [Fact]
    public void An_unstackable_quantity_becomes_one_row_per_unit()
    {
        var character = NewCharacter();

        EquipmentService.AddItem(character, DaggerId, 2).Should().BeTrue();

        var rows = Rows(character, DaggerId).ToList();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.Amount == 1);
        rows.Select(row => row.Identifier).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Two_daggers_added_together_can_be_wielded_one_in_each_hand()
    {
        var character = NewCharacter();
        EquipmentService.AddItem(character, DaggerId, 2).Should().BeTrue();
        var rows = Rows(character, DaggerId).ToList();

        EquipmentService.EquipToSlot(character, GearSlot.MainHand, rows[0].Identifier).Should().BeTrue();
        EquipmentService.EquipToSlot(character, GearSlot.OffHand, rows[1].Identifier).Should().BeTrue();

        character.Inventory.EquippedPrimary.Identifier.Should().Be(rows[0].Identifier);
        character.Inventory.EquippedSecondary.Identifier.Should().Be(rows[1].Identifier);
        // The engine reads one row in both hands as a two-handed grip; two rows are two weapons.
        character.Inventory.IsEquippedTwoHanded().Should().BeFalse();
        character.Inventory.IsEquippedVersatile().Should().BeFalse();
    }

    [Fact]
    public void The_gear_picker_says_which_dagger_is_already_in_a_hand()
    {
        var character = NewCharacter();
        EquipmentService.AddItem(character, DaggerId, 2).Should().BeTrue();
        var rows = Rows(character, DaggerId).ToList();

        // Nothing is held yet, so there is nothing to tell apart.
        EquipmentService.GetInventoryItemsForSlot(character, GearSlot.MainHand)
            .Select(option => option.Name).Should().Equal("Dagger", "Dagger");

        EquipmentService.EquipToSlot(character, GearSlot.MainHand, rows[0].Identifier).Should().BeTrue();
        var offHandChoices = EquipmentService.GetInventoryItemsForSlot(character, GearSlot.OffHand);

        offHandChoices.Single(option => option.Identifier == rows[0].Identifier).Name.Should().Be("Dagger (M)");
        offHandChoices.Single(option => option.Identifier == rows[1].Identifier).Name.Should().Be("Dagger");

        EquipmentService.EquipToSlot(character, GearSlot.OffHand, rows[1].Identifier).Should().BeTrue();
        var mainHandChoices = EquipmentService.GetInventoryItemsForSlot(character, GearSlot.MainHand);

        mainHandChoices.Single(option => option.Identifier == rows[0].Identifier).Name.Should().Be("Dagger (M)");
        mainHandChoices.Single(option => option.Identifier == rows[1].Identifier).Name.Should().Be("Dagger (O)");
        rows.Select(row => row.DisplayName ?? row.Name).Should().OnlyContain(name => name == "Dagger",
            "the label is only how the list shows the row, not its name on the character");
    }

    [Fact]
    public void Stackable_quantities_join_a_single_stack()
    {
        var character = NewCharacter();

        EquipmentService.AddItem(character, TorchId, 3).Should().BeTrue();
        EquipmentService.AddItem(character, TorchId, 2).Should().BeTrue();

        Rows(character, TorchId).Should().ContainSingle().Which.Amount.Should().Be(5);
    }

    [Fact]
    public void A_stack_is_only_joined_by_an_add_carrying_the_same_name()
    {
        var character = NewCharacter();
        EquipmentService.AddItem(character, TorchId, 2).Should().BeTrue();

        EquipmentService.AddItem(character, TorchId, 1, alternativeName: "Spare torch").Should().BeTrue();
        Rows(character, TorchId).Should().HaveCount(2, "a named add does not fold into the unnamed stack");

        EquipmentService.AddItem(character, TorchId, 4, alternativeName: "Spare torch").Should().BeTrue();
        EquipmentService.AddItem(character, TorchId, 1).Should().BeTrue();

        var rows = Rows(character, TorchId).ToList();
        rows.Should().HaveCount(2);
        rows.Single(row => row.AlternativeName == "Spare torch").Amount.Should().Be(5);
        rows.Single(row => string.IsNullOrEmpty(row.AlternativeName)).Amount.Should().Be(3);
    }

    [Fact]
    public void An_item_that_can_be_wielded_is_never_stacked_even_when_the_content_calls_it_stackable()
    {
        const string id = "ID_TEST_STACKABLE_JAVELIN";
        var javelin = new Item
        {
            ElementHeader = new ElementHeader("Throwing Spike", "Item", "Test Source", id),
            Category = "Weapons",
            IsStackable = true,
            Slot = "onehand",
        };
        javelin.Slots.Clear();
        javelin.Slots.Add("onehand");
        DataManager.Current.ElementsCollection.Add(javelin);
        try
        {
            var character = NewCharacter();

            EquipmentService.AddItem(character, id, 3).Should().BeTrue();
            EquipmentService.AddItem(character, id, 2).Should().BeTrue();

            var rows = Rows(character, id).ToList();
            rows.Should().HaveCount(5, "each can then go in its own hand");
            rows.Should().OnlyContain(row => row.Amount == 1);
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(javelin);
        }
    }

    [Fact]
    public void A_magic_template_quantity_is_one_composed_row_per_unit()
    {
        var character = NewCharacter();

        EquipmentService.AddItem(character, ArmorPlusOneId, 2, ChainMailId).Should().BeTrue();

        var rows = character.Inventory.Items.ToList();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.Item.Id == ChainMailId && row.AdornerItem != null && row.AdornerItem.Id == ArmorPlusOneId);
    }

    [Fact]
    public void Every_row_of_a_named_quantity_carries_the_name()
    {
        var character = NewCharacter();

        EquipmentService.AddItem(character, DaggerId, 2, alternativeName: "Twin Fang").Should().BeTrue();

        Rows(character, DaggerId).Should().OnlyContain(row => row.AlternativeName == "Twin Fang");
    }

    // ── Preparing, applying and failing ──────────────────────────────────────

    [Fact]
    public void Preparing_an_add_changes_nothing_until_it_is_applied()
    {
        var character = NewCharacter();
        var dagger = DataManager.Current.ElementsCollection.GetElement(DaggerId);

        var prepared = InventoryItemAdder.Prepare(character, dagger, 3);

        prepared.Should().NotBeNull();
        prepared!.NewRows.Should().HaveCount(3);
        prepared.ItemName.Should().Be("Dagger");
        character.Inventory.Items.Should().BeEmpty();

        InventoryItemAdder.Apply(character, prepared);

        character.Inventory.Items.Should().HaveCount(3);
        prepared.Rows.Should().BeEquivalentTo(character.Inventory.Items);
    }

    [Fact]
    public void An_add_that_cannot_be_made_changes_nothing()
    {
        var character = NewCharacter();

        // A magic armor template needs a base item; without one there is nothing to add.
        EquipmentService.AddItem(character, ArmorPlusOneId, 2).Should().BeFalse();
        EquipmentService.AddItem(character, ArmorPlusOneId, 2, DaggerId).Should().BeFalse("a dagger is not armor");
        EquipmentService.AddItem(character, "ID_DOES_NOT_EXIST", 2).Should().BeFalse();

        character.Inventory.Items.Should().BeEmpty();
    }

    [Fact]
    public void Adding_keeps_the_carried_weight_current_even_when_it_only_grows_a_stack()
    {
        var character = NewCharacter();

        EquipmentService.AddItem(character, TorchId, 3).Should().BeTrue();
        decimal withThree = character.Inventory.EquipmentWeight;
        EquipmentService.AddItem(character, TorchId, 2).Should().BeTrue();

        withThree.Should().BeGreaterThan(0);
        character.Inventory.EquipmentWeight.Should().Be(withThree / 3 * 5);
    }

    [Fact]
    public void AddAndEquip_equips_the_row_it_just_added_not_whichever_row_is_last()
    {
        var character = NewCharacter();
        EquipmentService.AddItem(character, DaggerId).Should().BeTrue();
        string firstIdentifier = character.Inventory.Items.Single().Identifier;

        EquipmentService.AddAndEquipToSlot(character, GearSlot.MainHand, DaggerId).Should().BeTrue();

        character.Inventory.Items.Should().HaveCount(2);
        character.Inventory.EquippedPrimary.Identifier.Should().NotBe(firstIdentifier);
    }

    // ── Extracting packs ─────────────────────────────────────────────────────

    [Fact]
    public void Extracting_a_pack_gives_unstackable_components_one_row_per_unit()
    {
        var character = NewCharacter();
        var pack = (Item)DataManager.Current.ElementsCollection.GetElement(DiplomatsPackId)!;
        var (caseId, caseCount) = pack.Extractables
            .Single(entry => DataManager.Current.ElementsCollection.GetElement(entry.Key)?.Name == "Case, Map or Scroll");
        caseCount.Should().Be(2, "the Diplomat's Pack holds two map or scroll cases");
        EquipmentService.AddItem(character, DiplomatsPackId).Should().BeTrue();

        var result = EquipmentService.ExtractPack(character, character.Inventory.Items.Single().Identifier);

        result.Success.Should().BeTrue();
        var cases = Rows(character, caseId).ToList();
        cases.Should().HaveCount(2);
        cases.Should().OnlyContain(row => row.Amount == 1);
    }

    [Fact]
    public void A_pack_with_a_component_that_cannot_be_resolved_is_left_whole()
    {
        const string packId = "ID_TEST_BROKEN_PACK";
        var pack = new Item
        {
            ElementHeader = new ElementHeader("Broken Pack", "Item", "Test Source", packId),
            Category = "Equipment Packs",
            IsExtractable = true,
        };
        pack.Extractables[TorchId] = 3;
        pack.Extractables["ID_NO_SUCH_COMPONENT"] = 1;
        DataManager.Current.ElementsCollection.Add(pack);
        try
        {
            var character = NewCharacter();
            EquipmentService.AddItem(character, packId).Should().BeTrue();

            var result = EquipmentService.ExtractPack(character, character.Inventory.Items.Single().Identifier);

            result.Success.Should().BeFalse();
            result.MissingElementIds.Should().Contain("ID_NO_SUCH_COMPONENT");
            character.Inventory.Items.Should().ContainSingle(row => row.Item.Id == packId);
            Rows(character, TorchId).Should().BeEmpty("nothing is added unless every component can be");
        }
        finally
        {
            DataManager.Current.ElementsCollection.Remove(pack);
        }
    }
}
