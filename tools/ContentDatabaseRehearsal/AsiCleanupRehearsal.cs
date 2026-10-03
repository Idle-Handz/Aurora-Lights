using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using System.Text.Json;

internal static class AsiCleanupRehearsal
{
    public static async Task<object> Run(string root)
    {
        var roundtrip = await CharacterRehearsal.Run(root, "Art E.dnd5e");
        if (JsonSerializer.SerializeToElement(roundtrip).GetProperty("hasFailures").GetBoolean())
            throw new InvalidOperationException("The ASI character failed load/save/reopen integrity.");
        var manager = CharacterManager.Current;
        const string option = "ID_WOTC_ASI_ABILITY_SCORE_IMPROVEMENT_OPTION_1";
        const string intelligence = "ID_WOTC_TCOE_OPTION_CUSTOMIZED_ASI_INTELLIGENCE_INCREASE_2";
        const string constitution = "ID_WOTC_TCOE_OPTION_CUSTOMIZED_ASI_CONSTITUTION_INCREASE_1";
        string[] racial = [option, intelligence, constitution];
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        bool Has(string id) => manager.GetElements().Any(e => e.Id == id);
        Check(Has("ID_INTERNAL_GRANTS_BACKGROUND_ASI"), "Background ASI authority must survive.");
        Check(Has("ID_INTERNAL_ABILITY_SCORE_IMPROVEMENT_COMBINATION_STR_CON_CHA"), "Background ability combination must survive.");
        Check(racial.All(id => !Has(id)), "The entire inactive racial ASI subtree must be removed.");
        Check(AbilityScoreSelectionCleanup.Normalize().Count == 0, "Cleanup must be idempotent.");

        // A race-only character must retain legal racial choices. These edits are
        // in memory after the disposable roundtrip; no user character is saved.
        var background = manager.GetElements().Single(e => e.Type == "Background");
        manager.UnregisterElement(background);
        manager.ReprocessCharacter();
        Check(!Has("ID_INTERNAL_GRANTS_BACKGROUND_ASI"), "Fixture background grant must be removed.");
        var race = manager.GetElements().Single(e => e.Type == "Race");
        var raceRule = race.GetSelectRules().Single(r => r.Attributes.Name == "Ability Score Improvement Option (Dragonborn)");
        Check(manager.GetProgressManager(raceRule) != null, "Racial ASI should reactivate without background ASIs.");
        SelectionRuleExpanderContext.Current.SetRegisteredElement(raceRule, option);
        var parent = DataManager.Current.ElementsCollection.GetElement(option);
        SelectionRuleExpanderContext.Current.SetRegisteredElement(parent.GetSelectRules().Single(r => r.Attributes.Name == "Custom Ability Score Improvement +2"), intelligence);
        SelectionRuleExpanderContext.Current.SetRegisteredElement(parent.GetSelectRules().Single(r => r.Attributes.Name == "Custom Ability Score Improvement +1"), constitution);
        Check(AbilityScoreSelectionCleanup.Normalize().Count == 0 && racial.All(Has), "Legal racial ASIs must be retained.");
        manager.RegisterElement(background);
        manager.ReprocessCharacter();
        Check(AbilityScoreSelectionCleanup.Normalize().Count == 3 && racial.All(id => !Has(id)), "Restoring background ASIs must clean all three racial choices.");
        Check(Has("ID_INTERNAL_GRANTS_BACKGROUND_ASI"), "Background grant must remain authoritative after cleanup.");
        return new { checksPassed = 9, roundtrip };
    }
}
