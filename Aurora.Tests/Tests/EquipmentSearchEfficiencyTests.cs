using System.Diagnostics;
using System.Globalization;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class EquipmentSearchEfficiencyTests(ITestOutputHelper output)
{
    [Fact]
    public void CategorySearchDoesNotAllocateMembershipTablesForEveryElement()
    {
        WithCatalog(() =>
        {
            for (int index = 0; index < 10_000; index++)
                DataManager.Current.ElementsCollection.Add(new ElementBase
                {
                    ElementHeader = new("Unrelated", "Spell", "Test", $"ID_SEARCH_{index}"),
                });

            // Warm one call so static initialization and JIT costs are outside the measurement.
            EquipmentService.SearchItemsByCategory("arcane focus", "");
            long before = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            var results = EquipmentService.SearchItemsByCategory("arcane focus", "");
            timer.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            results.Should().BeEmpty();
            output.WriteLine($"Category search over 10,000 unrelated elements: {allocated:N0} bytes; {timer.Elapsed.TotalMilliseconds:N2} ms");
            allocated.Should().BeLessThan(1_000_000, "a category scan must not build its fixed ID table for each element");
        });
    }

    [Fact]
    public void SlotSearchUsesCultureIndependentSlotsAndSkipsNonItems()
    {
        WithCatalog(() =>
        {
            var item = new WeaponElement
            {
                ElementHeader = new("Test blade", "Weapon", "Test", "ID_SEARCH_BLADE"),
                Slot = "PRIMARY",
                Slots = ["PRIMARY"],
            };
            DataManager.Current.ElementsCollection.Add(item);
            for (int index = 0; index < 1_000; index++)
                DataManager.Current.ElementsCollection.Add(new ElementBase
                {
                    ElementHeader = new("Unrelated", "Spell", "Test", $"ID_SEARCH_{index}"),
                });

            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                EquipmentService.SearchItemsForSlot(GearSlot.MainHand, "Test", BuildSourceRestrictionSnapshot.Empty)
                    .Select(result => result.Id).Should().Equal(item.Id);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        });
    }

    private static void WithCatalog(Action test)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var catalog = DataManager.Current.ElementsCollection;
        var originals = catalog.ToArray();
        try
        {
            catalog.Clear();
            test();
        }
        finally
        {
            catalog.Clear();
            catalog.AddRange(originals);
        }
    }
}
