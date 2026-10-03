// Only platform APIs are substituted: the character, catalog, refresh and save services
// in the lifecycle tests are the production implementations linked by the test project.
namespace Microsoft.Maui.Storage
{
    public sealed class Preferences
    {
        public static Preferences Default { get; } = new();
        public void Set(string key, string value) { }
    }

    public enum DevicePlatform { WinUI, Android, MacCatalyst, macOS }
    public sealed record FilePickerFileType(Dictionary<DevicePlatform, IEnumerable<string>> Types);
    public sealed class PickOptions
    {
        public string? PickerTitle { get; init; }
        public FilePickerFileType? FileTypes { get; init; }
    }
    public sealed class FileResult
    {
        public string FileName { get; init; } = "";
        public Func<Task<Stream>> OpenRead { get; init; } = () => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync() => OpenRead();
    }
    public sealed class FilePicker
    {
        public static FilePicker Default { get; } = new();
        public Func<PickOptions, Task<FileResult?>> Pick { get; set; } = _ => throw new NotSupportedException();
        public Task<FileResult?> PickAsync(PickOptions options) => Pick(options);
    }
}

namespace Aurora.App.Services
{
    // UserPreferencesService otherwise pulls the unrelated MAUI theme/storage surface in.
    public enum HpMethod { Average, Rolled }
}
