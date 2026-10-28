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
            PortraitDataUrl: "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes("Aurora.Reflections/wwwroot/brand/aurora-reflections-icon.png")),
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
using var creature = context.Render<CreatureManagementCard>(p => p.Add(c => c.Model, new CreatureManagementModel
{
    PortraitSource = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes("Aurora.Reflections/wwwroot/brand/aurora-reflections-icon.png")),
    Notes = "Likes shiny coins. Remember to bring a spare repair kit.",
    PendingChoices = [new("asi:4:0", "Level 4 ability improvement", "Ability improvements"), new("spell:0", "Known spell 1", "Known spells")],
    Id = "sidekick-fixture", Name = "Rook, the party's loyal traveling companion", Subtitle = "Level 18 · Warrior sidekick",
    MaximumHp = 75, CurrentHp = 43, TemporaryHp = 8, ArmorClass = "17", HitDice = 18, HitDiceUsed = 4,
    Conditions = ["Poisoned", "Frightened"], AvailableConditions = ["Blinded", "Poisoned", "Frightened", "Prone"],
    Slots = [new(1, 4, 2), new(2, 3, 0), new(3, 2, 2)],
    Resources = [new("second-wind", "Second Wind", 1, 0, "Short or long rest", false, false), new("indomitable", "Indomitable", 2, 1, "Long rest", false, false)],
    Details = [new("Features", string.Join("\n\n", Enumerable.Repeat("A custom feature with enough detail to check reading and wrapping.", 8)), true)]
}));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "creature.html"), creature.Markup);
await dialogs.InvokeAsync(async () => await context.Services.GetRequiredService<IDialogService>()
    .ShowAsync<PickerLayoutFixture>("Divine Domain", new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true }));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "picker.html"), dialogs.Markup);

using var sidekickChoices = context.Render<SidekickChoicesPanel>(p => p.Add(c => c.Model, new SidekickChoicesModel
{
    BaseFields = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" }
        .Select(a => new SidekickBaseField("base:" + a.ToLowerInvariant(), "Base " + a, "14")).ToArray(),
    PendingChoices = [new("asi:4:0", "Level 4 ability improvement", "Ability improvements"), new("spell:0", "Known spell 1", "Known spells")],
    HitPoints = new()
    {
        BaseLevel = 1, Method = "Average", DieSize = 8,
        Help = "Each new level adds 1d8 + Constitution (minimum 1 HP). Average uses 5 before Constitution.",
        Gains = [new(2, "Average", null, "5 +2 Con = +7 HP"), new(3, "Roll", 6, "6 +2 Con = +8 HP")]
    },
    Rows = Enumerable.Range(0, 4).Select(i => new SidekickChoiceRow($"asi:{(i < 2 ? 4 : 8)}:{i % 2}",
        $"Level {(i < 2 ? 4 : 8)}: +1 ability ({i % 2 + 1}/2)", i == 0 ? "" : "Intelligence",
        new[] { "", "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" }.Select(a => new SidekickChoiceOption(a, a.Length == 0 ? "Choose…" : a)).ToArray(), "Ability improvements"))
        .Append(new("save", "Saving-throw proficiency", "Wisdom", [new("Wisdom", "Wisdom"), new("Intelligence", "Intelligence"), new("Charisma", "Charisma")], "Proficiencies"))
        .Append(new("skill:0", "Skill proficiency 1", "Arcana", [new("Arcana", "Arcana"), new("History", "History")], "Proficiencies")).ToArray(),
    Spells = [new("cantrip:0", "Cantrip 1", "Ray of Frost", "Evocation cantrip · Player's Handbook", true),
        new("spell:0", "Known spell 1", "Not chosen", "", false)],
    Summary = "AC 13 · HP 24 · Initiative +2 · Proficiency 3 · Spell attack +7 · Spell save DC 15"
}));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "sidekick-choices.html"), sidekickChoices.Markup);
