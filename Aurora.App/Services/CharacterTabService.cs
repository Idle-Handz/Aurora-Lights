using Builder.Presentation;
using Builder.Presentation.Models;

namespace Aurora.App.Services;

/// <summary>
/// Manages the set of open character tabs. Each tab holds a cached Character
/// so switching tabs doesn't require reloading from disk.
/// </summary>
/// <remarks>
/// Not thread-safe by design: all mutations and reads must happen on the UI/sync-context
/// thread. Every caller today is a Blazor event handler or marshals through InvokeAsync, so
/// the invariant holds. If a future caller ever mutates tab state from a background thread
/// (e.g. a Task.Run autosave or a timer), add synchronization around the tab list and
/// ActiveTab before doing so.
/// </remarks>
public sealed class CharacterTabService
{
    private readonly List<CharacterTab> _tabs = [];

    public IReadOnlyList<CharacterTab> Tabs => _tabs;
    public CharacterTab? ActiveTab { get; private set; }
    public bool HasUnsavedChanges => _tabs.Any(t => t.IsDirty);

    /// <summary>Fires whenever the tab list or active tab changes.</summary>
    public event Action? TabsChanged;

    /// <summary>
    /// Finds an existing tab for the given file by reference.
    /// Returns null if the character is not currently open.
    /// </summary>
    public CharacterTab? FindTab(CharacterFile file) =>
        _tabs.FirstOrDefault(t => ReferenceEquals(t.File, file));

    /// <summary>
    /// Opens a new tab for the character, or activates the existing one if
    /// it is already open. Returns the tab that is now active.
    /// </summary>
    public CharacterTab OpenTab(CharacterFile file, Character character)
    {
        var existing = FindTab(file);
        if (existing != null)
        {
            existing.Character = character;   // refresh cached object on re-open
            ActiveTab = existing;
            TabsChanged?.Invoke();
            return existing;
        }

        var tab = new CharacterTab(file) { Character = character };
        _tabs.Add(tab);
        ActiveTab = tab;
        TabsChanged?.Invoke();
        return tab;
    }

    /// <summary>Switches the active tab without reloading the character.</summary>
    public void ActivateTab(CharacterTab tab)
    {
        if (!_tabs.Contains(tab)) return;
        ActiveTab = tab;
        TabsChanged?.Invoke();
    }

    /// <summary>
    /// Marks a tab as having unsaved changes and notifies listeners.
    /// </summary>
    public void MarkDirty(CharacterTab tab)
    {
        // Only notify on the clean→dirty transition. The dirty indicator is idempotent, so
        // re-firing TabsChanged on every keystroke (e.g. typing in a notes field) just causes
        // a needless re-render fan-out across the layout, nav, and subscribed pages.
        if (tab.IsDirty) return;
        tab.IsDirty = true;
        TabsChanged?.Invoke();
    }

    /// <summary>
    /// Clears the dirty flag on a tab (e.g. after a successful save).
    /// </summary>
    public void MarkClean(CharacterTab tab)
    {
        tab.IsDirty = false;
        TabsChanged?.Invoke();
    }

    /// <summary>
    /// Opens a placeholder tab immediately so the user can navigate to the character
    /// pages before loading completes. Set <see cref="CharacterTab.IsLoading"/> = false
    /// and call <see cref="NotifyChanged"/> once the load finishes.
    /// </summary>
    public CharacterTab OpenLoadingTab(CharacterFile file)
    {
        var existing = FindTab(file);
        if (existing != null)
        {
            existing.IsLoading = true;
            ActiveTab = existing;
            TabsChanged?.Invoke();
            return existing;
        }

        var tab = new CharacterTab(file) { IsLoading = true };
        _tabs.Add(tab);
        ActiveTab = tab;
        TabsChanged?.Invoke();
        return tab;
    }

    /// <summary>Fires <see cref="TabsChanged"/> without changing any state.</summary>
    public void NotifyChanged() => TabsChanged?.Invoke();

    /// <summary>
    /// Closes all open tabs. Used before reloading content so stale character
    /// objects don't reference the old element collection.
    /// </summary>
    public void CloseAllTabs()
    {
        _tabs.Clear();
        ActiveTab = null;
        TabsChanged?.Invoke();
    }

    /// <summary>
    /// Closes a tab. If it was active, the nearest remaining tab becomes active
    /// (or null if no tabs remain).
    /// </summary>
    public void CloseTab(CharacterTab tab)
    {
        var idx = _tabs.IndexOf(tab);
        if (idx < 0) return;

        _tabs.RemoveAt(idx);

        if (ActiveTab == tab)
            ActiveTab = _tabs.Count > 0 ? _tabs[Math.Max(0, idx - 1)] : null;

        TabsChanged?.Invoke();
    }
}
