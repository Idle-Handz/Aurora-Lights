using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Abstractions;

namespace Aurora.Tests.Tests;

public sealed class BuildSelectionOptionResolverTests : IAsyncLifetime
{
    private const string Acolyte2024BackgroundId = "ID_WOTC_PHB24_BACKGROUND_ACOLYTE";
    private const string DexterityAsi1Id = "ID_WOTC_TCOE_OPTION_CUSTOMIZED_ASI_DEXTERITY_INCREASE_1";
    private const string DruidClassId = "ID_WOTC_PHB24_CLASS_DRUID";
    private const string ElvishLanguageId = "ID_LANGUAGE_ELVISH";
    private const string DwarvishLanguageId = "ID_LANGUAGE_DWARVISH";
    private const string HumanRaceId = "ID_RACE_HUMAN";
    private const string ResolverSortType = "Resolver Test Sort Option";
    private const string ResolverFallbackType = "Resolver Test Fallback Option";
    private const string ResolverSourceType = "Resolver Test Source Option";

    private readonly ITestOutputHelper _output;

    public BuildSelectionOptionResolverTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync() => await ContentFixture.EnsureAvailableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ResolveOptions_DoesNotDisableOwnedAbilityScoreIncreaseOptions()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = await CreateEmptyCharacterAsync();
        var background = DataManager.Current.ElementsCollection.GetElement(Acolyte2024BackgroundId);
        var ownedAsi = DataManager.Current.ElementsCollection.GetElement(DexterityAsi1Id);
        if (background is null || ownedAsi is null)
        {
            _output.WriteLine("[SKIP] 2024 Acolyte ASI content is not available.");
            return;
        }

        CharacterManager.Current.RegisterElement(background);
        CharacterManager.Current.RegisterElement(ownedAsi);
        CharacterManager.Current.ReprocessCharacter();

        var rule = FindAbilityScoreRule("Custom Ability Score Increase 1");
        var options = BuildSelectionOptionResolver.ResolveOptions(rule, number: 1);
        var dexterity = options.Should().ContainSingle(option => option.Id == DexterityAsi1Id).Subject;

        dexterity.IsDisabled.Should().BeFalse(
            "ASI selection rows are stackable by rule type; a level ASI must not invalidate a race/background ASI");
        handler.GetRegisteredElement(rule).Should().BeNull("the option is not merely enabled because it is current");
    }

    [Fact]
    public async Task ResolveOptions_DisablesOwnedNonRepeatableOptionsFromOtherSlots()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        var elvish = DataManager.Current.ElementsCollection.GetElement(ElvishLanguageId);
        if (elvish is null)
        {
            _output.WriteLine("[SKIP] Elvish language content is not available.");
            return;
        }

        CharacterManager.Current.RegisterElement(elvish);
        CharacterManager.Current.ReprocessCharacter();

        var rule = CreateStartingLanguageRule();
        var options = BuildSelectionOptionResolver.ResolveOptions(rule, number: 1);
        var elvishOption = options.Should().ContainSingle(option => option.Id == ElvishLanguageId).Subject;

        elvishOption.IsDisabled.Should().BeTrue(
            "non-repeatable choices owned outside the current slot should not be selectable again");
    }

    [Fact]
    public async Task ResolveOptions_KeepsCurrentSelectionEnabled()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = await CreateEmptyCharacterAsync();
        var rule = CreateStartingLanguageRule();

        handler.SetRegisteredElement(rule, ElvishLanguageId);
        CharacterManager.Current.ReprocessCharacter();

        var options = BuildSelectionOptionResolver.ResolveOptions(rule, number: 1);
        var elvishOption = options.Should().ContainSingle(option => option.Id == ElvishLanguageId).Subject;
        var dwarvishOption = options.Should().ContainSingle(option => option.Id == DwarvishLanguageId).Subject;

        elvishOption.IsCurrentSelection.Should().BeTrue();
        elvishOption.IsDisabled.Should().BeFalse(
            "the selected value must stay available so editing an already-filled row does not appear invalid");
        dwarvishOption.IsDisabled.Should().BeFalse("unowned language choices remain selectable");
    }

    [Fact]
    public async Task ResolveOptions_UsesRequestedSelectionNumberForCurrentSelection()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = await CreateEmptyCharacterAsync();
        var rule = CreateStartingLanguageRule();
        rule.Attributes.Number = 2;

        handler.SetRegisteredElement(rule, ElvishLanguageId, number: 1);
        handler.SetRegisteredElement(rule, DwarvishLanguageId, number: 2);

        var options = BuildSelectionOptionResolver.ResolveOptions(rule, number: 2);
        var elvishOption = options.Should().ContainSingle(option => option.Id == ElvishLanguageId).Subject;
        var dwarvishOption = options.Should().ContainSingle(option => option.Id == DwarvishLanguageId).Subject;

        elvishOption.IsCurrentSelection.Should().BeFalse();
        dwarvishOption.IsCurrentSelection.Should().BeTrue(
            "multi-pick rows must preserve the current option for the row being edited, not always slot 1");
        dwarvishOption.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveOptions_ReturnsSpellOptionsForDynamicSpellcastingRules()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        var druid = DataManager.Current.ElementsCollection.GetElement(DruidClassId);
        if (druid is null)
        {
            _output.WriteLine("[SKIP] 2024 Druid is not available in the loaded content.");
            return;
        }

        CharacterManager.Current.RegisterElement(druid);
        CharacterManager.Current.ReprocessCharacter();

        var rule = CharacterManager.Current.SelectionRules.FirstOrDefault(candidate =>
            candidate.Attributes.Type.Equals("Spell", StringComparison.OrdinalIgnoreCase) &&
            (candidate.Attributes.Supports?.Contains("$(", StringComparison.Ordinal) ?? false));

        if (rule is null)
        {
            _output.WriteLine("[SKIP] No dynamic spell-selection rule is available in the loaded content.");
            return;
        }

        var options = BuildSelectionOptionResolver.ResolveOptions(rule, number: 1);

        options.Should().NotBeEmpty(
            "dynamic spellcasting supports expressions should fall back to spell access/support matching");
        var optionTypes = options
            .Select(option => DataManager.Current.ElementsCollection.GetElement(option.Id)?.Type ?? string.Empty)
            .ToList();
        optionTypes.Should().OnlyContain(type => type == "Spell");
    }

    [Fact]
    public async Task ResolveOptions_OrdersSameNameOptionsByDescendingReleaseThenEditDate()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        const string latestReleaseId = "ID_TEST_RESOLVER_SORT_LATEST_RELEASE";
        const string latestEditId = "ID_TEST_RESOLVER_SORT_LATEST_EDIT";
        const string olderEditId = "ID_TEST_RESOLVER_SORT_OLDER_EDIT";
        ResetSyntheticElements(latestReleaseId, latestEditId, olderEditId);

        AddSyntheticElement(latestReleaseId, "Duplicate Option", ResolverSortType, "Newest Source", "<p>Release newest.</p>");
        AddSyntheticElement(latestEditId, "Duplicate Option", ResolverSortType, "Edited Source", "<p>Edit newest.</p>");
        AddSyntheticElement(olderEditId, "Duplicate Option", ResolverSortType, "Older Source", "<p>Edit older.</p>");

        var rule = CreateSelectRule(ResolverSortType, "Duplicate Option");
        var options = BuildSelectionOptionResolver.ResolveOptions(
            rule,
            settings: new BuildSelectionOptionResolverSettings
            {
                SortMetadataSelector = element => element.Id switch
                {
                    latestReleaseId => new BuildSelectionOptionSortMetadata(
                        DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
                        DateTimeOffset.Parse("2024-01-01T00:00:00Z")),
                    latestEditId => new BuildSelectionOptionSortMetadata(
                        DateTimeOffset.Parse("2020-01-01T00:00:00Z"),
                        DateTimeOffset.Parse("2026-01-01T00:00:00Z")),
                    olderEditId => new BuildSelectionOptionSortMetadata(
                        DateTimeOffset.Parse("2020-01-01T00:00:00Z"),
                        DateTimeOffset.Parse("2025-01-01T00:00:00Z")),
                    _ => null
                }
            });

        options
            .Where(option => option.Name == "Duplicate Option")
            .Select(option => option.Id)
            .Should()
            .Equal(latestReleaseId, latestEditId, olderEditId);
    }

    [Fact]
    public async Task ResolveOptions_PreservesDescriptionMarkupForRichPickerRendering()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        const string optionId = "ID_TEST_RESOLVER_RICH_DESCRIPTION";
        const string optionType = "Resolver Test Rich Description";
        const string markup =
            "<p>Choose your path.</p><table class=\"class-features\"><tr><th>Level</th><th>Feature</th></tr><tr><td>1</td><td>Spellcasting</td></tr></table>";
        ResetSyntheticElements(optionId);
        AddSyntheticElement(optionId, "Rich Option", optionType, description: markup);

        var options = BuildSelectionOptionResolver.ResolveOptions(
            CreateSelectRule(optionType, "Rich Description"));

        var option = options.Should().ContainSingle(candidate => candidate.Id == optionId).Subject;
        option.Description.Should().Contain("Choose your path.");
        option.DescriptionMarkup.Should().Be(markup);
    }

    [Fact]
    public async Task ResolveOptions_FallsBackToCaseInsensitiveSupportsForMalformedNonSpellRules()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        const string matchingId = "ID_TEST_RESOLVER_FALLBACK_MATCH";
        const string distractorId = "ID_TEST_RESOLVER_FALLBACK_DISTRACTOR";
        ResetSyntheticElements(matchingId, distractorId);

        AddSyntheticElement(matchingId, "Fallback Match", ResolverFallbackType, supports: ["resolverfallback"]);
        AddSyntheticElement(distractorId, "Fallback Distractor", ResolverFallbackType, supports: ["other-token"]);

        var rule = CreateSelectRule(ResolverFallbackType, "Malformed Supports");
        rule.Attributes.Supports = "ResolverFallback && (";

        var options = BuildSelectionOptionResolver.ResolveOptions(rule);

        options.Should().ContainSingle(option => option.Id == matchingId);
        options.Should().NotContain(option => option.Id == distractorId);
    }

    [Fact]
    public async Task ResolveOptions_ExcludesRestrictedElementsAndSources()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        const string allowedId = "ID_TEST_RESOLVER_SOURCE_ALLOWED";
        const string restrictedId = "ID_TEST_RESOLVER_SOURCE_RESTRICTED_ID";
        const string restrictedSourceId = "ID_TEST_RESOLVER_SOURCE_RESTRICTED_NAME";
        ResetSyntheticElements(allowedId, restrictedId, restrictedSourceId);

        AddSyntheticElement(allowedId, "Allowed Option", ResolverSourceType, "Allowed Source");
        AddSyntheticElement(restrictedId, "Restricted by ID", ResolverSourceType, "Allowed Source");
        AddSyntheticElement(restrictedSourceId, "Restricted by Source", ResolverSourceType, "Blocked Source");

        var options = BuildSelectionOptionResolver.ResolveOptions(
            CreateSelectRule(ResolverSourceType, "Source Restrictions"),
            settings: new BuildSelectionOptionResolverSettings
            {
                RestrictedElementIds = new HashSet<string>(
                    [restrictedId],
                    StringComparer.OrdinalIgnoreCase),
                RestrictedSourceNames = new HashSet<string>(
                    ["Blocked Source"],
                    StringComparer.OrdinalIgnoreCase)
            });

        options.Should().ContainSingle(option => option.Id == allowedId);
        options.Should().NotContain(option => option.Id == restrictedId);
        options.Should().NotContain(option => option.Id == restrictedSourceId);
    }

    [Fact]
    public async Task Query_MergesHostAndCharacterSourceRestrictions()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        const string allowedId = "ID_TEST_QUERY_SOURCE_ALLOWED";
        const string hostRestrictedId = "ID_TEST_QUERY_SOURCE_HOST_RESTRICTED";
        const string characterRestrictedId = "ID_TEST_QUERY_SOURCE_CHARACTER_RESTRICTED";
        const string optionType = "Query Test Source Option";
        ResetSyntheticElements(allowedId, hostRestrictedId, characterRestrictedId);

        AddSyntheticElement(allowedId, "Allowed Query Option", optionType, "Allowed Source");
        AddSyntheticElement(hostRestrictedId, "Host Restricted", optionType, "Allowed Source");
        AddSyntheticElement(characterRestrictedId, "Character Restricted", optionType, "Blocked Source");

        var options = BuildSelectionOptionQueryService.Query(
            CreateSelectRule(optionType, "Shared Source Restrictions"),
            hostSettings: new BuildSelectionOptionResolverSettings
            {
                RestrictedElementIds = new HashSet<string>(
                    [hostRestrictedId.ToLowerInvariant()])
            },
            sourceRestrictions: new BuildSourceRestrictionSnapshot(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(["blocked source"])));

        options.Should().ContainSingle(option => option.Id == allowedId);
        options.Should().NotContain(option => option.Id == hostRestrictedId);
        options.Should().NotContain(option => option.Id == characterRestrictedId);
    }

    [Fact]
    public async Task Query_DoesNotReintroduceRestrictedFallbackOptions()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        const string restrictedId = "ID_TEST_QUERY_RESTRICTED_FALLBACK";
        const string optionType = "Query Test Restricted Fallback";
        var restrictedFallback = new ElementBase(
            "Restricted Fallback",
            optionType,
            "Blocked Source",
            restrictedId);

        var options = BuildSelectionOptionQueryService.Query(
            CreateSelectRule(optionType, "Restricted Fallback"),
            hostSettings: new BuildSelectionOptionResolverSettings
            {
                ElementFallbackProvider = _ => [restrictedFallback]
            },
            sourceRestrictions: new BuildSourceRestrictionSnapshot(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(["Blocked Source"], StringComparer.OrdinalIgnoreCase)));

        options.Should().BeEmpty(
            "an empty shared result is authoritative when every host fallback is source-restricted");
    }

    [Fact]
    public async Task Query_ReturnsAllowedXmlFallbackOptionsAfterFiltering()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        const string allowedId = "ID_TEST_QUERY_ALLOWED_FALLBACK";
        const string restrictedId = "ID_TEST_QUERY_FILTERED_FALLBACK";
        const string optionType = "Query Test Filtered Fallback";
        var allowedFallback = new ElementBase(
            "Allowed Fallback",
            optionType,
            "Allowed Source",
            allowedId);
        var restrictedFallback = new ElementBase(
            "Restricted Fallback",
            optionType,
            "Blocked Source",
            restrictedId);

        var options = BuildSelectionOptionQueryService.Query(
            CreateSelectRule(optionType, "Filtered Fallback"),
            hostSettings: new BuildSelectionOptionResolverSettings
            {
                ElementFallbackProvider = _ => [restrictedFallback, allowedFallback]
            },
            sourceRestrictions: new BuildSourceRestrictionSnapshot(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(["Blocked Source"], StringComparer.OrdinalIgnoreCase)));

        options.Should().ContainSingle(option => option.Id == allowedId);
        options.Should().NotContain(option => option.Id == restrictedId);
    }

    [Fact]
    public async Task Query_UsesHostListFallbackWhenInlineItemsAreUnavailable()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        await CreateEmptyCharacterAsync();
        var rule = CreateSelectRule("List", "Fallback List");

        var options = BuildSelectionOptionQueryService.Query(
            rule,
            hostSettings: new BuildSelectionOptionResolverSettings
            {
                ListFallbackProvider = _ =>
                [
                    new BuildSelectionOption("1", "Recovered Item", "Recovered Item")
                ]
            },
            sourceRestrictions: BuildSourceRestrictionSnapshot.Empty);

        options.Should().ContainSingle()
            .Which.Name.Should().Be("Recovered Item");
    }

    [Fact]
    public async Task Query_MarksHostListFallbackCurrentSelection()
    {
        if (!ContentFixture.SkipIfUnavailable(_output)) return;

        var handler = await CreateEmptyCharacterAsync();
        var rule = CreateSelectRule("List", "Fallback List");
        rule.Attributes.ListItems =
        [
            new SelectionRuleListItem(1, "Recovered First"),
            new SelectionRuleListItem(2, "Recovered Current")
        ];
        handler.SetRegisteredElement(rule, "2");
        rule.Attributes.ListItems = [];

        var options = BuildSelectionOptionQueryService.Query(
            rule,
            hostSettings: new BuildSelectionOptionResolverSettings
            {
                ListFallbackProvider = _ =>
                [
                    new BuildSelectionOption("1", "Recovered First", "Recovered First"),
                    new BuildSelectionOption("2", "Recovered Current", "Recovered Current")
                ]
            },
            sourceRestrictions: BuildSourceRestrictionSnapshot.Empty);

        options.Should().ContainSingle(option => option.Id == "1")
            .Which.IsCurrentSelection.Should().BeFalse();
        options.Should().ContainSingle(option => option.Id == "2")
            .Which.IsCurrentSelection.Should().BeTrue();
    }

    private static async Task<TestSelectionRuleExpanderHandler> CreateEmptyCharacterAsync()
    {
        var handler = new TestSelectionRuleExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();

        await CharacterManager.Current.New(initializeFirstLevel: true);
        return handler;
    }

    private static SelectRule CreateStartingLanguageRule()
    {
        var rule = new SelectRule(new ElementHeader(
            "Human",
            "Race",
            "Player's Handbook",
            HumanRaceId));

        rule.Attributes.Type = "Language";
        rule.Attributes.Name = "Language (Human)";
        rule.Attributes.Supports = "Starting";
        return rule;
    }

    private static SelectRule CreateSelectRule(string type, string name)
    {
        var rule = new SelectRule(new ElementHeader(
            "Resolver Test Owner",
            "Feature",
            "Test",
            "ID_TEST_RESOLVER_OWNER"));

        rule.Attributes.Type = type;
        rule.Attributes.Name = name;
        return rule;
    }

    private static ElementBase AddSyntheticElement(
        string id,
        string name,
        string type,
        string source = "Test Source",
        string description = "<p>Test option.</p>",
        IEnumerable<string>? supports = null)
    {
        var element = new ElementBase(name, type, source, id)
        {
            Description = description
        };

        if (supports is not null)
            element.Supports.AddRange(supports);

        DataManager.Current.ElementsCollection.Add(element);
        return element;
    }

    private static void ResetSyntheticElements(params string[] ids)
    {
        foreach (string id in ids)
        {
            var existing = DataManager.Current.ElementsCollection.GetElement(id);
            if (existing is not null)
                DataManager.Current.ElementsCollection.Remove(existing);
        }
    }

    private static SelectRule FindAbilityScoreRule(string supportsToken)
    {
        var matches = CharacterManager.Current.SelectionRules.Where(rule =>
                rule.Attributes.Type.Equals("Ability Score Improvement", StringComparison.OrdinalIgnoreCase) &&
                (rule.Attributes.Supports?.Contains(supportsToken, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        matches.Should().ContainSingle(
            $"the character should expose one {supportsToken} ability-score selection rule");
        return matches[0];
    }
}
