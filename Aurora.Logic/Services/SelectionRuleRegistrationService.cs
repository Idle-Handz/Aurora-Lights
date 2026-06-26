using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services.Data;

namespace Builder.Presentation.Services;

/// <summary>
/// Shared registration behavior for non-WPF selection rule expanders. The legacy WPF
/// controls own their registration state, while MAUI and tests use a slot-keyed map.
/// Keeping the mutation here prevents those two paths from drifting.
/// </summary>
public static class SelectionRuleRegistrationService
{
    public static void SetRegisteredElement(
        IDictionary<string, object> registrations,
        SelectRule selectionRule,
        string id,
        int number = 1)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(selectionRule);

        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A selection requires an element ID.", nameof(id));

        string key = GetKey(selectionRule, number);

        if (selectionRule.Attributes.IsList)
        {
            SetListItem(registrations, selectionRule, id, number, key);
            return;
        }

        ElementBase? source = DataManager.Current.ElementsCollection.GetElement(id);
        if (source is null)
            throw new InvalidOperationException($"The selected element '{id}' is not available in the loaded content.");

        registrations.TryGetValue(key, out object? registered);
        ElementBase? current = registered as ElementBase;
        var manager = CharacterManager.Current;
        var ownedElements = manager.GetElements();
        bool currentIsOwned = current is not null && ownedElements.Any(element => ReferenceEquals(element, current));
        if (currentIsOwned && current!.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase))
            return;

        bool alreadyOwned = ownedElements.Any(element =>
            element.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase) &&
            !ReferenceEquals(element, current));

        if (alreadyOwned && !source.AllowDuplicate)
        {
            throw new InvalidOperationException(
                $"'{source.Name}' is already selected and cannot be selected again.");
        }

        // A repeated selection needs its own acquisition record. Reusing the content
        // singleton would overwrite the first slot's SelectRule association.
        ElementBase toRegister = alreadyOwned
            ? DataManager.Current.ElementsCollection.GetFresh(source.Id)
                ?? throw new InvalidOperationException($"Could not create a separate selection instance for '{source.Name}'.")
            : source;

        // Validate first, then remove the previous slot value. A rejected replacement must
        // leave the current selection intact.
        if (currentIsOwned)
            manager.UnregisterElement(current!);

        toRegister.Aquisition.WasSelected = true;
        toRegister.Aquisition.SelectRule = selectionRule;
        manager.RegisterElement(toRegister);

        if (selectionRule.Attributes.Type.Equals("Background Feature", StringComparison.OrdinalIgnoreCase) &&
            selectionRule.Attributes.Optional)
        {
            var optionalGrant = DataManager.Current.ElementsCollection
                .GetElement("ID_INTERNAL_GRANT_OPTIONAL_BACKGROUND_FEATURE");
            if (optionalGrant is not null &&
                !manager.GetElements().Any(element => element.Id.Equals(optionalGrant.Id, StringComparison.Ordinal)))
            {
                manager.RegisterElement(optionalGrant);
            }
        }

        registrations[key] = toRegister;
    }

    public static void ClearRegisteredElement(
        IDictionary<string, object> registrations,
        SelectRule selectionRule,
        int number = 1)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(selectionRule);

        registrations.Remove(GetKey(selectionRule, number));

        if (!selectionRule.Attributes.IsList)
            return;

        string? ownerId = selectionRule.ElementHeader?.Id;
        if (string.IsNullOrWhiteSpace(ownerId))
            return;

        var owner = CharacterManager.Current.GetElements()
            .FirstOrDefault(element => element.Id.Equals(ownerId, StringComparison.OrdinalIgnoreCase));
        owner?.SelectionRuleListItems.Remove(GetListItemKey(selectionRule, number));
    }

    private static void SetListItem(
        IDictionary<string, object> registrations,
        SelectRule selectionRule,
        string id,
        int number,
        string key)
    {
        var listItem = selectionRule.Attributes.ListItems?
            .FirstOrDefault(item => item.ID.ToString().Equals(id, StringComparison.Ordinal));
        if (listItem is null)
            throw new InvalidOperationException(
                $"The selected list item '{id}' is not available for '{selectionRule.Attributes.Name}'.");

        string? ownerId = selectionRule.ElementHeader?.Id;
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            var owner = CharacterManager.Current.GetElements()
                .FirstOrDefault(element => element.Id.Equals(ownerId, StringComparison.OrdinalIgnoreCase));
            if (owner is not null)
            {
                string itemKey = GetListItemKey(selectionRule, number);
                owner.SelectionRuleListItems.Remove(itemKey);
                owner.SelectionRuleListItems.Add(itemKey, listItem);
            }
        }

        registrations[key] = listItem;
    }

    private static string GetKey(SelectRule selectionRule, int number) =>
        $"{selectionRule.UniqueIdentifier}:{number}";

    private static string GetListItemKey(SelectRule selectionRule, int number) =>
        $"{selectionRule.Attributes.Name}:{number}";
}
