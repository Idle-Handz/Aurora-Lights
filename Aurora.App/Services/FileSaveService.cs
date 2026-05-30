using Microsoft.Maui.Storage;

namespace Aurora.App.Services;

/// <summary>
/// Cross-platform "Save As…" dialog. Returns the chosen full path, or null when the
/// user cancels. Writing the bytes is the caller's responsibility so we don't have
/// to marshal large PDF buffers through platform-specific APIs.
/// </summary>
public static class FileSaveService
{
    public sealed record FileTypeChoice(string Label, string Extension);

    /// <summary>
    /// Prompts the user for a save location. <paramref name="suggestedFileName"/> should
    /// include the extension. Returns the chosen full path, or null if cancelled.
    /// </summary>
    public static Task<string?> PickSaveLocationAsync(
        string suggestedFileName,
        IReadOnlyList<FileTypeChoice> fileTypes,
        string? initialDirectory = null)
    {
#if WINDOWS
        return PickSaveLocationWindowsAsync(suggestedFileName, fileTypes, initialDirectory);
#elif MACCATALYST
        return PickSaveLocationMacAsync(suggestedFileName, fileTypes, initialDirectory);
#elif ANDROID
        return PickSaveLocationAndroidAsync(suggestedFileName);
#else
        return Task.FromResult<string?>(null);
#endif
    }

    /// <summary>
    /// On Android, triggers the OS share sheet so the user can save/share the file
    /// (e.g. send to Files, Drive, email, etc.). No-op on other platforms.
    /// Must be called on the main thread.
    /// </summary>
    public static Task ShareFileAsync(string filePath, string displayName)
    {
#if ANDROID
        return Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = displayName,
            File  = new ShareFile(filePath),
        });
#else
        _ = filePath;
        _ = displayName;
        return Task.CompletedTask;
#endif
    }

#if WINDOWS
    private static async Task<string?> PickSaveLocationWindowsAsync(
        string suggestedFileName,
        IReadOnlyList<FileTypeChoice> fileTypes,
        string? initialDirectory)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            SuggestedFileName      = Path.GetFileNameWithoutExtension(suggestedFileName),
        };
        foreach (var ft in fileTypes)
            picker.FileTypeChoices.Add(ft.Label, new List<string> { ft.Extension });

        // Unpackaged Win32 requires associating the picker with the app window.
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
                     as Microsoft.UI.Xaml.Window;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        // FileSavePicker in unpackaged apps doesn't accept an explicit starting folder path;
        // the OS remembers the last location per-extension. SuggestedFileName is the best hint.
        _ = initialDirectory;

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
#endif

#if ANDROID
    private static Task<string?> PickSaveLocationAndroidAsync(string suggestedFileName)
    {
        // Android has no file-save picker. Write to a cache path and follow up with
        // ShareFileAsync so the user can send it to Files, Drive, or any other app.
        string path = Path.Combine(FileSystem.Current.CacheDirectory, suggestedFileName);
        return Task.FromResult<string?>(path);
    }
#endif

#if MACCATALYST
    private static async Task<string?> PickSaveLocationMacAsync(
        string suggestedFileName,
        IReadOnlyList<FileTypeChoice> fileTypes,
        string? initialDirectory)
    {
        // NSSavePanel is AppKit-only and has no managed binding in Mac Catalyst.
        // UIDocumentPickerViewController (folder-open mode) maps to NSOpenPanel on macOS,
        // giving the user a real Finder-style dialog. We pick a destination folder and
        // append the suggestedFileName so the caller can write directly to the result.
        // Requires the com.apple.security.files.user-selected.read-write entitlement.
        _ = initialDirectory; // FolderPicker starts in the OS default location
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful) return null;
        return Path.Combine(result.Folder.Path, suggestedFileName);
    }
#endif
}
