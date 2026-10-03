using Aurora.Tests.Helpers;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Shell.Items;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class CharacterLoadCompatibilityTests : IAsyncLifetime
{
    private const string LongswordId = "ID_WOTC_PHB_WEAPON_LONGSWORD";
    private readonly ITestOutputHelper _output;

    public CharacterLoadCompatibilityTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisterLoadedEquipmentElements_RegistersEquippedWeaponBeforeValidation()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CharacterManager.Current.New(initializeFirstLevel: false);
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        var weapon = DataManager.Current.ElementsCollection.GetElement(LongswordId)
            .Should().BeOfType<WeaponElement>().Subject;
        var inventoryItem = new RefactoredEquipmentItem(weapon);
        inventoryItem.IsEquipped = true;
        inventoryItem.EquippedLocation = "Two-Handed";
        CharacterManager.Current.Character.Inventory.Items.Add(inventoryItem);

        int countBefore = CharacterManager.Current.GetElements().Count;
        CharacterLoadCompatibilityService.RegisterLoadedEquipmentElements(CharacterManager.Current.Character);

        CharacterManager.Current.GetElements().Count.Should().Be(countBefore + 1);
        CharacterManager.Current.GetElements().Should().Contain(element => element.Id == LongswordId);

        CharacterLoadCompatibilityService.RestoreEquippedSlots(CharacterManager.Current.Character);
        CharacterManager.Current.Character.Inventory.EquippedPrimary.Should().BeSameAs(inventoryItem);
        CharacterManager.Current.GetElements().Count.Should().Be(countBefore + 1,
            "slot restoration must not register an already-registered equipped item twice");
    }

    [Fact]
    public async Task Load_RegistersEquippedInventoryElementsBeforeElementValidation()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
        await new CharacterFile(ContentFixture.GetCharacterFixturePath("prepared-paladin.dnd5e")).Load();

        CharacterManager.Current.GetElements().Should().Contain(element => element.Id == LongswordId,
            "an equipped weapon is part of the saved element sum even when it is stored under equipment");
    }
}
