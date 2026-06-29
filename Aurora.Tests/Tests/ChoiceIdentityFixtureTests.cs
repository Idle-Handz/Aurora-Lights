using System.Text.Json;
using Aurora.Tests.Helpers;

namespace Aurora.Tests.Tests;

public sealed class ChoiceIdentityFixtureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void PaladinRangerWeaponMasteryFixture_HasDistinctStableRowsForSameVisibleLabels()
    {
        var fixture = Load("paladin-ranger-weapon-mastery-identity-complete.json");
        var weaponMasteryChoices = fixture.SelectedChoices
            .Where(choice => choice.OwnerName == "Level 1: Weapon Mastery"
                && choice.SelectName == "Weapon Mastery")
            .ToList();

        weaponMasteryChoices.Should().HaveCount(4);
        weaponMasteryChoices
            .Select(choice => choice.VisibleLabelKey)
            .Distinct(StringComparer.Ordinal)
            .Should().ContainSingle("the rows intentionally have identical visible labels");

        var rowGroups = weaponMasteryChoices
            .GroupBy(choice => choice.ChoiceRowKey, StringComparer.Ordinal)
            .ToList();

        rowGroups.Should().HaveCount(2);
        rowGroups.Should().OnlyContain(group => !string.IsNullOrWhiteSpace(group.Key));
        rowGroups.Should().OnlyContain(group => group.Count() == 2);
        rowGroups.Select(group => group.Key)
            .Should().Contain(key => key.Contains("paladin_weapon_mastery", StringComparison.Ordinal));
        rowGroups.Select(group => group.Key)
            .Should().Contain(key => key.Contains("ranger_weapon_mastery", StringComparison.Ordinal));
    }

    [Fact]
    public void ChoiceIdentityMatcher_PrefersChoiceRowKeyBeforeChoiceKeySelectIdAndLabels()
    {
        var fixture = Load("paladin-ranger-weapon-mastery-identity-complete.json");
        var paladin = fixture.SelectedChoices.First(choice =>
            choice.ChoiceRowKey?.Contains("paladin_weapon_mastery", StringComparison.Ordinal) == true);
        var ranger = fixture.SelectedChoices.First(choice =>
            choice.ChoiceRowKey?.Contains("ranger_weapon_mastery", StringComparison.Ordinal) == true);

        var rows = new[]
        {
            ChoiceRow.FromSelection(paladin) with { SelectId = "same-label-paladin" },
            ChoiceRow.FromSelection(ranger) with { SelectId = "same-label-ranger" }
        };

        var rowKeyMatch = FindBestMatch(rows, new SavedChoice(
            ChoiceRowKey: ranger.ChoiceRowKey,
            ChoiceKey: paladin.ChoiceKey,
            SelectId: rows[0].SelectId,
            OwnerName: paladin.OwnerName,
            SelectName: paladin.SelectName));

        rowKeyMatch.Should().NotBeNull();
        rowKeyMatch!.ChoiceRowKey.Should().Be(ranger.ChoiceRowKey);

        var choiceKeyMatch = FindBestMatch(rows, new SavedChoice(
            ChoiceRowKey: null,
            ChoiceKey: paladin.ChoiceKey,
            SelectId: rows[1].SelectId,
            OwnerName: paladin.OwnerName,
            SelectName: paladin.SelectName));

        choiceKeyMatch.Should().NotBeNull();
        choiceKeyMatch!.ChoiceRowKey.Should().Be(paladin.ChoiceRowKey);

        var selectIdMatch = FindBestMatch(rows, new SavedChoice(
            ChoiceRowKey: null,
            ChoiceKey: null,
            SelectId: rows[1].SelectId,
            OwnerName: paladin.OwnerName,
            SelectName: paladin.SelectName));

        selectIdMatch.Should().NotBeNull();
        selectIdMatch!.ChoiceRowKey.Should().Be(ranger.ChoiceRowKey);
    }

    [Fact]
    public void ChoiceIdentityMatcher_DoesNotGuessWhenOnlyLegacyLabelsAreAmbiguous()
    {
        var fixture = Load("paladin-ranger-weapon-mastery-identity-complete.json");
        var rows = fixture.SelectedChoices
            .GroupBy(choice => choice.ChoiceRowKey, StringComparer.Ordinal)
            .Select(group => ChoiceRow.FromSelection(group.First()))
            .ToArray();

        var match = FindBestMatch(rows, new SavedChoice(
            ChoiceRowKey: null,
            ChoiceKey: null,
            SelectId: null,
            OwnerName: "Level 1: Weapon Mastery",
            SelectName: "Weapon Mastery"));

        match.Should().BeNull("legacy labels are not enough to safely restore this ambiguous choice");
    }

    [Fact]
    public void LegacyAmbiguousFixture_DemonstratesWhyStableChoiceRowsAreRequired()
    {
        var fixture = Load("paladin-ranger-weapon-mastery-identity-legacy-ambiguous.json");
        fixture.SelectedChoices.Should().OnlyContain(choice =>
            string.IsNullOrWhiteSpace(choice.ChoiceRowKey)
            && string.IsNullOrWhiteSpace(choice.ChoiceKey));

        fixture.SelectedChoices
            .GroupBy(choice => choice.VisibleLabelKey, StringComparer.Ordinal)
            .Should().ContainSingle(group => group.Count() == 4);
    }

    private static ChoiceIdentityFixture Load(string fileName)
    {
        string path = ContentFixture.GetChoiceIdentityFixturePath(fileName);
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ChoiceIdentityFixture>(json, JsonOptions)
            ?? throw new InvalidDataException($"Unable to read choice identity fixture: {path}");
    }

    private static ChoiceRow? FindBestMatch(IEnumerable<ChoiceRow> rows, SavedChoice savedChoice)
    {
        var rowList = rows.ToList();
        return MatchBy(rowList, savedChoice.ChoiceRowKey, row => row.ChoiceRowKey)
            ?? MatchBy(rowList, savedChoice.ChoiceKey, row => row.ChoiceKey)
            ?? MatchBy(rowList, savedChoice.SelectId, row => row.SelectId)
            ?? MatchByLegacyLabels(rowList, savedChoice);
    }

    private static ChoiceRow? MatchBy(
        IReadOnlyList<ChoiceRow> rows,
        string? value,
        Func<ChoiceRow, string?> selector)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var matches = rows
            .Where(row => string.Equals(selector(row), value, StringComparison.Ordinal))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static ChoiceRow? MatchByLegacyLabels(IReadOnlyList<ChoiceRow> rows, SavedChoice savedChoice)
    {
        if (string.IsNullOrWhiteSpace(savedChoice.OwnerName)
            || string.IsNullOrWhiteSpace(savedChoice.SelectName))
        {
            return null;
        }

        var matches = rows.Where(row =>
                string.Equals(row.OwnerName, savedChoice.OwnerName, StringComparison.Ordinal)
                && string.Equals(row.SelectName, savedChoice.SelectName, StringComparison.Ordinal))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private sealed record ChoiceIdentityFixture(ChoiceSelection[] SelectedChoices);

    private sealed record ChoiceSelection(
        string? ChoiceRowKey,
        string? ChoiceKey,
        string OwnerName,
        string OwnerTypeName,
        string SelectName,
        string SelectType,
        string OptionAuroraId,
        string OptionName)
    {
        public string VisibleLabelKey => $"{OwnerTypeName}|{OwnerName}|{SelectType}|{SelectName}";
    }

    private sealed record ChoiceRow(
        string? ChoiceRowKey,
        string? ChoiceKey,
        string? SelectId,
        string OwnerName,
        string SelectName)
    {
        public static ChoiceRow FromSelection(ChoiceSelection selection) =>
            new(
                selection.ChoiceRowKey,
                selection.ChoiceKey,
                SelectId: null,
                selection.OwnerName,
                selection.SelectName);
    }

    private sealed record SavedChoice(
        string? ChoiceRowKey,
        string? ChoiceKey,
        string? SelectId,
        string? OwnerName,
        string? SelectName);
}
