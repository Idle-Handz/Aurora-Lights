using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;

namespace Builder.Presentation.Services;

public enum SpellPreparation { Feature, Known, Prepared, AlwaysPrepared, Spellbook, ListOnly, RitualOnly }
public enum SpellSlotPermission { None, AssociatedClass, Any }

/// <summary>One acquisition, not one spell ID. A second origin retains its own casting rules.</summary>
public sealed record SpellAcquisition(
    ElementBase Spell, RuleBase? Rule, string OriginId, string OriginName,
    SpellcastingInformation? Profile, SpellPreparation Preparation,
    SpellSlotPermission Slots, string Ability, int? FreeUses, string Recharge,
    bool CountsAgainstKnownLimit, string Diagnostic, int? SlotUses = null)
{
    public bool IsFeature => Preparation == SpellPreparation.Feature || Profile is null;
    public bool IsAlwaysPrepared => Preparation == SpellPreparation.AlwaysPrepared;
    public string ProfileKey => Profile is null ? "" : SpellAcquisitionResolver.ProfileKey(Profile);
    public bool CanUseSlots(bool prepared) => Slots != SpellSlotPermission.None
        && SlotUses is not 0
        && (Preparation is not (SpellPreparation.Prepared or SpellPreparation.Spellbook) || prepared);
}

/// <summary>
/// Resolves ownership from explicit metadata or an unambiguous acquisition chain. A spell's
/// supports list is eligibility, never ownership. Descriptive prose is not executable rules.
/// </summary>
public static class SpellAcquisitionResolver
{
    public static string ProfileKey(SpellcastingInformation profile) => $"{profile.ElementHeader.Id}|{profile.Name}";

    public static bool SameSelectionDomain(RuleBase? first, RuleBase second,
        IReadOnlyCollection<ElementBase> elements, IReadOnlyCollection<SpellcastingInformation> profiles)
    {
        var left = ResolveProfile(first, elements, profiles);
        var right = ResolveProfile(second, elements, profiles);
        return left is not null && right is not null ? ProfileKey(left) == ProfileKey(right)
            : left is null && right is null && first?.ElementHeader.Id == second.ElementHeader.Id;
    }

    public static RuleBase? AcquisitionRule(ElementBase element) => element.Aquisition.WasGranted
        ? element.Aquisition.GrantRule : element.Aquisition.WasSelected ? element.Aquisition.SelectRule : null;

    public static IReadOnlyList<SpellAcquisition> Resolve(IEnumerable<ElementBase> elements,
        IEnumerable<SpellcastingInformation> profiles)
    {
        var active = elements.ToArray();
        var casters = profiles.Where(p => !p.IsExtension).ToArray();
        return active.Where(e => e.Type == "Spell").Select(e => Resolve(e, active, casters)).ToArray();
    }

    public static SpellcastingInformation? ResolveProfile(RuleBase? rule,
        IReadOnlyCollection<ElementBase> elements, IReadOnlyCollection<SpellcastingInformation> profiles)
    {
        if (rule is null) return null;
        string explicitName = rule is SelectRule select ? select.Attributes.SpellcastingName : Value(rule, "spellcasting");
        var casters = profiles.Where(p => !p.IsExtension).ToArray();
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            var matches = casters.Where(p => string.Equals(p.Name, explicitName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ProfileKey(p), explicitName, StringComparison.OrdinalIgnoreCase)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        // A feat selected by a class is still a feat. Do not inherit the class's casting permissions.
        if (rule.ElementHeader.Type is not ("Class" or "Class Feature" or "Archetype" or "Archetype Feature"))
            return null;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        pending.Enqueue(rule.ElementHeader.Id);
        while (pending.TryDequeue(out var id))
        {
            if (!ids.Add(id)) continue;
            foreach (var owner in elements.Where(e => e.Id == id))
            {
                var parent = owner.Aquisition.GetParentHeader();
                if (parent is not null && parent.Type is "Class" or "Class Feature" or "Archetype" or "Archetype Feature")
                    pending.Enqueue(parent.Id);
            }
        }
        var candidates = casters.Where(p => ids.Contains(p.ElementHeader.Id)).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static SpellAcquisition Resolve(ElementBase spell, ElementBase[] elements, SpellcastingInformation[] profiles)
    {
        var rule = AcquisitionRule(spell);
        var origin = rule?.ElementHeader;
        var profile = ResolveProfile(rule, elements, profiles);
        var preparation = rule?.IsAlwaysPrepared() == true ? SpellPreparation.AlwaysPrepared
            : profile is null ? SpellPreparation.Feature
            : !profile.Prepare ? SpellPreparation.Known
            : profile.PrepareFromSpellList ? SpellPreparation.Prepared : SpellPreparation.Spellbook;
        var slots = profile is null ? SpellSlotPermission.None : SpellSlotPermission.AssociatedClass;
        var ability = profile?.AbilityName ?? "";
        int? uses = null;
        int? slotUses = null;
        string recharge = "";
        bool counts = preparation == SpellPreparation.Known;
        string diagnostic = profile is null ? "Casting limits and ability are not specified; consult the granting feature." : "";
        int level = spell is Spell typed ? typed.Level : -1;

        // Versioned compatibility rules for legacy XML that does not encode these semantics.
        // Exact origin IDs are intentional: names and list membership cannot establish permission.
        var classes = new[] { "BARD", "CLERIC", "DRUID", "SORCERER", "WARLOCK", "WIZARD" };
        string? legacyClass = classes.FirstOrDefault(c => origin?.Id == "ID_PHB_FEAT_MAGIC_INITIATE_" + c);
        string? revisedClass = classes.FirstOrDefault(c => origin?.Id == "ID_WOTC_PHB24_FEAT_MAGIC_INITIATE_" + c);
        if (legacyClass is not null || revisedClass is not null)
        {
            string chosenClass = legacyClass ?? revisedClass!;
            profile = null;
            preparation = revisedClass is null ? SpellPreparation.Feature : SpellPreparation.AlwaysPrepared;
            slots = revisedClass is null ? SpellSlotPermission.None : SpellSlotPermission.Any;
            uses = level > 0 ? 1 : null;
            recharge = level > 0 ? "Long Rest" : "";
            counts = false;
            ability = chosenClass is "CLERIC" or "DRUID" ? "Wisdom" : chosenClass == "WIZARD" ? "Intelligence" : "Charisma";
            diagnostic = "";
            if (revisedClass is not null)
            {
                var abilities = new[] { "Intelligence", "Wisdom", "Charisma" }.Where(a => elements.Any(e =>
                    e.Id == $"ID_WOTC_PHB24_FEAT_FEATURE_MAGIC_INITIATE_{chosenClass}_{a.ToUpperInvariant()}"
                    && e.Aquisition.GetParentHeader()?.Id == origin!.Id)).ToArray();
                ability = abilities.Length == 1 ? abilities[0] : "";
                if (ability.Length == 0) diagnostic = "Choose a casting ability for Magic Initiate.";
            }
            else
            {
                var matching = profiles.Where(p => p.Name.Equals(chosenClass, StringComparison.OrdinalIgnoreCase)).ToArray();
                // Prepared classes still require class preparation. Wizard learning does not
                // silently transcribe a feat spell into a spellbook; that is a separate acquisition.
                if (matching.Length == 1 && chosenClass != "WIZARD")
                {
                    profile = matching[0];
                    preparation = profile.Prepare ? SpellPreparation.Prepared : SpellPreparation.Known;
                    slots = SpellSlotPermission.AssociatedClass;
                }
            }
        }

        if (origin?.Id == "ID_WOTC_PHB24_ARCHETYPE_FEATURE_WARLOCK_FIEND_PATRON_FIEND_SPELLS" && profile is not null)
        {
            preparation = SpellPreparation.AlwaysPrepared;
            counts = false;
        }
        if (origin?.Id == "ID_JONOMAN3000_DAPC_ARCHETYPE_FEATURE_SORCERER_NECROTIC_AFFINITY")
            counts = false;
        if (origin?.Id == "ID_GFP_COFSA_ARCHETYPE_FEATURE_ACCURSED_ARCHIVE_VILEHERESIES")
        {
            preparation = SpellPreparation.Feature;
            slotUses = 1;
            recharge = "Long Rest";
            counts = false;
            diagnostic = "Uses a Warlock slot; exchange only in the Archive, then finish a Long Rest before using the replacement.";
        }
        // Legacy ritual-book selectors use this explicit rule convention. A ritual flag on
        // the spell alone does not grant ritual casting to a feature or to another class.
        if (rule is SelectRule ritual && ritual.Attributes.Supports?.Contains("ritual", StringComparison.OrdinalIgnoreCase) == true
            && new[] { "ritual caster", "ritual book", "book of ancient secrets", "book of rituals" }
                .Any(n => ritual.Attributes.Name?.Contains(n, StringComparison.OrdinalIgnoreCase) == true))
        {
            preparation = SpellPreparation.RitualOnly;
            slots = SpellSlotPermission.None;
            counts = false;
            diagnostic = "";
        }

        string access = Value(rule, "spell-access");
        if (access.Length > 0)
        {
            preparation = access switch
            {
                "known" => SpellPreparation.Known, "prepared" => SpellPreparation.Prepared,
                "always-prepared" => SpellPreparation.AlwaysPrepared, "spellbook" => SpellPreparation.Spellbook,
                "list" => SpellPreparation.ListOnly, "ritual" => SpellPreparation.RitualOnly, _ => SpellPreparation.Feature
            };
            if (preparation is SpellPreparation.Feature or SpellPreparation.ListOnly or SpellPreparation.RitualOnly) slots = SpellSlotPermission.None;
        }
        string slotValue = Value(rule, "spell-slots");
        if (slotValue.Length > 0) slots = slotValue switch
        {
            "any" => SpellSlotPermission.Any,
            "class" when profile is not null => SpellSlotPermission.AssociatedClass,
            _ => SpellSlotPermission.None
        };
        if (Value(rule, "spell-ability") is { Length: > 0 } explicitAbility) ability = explicitAbility;
        if (int.TryParse(Value(rule, "spell-uses"), out int explicitUses) && explicitUses >= 0) uses = explicitUses;
        if (Value(rule, "spell-recharge") is { Length: > 0 } explicitRecharge) recharge = explicitRecharge;
        if (bool.TryParse(Value(rule, "spell-counts-known"), out bool explicitCounts)) counts = explicitCounts;
        if (access.Length > 0 && (preparation is SpellPreparation.ListOnly or SpellPreparation.RitualOnly
            || profile is not null || uses.HasValue || slotValue == "any" || level == 0)) diagnostic = "";
        if (int.TryParse(Value(rule, "spell-slot-uses"), out int limit) && limit >= 0) slotUses = limit;
        string requestedProfile = rule is SelectRule selection ? selection.Attributes.SpellcastingName : Value(rule, "spellcasting");
        if (profile is null && (!string.IsNullOrWhiteSpace(requestedProfile) || slotValue == "class"))
            diagnostic = $"The requested spellcasting profile '{requestedProfile}' could not be resolved unambiguously.";
        if (access.Length > 0 && access is not ("feature" or "known" or "prepared" or "always-prepared" or "spellbook" or "list" or "ritual"))
            diagnostic = $"Unrecognized spell-access value '{access}'; review the granting feature.";
        if (slotValue.Length > 0 && slotValue is not ("none" or "class" or "any"))
            diagnostic = $"Unrecognized spell-slots value '{slotValue}'; slot permission was not inferred.";
        if (preparation == SpellPreparation.ListOnly) { slots = SpellSlotPermission.None; uses = null; counts = false; }
        if (level == 0) slots = SpellSlotPermission.None;
        return new(spell, rule, origin?.Id ?? "", origin?.Name ?? "Unknown origin", profile,
            preparation, slots, ability, uses, recharge, counts, diagnostic, slotUses);
    }

    private static string Value(RuleBase? rule, string name) =>
        rule?.Setters.ContainsSetter(name) == true ? rule.Setters.GetSetter(name).Value.Trim() : "";
}
