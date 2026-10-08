using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Aurora.App.Services;

/// <summary>
/// Lets a character open finish its navigation only while the original navigation
/// intent is current. Its lifetime follows the load, not the Characters component,
/// which is disposed when the loading page opens.
/// </summary>
public sealed class CharacterLoadNavigation : IDisposable
{
    private readonly NavigationManager _navigation;
    private readonly CharacterTabService _tabs;
    private readonly CharacterTab? _startingTab;
    private string? _loadingLocation;
    private bool _superseded;
    private bool _disposed;

    public CharacterLoadNavigation(NavigationManager navigation, CharacterTabService tabs, string? loadingRoute = null)
    {
        _navigation = navigation;
        _tabs = tabs;
        _startingTab = tabs.ActiveTab;
        _loadingLocation = loadingRoute is null ? null : navigation.ToAbsoluteUri(loadingRoute).AbsoluteUri;
        _navigation.LocationChanged += OnLocationChanged;
        _tabs.TabsChanged += OnTabsChanged;

        try
        {
            if (loadingRoute is not null) _navigation.NavigateTo(loadingRoute);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public bool IsCurrent => !_disposed && !_superseded
        && ReferenceEquals(_startingTab, _tabs.ActiveTab)
        && (_startingTab is null || _tabs.Tabs.Contains(_startingTab));

    public bool TryOpenOverview(CharacterTab tab)
    {
        if (!IsCurrent || !_tabs.Tabs.Contains(tab)) return false;
        Dispose();
        if (!ReferenceEquals(tab, _tabs.ActiveTab)) _tabs.ActivateTab(tab);
        _navigation.NavigateTo("/character");
        return true;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        // WebView can report our loading-page navigation after NavigateTo returns.
        // Accept that one transition, but never restore an intent after a later
        // user navigation, even if they return to the same page.
        if (!_superseded && _loadingLocation is not null
            && string.Equals(args.Location, _loadingLocation, StringComparison.Ordinal))
        {
            _loadingLocation = null;
            return;
        }
        _superseded = true;
    }

    private void OnTabsChanged()
    {
        if (!ReferenceEquals(_startingTab, _tabs.ActiveTab)
            || (_startingTab is not null && !_tabs.Tabs.Contains(_startingTab)))
            _superseded = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _navigation.LocationChanged -= OnLocationChanged;
        _tabs.TabsChanged -= OnTabsChanged;
    }
}
