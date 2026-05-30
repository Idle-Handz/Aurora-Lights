using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data.Elements;
using Builder.Presentation.Models;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class EquipmentPackExtractionTests : IAsyncLifetime
{
    private const string ExplorersPackId = "ID_WOTC_ITEM_EXPLORERS_PACK";
    private const string BackpackId = "ID_WOTC_PHB_ITEM_BACKPACK";
    private const string BedrollId = "ID_WOTC_PHB_ITEM_BEDROLL";
    private const string TorchId = "ID_WOTC_PHB_ITEM_TORCH";
    private const string RationsId = "ID_WOTC_PHB_ITEM_RATIONS_1DAY";
    private const string RopeId = "ID_WOTC_PHB_ITEM_ROPE_HEMPEN_50FEET";

    private readonly ITestOutputHelper _output;

    public EquipmentPackExtractionTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void AllLoadedExtractablePacksReferenceResolvableItems()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var extractableItems = DataManager.Current.ElementsCollection
            .OfType<Item>()
            .Where(item => item.IsExtractable)
            .ToList();

        extractableItems.Should().NotBeEmpty("the SRD equipment packs should expose legacy extract data");

        var missing = extractableItems
            .SelectMany(pack => pack.Extractables.Keys.Select(id => new { Pack = pack.Name, Id = id }))
            .Where(reference => DataManager.Current.ElementsCollection.GetElement(reference.Id) == null)
            .ToList();

        missing.Should().BeEmpty();
    }

    [Fact]
    public void ExtractPack_ExpandsLegacyXmlComponentsAndConsumesPack()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;
        if (!EnsureElementsAvailable()) return;

        var character = new Character();
        EquipmentService.AddItem(character, ExplorersPackId).Should().BeTrue();

        var pack = character.Inventory.Items
            .Single(item => item.Item.Id.Equals(ExplorersPackId, StringComparison.OrdinalIgnoreCase));

        EquipmentService.CanExtractPack(character, pack.Identifier).Should().BeTrue();
        EquipmentService.GetPackComponents(character, pack.Identifier)
            .Select(component => component.ElementId)
            .Should().Contain(new[] { BackpackId, BedrollId, TorchId, RationsId, RopeId });

        var result = EquipmentService.ExtractPack(character, pack.Identifier);

        result.Success.Should().BeTrue();
        result.MissingElementIds.Should().BeEmpty();
        result.Added.Select(component => component.ElementId)
            .Should().Contain(new[] { BackpackId, BedrollId, TorchId, RationsId, RopeId });

        character.Inventory.Items
            .Should().NotContain(item => item.Item.Id.Equals(ExplorersPackId, StringComparison.OrdinalIgnoreCase));
        FindAmount(character, BackpackId).Should().Be(1);
        FindAmount(character, BedrollId).Should().Be(1);
        FindAmount(character, TorchId).Should().Be(10);
        FindAmount(character, RationsId).Should().Be(10);
        FindAmount(character, RopeId).Should().Be(1);
    }

    private bool EnsureElementsAvailable()
    {
        string[] requiredIds =
        [
            ExplorersPackId,
            BackpackId,
            BedrollId,
            TorchId,
            RationsId,
            RopeId,
        ];

        var missing = requiredIds
            .Where(id => DataManager.Current.ElementsCollection.GetElement(id) == null)
            .ToList();

        if (missing.Count == 0) return true;

        _output.WriteLine($"[SKIP] Missing equipment element(s): {string.Join(", ", missing)}.");
        return false;
    }

    private static int FindAmount(Character character, string elementId) =>
        character.Inventory.Items
            .Single(item => item.Item.Id.Equals(elementId, StringComparison.OrdinalIgnoreCase))
            .Amount;
}
