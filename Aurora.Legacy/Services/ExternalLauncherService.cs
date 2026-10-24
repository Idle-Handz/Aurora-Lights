using Builder.Core.Logging;
using Builder.Presentation.Interfaces;
using System.Diagnostics;

namespace Builder.Presentation.Services;

/// <summary>
/// WPF/desktop implementation of the shared launcher contract.
/// Delegates to the OS shell so files, folders, and URLs open with the default handler.
/// </summary>
public sealed class ExternalLauncherService : IExternalLauncher
{
    public bool OpenPath(string path) => Open(path, nameof(OpenPath));

    public bool OpenUri(string uri) => Open(uri, nameof(OpenUri));

    private static bool Open(string target, string caller)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, caller);
            MessageDialogContext.Current?.ShowException(ex, $"Unable to open '{target}'.");
            return false;
        }
    }
}
