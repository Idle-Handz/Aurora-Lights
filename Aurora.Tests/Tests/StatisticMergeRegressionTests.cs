using Builder.Presentation.Services.Calculator;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using System.Reflection;

namespace Aurora.Tests.Tests;

public sealed class StatisticMergeRegressionTests
{
    [Fact]
    public Task SpellAttackAndSavePenaltiesReachCalculatedCharacterStatistics() =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var progression = (ProgressionManager)typeof(CharacterManager)
                .GetField("_progressionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            var profile = new SpellcastingInformation(new ElementHeader("Wizard", "Class", "Test", "ID_PENALTY_WIZARD"))
            {
                Name = "Wizard", AbilityName = "Intelligence", Prepare = true
            };
            progression.SpellcastingInformations.Add(profile);
            try
            {
                var seed = new StatisticValuesGroupCollection();
                seed.GetGroup(profile.GetSpellcasterSpellAttackStatisticName()).AddValue("base", 5);
                seed.GetGroup(profile.GetSpellcasterSpellSaveStatisticName()).AddValue("base", 13);
                seed.GetGroup(profile.GetSpellAttackStatisticName()).AddValue("penalty", -2);
                seed.GetGroup(profile.GetSpellSaveStatisticName()).AddValue("penalty", -2);

                var result = manager.StatisticsCalculator.CalculateValues(new ElementBaseCollection(), seed);

                result.GetValue(profile.GetSpellcasterSpellAttackStatisticName()).Should().Be(3);
                result.GetValue(profile.GetSpellcasterSpellSaveStatisticName()).Should().Be(11);
            }
            finally { progression.SpellcastingInformations.Remove(profile); }
            await Task.CompletedTask;
        });

    [Fact]
    public Task ArmorClassPenaltyReachesCalculatedCharacterStatistics() =>
        EquipmentAttunementRegressionTests.WithCharacter(async manager =>
        {
            var seed = new StatisticValuesGroupCollection();
            seed.GetGroup("ac:calculation").AddValue("base", 10);
            seed.GetGroup("ac:misc").AddValue("penalty", -2);

            var result = manager.StatisticsCalculator.CalculateValues(new ElementBaseCollection(), seed);

            result.GetValue("ac").Should().Be(8);
            await Task.CompletedTask;
        });

    [Theory]
    [InlineData(2, 12)]
    [InlineData(-2, 8)]
    [InlineData(0, 10)]
    public void MergeRetainsPositiveNegativeAndZeroContributions(int contribution, int expected)
    {
        var result = new StatisticValuesGroup("ac");
        result.AddValue("base", 10);
        var modifier = new StatisticValuesGroup("ac:misc");
        modifier.AddValue("modifier", contribution);

        result.Merge(modifier);

        result.Sum().Should().Be(expected);
        result.GetValues().Should().ContainKey("modifier");
    }

    [Fact]
    public void MergeRetainsAllSourcesWhenPositiveAndNegativeContributionsCancel()
    {
        var result = new StatisticValuesGroup("spellcasting:attack");
        result.AddValue("base", 5);
        var modifier = new StatisticValuesGroup("spellcasting:attack:misc");
        modifier.AddValue("bonus", 2);
        modifier.AddValue("penalty", -2);

        result.Merge(modifier);

        result.Sum().Should().Be(5);
        result.GetValues().Should().ContainKey("bonus").And.ContainKey("penalty");
    }

    [Fact]
    public void MergeIgnoresAMissingGroup()
    {
        var result = new StatisticValuesGroup("ac");
        result.AddValue("base", 10);

        result.Merge(null!);

        result.Sum().Should().Be(10);
    }
}
