using Builder.Core.Events;
using Builder.Presentation;
using Builder.Presentation.Interfaces;

namespace Aurora.Reflections.Services;

/// <summary>
/// Minimal IApplicationContext for the MAUI Blazor Hybrid shell.
/// Satisfies the Aurora.Logic static seam without pulling in WPF.
/// </summary>
public sealed class MauiApplicationContext : IApplicationContext
{
    public IEventAggregator EventAggregator { get; } = new Builder.Core.Events.EventAggregator();

    public AppSettingsStore Settings { get; } = AppSettingsStore.Load();

    public bool IsInDeveloperMode { get; set; }

    public bool EnableDiagnostics { get; set; }

    public string? LoadedCharacterFilePath { get; set; }

    public bool HasCharacterFileRequest => !string.IsNullOrEmpty(LoadedCharacterFilePath);

    public void SendStatusMessage(string statusMessage)
    {
        System.Diagnostics.Debug.WriteLine($"[Status] {statusMessage}");
    }
}
