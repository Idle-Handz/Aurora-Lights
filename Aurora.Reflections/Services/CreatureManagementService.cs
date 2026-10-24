using Aurora.Components.Models;

namespace Aurora.Reflections.Services;

public static class CreatureManagementService
{
    public static CreatureManagementModel Model(SidekickProfile profile)
    {
        IReadOnlyList<CreatureSlotModel> slots = [];
        string? slotError = null;
        try { slots = CreatureCombat.Slots(profile).Where(s => s.Value > 0)
            .Select(s => new CreatureSlotModel(s.Key, s.Value, profile.Combat.SpellSlotsUsed.GetValueOrDefault(s.Key))).ToList(); }
        catch (InvalidOperationException ex) { slotError = ex.Message; }
        return new()
        {
            Id = profile.Id, Name = profile.Name,
            PortraitSource = CreaturePortrait.DisplaySource(profile.Get("portrait")), Notes = profile.Get("notes"),
            Subtitle = profile.IsCompanion ? "Companion" : $"Level {profile.Get("level")} · {profile.Get("role")} sidekick · {(profile.FollowOwnerLevel ? "Matches owner" : "Manual level")}",
            PendingChoices = SidekickChoices.Pending(profile),
            ArmorClass = profile.Get("ac"), MaximumHp = CreatureCombat.MaximumHp(profile),
            CurrentHp = CreatureCombat.CurrentHp(profile), TemporaryHp = CreatureCombat.TemporaryHp(profile),
            HitDice = CreatureCombat.HitDice(profile), HitDiceUsed = profile.Combat.HitDiceUsed,
            Exhaustion = profile.Combat.Exhaustion, Conditions = profile.Combat.Conditions,
            AvailableConditions = CreatureCombat.Conditions, Slots = slots, SlotError = slotError,
            SlotRecovery = profile.Get("slot-recovery"),
            Resources = CreatureCombat.Resources(profile).Select(r => new CreatureResourceModel(r.Id, r.Name, r.Max, r.Used,
                r.ResetOn switch { ResetOn.ShortRest => "Short or long rest", ResetOn.LongRest => "Long rest", _ => "Manual" },
                profile.Combat.Resources.Any(c => c.Id == r.Id), IsGeneratedResource(r.Id))).ToList(),
            Details = SidekickService.Fields.Where(f => SidekickProgression.IsVisible(profile, f))
                .Where(f => f.Key is not ("name" or "current-hp" or "temporary-hp" or "notes") && !string.IsNullOrWhiteSpace(profile.Get(f.Key)))
                .Select(f => new CreatureDetailModel(f.Label, profile.Get(f.Key), f.Lines > 1)).ToList()
        };
    }

    public static void Apply(SidekickProfile profile, CreatureAction action)
    {
        switch (action.Kind)
        {
            case "damage": CreatureCombat.Damage(profile, action.Amount); break;
            case "heal": CreatureCombat.Heal(profile, action.Amount); break;
            case "temporary-hp": CreatureCombat.GrantTemporaryHp(profile, action.Amount); break;
            case "clear-temporary-hp": profile.Set("temporary-hp", "0"); break;
            case "condition": CreatureCombat.SetCondition(profile, action.Key, action.Active); break;
            case "exhaustion": profile.Combat.Exhaustion = Math.Clamp(action.Amount, 0, 6); break;
            case "spend-slot": CreatureCombat.AdjustSlot(profile, int.Parse(action.Key), 1); break;
            case "recover-slot": CreatureCombat.AdjustSlot(profile, int.Parse(action.Key), -1); break;
            case "use-resource": CreatureCombat.AdjustResource(profile, action.Key, 1); break;
            case "recover-resource": CreatureCombat.AdjustResource(profile, action.Key, -1); break;
            case "remove-resource":
                if (profile.Combat.Resources.FirstOrDefault(r => r.Id == action.Key) is { } removed
                    && IsGeneratedResource(action.Key))
                    profile.Combat.FeatureUses[action.Key] = removed.Used;
                profile.Combat.Resources.RemoveAll(r => r.Id == action.Key);
                break;
            case "hit-die": CreatureCombat.SpendHitDie(profile, action.Amount); break;
            case "short-rest": CreatureCombat.Rest(profile, false); break;
            case "long-rest": CreatureCombat.Rest(profile, true); break;
            default: throw new InvalidOperationException("That creature action is not supported.");
        }
    }

    private static bool IsGeneratedResource(string id) => id.StartsWith("sidekick:", StringComparison.Ordinal)
        || id.StartsWith("companion:", StringComparison.Ordinal);

    public static void SetResource(SidekickProfile profile, CustomResource resource)
    {
        if (string.IsNullOrWhiteSpace(resource.Name) || resource.Max is < 1 or > 99)
            throw new InvalidOperationException("Enter a resource name and a maximum between 1 and 99.");
        // Preserve the latest usage if another control changed it while the resource editor was open.
        int used = CreatureCombat.Resources(profile).FirstOrDefault(r => r.Id == resource.Id)?.Used ?? 0;
        profile.Combat.Resources.RemoveAll(r => r.Id == resource.Id);
        profile.Combat.Resources.Add(new CustomResource { Id = resource.Id, Name = resource.Name, Max = resource.Max,
            Used = Math.Clamp(used, 0, resource.Max), ResetOn = resource.ResetOn });
    }
}
