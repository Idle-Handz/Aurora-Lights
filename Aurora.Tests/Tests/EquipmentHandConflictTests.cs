using Aurora.App.Services;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Calculator;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;

namespace Aurora.Tests.Tests;

public sealed class EquipmentHandConflictTests
{
    [Fact]
    public Task NaturalSlotPrefersExplicitSecondaryOverGenericOneHand() =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var (_, shield, weapon, _) = AddEquipment(manager, versatile: true);
            shield.IsOneHandTarget().Should().BeTrue();
            shield.IsSecondaryTarget().Should().BeTrue();
            EquipmentService.GetNaturalSlot(shield).Should().Be(GearSlot.OffHand);
            EquipmentService.GetNaturalSlot(weapon).Should().Be(GearSlot.MainHand);
            await Task.CompletedTask;
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task MovingOneHandItemBetweenHandsKeepsOneSlotAndItsAttunement(bool attuned, bool direct) =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var (armor, shield, weapon, ring) = AddEquipment(manager, versatile: true);
            weapon.Item.RequiresAttunement = true;
            var inventory = manager.Character.Inventory;
            inventory.EquipArmor(armor);
            inventory.EquipPrimary(weapon, attune: attuned);
            inventory.EquipSecondary(shield);
            ring.Activate(true, true);

            if (direct) inventory.EquipSecondary(weapon);
            else EquipmentService.EquipToSlot(manager.Character, GearSlot.OffHand, weapon.Identifier).Should().BeTrue();

            inventory.EquippedPrimary.Should().BeNull();
            inventory.EquippedSecondary.Should().BeSameAs(weapon);
            weapon.EquippedLocation.Should().Be("Secondary Hand");
            weapon.IsEquipped.Should().BeTrue();
            weapon.IsAttuned.Should().Be(attuned);
            shield.IsEquipped.Should().BeFalse();

            if (direct) inventory.EquipPrimary(weapon);
            else EquipmentService.EquipToSlot(manager.Character, GearSlot.MainHand, weapon.Identifier).Should().BeTrue();

            inventory.EquippedPrimary.Should().BeSameAs(weapon);
            inventory.EquippedSecondary.Should().BeNull();
            weapon.EquippedLocation.Should().Be("Primary Hand");
            weapon.IsEquipped.Should().BeTrue();
            weapon.IsAttuned.Should().Be(attuned);
            manager.GetElements().Any(element => element.Id == weapon.Item.Id).Should().Be(attuned);
            inventory.EquippedArmor.Should().BeSameAs(armor);
            ring.IsAttuned.Should().BeTrue();
            await Task.CompletedTask;
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ChangingVersatileGripPreservesExistingAttunement(bool attuned) =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var (_, _, weapon, _) = AddEquipment(manager, versatile: true);
            weapon.Item.RequiresAttunement = true;
            var inventory = manager.Character.Inventory;
            inventory.EquipPrimary(weapon, attune: attuned);

            EquipmentService.SetVersatileWield(manager.Character, weapon.Identifier, true).Should().BeTrue();
            weapon.IsAttuned.Should().Be(attuned);
            inventory.EquippedPrimary.Should().BeSameAs(weapon);
            inventory.EquippedSecondary.Should().BeSameAs(weapon);
            EquipmentService.SetVersatileWield(manager.Character, weapon.Identifier, false).Should().BeTrue();

            weapon.IsAttuned.Should().Be(attuned);
            weapon.IsEquipped.Should().BeTrue();
            weapon.EquippedLocation.Should().Be("Primary Hand");
            inventory.EquippedPrimary.Should().BeSameAs(weapon);
            inventory.EquippedSecondary.Should().BeNull();
            await Task.CompletedTask;
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task EquippingTwoHandedWeaponRemovesOffHandEffects(bool versatile, bool direct) =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var (armor, shield, weapon, ring) = AddEquipment(manager, versatile);
            EquipmentService.EquipToSlot(manager.Character, GearSlot.Armor, armor.Identifier).Should().BeTrue();
            EquipmentService.EquipToSlot(manager.Character, GearSlot.OffHand, shield.Identifier).Should().BeTrue();
            ring.Activate(true, true);
            ArmorClass(manager).Should().Be(18);
            if (direct)
                manager.Character.Inventory.EquipPrimary(weapon, twohanded: true);
            else if (versatile)
            {
                EquipmentService.EquipToSlot(manager.Character, GearSlot.MainHand, weapon.Identifier).Should().BeTrue();
                EquipmentService.SetVersatileWield(manager.Character, weapon.Identifier, true).Should().BeTrue();
            }
            else
                EquipmentService.EquipToSlot(manager.Character, GearSlot.MainHand, weapon.Identifier).Should().BeTrue();

            AssertTwoHanded(manager, armor, shield, weapon, ring, versatile);
            await Task.CompletedTask;
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task EquippingOffHandReleasesTwoHandedGrip(bool versatile, bool direct) =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var (armor, shield, weapon, ring) = AddEquipment(manager, versatile);
            manager.Character.Inventory.EquipArmor(armor);
            manager.Character.Inventory.EquipPrimary(weapon, twohanded: true);
            ring.Activate(true, true);

            if (direct) manager.Character.Inventory.EquipSecondary(shield);
            else EquipmentService.EquipToSlot(manager.Character, GearSlot.OffHand, shield.Identifier).Should().BeTrue();

            var inventory = manager.Character.Inventory;
            inventory.EquippedSecondary.Should().BeSameAs(shield);
            shield.IsEquipped.Should().BeTrue();
            weapon.IsEquipped.Should().Be(versatile);
            weapon.EquippedLocation.Should().Be(versatile ? "Primary Hand" : "");
            if (versatile) inventory.EquippedPrimary.Should().BeSameAs(weapon);
            else inventory.EquippedPrimary.Should().BeNull();
            manager.GetElements().Any(element => element.Id == weapon.Item.Id).Should().Be(versatile);
            manager.GetElements().Any(element => element.Id == shield.Item.Id).Should().BeTrue();
            armor.IsEquipped.Should().BeTrue();
            ring.IsAttuned.Should().BeTrue();
            ArmorClass(manager).Should().Be(18);
            await Task.CompletedTask;
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task RestoringConflictingSavePrefersTwoHandedWeaponRegardlessOfItemOrder(bool shieldFirst, bool versatile) =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var (armor, shield, weapon, ring) = AddEquipment(manager, versatile);
            var inventory = manager.Character.Inventory;
            if (shieldFirst)
                inventory.Items.Move(inventory.Items.IndexOf(shield), 0);
            else
                inventory.Items.Move(inventory.Items.IndexOf(weapon), 0);
            armor.Activate(true, false);
            armor.EquippedLocation = "Armor";
            shield.Activate(true, false);
            shield.EquippedLocation = "Secondary Hand";
            weapon.Activate(true, false);
            weapon.EquippedLocation = versatile ? "Two-Handed (Versatile)" : "Two-Handed";
            ring.Activate(true, true);

            CharacterLoadCompatibilityService.RestoreEquippedSlots(manager.Character);
            CharacterLoadCompatibilityService.RestoreEquippedSlots(manager.Character);

            AssertTwoHanded(manager, armor, shield, weapon, ring, versatile);
            var serialized = new CharacterFile("hand-conflict.dnd5e").SerializeCharacter(manager.Character);
            var document = new System.Xml.XmlDocument();
            document.LoadXml(System.Text.Encoding.UTF8.GetString(serialized).TrimStart('\uFEFF'));
            var savedShield = document.SelectSingleNode($"/character/build/equipment/item[@identifier='{shield.Identifier}']");
            savedShield.Should().NotBeNull("unequipping must retain the inventory item");
            savedShield!.SelectSingleNode("equipped").Should().BeNull("CharacterFile omits the equipped node for unequipped items");
            await Task.CompletedTask;
        });

    private static void AssertTwoHanded(CharacterManager manager, RefactoredEquipmentItem armor,
        RefactoredEquipmentItem shield, RefactoredEquipmentItem weapon, RefactoredEquipmentItem ring, bool versatile)
    {
        var inventory = manager.Character.Inventory;
        inventory.EquippedPrimary.Should().BeSameAs(weapon);
        inventory.EquippedSecondary.Should().BeSameAs(weapon);
        weapon.EquippedLocation.Should().Be(versatile ? "Two-Handed (Versatile)" : "Two-Handed");
        shield.IsEquipped.Should().BeFalse();
        shield.EquippedLocation.Should().BeEmpty();
        manager.GetElements().Any(element => element.Id == shield.Item.Id).Should().BeFalse("the shield AC rules must be removed");
        manager.GetElements().Any(element => element.Id == weapon.Item.Id).Should().BeTrue();
        armor.IsEquipped.Should().BeTrue();
        inventory.EquippedArmor.Should().BeSameAs(armor);
        ring.IsEquipped.Should().BeTrue();
        ring.IsAttuned.Should().BeTrue();
        ArmorClass(manager).Should().Be(16);
    }

    private static int ArmorClass(CharacterManager manager)
    {
        var seed = new StatisticValuesGroupCollection();
        seed.GetGroup("ac:calculation").AddValue("armor", 16);
        var elements = new ElementBaseCollection();
        foreach (var element in manager.GetElements()) elements.Add(element);
        return manager.StatisticsCalculator.CalculateValues(elements, seed).GetValue("ac");
    }

    private static (RefactoredEquipmentItem Armor, RefactoredEquipmentItem Shield,
        RefactoredEquipmentItem Weapon, RefactoredEquipmentItem Ring) AddEquipment(CharacterManager manager, bool versatile)
    {
        RefactoredEquipmentItem Add(string id, string type, string slot, bool needsAttunement = false)
        {
            string rules = slot.Split(',').Contains("secondary") ? "<rules><stat name='ac:shield' value='2' /></rules>" : "";
            var parsed = EquipmentAttunementRegressionTests.Parse($"<element name='{id}' type='{type}' source='Test' id='{id}'>{rules}</element>");
            Item item = type == "Armor" ? parsed.Construct<ArmorElement>()
                : type == "Weapon" ? parsed.Construct<WeaponElement>() : parsed.Construct<MagicItemElement>();
            item.Category = string.Empty;
            item.Slot = slot;
            item.Slots = slot.Split(',').ToList();
            item.RequiresAttunement = needsAttunement;
            if (type == "Weapon" && versatile) item.Versatile = "1d10";
            DataManager.Current.ElementsCollection.Add(item);
            var equipment = new RefactoredEquipmentItem(item);
            manager.Character.Inventory.Items.Add(equipment);
            return equipment;
        }
        return (Add("ID_HAND_ARMOR", "Armor", "armor"),
            Add("ID_HAND_SHIELD", "Armor", "onehand,secondary"),
            Add("ID_HAND_WEAPON", "Weapon", versatile ? "onehand" : "twohand"),
            Add("ID_HAND_RING", "Magic Item", "ring", needsAttunement: true));
    }
}
