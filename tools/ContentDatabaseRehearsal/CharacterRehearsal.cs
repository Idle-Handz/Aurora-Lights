using Aurora.App.Services;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

internal static class CharacterRehearsal
{
    /// <summary>
    /// The heaviest character in the corpus loads in about two minutes on its own, so a suite
    /// running several processes at once was reporting it as a difference when it had only run out
    /// of time. The limit is here to catch a hang, not to measure the machine.
    /// </summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromMinutes(5);

    public static async Task<object> Run(string root, string output, string? onlyFile = null, bool afterReload = false, bool editBackground = false)
    {
        var trace = new CharacterTrace(Path.Combine(output, "character-trace.log"));
        Builder.Core.Logging.Logger.RegisterLogger(trace);
        if (afterReload)
        {
            string database = Path.Combine(root, "custom", ContentDatabaseService.DatabaseFileName);
            string current = Path.Combine(root, "current-v12.sqlite");
            File.Copy(database, current, true);
            try
            {
                File.Copy(Path.Combine(root, "previous-v11.sqlite"), database, true);
                var previous = await DbElementLoader.TryLoadAsync(DataManager.Current.ElementsCollection);
                if (!previous.Success) throw new InvalidOperationException(previous.Summary);
                Reset();
                var initial = await new CharacterFile(Path.Combine(root, "characters", onlyFile ?? "Test E.dnd5e"))
                    .Load().WaitAsync(LoadTimeout);
                File.WriteAllText(Path.Combine(output, "before-reload.json"), JsonSerializer.Serialize(new {
                    previous.DataVersion, initial, missingGrants = trace.MissingGrants.ToArray(), state = Capture() }));
                trace.MissingGrants.Clear();
            }
            finally { File.Copy(current, database, true); }
        }
        var loaded = await DbElementLoader.TryLoadAsync(DataManager.Current.ElementsCollection);
        if (!loaded.Success) throw new InvalidOperationException(loaded.Summary);
        File.WriteAllText(Path.Combine(output, "proficiency-projection.json"), JsonSerializer.Serialize(
            DataManager.Current.ElementsCollection.Where(e => e.Type == "Proficiency")
                .Select(e => new { e.Id, ContentFilePath = ElementProvenance.GetContentFilePath(e), grants = e.GetGrantRules().Select(g => g.Attributes.Name).ToArray() }),
            new JsonSerializerOptions { WriteIndented = true }));
        var results = new List<object>();
        int shards = int.TryParse(Environment.GetEnvironmentVariable("REHEARSAL_SHARDS"), out int count) ? count : 1;
        int shard = int.TryParse(Environment.GetEnvironmentVariable("REHEARSAL_SHARD"), out int index) ? index : 0;
        if (shards < 1 || shard < 0 || shard >= shards) throw new ArgumentException("Invalid rehearsal shard.");
        foreach (string path in Directory.GetFiles(Path.Combine(root, "characters"), "*.dnd5e")
            .OrderBy(p => p, StringComparer.Ordinal)
            .Where(p => onlyFile == null || Path.GetFileName(p) == onlyFile)
            .Where((_, index) => index % shards == shard))
        {
            string originalHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            try
            {
                Reset();
                var file = new CharacterFile(path);
                File.WriteAllText(Path.Combine(output, "character-phase.json"), JsonSerializer.Serialize(new { file = Path.GetFileName(path), phase = "first load" }));
                var first = await Task.Run(() => file.Load()).WaitAsync(LoadTimeout);
                CharacterLoadCompatibilityService.RestoreEquippedSlots(CharacterManager.Current.Character);
                if (editBackground) ExerciseBackgroundReplacement();
                var firstState = Capture();
                File.WriteAllText(Path.Combine(output, "choice-diagnostics-" + Path.GetFileName(path) + ".json"),
                    JsonSerializer.Serialize(CharacterManager.Current.GetElements()
                        .Where(e => e.Type is "Race" or "Ability Score Improvement")
                        .Select(e => new { e.Id, rules = e.GetSelectRules().Select(rule => new {
                            rule.UniqueIdentifier, rule.Attributes.Name,
                            hasProgressManager = CharacterManager.Current.GetProgressManager(rule) != null,
                            registeredId = (SelectionRuleExpanderContext.Current!.GetRegisteredElement(rule) as ElementBase)?.Id
                        }).ToArray() }), new JsonSerializerOptions { WriteIndented = true }));
                string savedPath = Path.Combine(output, "roundtrip", Path.GetFileName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(savedPath)!);
                // Exact shared writer, redirected to a disposable output path.
                File.WriteAllBytes(savedPath, file.SerializeCharacter(CharacterManager.Current.Character));
                Reset();
                var secondFile = new CharacterFile(savedPath);
                File.WriteAllText(Path.Combine(output, "character-phase.json"), JsonSerializer.Serialize(new { file = Path.GetFileName(path), phase = "roundtrip load" }));
                var second = await Task.Run(() => secondFile.Load()).WaitAsync(LoadTimeout);
                CharacterLoadCompatibilityService.RestoreEquippedSlots(CharacterManager.Current.Character);
                var secondState = Capture();
                string secondPath = savedPath + ".second.xml";
                File.WriteAllBytes(secondPath, secondFile.SerializeCharacter(CharacterManager.Current.Character));
                var choice1 = Choices(savedPath);
                var choice2 = Choices(secondPath);
                results.Add(new { file = Path.GetFileName(path), first, second, firstState, secondState,
                    stateStable = JsonSerializer.Serialize(firstState) == JsonSerializer.Serialize(secondState),
                    choiceCount = choice1.Length, choicesStable = choice1.SequenceEqual(choice2), lostChoices = choice1.Except(choice2).ToArray(),
                    addedChoices = choice2.Except(choice1).ToArray(),
                    inventoryStable = Inventory(savedPath) == Inventory(secondPath),
                    originalCopyUnchanged = originalHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) });
            }
            catch (TimeoutException error)
            {
                results.Add(new { file = Path.GetFileName(path), error = error.ToString() });
                break; // Do not run another character beside the timed-out shared singleton.
            }
            catch (Exception error) { results.Add(new { file = Path.GetFileName(path), error = error.ToString() }); }
            File.WriteAllText(Path.Combine(output, "character-progress.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        bool hasFailures = results.Select(r => JsonSerializer.SerializeToElement(r)).Any(r =>
            r.TryGetProperty("error", out _) || !r.GetProperty("first").GetProperty("Success").GetBoolean()
            || !r.GetProperty("second").GetProperty("Success").GetBoolean() || !r.GetProperty("stateStable").GetBoolean()
            || !r.GetProperty("choicesStable").GetBoolean() || !r.GetProperty("inventoryStable").GetBoolean());
        hasFailures |= trace.MissingGrants.Count != 0;
        return new { dataVersion = loaded.DataVersion, editBackground, hasFailures, missingGrants = trace.MissingGrants,
            loadWarnings = trace.LoadWarnings, results };
    }

    private static void ExerciseBackgroundReplacement()
    {
        var manager = CharacterManager.Current;
        var handler = SelectionRuleExpanderContext.Current!;
        var background = manager.GetElements().Single(e => e.Type == "Background");
        var backgroundChoice = background.Aquisition.SelectRule
            ?? throw new InvalidOperationException("The background must be a registered choice.");
        handler.SetRegisteredElement(backgroundChoice, "ID_WOTC_AAG_BACKGROUND_ASTRAL_DRIFTER");
        var astral = manager.GetElements().Single(e => e.Type == "Background");
        var languages = astral.GetSelectRules().Single(r => r.Attributes.Type == "Language");
        handler.SetRegisteredElement(languages, "ID_LANGUAGE_AARAKOCRA", 1);
        handler.SetRegisteredElement(languages, "ID_WOTC_PSA_LANGUAGE_AVEN", 2);
        var feat = manager.GetElements().Single(e => e.Id == "ID_PHB_FEAT_MAGICINITIATE");
        handler.SetRegisteredElement(feat.GetSelectRules().Single(), "ID_PHB_FEAT_MAGIC_INITIATE_SORCERER");
        var sorcerer = manager.GetElements().Single(e => e.Id == "ID_PHB_FEAT_MAGIC_INITIATE_SORCERER");
        var cantrips = sorcerer.GetSelectRules().Single(r => r.Attributes.Number == 2);
        var spell = sorcerer.GetSelectRules().Single(r => r.Attributes.Number == 1);
        handler.SetRegisteredElement(cantrips, "ID_DMSG_GRGR_SPELL_BLOOD_SIPHON", 1);
        handler.SetRegisteredElement(cantrips, "ID_WOTC_PHB24_SPELL_FIRE_BOLT", 2);
        handler.SetRegisteredElement(spell, "ID_WOTC_UA20200326_SPELL_ACID_STREAM");
        string[] removedIds = ["ID_LANGUAGE_AARAKOCRA", "ID_WOTC_PSA_LANGUAGE_AVEN",
            "ID_PHB_FEAT_MAGIC_INITIATE_SORCERER", "ID_DMSG_GRGR_SPELL_BLOOD_SIPHON",
            "ID_WOTC_PHB24_SPELL_FIRE_BOLT", "ID_WOTC_UA20200326_SPELL_ACID_STREAM"];
        if (removedIds.Any(id => !manager.GetElements().Any(e => e.Id == id)))
            throw new InvalidOperationException("The old background fixture did not acquire all six selections.");
        handler.SetRegisteredElement(backgroundChoice, "ID_WOTC_PHB24_BACKGROUND_GUARD");
        manager.ReprocessCharacter();
        if (removedIds.Any(id => manager.GetElements().Any(e => e.Id == id)))
            throw new InvalidOperationException("Changing to Guard retained an orphaned language or spell choice.");
        foreach (var rule in new[] { languages, feat.GetSelectRules().Single(), cantrips, spell })
            for (int slot = 1; slot <= Math.Max(1, rule.Attributes.Number); slot++)
                if (handler.GetRegisteredElement(rule, slot) != null)
                    throw new InvalidOperationException("An obsolete choice registry entry survived the background change.");
    }

    private static void Reset()
    {
        SelectionRuleExpanderContext.Current = new MauiSelectionRuleExpanderHandler();
        GrantPolicyContext.Current = new RestrictedSourceGrantPolicy();
        SpellcastingSectionContext.Current = new MauiSpellcastingSectionHandler();
        CharacterLoadCompatibilityService.PrepareForCharacterLoad();
    }

    private static object Capture()
    {
        var character = CharacterManager.Current.Character;
        var elements = CharacterManager.Current.GetElements().ToArray();
        return new { character.Level, character.Class, character.Race, character.Background,
            abilityIncreases = new[] { character.Abilities.Strength.AdditionalScore, character.Abilities.Dexterity.AdditionalScore,
                character.Abilities.Constitution.AdditionalScore, character.Abilities.Intelligence.AdditionalScore,
                character.Abilities.Wisdom.AdditionalScore, character.Abilities.Charisma.AdditionalScore },
            ids = elements.Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            companions = character.Companions.Select(c => new { c.Element.Id, name = c.CompanionName.Content,
                c.Statistics.ArmorClass, c.Statistics.MaxHp, c.Statistics.Speed,
                strength = c.Abilities.Strength.FinalScore, dexterity = c.Abilities.Dexterity.FinalScore })
                .OrderBy(c => c.Id).ThenBy(c => c.name).ToArray(),
            prepared = elements.Where(e => e.SpellcastingInformation != null).Select(e => e.SpellcastingInformation.Name)
                .Distinct().Order().ToDictionary(n => n, n => SpellcastingSectionContext.Current!.GetPreparedIds(n).Order().ToArray()) };
    }

    private static string[] Choices(string path) => XDocument.Load(path).Descendants("element")
        .Where(e => e.Attribute("choiceRowKey") != null || e.Attribute("registered") != null)
        .Select(e => string.Join("|", e.Attributes().Where(a => a.Name.LocalName is "id" or "choiceRowKey" or "choiceKey" or "selectId" or "number" or "registered" or "type" or "name")
            .OrderBy(a => a.Name.LocalName).Select(a => a.Name + "=" + System.Text.RegularExpressions.Regex.Replace(a.Value,
                "runtime:[0-9a-fA-F-]{36}", "runtime:<generated>"))))
        .OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static string Inventory(string path) => string.Join("\n", XDocument.Load(path).Descendants("item")
        .Where(e => e.Ancestors().Any(a => a.Name.LocalName is "equipment" or "inventory"))
        .Select(e => e.ToString(SaveOptions.DisableFormatting)).Order(StringComparer.Ordinal));
}
