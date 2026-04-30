using Builder.Presentation.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Aurora.App.Services;

internal sealed class MauiMessageDialogService : IMessageDialogService
{
    public void Show(string message, string? caption = null)
    {
        string title = GetCaption(caption);
        Debug.WriteLine($"[Dialog] {title}: {message}");

#if WINDOWS
        ShowWindowsMessageBox(message, title, MessageBoxStyle.Ok, MessageBoxIcon.Info);
#else
        _ = ShowAlertAsync(title, message);
#endif
    }

    public void ShowException(Exception ex, string? message = null, string? caption = null)
    {
        string title = GetCaption(caption ?? ex.GetType().Name);
        string body = BuildExceptionMessage(ex, message);
        Debug.WriteLine($"[Dialog:Exception] {title}: {body}");

#if WINDOWS
        ShowWindowsMessageBox(body, title, MessageBoxStyle.Ok, MessageBoxIcon.Error);
#else
        _ = ShowAlertAsync(title, body);
#endif
    }

    public bool Confirm(string message, string? caption = null)
    {
        string title = GetCaption(caption ?? "Confirm");
        Debug.WriteLine($"[Dialog:Confirm] {title}: {message}");

#if WINDOWS
        return ShowWindowsMessageBox(message, title, MessageBoxStyle.YesNo, MessageBoxIcon.Question) == MessageBoxResult.Yes;
#else
        Page? page = GetDialogPage();
        if (page == null)
            return false;

        if (MainThread.IsMainThread)
        {
            Debug.WriteLine("[Dialog:Confirm] synchronous confirm requested on UI thread without native sync dialog support; returning false.");
            _ = ShowAlertAsync(title, message);
            return false;
        }

        return MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlert(title, message, "Yes", "No"))
            .GetAwaiter()
            .GetResult();
#endif
    }

    private static string GetCaption(string? caption)
        => string.IsNullOrWhiteSpace(caption) ? "Aurora: Reflections" : caption;

    private static string BuildExceptionMessage(Exception ex, string? intro)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(intro))
        {
            sb.AppendLine(intro);
            sb.AppendLine();
        }

        sb.AppendLine(ex.Message);

        if (ex.Data.Contains("filename"))
        {
            sb.AppendLine();
            sb.AppendLine($"File: {ex.Data["filename"]}");
        }

        if (ex.Data.Contains("warning"))
        {
            sb.AppendLine();
            sb.AppendLine(ex.Data["warning"]?.ToString());
        }

        if (Debugger.IsAttached)
        {
            sb.AppendLine();
            sb.AppendLine(ex.ToString());
        }

        return sb.ToString().Trim();
    }

    private static Page? GetDialogPage()
        => Application.Current?.Windows.FirstOrDefault()?.Page;

    private static Task ShowAlertAsync(string title, string message)
    {
        Page? page = GetDialogPage();
        if (page == null)
            return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(async () => await page.DisplayAlertAsync(title, message, "OK"));
    }

#if WINDOWS
    private static MessageBoxResult ShowWindowsMessageBox(
        string message,
        string title,
        MessageBoxStyle style,
        MessageBoxIcon icon)
    {
        nint hwnd = GetActiveWindow();
        int result = MessageBoxW(hwnd, message, title, (uint)style | (uint)icon);
        return (MessageBoxResult)result;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern nint GetActiveWindow();

    private enum MessageBoxStyle : uint
    {
        Ok = 0x00000000,
        YesNo = 0x00000004
    }

    private enum MessageBoxIcon : uint
    {
        Info = 0x00000040,
        Question = 0x00000020,
        Error = 0x00000010
    }

    private enum MessageBoxResult
    {
        Ok = 1,
        Cancel = 2,
        No = 7,
        Yes = 6
    }
#endif
}
