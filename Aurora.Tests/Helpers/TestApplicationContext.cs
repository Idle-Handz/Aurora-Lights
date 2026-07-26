using Builder.Core.Events;
using Builder.Presentation.Interfaces;

namespace Aurora.Tests.Helpers;

internal sealed class TestApplicationContext : IApplicationContext
{
    public TestApplicationContext()
    {
        string documentsRoot = Path.Combine(
            Path.GetTempPath(),
            "Aurora.Tests",
            Environment.ProcessId.ToString());
        Directory.CreateDirectory(documentsRoot);
        Settings.DocumentsRootDirectory = documentsRoot;
        Settings.AdditionalCustomDirectory = string.Empty;
        Settings.AdditionalCustomDirectories.Clear();
    }

    public IEventAggregator EventAggregator { get; } = new EventAggregator();

    public Builder.Presentation.AppSettingsStore Settings { get; } = new();

    public bool IsInDeveloperMode { get; set; }

    public bool EnableDiagnostics { get; set; }

    public string? LoadedCharacterFilePath { get; set; }

    public bool HasCharacterFileRequest => !string.IsNullOrWhiteSpace(LoadedCharacterFilePath);

    public void SendStatusMessage(string statusMessage)
    {
    }
}

internal static class TestApplicationContextInstaller
{
    private static readonly object Sync = new();
    private static bool _installed;

    public static void EnsureInstalled()
    {
        if (_installed)
            return;

        lock (Sync)
        {
            if (_installed)
                return;

            Builder.Presentation.ApplicationContext.SetCurrent(new TestApplicationContext());
            _installed = true;
        }
    }
}
