namespace Aurora.Components.Models;

public sealed record CharacterBrowserEntry(
    string Id,
    string DisplayName,
    string PrimaryText,
    string? SecondaryText = null,
    string? CaptionText = null,
    string? GroupName = null,
    bool IsFavorite = false,
    string? PortraitDataUrl = null,
    bool IsCloud = false);
