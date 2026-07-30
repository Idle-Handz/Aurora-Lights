using Aurora.Components.Models;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using System.Text.RegularExpressions;
using System.Xml;

namespace Aurora.App.Services;

public static partial class BuildService
{
    /// <summary>
    /// Adds a custom-feature proxy (an "Additional …" feat/spell/feature/etc. or a Supernatural
    /// Gift) to the active character and activates it so its granted content applies, then
    /// reprocesses, re-snaps, and saves. Returns null on success or an error message.
    /// </summary>
    public static async Task<string?> AddCustomFeatureAsync(CharacterTab tab, string elementId)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                var proxy = DataManager.Current.ElementsCollection.GetElement(elementId);
                if (proxy == null) return "That feature could not be found.";

                // "Additional X" proxies wrap the real feat/spell/feature; resolve the underlying
                // element so its grant applies. Non-proxy items (e.g. Supernatural Gifts) resolve to
                // themselves. See EquipmentService.ResolveCustomFeatureTarget.
                var target = EquipmentService.ResolveCustomFeatureTarget(proxy);
                string targetId = target.Id ?? elementId;
                bool repeatable = EquipmentService.IsRepeatableCustomFeature(target)
                                  || EquipmentService.IsRepeatableCustomFeature(proxy);

                var file = tab.File;
                var list = file?.LoadCustomFeatures() ?? [];
                if (!repeatable && list.Contains(targetId, StringComparer.OrdinalIgnoreCase))
                    return (string?)null;

                // Ability-score elements are the exception to the normal fresh-copy rule. The legacy
                // engine counts repeated registration of the shared ASI instance, while GetFresh()
                // creates a distinct instance that does not contribute a second increase.
                var toRegister = target;
                bool isAbilityScoreIncrease = string.Equals(
                    target.Type,
                    "Ability Score Improvement",
                    StringComparison.OrdinalIgnoreCase);
                if (repeatable && !isAbilityScoreIncrease)
                {
                    toRegister = DataManager.Current.ElementsCollection.GetFresh(targetId) ?? target;
                }

                CharacterManager.Current.RegisterElement(toRegister);
                CharacterManager.Current.ReprocessCharacter();
                ResnapTab(tab);
                SaveCharacterFile(tab);

                // Track the added id (root-level <custom-features> node) so the Extras tab can
                // list and remove it. Written after SaveCharacterFile so it lands in the saved file.
                if (file != null)
                {
                    if (repeatable || !list.Contains(targetId, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(targetId);
                        CharacterFileWriteCoordinator.Write(
                            tab.FileSaveSemaphore,
                            file,
                            "Custom features",
                            () => file.SaveCustomFeatures(list)).ThrowIfFailed();
                    }
                }
                return (string?)null;
            }
            catch (Exception ex) { return DebugLogService.Catch(ex, "BuildService.AddCustomFeatureAsync"); }
        });
    }

    /// <summary>
    /// Returns the custom features added to the active character (id, display name, type),
    /// resolved from the persisted &lt;custom-features&gt; id list.
    /// </summary>
    public static IReadOnlyList<(string Id, string Name, string Type)> GetCustomFeatures(CharacterTab tab)
    {
        var file = tab.File;
        if (file == null) return [];
        var result = new List<(string, string, string)>();
        foreach (var id in file.LoadCustomFeatures())
        {
            var el = DataManager.Current.ElementsCollection.GetElement(id);
            var target = el == null ? null : EquipmentService.ResolveCustomFeatureTarget(el);
            result.Add((id, target?.Name ?? el?.Name ?? id, target?.Type ?? el?.Type ?? ""));
        }
        return result;
    }

    /// <summary>
    /// Re-registers the character's custom features (the persisted &lt;custom-features&gt; ids) into the
    /// freshly-loaded CharacterManager so their granted content (spells, etc.) takes effect again.
    /// CharacterFile.Save rebuilds only the standard build, so a directly-registered custom feature is
    /// NOT round-tripped by a normal load — it must be re-applied here. Call once after each character
    /// load. Idempotent: a feature already present as our (blank-acquisition) instance is skipped, so a
    /// duplicate call is harmless. Mirrors <see cref="AddCustomFeatureAsync"/>'s registration.
    /// </summary>
    public static void ReapplyCustomFeatures(CharacterFile? file)
    {
        if (file == null) return;
        var ids = file.LoadCustomFeatures();
        if (ids.Count == 0) return;

        var cm = CharacterManager.Current;
        if (cm?.Character == null) return;

        bool any = false;
        foreach (var group in ids.GroupBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            string id = group.Key;
            var proxy = DataManager.Current.ElementsCollection.GetElement(id);
            if (proxy == null) continue;

            var target = EquipmentService.ResolveCustomFeatureTarget(proxy);
            string targetId = target.Id ?? id;
            bool repeatable = EquipmentService.IsRepeatableCustomFeature(target)
                              || EquipmentService.IsRepeatableCustomFeature(proxy);

            bool isAbilityScoreIncrease = string.Equals(
                target.Type,
                "Ability Score Improvement",
                StringComparison.OrdinalIgnoreCase);
            var existingMatches = cm.GetElements().Where(e => e.Id == targetId).ToList();
            int ownedBaseline = existingMatches.Any(e =>
                e.Aquisition.WasGranted || e.Aquisition.WasSelected) ? 1 : 0;
            int existingCustomCount = isAbilityScoreIncrease
                ? Math.Max(0, existingMatches.Count - ownedBaseline)
                : existingMatches.Count(e => !e.Aquisition.WasGranted && !e.Aquisition.WasSelected);
            int desiredCount = repeatable ? group.Count() : 1;
            int toAddCount = Math.Max(0, desiredCount - existingCustomCount);
            if (toAddCount == 0)
                continue;

            for (int i = 0; i < toAddCount; i++)
            {
                // Repeatable elements normally need separate acquisition records. ASIs instead use
                // repeated registration of the shared instance because that is what the engine counts.
                var toRegister = target;
                if (repeatable && !isAbilityScoreIncrease)
                    toRegister = DataManager.Current.ElementsCollection.GetFresh(targetId) ?? target;

                cm.RegisterElement(toRegister);
                any = true;
            }
        }

        if (any) cm.ReprocessCharacter();
    }

    /// <summary>
    /// Removes a previously added custom feature: unregisters the element, reprocesses, saves,
    /// and drops its id from the &lt;custom-features&gt; list. Returns null on success.
    /// </summary>
    public static async Task<string?> RemoveCustomFeatureAsync(CharacterTab tab, string elementId)
    {
        using var scope = await CharacterContext.EnterAsync(tab);
        return await Task.Run(() =>
        {
            try
            {
                var cm = CharacterManager.Current;
                var proxy = DataManager.Current.ElementsCollection.GetElement(elementId);
                var target = proxy == null ? null : EquipmentService.ResolveCustomFeatureTarget(proxy);
                string targetId = target?.Id ?? elementId;

                // Repeated ASIs share one engine instance, so remove the last registration and leave
                // the first (owned) occurrence intact. Other custom copies retain blank acquisition.
                var matches = cm.GetElements().Where(e => e.Id == targetId).ToList();
                var el = matches.Count > 1
                    ? matches.Last()
                    : matches.FirstOrDefault(e => !e.Aquisition.WasGranted && !e.Aquisition.WasSelected)
                         ?? matches.FirstOrDefault();
                if (el != null)
                {
                    bool preserveAcquisition = matches.Count > 1;
                    bool wasSelected = el.Aquisition.WasSelected;
                    bool wasGranted = el.Aquisition.WasGranted;
                    var selectRule = el.Aquisition.SelectRule;
                    var grantRule = el.Aquisition.GrantRule;

                    cm.UnregisterElement(el);

                    if (preserveAcquisition && wasSelected && selectRule != null)
                        el.Aquisition.SelectedBy(selectRule);
                    else if (preserveAcquisition && wasGranted && grantRule != null)
                        el.Aquisition.GrantedBy(grantRule);
                }
                cm.ReprocessCharacter();
                ResnapTab(tab);
                SaveCharacterFile(tab);

                var file = tab.File;
                if (file != null)
                {
                    var list = file.LoadCustomFeatures();
                    int index = list.FindIndex(x => string.Equals(x, elementId, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                        list.RemoveAt(index);
                    CharacterFileWriteCoordinator.Write(
                        tab.FileSaveSemaphore,
                        file,
                        "Custom features",
                        () => file.SaveCustomFeatures(list)).ThrowIfFailed();
                }
                return (string?)null;
            }
            catch (Exception ex) { return DebugLogService.Catch(ex, "BuildService.RemoveCustomFeatureAsync"); }
        });
    }
}
