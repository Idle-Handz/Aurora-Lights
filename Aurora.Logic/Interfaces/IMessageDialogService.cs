namespace Builder.Presentation.Interfaces;

/// <summary>
/// Cross-platform contract for displaying error and info dialogs.
/// The WPF implementation (MessageDialogService) shows WPF MessageBox/windows;
/// Aurora.Reflections will provide a MAUI-based implementation.
/// Accessed via MessageDialogContext.Current.
/// </summary>
public interface IMessageDialogService
{
    void Show(string message, string? caption = null);
    void ShowException(Exception ex, string? message = null, string? caption = null);
    /// <summary>Returns true if user confirmed (Yes/OK), false otherwise.</summary>
    bool Confirm(string message, string? caption = null);
}
