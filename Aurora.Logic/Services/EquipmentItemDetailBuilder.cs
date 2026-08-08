using Builder.Data;
using Builder.Presentation.Models;
using Builder.Presentation.Utilities;
using Builder.Presentation.ViewModels.Shell.Items;
using System.Reflection;

namespace Builder.Presentation.Services;

public sealed record EquipmentItemDetailSectionModel(
    string Label,
    string Name,
    string Type,
    string Source,
    string Description,
    string DescriptionHtml);

public sealed record EquipmentItemDetailModel(
    string Name,
    string BaseName,
    string Type,
    string Source,
    string Notes,
    string Damage,
    string Range,
    string Properties,
    string DisplayWeight,
    string DisplayPrice,
    bool IsEquipped,
    string EquippedLocation,
    IReadOnlyList<EquipmentItemDetailSectionModel> Sections);

/// <summary>
/// Builds host-neutral equipment details from the inventory item's own element copies.
/// Reading the copies is important: if a host or imported character has replaced an
/// element description, the inventory-specific value wins over the content database.
/// </summary>
public static class EquipmentItemDetailBuilder
{
    public static EquipmentItemDetailModel Build(RefactoredEquipmentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(item.Item);

        var sections = new List<EquipmentItemDetailSectionModel>();
        if (item.IsAdorned && item.AdornerItem is not null)
        {
            sections.Add(CreateSection("Magic properties", item.AdornerItem));
            sections.Add(CreateSection("Base item", item.Item));
        }
        else
        {
            sections.Add(CreateSection("Item details", item.Item));
        }

        var primary = item.IsAdorned && item.AdornerItem is not null
            ? item.AdornerItem
            : item.Item;

        return new EquipmentItemDetailModel(
            item.DisplayName ?? item.Name ?? item.Item.Name ?? item.Identifier,
            item.Name ?? item.Item.Name ?? item.Identifier,
            primary.Type ?? string.Empty,
            primary.Source ?? string.Empty,
            item.Notes ?? string.Empty,
            FormatDamage(item.Item),
            GetElementString(item.Item, "Range"),
            GetElementString(item.Item, "DisplayWeaponProperties"),
            item.DisplayWeight ?? GetElementString(item.Item, "DisplayWeight"),
            item.DisplayPrice ?? GetElementString(item.Item, "DisplayPrice"),
            item.IsEquipped,
            item.EquippedLocation ?? string.Empty,
            sections);
    }

    private static EquipmentItemDetailSectionModel CreateSection(
        string label,
        ElementBase element)
    {
        return new EquipmentItemDetailSectionModel(
            label,
            element.Name ?? string.Empty,
            element.Type ?? string.Empty,
            element.Source ?? string.Empty,
            GetDescription(element),
            element.Description ?? string.Empty);
    }

    private static string GetDescription(ElementBase element)
    {
        try
        {
            if (element.SheetDescription?.Count > 0)
            {
                var sheetDescription = element.SheetDescription[0].Description?.Trim();
                if (!string.IsNullOrEmpty(sheetDescription))
                    return sheetDescription;
            }
        }
        catch
        {
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(element.Description))
            {
                return ElementDescriptionGenerator
                    .GeneratePlainDescription(element.Description)
                    .Trim();
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string FormatDamage(ElementBase element)
    {
        var damage = GetElementString(element, "Damage");
        if (string.IsNullOrWhiteSpace(damage) || damage == "—")
            return string.Empty;

        var damageType = GetElementString(element, "DamageType");
        return string.IsNullOrWhiteSpace(damageType)
            ? damage
            : $"{damage} {damageType}";
    }

    private static string GetElementString(object element, string propertyName)
    {
        try
        {
            var property = element.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(element)?.ToString()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
