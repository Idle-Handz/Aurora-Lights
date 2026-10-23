using Aurora.App.Services;
using Microsoft.Extensions.DependencyInjection;
namespace Aurora.App;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        DebugLogService.Instance.Info("MainPage constructor entered.");
        InitializeComponent();
        DebugLogService.Instance.Info("MainPage InitializeComponent completed.");

        Loaded += OnLoaded;
        blazorWebView.BlazorWebViewInitializing += (_, args) =>
        {
#if WINDOWS
            var userDataFolder = Path.Combine(FileSystem.Current.AppDataDirectory, "WebView2");
            Directory.CreateDirectory(userDataFolder);
            args.UserDataFolder = userDataFolder;
            DebugLogService.Instance.Info(
                "BlazorWebView initializing.",
                $"Host page: {blazorWebView.HostPage}; WebView2 user data: {userDataFolder}");
#else
            DebugLogService.Instance.Info("BlazorWebView initializing.", $"Host page: {blazorWebView.HostPage}");
#endif
        };
        blazorWebView.BlazorWebViewInitialized += (_, args) =>
        {
#if ANDROID
            // Honor the host page's zoom policy and allow pinch magnification without
            // Android's deprecated floating zoom buttons covering app controls.
            args.WebView.Settings.SetSupportZoom(true);
            args.WebView.Settings.BuiltInZoomControls = true;
            args.WebView.Settings.DisplayZoomControls = false;
            // ConnectHandler installs BlazorWebView's history callback. Post until
            // it finishes so our overlay/unsaved-close callback takes precedence.
            args.WebView.Post(() =>
            {
                if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is MainActivity activity &&
                    !activity.IsDestroyed)
                    activity.RegisterBackCallback();
            });
#endif
            DebugLogService.Instance.Info("BlazorWebView initialized.");
        };
        blazorWebView.HandlerChanged += OnBlazorWebViewHandlerChanged;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        DebugLogService.Instance.Info("MainPage loaded.");
    }

    private void OnBlazorWebViewHandlerChanged(object? sender, EventArgs e)
    {
        DebugLogService.Instance.Info("BlazorWebView handler changed.", blazorWebView.Handler?.GetType().FullName);
    }

#if ANDROID
    private bool _androidBackPending;
    private bool _continueAndroidBack;

    // MAUI invokes this after Android has had the chance to dismiss the keyboard.
    // Ask the rendered UI first so a modal never causes the activity to exit.
    internal bool HandleAndroidBack(Android.App.Activity activity, bool navigateWebHistory = false)
    {
        if (_continueAndroidBack || blazorWebView.Handler?.PlatformView is not Android.Webkit.WebView webView)
            return false;
        if (_androidBackPending)
            return true;

        _androidBackPending = true;
        _ = CompleteAndroidBackAsync(activity, webView, navigateWebHistory);
        return true;
    }

    private async Task CompleteAndroidBackAsync(Android.App.Activity activity, Android.Webkit.WebView webView, bool navigateWebHistory)
    {
        var handled = false;
        try
        {
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            webView.EvaluateJavascript("window.AuroraBack?.dismissOverlay() === true",
                new BackResult(value => result.TrySetResult(value)));
            // A disposed or unresponsive WebView must not permanently swallow Back.
            handled = await result.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            DebugLogService.Catch(ex, "MainPage.HandleAndroidBack");
        }
        finally { _androidBackPending = false; }

        if (handled || activity.IsFinishing || activity.IsDestroyed)
            return;

        // Hardware Back previously used BlazorWebView's history before the activity.
        // Preserve that order, but only after the overlay had the first opportunity.
        try
        {
            if (navigateWebHistory && webView.CanGoBack())
            {
                webView.GoBack();
                return;
            }
        }
        catch (ObjectDisposedException) { /* Continue normal Back after WebView teardown. */ }

        try
        {
            var close = Handler?.MauiContext?.Services.GetService<WindowCloseCoordinator>();
            if (close is null)
            {
                DebugLogService.Instance.Warn("Android close handling is unavailable. Keep the app open until it finishes loading.");
                return;
            }

            // Root Back is an exit request: join pending saves and offer Save/Discard/Cancel
            // before allowing Android to finish or background this activity.
            await close.RequestCloseAsync(() =>
            {
                if (activity.IsFinishing || activity.IsDestroyed) return;
                _continueAndroidBack = true;
                try
                {
                    if (activity is MainActivity mainActivity)
                        mainActivity.ContinueAcceptedBack();
                    else
                        activity.MoveTaskToBack(true);
                }
                finally { _continueAndroidBack = false; }
            });
        }
        catch (Exception ex) { DebugLogService.Catch(ex, "MainPage.CloseAndroidActivity"); }
    }

    private sealed class BackResult(Action<bool> completed) : Java.Lang.Object, Android.Webkit.IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => completed(value?.ToString() == "true");
    }
#endif
}
