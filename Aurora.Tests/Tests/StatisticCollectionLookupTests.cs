using System.Diagnostics;
using Builder.Presentation.Services.Calculator;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class StatisticCollectionLookupTests(ITestOutputHelper output)
{
    [Fact]
    public void LookupRemainsCurrentAfterAllPublicListMutations()
    {
        var groups = new StatisticValuesGroupCollection();
        groups.GetValue("ac").Should().Be(0);
        groups.Count.Should().Be(0);
        groups.Add(Group("ac", 10));
        groups.GetValue("-ac").Should().Be(10);
        groups[0] = Group("ac", 8);
        groups.GetValue("ac").Should().Be(8);
        groups.RemoveAt(0);
        groups.GetGroup("ac", false).Should().BeNull();
        groups.Add(Group("ac", 12));
        groups.Clear();
        groups.ContainsGroup("ac").Should().BeFalse();
        groups.GetValue("ac").Should().Be(0);
        groups.GetValue(null!).Should().Be(0);
    }

    [Fact]
    public void AddGroupMergesSourcesWithoutAddingADuplicateGroup()
    {
        var groups = new StatisticValuesGroupCollection();
        groups.AddGroup(Group("ac", 10));
        var modifier = new StatisticValuesGroup("ac");
        modifier.AddValue("penalty", -2);

        groups.AddGroup(modifier);

        groups.Should().ContainSingle();
        groups.GetValue("ac").Should().Be(8);
    }

    [Fact]
    public void DuplicateNamesStillSurfaceAnInvalidCollection()
    {
        var groups = new StatisticValuesGroupCollection { Group("ac", 10), Group("ac", 20) };

        Action lookup = () => groups.GetGroup("ac");

        lookup.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void RepeatedLookupMatchesLegacyResultsAndReportsElapsedTime(int groupCount)
    {
        var groups = new StatisticValuesGroupCollection();
        for (int i = 0; i < groupCount; i++) groups.Add(Group("stat-" + i, i));
        string key = "stat-" + (groupCount - 1);
        const int iterations = 1000;
        LegacyGetValue(groups, key).Should().Be(groups.GetValue(key));
        var timer = Stopwatch.StartNew();
        long baseline = 0;
        for (int i = 0; i < iterations; i++) baseline += LegacyGetValue(groups, key);
        timer.Stop();
        double legacyMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        long optimized = 0;
        for (int i = 0; i < iterations; i++) optimized += groups.GetValue(key);
        timer.Stop();
        optimized.Should().Be(baseline);
        output.WriteLine($"{groupCount} groups, {iterations} lookups: legacy {legacyMs:F2} ms; single scan {timer.Elapsed.TotalMilliseconds:F2} ms. Expected group comparisons: {3L * groupCount * iterations} -> {(long)groupCount * iterations}.");
    }

    private static int LegacyGetValue(StatisticValuesGroupCollection groups, string name)
    {
        if (!groups.Any(group => group.GroupName == name)) return 0;
        if (!groups.Any(group => group.GroupName == name)) return 0;
        return groups.Single(group => group.GroupName == name).Sum();
    }

    private static StatisticValuesGroup Group(string name, int value)
    {
        var group = new StatisticValuesGroup(name);
        group.AddValue("source", value);
        return group;
    }
}
