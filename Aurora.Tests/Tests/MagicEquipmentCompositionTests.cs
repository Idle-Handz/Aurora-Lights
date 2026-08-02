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
    public async Task ItemSearch_MatchesComposedTemplateNames()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(
                ShieldPlusOneId,
                WeaponPlusOneId,
                LongswordId))
        {
            return;
        }

        var character = new Character();
        InventoryItemFactory.InvalidateSearchIndex();
        await InventoryItemFactory.PrecomputeSearchIndexAsync();

        EquipmentService.SearchItems(character, "Shield +1")
            .Should().Contain(option => option.Id == ShieldPlusOneId);
        EquipmentService.SearchItems(character, "Longsword +1")
            .Should().Contain(option => option.Id == WeaponPlusOneId);

        var restrictedTemplate = new BuildSourceRestrictionSnapshot(
            new HashSet<string>([ShieldPlusOneId], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        EquipmentService.SearchItems(character, "Shield +1", restrictedTemplate)
            .Should().NotContain(option => option.Id == ShieldPlusOneId);

        var restrictedBase = new BuildSourceRestrictionSnapshot(
            new HashSet<string>([LongswordId], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        EquipmentService.SearchItems(character, "Longsword +1", restrictedBase)
            .Should().NotContain(option => option.Id == WeaponPlusOneId);
    }

    [Fact]
    public void ItemSearch_HonorsCancellationBeforeScanningTheCatalog()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var character = new Character();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Action search = () => EquipmentService.SearchItems(
            character,
            "Longsword",
            BuildSourceRestrictionSnapshot.Empty,
            cancellation.Token);

        search.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void GetItemDetail_IncludesBaseWeaponRulesForSingleItems()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(LongswordId)) return;

        var character = new Character();

        EquipmentService.AddItem(character, LongswordId).Should().BeTrue();

        var longsword = character.Inventory.Items.Should().ContainSingle().Subject;
        var detail = EquipmentService.GetItemDetail(character, longsword.Identifier);

        detail.Should().NotBeNull();
        detail!.Name.Should().Be("Longsword");
        detail.BaseName.Should().Be("Longsword");
        detail.Type.Should().Be("Weapon");
        detail.Source.Should().Be("System Reference Document");
        detail.Damage.Should().Be("1d8 slashing");
        detail.Properties.Should().Contain("Versatile");
        detail.DisplayWeight.Should().Be("3 lb.");
        detail.DisplayPrice.Should().Be("15 gp");
        detail.Sections.Should().ContainSingle()
            .Which.Should().Match<EquipmentItemDetailSectionModel>(section =>
                section.Label == "Item details" &&
                section.Name == "Longsword" &&
                section.Type == "Weapon");
    }

    [Fact]
    public void SlotSearch_AppliesSourceRestrictionsToAllowedResults()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable(LongswordId)) return;

        EquipmentService.SearchItemsForSlot(GearSlot.MainHand, "Longsword", BuildSourceRestrictionSnapshot.Empty)
            .Should().Contain(option => option.Id == LongswordId);

        var restricted = new BuildSourceRestrictionSnapshot(
            new HashSet<string>([LongswordId], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        EquipmentService.SearchItemsForSlot(GearSlot.MainHand, "Longsword", restricted)
            .Should().NotContain(option => option.Id == LongswordId);
    }

    [Fact]
    public void AllLoadedEquipmentTemplates_HaveAtLeastOneCompatibleBase()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var character = new Character();
        var templates = DataManager.Current.ElementsCollection
            .Where(element => InventoryItemFactory.GetTemplateKind(element) is not null)
            .ToList();

        templates.Should().NotBeEmpty();

        var unresolved = templates
            .Where(template =>
                InventoryItemFactory.GetCompatibleBaseItems(character.Inventory, template).Count == 0)
            .Select(template => $"{template.Name} ({template.Id})")
            .OrderBy(name => name)
            .ToList();

        unresolved.Should().BeEmpty(
            "every armor or weapon template exposed by the item picker must have a selectable base");
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

        var detail = EquipmentService.GetItemDetail(character, shield.Identifier);
        detail.Should().NotBeNull();
        detail!.Sections.Should().HaveCount(2);
        detail.Sections[0].Label.Should().Be("Magic properties");
        detail.Sections[0].Name.Should().Be("Shield, +1");
        detail.Sections[0].DescriptionHtml.Should().NotBeNullOrWhiteSpace();
        detail.Sections[1].Label.Should().Be("Base item");
        detail.Sections[1].Name.Should().Be("Shield");
        detail.Sections[1].DescriptionHtml.Should().NotBeNullOrWhiteSpace();

        EquipmentService.GetInventoryItemsForSlot(character, GearSlot.OffHand)
            .Should().ContainSingle(option =>
                option.Identifier == shield.Identifier &&
                option.Detail.Sections.Count == 2);

        shield.AdornerItem.Description = "<p>Inventory-specific magic details.</p>";
        EquipmentService.GetItemDetail(character, shield.Identifier)!
            .Sections[0].DescriptionHtml
            .Should().Be("<p>Inventory-specific magic details.</p>");
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
