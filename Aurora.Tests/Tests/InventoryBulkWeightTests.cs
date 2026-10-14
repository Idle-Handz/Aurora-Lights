using System.Diagnostics;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.ViewModels.Shell.Items;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class InventoryBulkWeightTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public Task BulkInsertMatchesImmediateTotalsWithOneAggregateUpdate(int count) =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var inventory = manager.Character.Inventory;
            var item = EquipmentAttunementRegressionTests.Parse(
                "<element name='Weighted item' type='Item' source='Test' id='ID_BULK_WEIGHT' />").Construct<Item>();
            item.Weight = "2";
            item.Category = string.Empty;
            item.IsStackable = true;
            item.CalculableWeight = 2m;
            item.IncludeInCarriedWeight = true;
            var rows = Enumerable.Range(0, count).Select(_ => new RefactoredEquipmentItem(item) { Amount = 1 }).ToArray();
            int notifications = 0;
            inventory.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == "EquipmentWeight") notifications++;
            };

            var timer = Stopwatch.StartNew();
            foreach (var row in rows) inventory.Items.Add(row);
            timer.Stop();
            double immediateMs = timer.Elapsed.TotalMilliseconds;
            decimal expected = inventory.EquipmentWeight;
            expected.Should().Be(count * 2m);
            notifications.Should().Be(count);

            inventory.ClearInventory();
            notifications = 0;
            timer.Restart();
            using (inventory.DeferWeightCalculation())
            {
                foreach (var row in rows) inventory.Items.Add(row);
                notifications.Should().Be(0);
            }
            timer.Stop();

            inventory.EquipmentWeight.Should().Be(expected);
            notifications.Should().Be(1);
            output.WriteLine($"{count} inventory rows: immediate {immediateMs:F2} ms; deferred {timer.Elapsed.TotalMilliseconds:F2} ms. Aggregate notifications: {count} -> 1; expected row weight reads: {(long)count * (count + 1) / 2} -> {count}.");
            await Task.CompletedTask;
        });

    [Fact]
    public Task NestedScopesFlushOnExceptionAndSubsequentEditsRemainImmediate() =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var inventory = manager.Character.Inventory;
            var item = EquipmentAttunementRegressionTests.Parse(
                "<element name='Weighted item' type='Item' source='Test' id='ID_BULK_WEIGHT' />").Construct<Item>();
            item.Weight = "2";
            item.Category = string.Empty;
            item.IsStackable = true;
            item.CalculableWeight = 2m;
            item.IncludeInCarriedWeight = true;
            var first = new RefactoredEquipmentItem(item) { Amount = 1 };
            var second = new RefactoredEquipmentItem(item) { Amount = 1 };

            Action insert = () =>
            {
                using var outer = inventory.DeferWeightCalculation();
                inventory.Items.Add(first);
                using (inventory.DeferWeightCalculation()) inventory.Items.Add(second);
                inventory.EquipmentWeight.Should().Be(0);
                throw new InvalidOperationException("Simulated interrupted load");
            };
            insert.Should().Throw<InvalidOperationException>();
            inventory.EquipmentWeight.Should().Be(4);

            inventory.Items.Remove(first);
            inventory.EquipmentWeight.Should().Be(2);
            inventory.Items.Add(first);
            inventory.EquipmentWeight.Should().Be(4);
            first.Amount = 3;
            inventory.CalculateWeight().Should().Be(8);
            first.IsStored = true;
            inventory.CalculateWeight().Should().Be(2);
            first.IsStored = false;
            first.Item.CalculableWeight = 3m;
            second.Item.CalculableWeight = 3m;
            inventory.CalculateWeight().Should().Be(12);
            inventory.ClearInventory();
            inventory.EquipmentWeight.Should().Be(0);
            await Task.CompletedTask;
        });
}
