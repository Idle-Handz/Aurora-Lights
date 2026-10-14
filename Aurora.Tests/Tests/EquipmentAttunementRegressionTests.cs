using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;

namespace Aurora.Tests.Tests;

public sealed class EquipmentAttunementRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task StandaloneItemEffectsFollowItsAttunementRequirement(bool required) =>
        WithCharacter(async manager =>
        {
            var baseItem = CreateEquipment("Weapon", "onehand", required, false).Item;
            var equipment = new RefactoredEquipmentItem(baseItem);
            manager.Character.Inventory.Items.Add(equipment);

            equipment.Activate(true, false);
            manager.GetElements().Any(element => element.Id == baseItem.Id).Should().Be(!required);
            equipment.Activate(false, true);
            manager.GetElements().Any(element => element.Id == baseItem.Id).Should().BeTrue();
            equipment.DeactivateAttunement();
            manager.GetElements().Any(element => element.Id == baseItem.Id).Should().Be(!required);
            equipment.Deactivate();
            manager.GetElements().Any(element => element.Id == baseItem.Id).Should().BeFalse();
            await Task.CompletedTask;
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task BaseAndTemplateEffectsFollowTheirOwnAttunementRequirement(bool baseRequired, bool templateRequired) =>
        WithCharacter(async manager =>
        {
            var equipment = CreateEquipment("Weapon", "onehand", baseRequired, templateRequired);
            manager.Character.Inventory.Items.Add(equipment);

            equipment.Activate(equip: true, attune: false);
            AssertRegistered(manager, equipment, !baseRequired, !templateRequired);
            equipment.IsAttuned.Should().BeFalse();

            equipment.Activate(equip: false, attune: true);
            AssertRegistered(manager, equipment, true, true);
            equipment.IsAttuned.Should().Be(baseRequired || templateRequired);

            equipment.DeactivateAttunement();
            AssertRegistered(manager, equipment, !baseRequired, !templateRequired);
            equipment.IsEquipped.Should().BeTrue();
            equipment.IsAttuned.Should().BeFalse();

            equipment.Deactivate();
            AssertRegistered(manager, equipment, false, false);
            equipment.IsEquipped.Should().BeFalse();
            await Task.CompletedTask;
        });

    [Theory]
    [InlineData("Armor", false)]
    [InlineData("Armor", true)]
    [InlineData("Primary Hand", false)]
    [InlineData("Primary Hand", true)]
    [InlineData("Secondary Hand", false)]
    [InlineData("Secondary Hand", true)]
    [InlineData("Two-Handed", false)]
    [InlineData("Two-Handed", true)]
    [InlineData("Two-Handed (Versatile)", false)]
    [InlineData("Two-Handed (Versatile)", true)]
    public Task ReloadAndRepeatedSlotRestorationPreserveSavedAttunement(string location, bool attuned) =>
        WithCharacter(async manager =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "attunement-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var equipment = CreateEquipment(location == "Armor" ? "Armor" : "Weapon",
                    location == "Armor" ? "armor" : location == "Secondary Hand" ? "secondary" :
                        location == "Two-Handed" ? "twohand" : "onehand",
                    baseRequired: false, templateRequired: true);
                if (location == "Two-Handed (Versatile)")
                {
                    equipment.Item.Versatile = "1d10";
                    ((Item)DataManager.Current.ElementsCollection.GetElement(equipment.Item.Id)).Versatile = "1d10";
                }
                manager.Character.Inventory.Items.Add(equipment);
                equipment.Activate(true, attuned);
                equipment.EquippedLocation = location;
                var file = new CharacterFile(Path.Combine(directory, "character.dnd5e"));
                file.Save(manager.Character).Should().BeTrue();

                var load = await file.Load();
                load.Success.Should().BeTrue(load.Message);
                var restored = manager.Character.Inventory.Items.Single();
                restored.IsEquipped.Should().BeTrue();
                restored.IsAttuned.Should().Be(attuned);

                CharacterLoadCompatibilityService.RestoreEquippedSlots(manager.Character);
                CharacterLoadCompatibilityService.RestoreEquippedSlots(manager.Character);

                restored.IsEquipped.Should().BeTrue();
                restored.IsAttuned.Should().Be(attuned);
                restored.EquippedLocation.Should().Be(location);
                AssertRegistered(manager, restored, true, attuned);
                manager.Character.Inventory.Items.Count(item => item.IsAttuned).Should().Be(attuned ? 1 : 0);
                var inventory = manager.Character.Inventory;
                if (location == "Armor") inventory.EquippedArmor.Should().BeSameAs(restored);
                else if (location == "Secondary Hand") inventory.EquippedSecondary.Should().BeSameAs(restored);
                else inventory.EquippedPrimary.Should().BeSameAs(restored);
            }
            finally { Directory.Delete(directory, true); }
        });

    private static RefactoredEquipmentItem CreateEquipment(string type, string slot, bool baseRequired, bool templateRequired)
    {
        var parsed = Parse($"<element name='Base item' type='{type}' source='Test' id='ID_ATTUNEMENT_BASE' />");
        Item item = type == "Armor" ? parsed.Construct<ArmorElement>() : parsed.Construct<WeaponElement>();
        item.Category = string.Empty;
        item.Slot = slot;
        item.RequiresAttunement = baseRequired;
        var template = Parse("<element name='Magic template' type='Magic Item' source='Test' id='ID_ATTUNEMENT_TEMPLATE' />")
            .Construct<MagicItemElement>();
        template.RequiresAttunement = templateRequired;
        template.Category = string.Empty;
        DataManager.Current.ElementsCollection.Add(item);
        DataManager.Current.ElementsCollection.Add(template);
        return new RefactoredEquipmentItem(item, template);
    }

    private static void AssertRegistered(CharacterManager manager, RefactoredEquipmentItem equipment, bool baseRegistered, bool templateRegistered)
    {
        manager.GetElements().Any(element => element.Id == equipment.Item.Id).Should().Be(baseRegistered);
        manager.GetElements().Any(element => element.Id == equipment.AdornerItem.Id).Should().Be(templateRegistered);
    }

    internal static async Task WithCharacter(Func<CharacterManager, Task> test)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var catalog = DataManager.Current.ElementsCollection;
        var originals = catalog.ToArray();
        var previousExpander = SelectionRuleExpanderContext.Current;
        var previousSpells = SpellcastingSectionContext.Current;
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        var previousAttackPrompt = ApplicationContext.Current.Settings.RequestAddAttackWhenEquippingWeapon;
        ApplicationContext.Current.Settings.RequestAddAttackWhenEquippingWeapon = false;
        var manager = CharacterManager.Current;
        try
        {
            await manager.New(false);
            catalog.Clear();
            for (int number = 1; number <= 20; number++)
            {
                var level = Parse($"<element name='{number}' type='Level' source='Internal' id='ID_ATTUNEMENT_LEVEL_{number}' />")
                    .Construct<LevelElement>();
                level.Level = number;
                level.RequiredExperience = (number - 1) * 300;
                level.ElementSetters.Add(new("Level", number.ToString()));
                catalog.Add(level);
            }
            manager.RegisterElement(catalog.GetElement("ID_ATTUNEMENT_LEVEL_1"));
            await test(manager);
        }
        finally
        {
            await manager.New(false);
            catalog.Clear();
            foreach (var element in originals) catalog.Add(element);
            SelectionRuleExpanderContext.Current = previousExpander;
            SpellcastingSectionContext.Current = previousSpells;
            ApplicationContext.Current.Settings.RequestAddAttackWhenEquippingWeapon = previousAttackPrompt;
        }
    }

    internal static ElementBase Parse(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return new ElementParser().ParseElement(document.DocumentElement!);
    }
}
