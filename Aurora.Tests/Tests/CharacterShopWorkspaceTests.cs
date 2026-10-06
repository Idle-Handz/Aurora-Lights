using Aurora.Components.Models;
using Aurora.Components.Shared;
using Aurora.Tests.Helpers;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Aurora.Tests.Helpers.ShopTestData;

namespace Aurora.Tests.Tests;

public sealed class CharacterShopWorkspaceTests : BunitContext
{
    private static readonly CoinPurse TwentyGold = new(0, 0, 0, 20, 0);

    private static List<ShopItemModel> Stock() =>
    [
        Item("Longsword", "Weapons", "Weapon", 1500, subtype: "Martial Melee", weight: 3, summary: "1d8 slashing · Versatile"),
        Item("Dagger", "Weapons", "Weapon", 200, subtype: "Simple Melee", weight: 1),
        Item("Longbow", "Weapons", "Weapon", 5000, subtype: "Martial Ranged", weight: 2),
        Item("Chain Mail", "Armor", "Armor", 7500, subtype: "Heavy Armor", weight: 55),
        Item("Rope", "Adventuring Gear", priceCopper: 100, weight: 10),
        Item("Potion of Healing", "Potions", "Magic Item", 5000, "Common"),
        Item("Ring of Protection", "Rings", "Magic Item", 0, "Rare", attunement: true),
        Item("Armor, +1", "Magic Armor", "Magic Item", 0, "Rare", template: true, followsBase: true),
    ];

    private IRenderedComponent<CharacterShopWorkspace> RenderShop(
        IReadOnlyList<ShopItemModel>? catalog = null,
        IReadOnlyList<ShopInventoryEntryModel>? inventory = null,
        ShopItemDetailModel? detail = null,
        Action<string>? onSelect = null,
        Action<ShopPurchaseRequest>? onPurchase = null,
        Action<ShopSaleRequest>? onSell = null,
        bool busy = false,
        bool loading = false,
        string? error = null,
        CoinPurse? wallet = null) =>
        Render<CharacterShopWorkspace>(p => p
            .Add(c => c.Catalog, catalog ?? Stock())
            .Add(c => c.Inventory, inventory ?? [])
            .Add(c => c.Wallet, wallet ?? TwentyGold)
            .Add(c => c.Detail, detail)
            .Add(c => c.Busy, busy)
            .Add(c => c.Loading, loading)
            .Add(c => c.ErrorMessage, error)
            .Add(c => c.OnSelectItem, EventCallback.Factory.Create<string>(this, id => onSelect?.Invoke(id)))
            .Add(c => c.OnPurchase, EventCallback.Factory.Create<ShopPurchaseRequest>(this, r => onPurchase?.Invoke(r)))
            .Add(c => c.OnSell, EventCallback.Factory.Create<ShopSaleRequest>(this, r => onSell?.Invoke(r))));

    private static List<string> RowNames(IRenderedComponent<CharacterShopWorkspace> cut) =>
        cut.FindAll("button.shop-row .shop-row-name").Select(e => e.TextContent.Trim()).ToList();

    private static IElement Rail(IRenderedComponent<CharacterShopWorkspace> cut, string text) =>
        cut.FindAll("button.shop-rail-item").First(b => b.QuerySelector(".shop-rail-name")!.TextContent.Trim() == text);

    private static void SelectRow(IRenderedComponent<CharacterShopWorkspace> cut, string name) =>
        cut.FindAll("button.shop-row")
            .First(b => b.QuerySelector(".shop-row-name")!.TextContent.Trim().StartsWith(name, StringComparison.Ordinal))
            .Click();

    private static IElement Primary(IRenderedComponent<CharacterShopWorkspace> cut) =>
        cut.Find("aside .shop-button--primary");

    private static IElement FreeButton(IRenderedComponent<CharacterShopWorkspace> cut) =>
        cut.FindAll("aside .shop-actions .shop-button").First(b => b.TextContent.Contains("Add free"));

    private static string Normalised(IElement element) => string.Join(' ', element.TextContent.Split(
        (char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // ── Browsing ──────────────────────────────────────────────────────────────

    [Fact]
    public void Lists_every_item_by_name_with_prices_and_department_counts()
    {
        var cut = RenderShop();

        RowNames(cut).Should().Equal(
            "Armor, +1", "Chain Mail", "Dagger", "Longbow", "Longsword", "Potion of Healing", "Ring of Protection", "Rope");
        cut.FindAll("button.shop-row")
            .Single(b => b.TextContent.Contains("Longsword")).TextContent.Should().Contain("15 gp");

        var rail = cut.FindAll(".shop-rail-section").First().QuerySelectorAll("button.shop-rail-item")
            .Select(Normalised).ToList();
        rail.Should().Equal(
            "All items 8",
            "Weapons & Armor 4",
            "Adventuring Gear 1",
            "Potions, Poisons & Scrolls 1",
            "Magic Items 2");
    }

    [Fact]
    public void Shows_what_the_character_is_carrying_in_their_purse()
    {
        var cut = RenderShop(wallet: new CoinPurse(3, 0, 0, 12, 1));

        var wallet = Normalised(cut.Find(".shop-wallet"));

        wallet.Should().Contain("1 pp").And.Contain("12 gp").And.Contain("3 cp");
        wallet.Should().Contain("Total 22 gp 3 cp");
        Normalised(RenderShop(wallet: new CoinPurse()).Find(".shop-wallet")).Should().Contain("No coins");
    }

    [Fact]
    public void Narrowing_by_department_shelf_and_type_walks_down_the_shelves()
    {
        var cut = RenderShop();

        Rail(cut, "Weapons & Armor").Click();
        RowNames(cut).Should().BeEquivalentTo("Longsword", "Dagger", "Longbow", "Chain Mail");
        cut.FindAll(".shop-rail-title").Select(e => e.TextContent.Trim()).Should().Contain("Weapons & Armor");

        Rail(cut, "Weapons").Click();
        RowNames(cut).Should().BeEquivalentTo("Longsword", "Dagger", "Longbow");
        cut.FindAll("button.shop-chip-button").Select(b => b.TextContent.Trim().Split(' ')[0])
            .Should().Contain(["All", "Martial", "Simple"]);

        cut.FindAll("button.shop-chip-button").First(b => b.TextContent.Contains("Martial Ranged")).Click();
        RowNames(cut).Should().Equal("Longbow");
        cut.Find(".shop-crumb-path").TextContent.Should().Be("Shop › Weapons & Armor › Weapons › Martial Ranged");
    }

    [Fact]
    public void Search_narrows_the_list_and_the_department_counts_after_a_pause()
    {
        var cut = RenderShop();

        cut.Find("input[type=search]").Input("long");

        cut.WaitForAssertion(() => RowNames(cut).Should().Equal("Longbow", "Longsword"), TimeSpan.FromSeconds(3));
        cut.Find(".shop-result-count").TextContent.Should().Contain("2 of 8 items");
        cut.FindAll(".shop-rail-section").First().QuerySelectorAll("button.shop-rail-item")
            .Select(Normalised).Should().Equal("All items 2", "Weapons & Armor 2");
    }

    [Fact]
    public void A_search_with_no_matches_explains_itself_and_can_be_cleared()
    {
        var cut = RenderShop();

        cut.Find("input[type=search]").Input("zzzz");

        cut.WaitForAssertion(() => cut.Find(".shop-state-title").TextContent.Should().Be("Nothing matches"), TimeSpan.FromSeconds(3));
        cut.FindAll("button.shop-row").Should().BeEmpty();

        cut.FindAll(".shop-state .shop-button").Single(b => b.TextContent.Contains("Clear search")).Click();

        cut.FindAll("button.shop-row").Should().HaveCount(8);
        cut.Find("input[type=search]").GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public void An_emptied_department_offers_the_matches_that_exist_elsewhere()
    {
        var cut = RenderShop();
        Rail(cut, "Adventuring Gear").Click();

        cut.Find("input[type=search]").Input("sword");

        cut.WaitForAssertion(() => cut.FindAll("button.shop-row").Should().BeEmpty(), TimeSpan.FromSeconds(3));
        var offer = cut.FindAll(".shop-state .shop-button").Single();
        Normalised(offer).Should().Be("Show 1 match in other departments");
        offer.Click();
        RowNames(cut).Should().Equal("Longsword");
    }

    [Fact]
    public void Filters_by_affordability_price_rarity_and_sort_order()
    {
        var cut = RenderShop();

        cut.FindAll(".shop-filters .shop-toggle input")[0].Change(true);
        RowNames(cut).Should().Equal("Dagger", "Longsword", "Rope");

        cut.FindAll(".shop-filters .shop-toggle input")[0].Change(false);
        cut.FindAll(".shop-filters .shop-toggle input")[1].Change(true);
        RowNames(cut).Should().NotContain(["Ring of Protection", "Armor, +1"]);
        cut.FindAll(".shop-filters .shop-toggle input")[1].Change(false);

        cut.Find(".shop-filters select").Change("Rare");
        RowNames(cut).Should().Equal("Armor, +1", "Ring of Protection");

        cut.Find(".shop-filters select").Change(string.Empty);
        cut.Find(".shop-sort select").Change(nameof(ShopSort.PriceHighToLow));
        RowNames(cut).First().Should().Be("Chain Mail");
        RowNames(cut).TakeLast(2).Should().BeEquivalentTo("Armor, +1", "Ring of Protection");
    }

    [Fact]
    public void Shows_more_rows_a_page_at_a_time()
    {
        var many = Enumerable.Range(1, 250)
            .Select(i => Item($"Widget {i:000}", priceCopper: i))
            .ToList();
        var cut = RenderShop(many);

        cut.FindAll("button.shop-row").Should().HaveCount(100);
        Normalised(cut.Find(".shop-more .shop-button")).Should().Be("Show 100 more 150 left");

        cut.Find(".shop-more .shop-button").Click();

        cut.FindAll("button.shop-row").Should().HaveCount(200);
        Normalised(cut.Find(".shop-more .shop-button")).Should().Be("Show 50 more 50 left");
    }

    [Fact]
    public void Shows_loading_and_error_states_instead_of_rows()
    {
        var loading = RenderShop(loading: true);
        loading.FindAll(".shop-spinner").Should().HaveCount(1);
        loading.FindAll("button.shop-row").Should().BeEmpty();

        var failed = RenderShop(error: "The catalog could not be built.");
        failed.Find("[role=alert]").TextContent.Should().Contain("could not be built");
    }

    // ── Buying ────────────────────────────────────────────────────────────────

    [Fact]
    public void Selecting_an_item_asks_the_host_for_its_details_and_quotes_the_price()
    {
        string? requested = null;
        var cut = RenderShop(onSelect: id => requested = id);

        SelectRow(cut, "Longsword");

        requested.Should().Be("ID_LONGSWORD");
        cut.Find(".shop-detail-name").TextContent.Should().Be("Longsword");
        cut.Find("aside").TextContent.Should().Contain("Loading details");
        Normalised(Primary(cut)).Should().Be("Buy · 15 gp");
        Normalised(cut.Find(".shop-summary")).Should().Contain("Total 15 gp").And.Contain("After buying 5 gp");
    }

    [Fact]
    public void Shows_stats_and_rules_text_once_the_details_arrive()
    {
        var cut = RenderShop();
        SelectRow(cut, "Longsword");

        cut.Render(p => p.Add(c => c.Detail, new ShopItemDetailModel(
            "ID_LONGSWORD", "Longsword", "<p>A versatile blade.</p>", "A versatile blade.",
            [new ShopStatModel("Damage", "1d8 slashing")], [])));

        cut.Find(".shop-stats").TextContent.Should().Contain("Damage").And.Contain("1d8 slashing");
        cut.Find(".shop-description").TextContent.Should().Contain("A versatile blade.");
        cut.Find("aside").TextContent.Should().NotContain("Loading details");
    }

    [Fact]
    public void Buy_sends_the_request_for_the_chosen_quantity()
    {
        ShopPurchaseRequest? sent = null;
        var cut = RenderShop(onPurchase: r => sent = r);
        SelectRow(cut, "Dagger");

        cut.Find("button[aria-label='One more']").Click();
        cut.Find("button[aria-label='One more']").Click();
        Normalised(Primary(cut)).Should().Be("Buy · 6 gp");
        Primary(cut).Click();

        sent.Should().Be(new ShopPurchaseRequest("ID_DAGGER", null, 3, null, Free: false));
    }

    [Fact]
    public void Quantity_can_be_typed_and_stays_within_bounds()
    {
        var cut = RenderShop();
        SelectRow(cut, "Rope");

        cut.Find("input[aria-label=Quantity]").Change("5");
        Normalised(Primary(cut)).Should().Be("Buy · 5 gp");

        cut.Find("input[aria-label=Quantity]").Change("-4");
        cut.Find("input[aria-label=Quantity]").GetAttribute("value").Should().Be("1");

        cut.Find("input[aria-label=Quantity]").Change("99999");
        cut.Find("input[aria-label=Quantity]").GetAttribute("value").Should().Be(ShopPricing.MaxQuantity.ToString());
    }

    [Fact]
    public void An_item_the_character_cannot_afford_says_by_how_much_but_can_still_be_added_free()
    {
        ShopPurchaseRequest? sent = null;
        var cut = RenderShop(onPurchase: r => sent = r);
        SelectRow(cut, "Chain Mail");

        Primary(cut).HasAttribute("disabled").Should().BeTrue();
        cut.Find(".shop-problem").TextContent.Should().Be("You need 55 gp more.");
        FreeButton(cut).HasAttribute("disabled").Should().BeFalse();

        FreeButton(cut).Click();

        sent.Should().Be(new ShopPurchaseRequest("ID_CHAIN_MAIL", null, 1, null, Free: true));
    }

    [Fact]
    public void An_item_with_no_price_asks_for_one_before_it_can_be_bought()
    {
        ShopPurchaseRequest? sent = null;
        var cut = RenderShop(onPurchase: r => sent = r);
        SelectRow(cut, "Ring of Protection");

        Primary(cut).HasAttribute("disabled").Should().BeTrue();
        cut.Find(".shop-problem").TextContent.Should().Contain("Set a price");
        cut.Find("aside input[step='0.01']").Should().NotBeNull();

        cut.Find("aside input[step='0.01']").Change("2.5");
        Normalised(Primary(cut)).Should().Be("Buy · 2 gp 5 sp");
        Primary(cut).Click();

        sent.Should().Be(new ShopPurchaseRequest("ID_RING_OF_PROTECTION", null, 1, 250, Free: false));
    }

    [Fact]
    public void A_priced_item_can_be_haggled_with_a_price_of_the_shoppers_own()
    {
        ShopPurchaseRequest? sent = null;
        var cut = RenderShop(onPurchase: r => sent = r);
        SelectRow(cut, "Longsword");
        cut.FindAll("aside input[step='0.01']").Should().BeEmpty("a listed price needs no price field until asked");

        cut.Find("aside .shop-toggle input").Change(true);
        cut.Find("aside input[step='0.01']").Change("10");
        Primary(cut).Click();

        sent.Should().Be(new ShopPurchaseRequest("ID_LONGSWORD", null, 1, 1000, Free: false));
    }

    [Fact]
    public void A_magic_template_needs_a_base_item_chosen_and_prices_it()
    {
        ShopPurchaseRequest? sent = null;
        var cut = RenderShop(onPurchase: r => sent = r, wallet: new CoinPurse(0, 0, 0, 100, 0));
        SelectRow(cut, "Armor, +1");
        cut.Render(p => p.Add(c => c.Detail, new ShopItemDetailModel(
            "ID_ARMOR_+1", "Armor, +1", string.Empty, string.Empty, [],
            [new ShopBaseOptionModel("ID_CHAIN", "Chain Mail", 7500), new ShopBaseOptionModel("ID_PLATE", "Plate", 150000)])));

        Primary(cut).HasAttribute("disabled").Should().BeTrue();
        FreeButton(cut).HasAttribute("disabled").Should().BeTrue();
        cut.Find(".shop-problem").TextContent.Should().Be("Choose a base item first.");
        cut.FindAll("aside select option").Select(o => Normalised(o)).Should().Contain(["Chain Mail — 75 gp", "Plate — 1,500 gp"]);

        cut.Find("aside select").Change("ID_CHAIN");

        Normalised(Primary(cut)).Should().Be("Buy · 75 gp");
        Primary(cut).Click();
        sent.Should().Be(new ShopPurchaseRequest("ID_ARMOR_+1", "ID_CHAIN", 1, null, Free: false));
    }

    [Fact]
    public void A_template_with_a_single_compatible_base_picks_it_for_the_shopper()
    {
        var cut = RenderShop(wallet: new CoinPurse(0, 0, 0, 100, 0));
        SelectRow(cut, "Armor, +1");

        cut.Render(p => p.Add(c => c.Detail, new ShopItemDetailModel(
            "ID_ARMOR_+1", "Armor, +1", string.Empty, string.Empty, [],
            [new ShopBaseOptionModel("ID_CHAIN", "Chain Mail", 7500)])));

        cut.Find("aside select").GetAttribute("value").Should().Be("ID_CHAIN");
        Normalised(Primary(cut)).Should().Be("Buy · 75 gp");
    }

    [Fact]
    public void Actions_are_disabled_while_a_transaction_is_running()
    {
        var cut = RenderShop(busy: true);
        SelectRow(cut, "Dagger");

        Primary(cut).HasAttribute("disabled").Should().BeTrue();
        FreeButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Owned_items_are_marked_in_the_list_and_the_details()
    {
        var cut = Render<CharacterShopWorkspace>(p => p
            .Add(c => c.Catalog, Stock())
            .Add(c => c.Wallet, TwentyGold)
            .Add(c => c.OwnedCounts, new Dictionary<string, int> { ["ID_ROPE"] = 2 }));

        cut.FindAll("button.shop-row").Single(b => b.TextContent.Contains("Rope"))
            .TextContent.Should().Contain("Owned ×2");

        SelectRow(cut, "Rope");
        cut.Find(".shop-detail-tags").TextContent.Should().Contain("Owned ×2");
    }

    [Fact]
    public void Escape_closes_the_details()
    {
        var cut = RenderShop();
        SelectRow(cut, "Dagger");
        cut.FindAll(".shop--selected").Should().HaveCount(1);

        cut.Find("aside.shop-detail").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll(".shop--selected").Should().BeEmpty();
        cut.Find(".shop-detail-empty").TextContent.Should().Contain("Pick something to buy");
    }

    // ── Selling ───────────────────────────────────────────────────────────────

    private static List<ShopInventoryEntryModel> Pack() =>
    [
        Owned("Longsword", "a", 1500, category: "Weapons", type: "Weapon", equipped: true),
        Owned("Torch", "b", 100, amount: 3),
        Owned("Amulet of Health", "c", 0, category: "Wondrous Items", type: "Magic Item", rarity: "Rare"),
    ];

    [Fact]
    public void Sell_mode_lists_the_pack_with_what_each_would_fetch()
    {
        var cut = RenderShop(inventory: Pack());

        cut.FindAll(".shop-mode-button").Single(b => b.TextContent.Contains("Sell")).Click();

        RowNames(cut).Should().Equal("Amulet of Health", "Longsword", "Torch×3");
        var rows = cut.FindAll("button.shop-row").ToDictionary(
            b => b.QuerySelector(".shop-row-name")!.TextContent.Trim().Split(' ', '×')[0], Normalised);
        rows["Longsword"].Should().Contain("7 gp 5 sp").And.NotContain("total").And.Contain("Equipped");
        rows["Torch"].Should().Contain("×3").And.Contain("1 gp 5 sp total");
        rows["Amulet"].Should().Contain("No value");
        cut.Find(".shop-mode-button.is-active").TextContent.Should().Contain("Sell");
    }

    [Fact]
    public void A_cheap_item_that_rounds_down_to_nothing_is_not_called_worthless()
    {
        var cut = RenderShop(inventory: [Owned("Copper Nail", "n", 1)]);

        cut.FindAll(".shop-mode-button").Single(b => b.TextContent.Contains("Sell")).Click();

        Normalised(cut.Find("button.shop-row")).Should().Contain("Under 1 cp").And.NotContain("No value");
    }

    [Fact]
    public void Sell_quotes_the_proceeds_and_sends_the_request()
    {
        ShopSaleRequest? sent = null;
        var cut = RenderShop(inventory: Pack(), onSell: r => sent = r);
        cut.FindAll(".shop-mode-button").Single(b => b.TextContent.Contains("Sell")).Click();
        SelectRow(cut, "Longsword");

        cut.Find(".shop-notice").TextContent.Should().Contain("equipped");
        Normalised(Primary(cut)).Should().Be("Sell · 7 gp 5 sp");

        cut.Find("aside select").Change("100");
        Normalised(Primary(cut)).Should().Be("Sell · 15 gp");
        Primary(cut).Click();

        sent.Should().Be(new ShopSaleRequest("a", 1, 100, null));
    }

    [Fact]
    public void Sell_quantity_cannot_exceed_what_is_owned()
    {
        var cut = RenderShop(inventory: Pack());
        cut.FindAll(".shop-mode-button").Single(b => b.TextContent.Contains("Sell")).Click();
        SelectRow(cut, "Torch");

        for (int i = 0; i < 6; i++)
            cut.Find("button[aria-label='One more']").Click();

        cut.Find("input[aria-label=Quantity]").GetAttribute("value").Should().Be("3");
        cut.Find("button[aria-label='One more']").HasAttribute("disabled").Should().BeTrue();
        Normalised(Primary(cut)).Should().Be("Sell · 1 gp 5 sp");
    }

    [Fact]
    public void Sell_of_an_item_with_no_value_asks_for_a_price()
    {
        ShopSaleRequest? sent = null;
        var cut = RenderShop(inventory: Pack(), onSell: r => sent = r);
        cut.FindAll(".shop-mode-button").Single(b => b.TextContent.Contains("Sell")).Click();
        SelectRow(cut, "Amulet");

        Primary(cut).HasAttribute("disabled").Should().BeTrue();
        cut.Find(".shop-problem").TextContent.Should().Contain("Set a price");

        cut.Find("aside input[step='0.01']").Change("120");
        Primary(cut).Click();

        sent.Should().Be(new ShopSaleRequest("c", 1, 50, 12000));
    }

    [Fact]
    public void Switching_modes_clears_the_selection_and_the_filters()
    {
        var cut = RenderShop(inventory: Pack());
        Rail(cut, "Weapons & Armor").Click();
        SelectRow(cut, "Dagger");

        cut.FindAll(".shop-mode-button").Single(b => b.TextContent.Contains("Sell")).Click();
        cut.FindAll(".shop--selected").Should().BeEmpty();
        Normalised(cut.Find(".shop-crumb-path")).Should().Be("Your pack");

        cut.FindAll(".shop-mode-button").Single(b => b.TextContent.Contains("Buy")).Click();
        RowNames(cut).Should().HaveCount(8);
    }

    [Fact]
    public void An_empty_pack_says_so()
    {
        var cut = RenderShop(inventory: []);

        cut.FindAll(".shop-mode-button").Single(b => b.TextContent.Contains("Sell")).Click();

        cut.Find(".shop-state-title").TextContent.Should().Be("Your pack is empty");
    }

    // ── Accessibility ─────────────────────────────────────────────────────────

    [Fact]
    public void Controls_expose_their_state_to_assistive_technology()
    {
        var cut = RenderShop();

        cut.Find("[role=tablist]").GetAttribute("aria-label").Should().Be("Shop mode");
        cut.FindAll("[role=tab]").Select(t => t.GetAttribute("aria-selected")).Should().Equal("true", "false");
        cut.Find("input[type=search]").ParentElement!.TextContent.Should().Contain("Search the shop");
        cut.Find(".shop-result-count").GetAttribute("aria-live").Should().Be("polite");
        cut.FindAll("button.shop-row").Should().OnlyContain(b => b.GetAttribute("aria-pressed") == "false");

        SelectRow(cut, "Dagger");

        cut.FindAll("button.shop-row").Single(b => b.GetAttribute("aria-pressed") == "true")
            .TextContent.Should().Contain("Dagger");
        Rail(cut, "All items").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public void An_unaffordable_price_is_flagged_in_text_as_well_as_colour()
    {
        var cut = RenderShop();

        var row = cut.FindAll("button.shop-row").Single(b => b.TextContent.Contains("Chain Mail"));

        row.QuerySelector(".shop-row-price")!.ClassList.Should().Contain("is-unaffordable");
        row.TextContent.Should().Contain("you cannot afford this");
    }
}
