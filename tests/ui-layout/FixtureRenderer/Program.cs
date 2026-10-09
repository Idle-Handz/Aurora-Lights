using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components;

// Render the production components, including their compiler-generated CSS scopes.
// Browser tests measure these fixtures; no production markup or component CSS is copied here.
using var context = new BunitContext();
var catalog = Enumerable.Range(0, 120).Select(i => new ShopItemModel(
    "item" + i, $"Supply {i:000}", "Item", "Test source", "Adventuring Gear", "Adventuring Gear",
    "Supplies", "Common", 100, "1 lb.", 1, "Travel supplies", false, true, false, false, $"supply {i}")).ToArray();

foreach (var longDescription in new[] { false, true })
{
    using var shop = context.Render<CharacterShopWorkspace>(p => p
        .Add(c => c.Catalog, catalog)
        .Add(c => c.Wallet, new CoinPurse(0, 0, 0, 200, 0))
        .Add(c => c.Detail, new ShopItemDetailModel("item0", "Supply 000",
            string.Concat(Enumerable.Repeat("<p>Supplies for the journey, packed for safe travel and everyday use.</p>", longDescription ? 30 : 1)),
            "", [], [])));
    shop.Find("button.shop-row").Click();
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, longDescription ? "shop-long.html" : "shop-short.html"), shop.Markup);
}

var equipment = new EquipmentOverviewModel
{
    InventoryItems = new[] { "Sword", "Staff of Flowers from the Dungeon Master's Guide", "LongswordoftheVeryLongUnbrokenInventoryName" }
        .Select((name, i) => new EquipmentInventoryItemModel($"item-{i}", name, 2, true, true, "Main Hand", true, true, "3 lb.", "15 gp", true, true)).ToArray()
};
var action = EventCallback.Factory.Create<string>(equipment, _ => { });
using var inventory = context.Render<CharacterEquipmentWorkspace>(p => p
    .Add(c => c.Model, equipment)
    .Add(c => c.OnOpenItemDetails, action)
    .Add(c => c.OnEditItem, action)
    .Add(c => c.OnRemoveItem, action)
    .Add(c => c.OnToggleEquipped, action)
    .Add(c => c.OnToggleAttuned, action)
    .Add(c => c.OnToggleVersatileWield, action));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "equipment.html"), inventory.Markup);

using var browser = context.Render<CharacterBrowser>(p => p
    .Add(c => c.Characters, [new CharacterBrowserEntry("test-character", "Aurora Drive Roundtrip 2026-10-08", "Level 5 Goliath Paladin", GroupName: "Aurora Disposable Acceptance Tests")])
    .Add(c => c.DirectoryCaption, "1 character - /storage/emulated/0/Android/data/com.auroralights.app/files/5e Character Builder")
    .Add(c => c.ShowCreateButton, true)
    // Caller-provided controls have no CharacterBrowser CSS scope, just like the App's import buttons.
    .Add(c => c.AdditionalHeaderContent, builder =>
        builder.AddMarkupContent(0, "<button class=\"character-browser-create\">Import Character</button><button class=\"character-browser-create\">Open File</button>")));
using var library = context.Render<CharacterLibraryShell>(p => p
    .Add(c => c.BrowserContent, builder => builder.AddMarkupContent(0, browser.Markup)));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "characters.html"), library.Markup);
