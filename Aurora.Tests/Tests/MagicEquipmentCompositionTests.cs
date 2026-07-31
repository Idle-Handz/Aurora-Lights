using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using System.Xml;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class MagicEquipmentCompositionTests : IAsyncLifetime
{
    private const string ShieldId = "ID_WOTC_GEAR_SHIELD";
    private const string ShieldPlusOneId = "ID_WOTC_DMG_MAGIC_ITEM_SHIELD_1";
    private const string ArmorPlusOneId = "ID_WOTC_DMG_MAGIC_ITEM_ARMOR_1";
    private const string ChainMailId = "ID_WOTC_ARMOR_HEAVY_CHAIN_MAIL";
    private const string WeaponPlusOneId = "ID_WOTC_DMG_MAGIC_ITEM_WEAPON_1";
    private const string LongswordId = "ID_WOTC_PHB_WEAPON_LONGSWORD";

    private readonly ITestOutputHelper _output;

    public MagicEquipmentCompositionTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void TemplateOptions_ExposeCompatibleBaseItems()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(
                ShieldId,
                ShieldPlusOneId,
                ArmorPlusOneId,
                ChainMailId,
                WeaponPlusOneId,
                LongswordId))
        {
            return;
        }

        var character = new Character();

        var shieldOptions = EquipmentService.GetItemTemplateOptions(character, ShieldPlusOneId);
        var armorOptions = EquipmentService.GetItemTemplateOptions(character, ArmorPlusOneId);
        var weaponOptions = EquipmentService.GetItemTemplateOptions(character, WeaponPlusOneId);

        shieldOptions.Should().NotBeNull();
        shieldOptions!.BaseOptions.Should().Contain(option => option.Id == ShieldId);
        armorOptions.Should().NotBeNull();
        armorOptions!.BaseOptions.Should().Contain(option => option.Id == ChainMailId);
        armorOptions.BaseOptions.Should().NotContain(option => option.Id == ShieldId);
        weaponOptions.Should().NotBeNull();
        weaponOptions!.BaseOptions.Should().Contain(option => option.Id == LongswordId);
    }

    [Fact]
    public void AddItem_TemplateRequiresAnExplicitCompatibleBase()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(ShieldId, ShieldPlusOneId, ArmorPlusOneId)) return;

        var character = new Character();

        EquipmentService.AddItem(character, ShieldPlusOneId).Should().BeFalse();
        EquipmentService.AddItem(
            character,
            ArmorPlusOneId,
            baseElementId: ShieldId).Should().BeFalse();
        character.Inventory.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_ComposesMagicShieldWithSelectedBaseArmor()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(ShieldId, ShieldPlusOneId)) return;

        var character = new Character();

        EquipmentService.AddItem(
            character,
            ShieldPlusOneId,
            baseElementId: ShieldId).Should().BeTrue();

        var shield = character.Inventory.Items.Should().ContainSingle().Subject;
        shield.IsAdorned.Should().BeTrue();
        shield.Item.Id.Should().Be(ShieldId);
        shield.AdornerItem.Id.Should().Be(ShieldPlusOneId);
        shield.DisplayName.Should().Be("Shield +1");
        shield.IsEquippable.Should().BeTrue();
    }

    [Fact]
    public void AddItem_ComposesGenericMagicArmorWithSelectedBase()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(ArmorPlusOneId, ChainMailId)) return;

        var character = new Character();

        EquipmentService.AddItem(
            character,
            ArmorPlusOneId,
            baseElementId: ChainMailId).Should().BeTrue();

        var armor = character.Inventory.Items.Should().ContainSingle().Subject;
        armor.IsAdorned.Should().BeTrue();
        armor.Item.Id.Should().Be(ChainMailId);
        armor.AdornerItem.Id.Should().Be(ArmorPlusOneId);
        armor.DisplayName.Should().Be("Chain Mail +1");
        armor.IsEquippable.Should().BeTrue();
    }

    [Fact]
    public void AddItem_ComposesGenericMagicWeaponWithSelectedBase()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(WeaponPlusOneId, LongswordId)) return;

        var character = new Character();

        EquipmentService.AddItem(
            character,
            WeaponPlusOneId,
            baseElementId: LongswordId).Should().BeTrue();

        var weapon = character.Inventory.Items.Should().ContainSingle().Subject;
        weapon.IsAdorned.Should().BeTrue();
        weapon.Item.Id.Should().Be(LongswordId);
        weapon.AdornerItem.Id.Should().Be(WeaponPlusOneId);
        weapon.DisplayName.Should().Be("Longsword +1");
        weapon.IsEquippable.Should().BeTrue();
    }

    [Fact]
    public async Task Load_RepairsStandaloneMagicShieldSavedByEarlierClients()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(ShieldId, ShieldPlusOneId)) return;

        const string identifier = "standalone-magic-shield-regression";
        var document = new XmlDocument();
        document.Load(ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e"));

        var equipment = document.SelectSingleNode("/character/build/equipment");
        equipment.Should().NotBeNull();

        var item = document.CreateElement("item");
        item.SetAttribute("identifier", identifier);
        item.SetAttribute("name", "Shield, +1");
        item.SetAttribute("id", ShieldPlusOneId);

        var details = document.CreateElement("details");
        details.SetAttribute("card", "true");
        details.AppendChild(document.CreateElement("name"));
        details.AppendChild(document.CreateElement("notes"));
        item.AppendChild(details);
        equipment!.AppendChild(item);

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"aurora_magic_shield_{Guid.NewGuid():N}.dnd5e");

        try
        {
            document.Save(tempPath);
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            await new CharacterFile(tempPath).Load();
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        var shield = CharacterManager.Current.Character!.Inventory.Items
            .Single(candidate => candidate.Identifier == identifier);
        shield.IsAdorned.Should().BeTrue();
        shield.Item.Id.Should().Be(ShieldId);
        shield.AdornerItem.Id.Should().Be(ShieldPlusOneId);
        shield.IsEquippable.Should().BeTrue();
    }

    private bool EnsureElementsAvailable(params string[] requiredIds)
    {
        var missing = requiredIds
            .Where(id => DataManager.Current.ElementsCollection.GetElement(id) == null)
            .ToList();

        if (missing.Count == 0) return true;

        _output.WriteLine($"[SKIP] Missing equipment element(s): {string.Join(", ", missing)}.");
        return false;
    }
}
