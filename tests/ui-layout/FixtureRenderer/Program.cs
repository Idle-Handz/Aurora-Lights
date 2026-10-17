using Aurora.Components.Models;
using Aurora.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using FixtureRenderer;

// Render the production components, including their compiler-generated CSS scopes.
// Browser tests measure these fixtures; no production markup or component CSS is copied here.
await using var context = new BunitContext();
context.Services.AddMudServices();
context.JSInterop.Mode = JSRuntimeMode.Loose;
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

var magic = new MagicOverviewModel
{
    HasSpellcasting = true,
    Sections =
    [
        new MagicSpellcastingSectionModel
        {
            Id = "cleric", Label = "Cleric", IsPreparedCaster = true,
            SpellcastingAbility = "Wisdom", SpellcastingDc = "14", SpellcastingAttack = "+6", MaxPrepared = 9,
            Cantrips = [new MagicSpellListEntryModel("ray-of-frost", "Ray of Frost", 0, "Test source")],
            SpellLevels = [new MagicSpellLevelModel(1, Enumerable.Range(0, 60)
                .Select(i => new MagicSpellListEntryModel($"spell-{i}", $"Spell {i:00}", 1, "Test source", true, false)).ToArray(), 4, 0)]
        }
    ]
};
foreach (var tab in new[] { MagicWorkspaceState.CastableTabId, "cleric" })
{
    using var spells = context.Render<CharacterMagicWorkspace>(p => p
        .Add(c => c.Model, magic)
        .Add(c => c.State, new MagicWorkspaceState { SelectedTab = tab })
        .Add(c => c.SelectedSpell, new MagicSpellDetailModel(
            "ray-of-frost", "Ray of Frost", "Test source", 0, "Evocation cantrip", "Evocation", false, false,
            "1 action", "60 feet", "V, S", "Instantaneous",
            "<p>Make a ranged spell attack. On a hit, the target takes 1d8 cold damage.</p>"
            + string.Concat(Enumerable.Repeat("<p>Additional spell effects and rules remain readable when the description is long.</p>", 15))
            + "<p>At higher levels, the damage increases to 2d8, 3d8, and 4d8.</p>")));
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"magic-{(tab == "cleric" ? "prepared" : "all")}.html"), spells.Markup);
}

using var browser = context.Render<CharacterBrowser>(p => p
    .Add(c => c.Characters, [
        new CharacterBrowserEntry("test-character", "Aurora Drive Roundtrip 2026-10-08", "Level 5 Goliath Paladin", GroupName: "Aurora Disposable Acceptance Tests"),
        new CharacterBrowserEntry("cloud-character", "Cloud Portrait Test", "Level 5 - Elf", "Wizard",
            GroupName: "Cloud Saves", IsFavorite: true,
            PortraitDataUrl: "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes("Aurora.App/wwwroot/brand/aurora-appicon.png")),
            IsCloud: true)
    ])
    .Add(c => c.DirectoryCaption, "1 character - /storage/emulated/0/Android/data/com.auroralights.app/files/5e Character Builder")
    .Add(c => c.ShowCreateButton, true)
    // Caller-provided controls have no CharacterBrowser CSS scope, just like the App's import buttons.
    .Add(c => c.AdditionalHeaderContent, builder =>
        builder.AddMarkupContent(0, "<button class=\"character-browser-create\">Import Character</button><button class=\"character-browser-create\">Open File</button>")));
using var library = context.Render<CharacterLibraryShell>(p => p
    .Add(c => c.BrowserContent, builder => builder.AddMarkupContent(0, browser.Markup)));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "characters.html"), library.Markup);

using var dialogs = context.Render<MudDialogProvider>();
await dialogs.InvokeAsync(async () => await context.Services.GetRequiredService<IDialogService>()
    .ShowAsync<PickerLayoutFixture>("Divine Domain", new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true }));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "picker.html"), dialogs.Markup);
