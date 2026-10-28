using System.Xml;
using System.Text.Json;
using System.Text.RegularExpressions;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;

namespace Aurora.Reflections.Services;

public sealed record SidekickField(string Key, string Label, string Group, string DefaultValue = "", int Lines = 1);

/// <summary>A character-owned creature profile. Its rules never register on the player character.</summary>
public sealed class SidekickProfile
{
    public string Id { get; init; } = "sidekick-instance:" + Guid.NewGuid().ToString("N");
    public string TemplateId { get; init; } = "";
    public string ProgressionClass { get; set; } = "Custom";
    public bool IsCompanion => Id.StartsWith("companion-instance:", StringComparison.Ordinal);
    public bool FollowOwnerLevel { get; set; } = true;
    public long Revision { get; set; }
    public CreatureCombatState Combat { get; set; } = new();
    public SidekickChoiceState? Choices { get; set; }
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.Ordinal);
    // Retain the last defaults so refreshing a level can distinguish edits from generated values.
    public Dictionary<string, string> LevelDefaults { get; init; } = new(StringComparer.Ordinal);
    public HashSet<string> Overrides { get; init; } = new(StringComparer.Ordinal);
    public string Name => Get("name");
    public string Get(string key) => Fields.GetValueOrDefault(key)
        ?? SidekickService.Fields.FirstOrDefault(f => f.Key == key)?.DefaultValue ?? "";
    public void Set(string key, string? value)
    {
        Fields[key] = value ?? "";
        if (LevelDefaults.ContainsKey(key)) Overrides.Add(key);
    }
    public SidekickProfile Copy(string? templateId = null) => new()
    {
        Id = Id, TemplateId = templateId ?? TemplateId, ProgressionClass = ProgressionClass, FollowOwnerLevel = FollowOwnerLevel,
        Fields = new(Fields, StringComparer.Ordinal), LevelDefaults = new(LevelDefaults, StringComparer.Ordinal),
        Overrides = new(Overrides, StringComparer.Ordinal), Revision = Revision, Combat = Combat.Copy(), Choices = Choices?.Copy()
    };
}

public static class SidekickService
{
    public static IReadOnlyList<SidekickField> Fields { get; } =
    [
        new("name", "Name", "Identity"),
        new("role", "Role", "Identity", "Custom"),
        new("level", "Level", "Identity", "1"),
        new("size", "Size", "Identity"),
        new("type", "Creature type", "Identity"),
        new("alignment", "Alignment", "Identity"),
        new("ac", "Armor Class", "Combat"),
        new("hp", "Maximum HP", "Combat"),
        new("current-hp", "Current HP", "Combat"),
        new("temporary-hp", "Temporary HP", "Combat"),
        new("hit-dice", "Hit dice", "Combat"),
        new("speed", "Speed", "Combat"),
        new("initiative", "Initiative", "Combat", "+0"),
        new("proficiency", "Proficiency bonus", "Combat", "2"),
        new("strength", "Strength", "Ability scores", "10"),
        new("dexterity", "Dexterity", "Ability scores", "10"),
        new("constitution", "Constitution", "Ability scores", "10"),
        new("intelligence", "Intelligence", "Ability scores", "10"),
        new("wisdom", "Wisdom", "Ability scores", "10"),
        new("charisma", "Charisma", "Ability scores", "10"),
        new("martial-role", "Martial role", "Class progression", "Attacker"),
        new("spellcasting-role", "Spellcaster role", "Class progression", "Mage"),
        new("empowered-school", "Empowered Spells school", "Class progression"),
        new("class-features", "Class features", "Class progression", Lines: 12),
        new("spellcasting-ability", "Spellcasting ability", "Class progression"),
        new("spell-save-dc", "Spell save DC", "Class progression"),
        new("spell-attack", "Spell attack bonus", "Class progression"),
        new("cantrips-known", "Cantrips known", "Class progression"),
        new("spells-known", "Spells known", "Class progression"),
        new("spell-slots", "Spell slots (maximum)", "Class progression", Lines: 2),
        new("slot-recovery", "Spell slot recovery", "Class progression", "Long rest"),
        new("saves", "Saving throws", "Details"),
        new("skills", "Skills", "Details"),
        new("proficiencies", "Armor, weapons, and tools", "Details"),
        new("senses", "Senses", "Details"),
        new("languages", "Languages", "Details"),
        new("vulnerabilities", "Damage vulnerabilities", "Details"),
        new("resistances", "Damage resistances", "Details"),
        new("immunities", "Damage immunities", "Details"),
        new("conditionVulnerabilities", "Condition vulnerabilities", "Details"),
        new("conditionResistances", "Condition resistances", "Details"),
        new("conditionImmunities", "Condition immunities", "Details"),
        new("challenge", "Challenge rating", "Details"),
        new("experience", "Experience", "Details"),
        new("description", "Description", "Features and notes", Lines: 4),
        new("features", "Creature features", "Features and notes", Lines: 6),
        new("owner-features", "Companion benefits from your class", "Features and notes", Lines: 8),
        new("custom-additions", "Custom features and descriptions", "Features and notes", Lines: 6),
        new("actions", "Actions", "Features and notes", Lines: 4),
        new("bonus-actions", "Bonus actions", "Features and notes", Lines: 4),
        new("reactions", "Reactions", "Features and notes", Lines: 4),
        new("selected-spells", "Selected sidekick spells", "Features and notes", Lines: 8),
        new("spells", "Additional spells and custom spellcasting", "Features and notes", Lines: 6),
        new("notes", "Notes", "Features and notes", Lines: 4)
    ];

    private static readonly string[] Roles = ["Expert", "Warrior", "Spellcaster", "Custom"];
    private static string StarterId(string role) => "sidekick-template:" + role.ToLowerInvariant();
    public static bool IsInstanceId(string id) => id.StartsWith("sidekick-instance:", StringComparison.Ordinal);

    // Content can declare its own Sidekick type or tag a normal Companion definition.
    public static bool IsSidekickContent(ElementBase element) =>
        string.Equals(element.Type, "Sidekick", StringComparison.OrdinalIgnoreCase)
        || element is CompanionElement && (
            element.Supports.Any(s => s.Equals("Sidekick", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Sidekicks", StringComparison.OrdinalIgnoreCase))
            || string.Equals(element.ElementSetters.GetSetter("category")?.Value, "Sidekicks", StringComparison.OrdinalIgnoreCase));

    public static bool IsTemplate(string id) => Roles.Any(r => StarterId(r) == id)
        || DataManager.Current.ElementsCollection.GetElement(id) is { } element && IsSidekickContent(element);

    public static IReadOnlyList<ItemSearchResult> SearchTemplates(string query)
    {
        var starters = Roles.Select(role => new ItemSearchResult(StarterId(role), $"Sidekick - {role}", "Sidekick",
            SidekickProgression.TemplateDescription(role),
            role == "Custom" ? "Editable starter" : SidekickProgression.Source));
        var content = DataManager.Current.ElementsCollection.Where(IsSidekickContent)
            .Select(e => new ItemSearchResult(e.Id, e.Name, "Sidekick", PlainText(e.Description), e.Source ?? ""));
        return content.Concat(starters)
            .Where(e => string.IsNullOrWhiteSpace(query) || e.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name).ToList();
    }

    public static SidekickProfile Create(string templateId)
    {
        var profile = new SidekickProfile { TemplateId = templateId };
        foreach (var field in Fields) profile.Set(field.Key, field.DefaultValue);
        string? role = Roles.FirstOrDefault(r => StarterId(r) == templateId);
        if (role != null)
        {
            profile.Set("name", $"Sidekick - {role}");
            profile.Set("role", role);
            profile.ProgressionClass = role;
            SidekickProgression.Refresh(profile);
            SidekickChoices.Initialize(profile, preserveExistingTotals: false);
            SidekickProgression.Refresh(profile);
            return profile;
        }

        var element = DataManager.Current.ElementsCollection.GetElement(templateId);
        if (element == null || !IsSidekickContent(element))
            throw new InvalidOperationException("That sidekick template could not be found.");
        foreach (var field in Fields)
        {
            if (element.ElementSetters.GetSetter(field.Key) is { } setter)
                profile.Set(field.Key, setter.Value);
        }
        profile.Set("name", element.Name);
        profile.Set("description", PlainText(element.Description));
        profile.Set("role", element.ElementSetters.GetSetter("sidekick role")?.Value ?? profile.Get("role"));
        if (element is CompanionElement companion)
        {
            if (element.ElementSetters.GetSetter("initiative") == null)
            {
                int initiative = (int)Math.Floor((companion.Dexterity - 10) / 2d);
                profile.Set("initiative", initiative >= 0 ? $"+{initiative}" : initiative.ToString());
            }
            profile.Set("proficiency", companion.Proficiency.ToString());
            profile.Set("features", FeatureText(companion.Traits));
            profile.Set("actions", FeatureText(companion.Actions));
            profile.Set("reactions", FeatureText(companion.Reactions));
        }
        profile.ProgressionClass = SidekickProgression.NormalizeClass(profile.Get("role"));
        SidekickChoices.Initialize(profile, preserveExistingTotals: false);
        SidekickProgression.Refresh(profile);
        return profile;
    }

    private static string FeatureText(IEnumerable<string> ids) => string.Join("\n\n", ids.Select(id =>
        DataManager.Current.ElementsCollection.GetElement(id) is { } element
            ? element.Name + "\n" + PlainText(element.Description) : id));

    private static string PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        try { return ElementDescriptionGenerator.GeneratePlainDescription(html).Trim(); }
        catch { return html; } // The editor and card always render this as escaped plain text.
    }

    public static bool MatchOwnerLevel(SidekickProfile profile, int ownerLevel)
    {
        if (profile.IsCompanion || !profile.FollowOwnerLevel || ownerLevel is < 1 or > 20
            || profile.Get("level") == ownerLevel.ToString()) return false;
        // Capture old HP totals before changing a legacy sidekick's level.
        SidekickChoices.Initialize(profile);
        profile.Fields["level"] = ownerLevel.ToString();
        SidekickProgression.Refresh(profile);
        return true;
    }

    // Called inside the owner's full-save transaction, before its single atomic write.
    internal static bool SynchronizeOwnerLevels(XmlDocument document, int ownerLevel)
    {
        var profiles = ReadProfiles(document);
        bool changed = false;
        foreach (var profile in profiles)
        {
            if (!MatchOwnerLevel(profile, ownerLevel)) continue;
            profile.Revision++;
            changed = true;
        }
        if (changed) WriteProfiles(document, profiles);
        return changed;
    }

    public static async Task<CharacterFileWriteResult> SynchronizeOwnerLevelsAsync(CharacterTab tab)
    {
        try
        {
            using var scope = await CharacterContext.EnterAsync(tab);
            return await CharacterFileWriteCoordinator.WriteAsync(tab.FileSaveSemaphore, tab.File, "Sidekick levels", () =>
            {
                if (tab.CloudReloadFailed) throw new InvalidOperationException("Load the Drive save again before updating sidekick levels.");
                var document = CharacterFileIo.LoadXmlDocument(tab.File.FilePath);
                if (!SynchronizeOwnerLevels(document, tab.Character?.Level ?? 0)) return true;
                CharacterFileIo.SaveXmlDocumentAtomic(tab.File.FilePath, document);
                tab.File.InitializeDisplayPropertiesFromFilePath();
                tab.CloudSession?.InvalidatePendingState();
                return true;
            });
        }
        catch (Exception ex) { return CharacterFileWriteResult.Failed("Sidekick levels", ex); }
    }

    public static string? Validate(SidekickProfile profile)
    {
        if (!IsInstanceId(profile.Id) && !profile.IsCompanion) return "The creature identifier is invalid.";
        if (string.IsNullOrWhiteSpace(profile.Name)) return "Enter a name for the creature.";
        if (!SidekickProgression.Classes.Contains(profile.ProgressionClass)) return "Select a sidekick progression.";
        if (!int.TryParse(profile.Get("level"), out int level) || level is < 1 or > 20)
            return "Level must be between 1 and 20.";
        foreach (var field in Fields.Where(f => f.Group == "Ability scores"))
            if (!int.TryParse(profile.Get(field.Key), out int score) || score is < 1 or > 30)
                return $"{field.Label} must be between 1 and 30.";
        foreach (string key in new[] { "current-hp", "temporary-hp" })
            if (profile.Get(key).Length > 0 && (!int.TryParse(profile.Get(key), out int hp) || hp < 0))
                return "Current and temporary HP must be whole numbers of zero or more.";
        if (profile.Combat.Exhaustion is < 0 or > 6 || profile.Combat.HitDiceUsed < 0)
            return "Exhaustion must be 0–6 and spent hit dice cannot be negative.";
        if (profile.Get("portrait").Length > 0 && CreaturePortrait.DisplaySource(profile.Get("portrait")) == null)
            return "Choose a valid portrait image smaller than 5 MB.";
        return null;
    }

    internal static Dictionary<string, string> CompanionFields(Companion companion)
    {
        var element = companion.Element;
        var fields = Fields.ToDictionary(f => f.Key, f => element.ElementSetters.GetSetter(f.Key)?.Value ?? f.DefaultValue);
        fields["name"] = companion.CompanionName.Content ?? companion.CompanionName.OriginalContent ?? element.Name;
        fields["role"] = "Companion";
        fields["portrait"] = CreaturePortrait.LegacySource(companion.Portrait.ToString());
        fields["description"] = PlainText(element.Description);
        fields["ac"] = companion.ArmorClass.IsUserInput ? companion.ArmorClass.Content : (companion.Statistics.ArmorClass > 0
            ? companion.Statistics.ArmorClass.ToString() : companion.ArmorClass.OriginalContent ?? "");
        fields["hp"] = companion.MaxHp.IsUserInput ? companion.MaxHp.Content : (companion.Statistics.MaxHp > 0
            ? companion.Statistics.MaxHp.ToString() : companion.MaxHp.OriginalContent ?? "");
        fields["speed"] = companion.Speed.IsUserInput ? companion.Speed.Content : (companion.Statistics.Speed > 0
            ? companion.Statistics.Speed.ToString() : companion.Speed.OriginalContent ?? "");
        fields["initiative"] = companion.Initiative.IsUserInput ? companion.Initiative.Content : companion.Statistics.Initiative.ToString("+0;-0;0");
        fields["proficiency"] = companion.Statistics.Proficiency.ToString();
        foreach (var ability in companion.Abilities.GetCollection()) fields[ability.Name.ToLowerInvariant()] = ability.FinalScore.ToString();
        fields["features"] = FeatureText(element.Traits);
        fields["actions"] = FeatureText(element.Actions);
        fields["reactions"] = FeatureText(element.Reactions);
        if (string.IsNullOrWhiteSpace(fields["hit-dice"]))
            fields["hit-dice"] = Regex.Match(element.HitPoints ?? "", @"\b\d+d(?:4|6|8|10|12|20)\b", RegexOptions.IgnoreCase).Value;
        CompanionProgression.RefreshFields(companion, fields);
        return fields;
    }

    public static SidekickProfile MergeCompanion(CompanionSnapshot snapshot, SidekickProfile? saved)
    {
        var profile = saved?.Copy(snapshot.TemplateId) ?? new SidekickProfile { Id = snapshot.InstanceId, TemplateId = snapshot.TemplateId };
        foreach (var (key, value) in snapshot.DefaultFields)
        {
            if (key is "current-hp" or "temporary-hp") continue;
            if (key is "notes" or "custom-additions") { profile.Fields.TryAdd(key, value); continue; }
            if (!profile.Overrides.Contains(key)) profile.Fields[key] = value;
            profile.LevelDefaults[key] = value;
        }
        return profile;
    }

    public static bool HasCompanions(CharacterTab? tab) =>
        tab is { IsLoading: false, Snapshot: not null }
        && (tab.Snapshot.HasCompanion || Load(tab.File).Count > 0);

    public static int CountSidekicksNeedingChoices(CharacterTab? tab)
    {
        if (tab is not { IsLoading: false, Snapshot: not null }) return 0;
        int ownerLevel = tab.Character?.Level ?? tab.Snapshot.Level;
        return Load(tab.File).Count(profile => SidekickChoices.Pending(profile, ownerLevel).Count > 0);
    }

    public static IReadOnlyList<SidekickProfile> LoadAll(CharacterTab tab)
    {
        var document = CharacterFileIo.LoadXmlDocument(tab.File.FilePath);
        var saved = ReadProfiles(document, true);
        var companions = (tab.Snapshot?.Companions ?? []).Select(c => MergeCompanion(c, saved.FirstOrDefault(p => p.Id == c.InstanceId)));
        return companions.Concat(ReadProfiles(document)).ToList();
    }

    public static IReadOnlyList<SidekickProfile> Load(CharacterFile? file)
    {
        if (file == null) return [];
        try
        {
            var document = CharacterFileIo.LoadXmlDocument(file.FilePath);
            return ReadProfiles(document);
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "SidekickService.Load");
            return [];
        }
    }

    private static List<SidekickProfile> ReadProfiles(XmlDocument document, bool companions = false)
    {
        var profiles = new List<SidekickProfile>();
        var nodes = document.DocumentElement?[companions ? "companion-profiles" : "sidekicks"]?.SelectNodes("sidekick");
        if (nodes == null) return profiles;
        foreach (XmlElement node in nodes)
        {
            var profile = new SidekickProfile { Id = node.GetAttribute("id"), TemplateId = node.GetAttribute("template"),
                FollowOwnerLevel = !node.GetAttribute("follow-owner-level").Equals("false", StringComparison.OrdinalIgnoreCase) };
            if (node["choices"] is { } choices) profile.Choices = JsonSerializer.Deserialize<SidekickChoiceState>(choices.InnerText);
            profile.Revision = long.TryParse(node.GetAttribute("revision"), out long revision) ? revision : 0;
            if (node["combat"] is { } combat) profile.Combat = JsonSerializer.Deserialize<CreatureCombatState>(combat.InnerText)
                ?? throw new InvalidDataException("The saved creature tracking is empty.");
            foreach (XmlElement field in node.SelectNodes("field")!)
            {
                profile.Set(field.GetAttribute("name"), field.InnerText);
                if (field.GetAttribute("customized") == "true") profile.Overrides.Add(field.GetAttribute("name"));
            }
            profile.ProgressionClass = node.HasAttribute("progression")
                ? node.GetAttribute("progression") : SidekickProgression.NormalizeClass(profile.Get("role"));
            foreach (XmlElement field in node.SelectNodes("level-defaults/field")!)
                profile.LevelDefaults[field.GetAttribute("name")] = field.InnerText;
            if (!companions) SidekickProgression.Refresh(profile);
            if ((companions ? !profile.IsCompanion : !IsInstanceId(profile.Id)) || profiles.Any(p => p.Id == profile.Id))
                throw new InvalidDataException("The saved sidekick list has an invalid or duplicate identifier.");
            profiles.Add(profile);
        }
        return profiles;
    }

    public static async Task<CharacterFileWriteResult> SaveAsync(CharacterTab tab, SidekickProfile profile, bool isNew = false)
    {
        if (Validate(profile) is { } error)
            return new CharacterFileWriteResult(CharacterFileWriteStatus.Failed, error);
        var copy = profile.Copy();
        var result = await UpdateAsync(tab, profiles =>
        {
            int index = profiles.FindIndex(p => p.Id == copy.Id);
            if (isNew && index >= 0 || !isNew && index < 0 && !copy.IsCompanion
                || index >= 0 && profiles[index].Revision != copy.Revision)
                throw new InvalidOperationException("This creature changed while the editor was open. Reopen it and try again.");
            MatchOwnerLevel(copy, tab.Character?.Level ?? 0);
            if (!copy.IsCompanion) SidekickProgression.Refresh(copy);
            if (Validate(copy) is { } validationError) throw new InvalidOperationException(validationError);
            if (SidekickChoices.Errors(copy).FirstOrDefault() is { } choiceError) throw new InvalidOperationException(choiceError);
            copy.Revision++;
            if (index < 0) profiles.Add(copy);
            else profiles[index] = copy;
        }, copy.IsCompanion, copy.Id);
        if (result.Succeeded) profile.Revision = copy.Revision;
        return result;
    }

    public static Task<CharacterFileWriteResult> MutateAsync(CharacterTab tab, string id, Action<SidekickProfile> change)
    {
        bool companion = id.StartsWith("companion-instance:", StringComparison.Ordinal);
        return UpdateAsync(tab, profiles =>
        {
            var profile = profiles.FirstOrDefault(p => p.Id == id)
                ?? throw new InvalidOperationException("This creature is no longer on the character.");
            MatchOwnerLevel(profile, tab.Character?.Level ?? 0);
            change(profile);
            if (Validate(profile) is { } error) throw new InvalidOperationException(error);
            profile.Revision++;
        }, companion, id);
    }

    public static Task<CharacterFileWriteResult> RemoveAsync(CharacterTab tab, string id) =>
        UpdateAsync(tab, profiles => profiles.RemoveAll(p => p.Id == id));

    private static async Task<CharacterFileWriteResult> UpdateAsync(CharacterTab tab, Action<List<SidekickProfile>> update,
        bool companions = false, string? companionId = null)
    {
        try
        {
            // Serialize against tab swaps as well as file writes. The file's cached XML must carry
            // the new extension node into full saves and in-memory tab snapshots.
            using var scope = await CharacterContext.EnterAsync(tab);
            return await CharacterFileWriteCoordinator.WriteAsync(tab.FileSaveSemaphore, tab.File, "Sidekicks", () =>
            {
                var document = CharacterFileIo.LoadXmlDocument(tab.File.FilePath);
                var profiles = ReadProfiles(document, companions);
                if (companions)
                {
                    var creature = tab.Character?.Companions.FirstOrDefault(c => c.InstanceId == companionId)
                        ?? throw new InvalidOperationException("This companion is no longer on the character.");
                    EnsureCompanionIdentities(document, tab.Character!.Companions);
                    var snapshot = CharacterSnapshot.BuildCompanionSnapshot(creature)
                        ?? throw new InvalidOperationException("The companion's statistics could not be loaded.");
                    var current = profiles.FirstOrDefault(p => p.Id == companionId);
                    var merged = MergeCompanion(snapshot, current);
                    if (current == null) profiles.Add(merged);
                    else profiles[profiles.IndexOf(current)] = merged;
                }
                update(profiles);
                WriteProfiles(document, profiles, companions);
                CharacterFileIo.SaveXmlDocumentAtomic(tab.File.FilePath, document);
                tab.File.InitializeDisplayPropertiesFromFilePath();
                tab.CloudSession?.InvalidatePendingState();
                return true;
            });
        }
        catch (Exception ex) { return CharacterFileWriteResult.Failed("Sidekicks", ex); }
    }

    private static void WriteProfiles(XmlDocument document, IReadOnlyList<SidekickProfile> profiles, bool companions = false)
    {
        var root = document.DocumentElement ?? throw new InvalidDataException("The character file is empty.");
        string rootName = companions ? "companion-profiles" : "sidekicks";
        var node = document.CreateElement(rootName);
        foreach (var profile in profiles)
        {
            var entry = document.CreateElement("sidekick");
            entry.SetAttribute("id", profile.Id);
            entry.SetAttribute("template", profile.TemplateId);
            entry.SetAttribute("progression", profile.ProgressionClass);
            entry.SetAttribute("revision", profile.Revision.ToString());
            if (!companions) entry.SetAttribute("follow-owner-level", profile.FollowOwnerLevel ? "true" : "false");
            var combat = document.CreateElement("combat");
            combat.InnerText = JsonSerializer.Serialize(profile.Combat);
            entry.AppendChild(combat);
            if (profile.Choices != null)
            {
                var choices = document.CreateElement("choices");
                choices.InnerText = JsonSerializer.Serialize(profile.Choices);
                entry.AppendChild(choices);
            }
            foreach (var (key, value) in profile.Fields)
            {
                var field = document.CreateElement("field");
                field.SetAttribute("name", key);
                if (profile.Overrides.Contains(key)) field.SetAttribute("customized", "true");
                field.InnerText = value;
                entry.AppendChild(field);
            }
            var defaults = document.CreateElement("level-defaults");
            foreach (var (key, value) in profile.LevelDefaults)
            {
                var field = document.CreateElement("field");
                field.SetAttribute("name", key);
                field.InnerText = value;
                defaults.AppendChild(field);
            }
            entry.AppendChild(defaults);
            node.AppendChild(entry);
        }
        if (root[rootName] is { } previous) root.ReplaceChild(node, previous);
        else root.AppendChild(node);
    }

    private static void EnsureCompanionIdentities(XmlDocument document, IReadOnlyList<Companion> companions)
    {
        // Upgrade old saves and newly unlocked companions in the same atomic write as their first edit.
        var build = document.SelectSingleNode("/*/build") as XmlElement
            ?? throw new InvalidDataException("The character file has no build.");
        var collection = build["companions"];
        if (collection == null)
        {
            collection = document.CreateElement("companions");
            build.AppendChild(collection);
        }
        var available = companions.ToList();
        foreach (XmlElement node in collection.SelectNodes("companion")!)
        {
            string id = node.GetAttribute("instance");
            string templateId = ElementIdAliases.Resolve(DataManager.Current.ElementsCollection,
                node.GetAttribute("id"))?.Id ?? node.GetAttribute("id");
            string origin = node.GetAttribute("origin");
            var match = id.Length > 0 ? available.FirstOrDefault(c => c.InstanceId == id)
                : available.FirstOrDefault(c => c.Element.Id == templateId
                    && (origin.Length == 0 || origin == c.AcquisitionKey));
            if (match == null) continue;
            node.SetAttribute("instance", match.InstanceId);
            node.SetAttribute("origin", match.AcquisitionKey);
            available.Remove(match);
        }
        foreach (var companion in available)
        {
            var node = document.CreateElement("companion");
            node.SetAttribute("id", companion.Element.Id);
            node.SetAttribute("instance", companion.InstanceId);
            node.SetAttribute("origin", companion.AcquisitionKey);
            node.SetAttribute("name", companion.CompanionName.Content ?? "");
            var portrait = document.CreateElement("portrait");
            portrait.SetAttribute("location", "local");
            portrait.InnerText = companion.Portrait.ToString();
            node.AppendChild(portrait);
            collection.AppendChild(node);
        }
    }
}
