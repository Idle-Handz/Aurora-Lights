using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
using AndroidX.Core.View;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.App;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private bool _capturedHardwareBack;
    private OnBackPressedCallback? _backCallback;

    internal void RegisterBackCallback()
    {
        _backCallback ??= new CharacterBackCallback(this);
        // The dispatcher is LIFO. BlazorWebView registers its own history callback
        // when its handler connects, so put overlay/close handling above it.
        _backCallback.Remove();
        OnBackPressedDispatcher.AddCallback(this, _backCallback);
    }

    protected override void OnPostResume()
    {
        base.OnPostResume();
        RegisterBackCallback();
    }

    internal void ContinueAcceptedBack()
    {
        if (_backCallback is not null) _backCallback.Enabled = false;
        try { OnBackPressedDispatcher.OnBackPressed(); }
        finally
        {
            if (_backCallback is not null && !IsDestroyed) _backCallback.Enabled = true;
        }
    }

    private sealed class CharacterBackCallback(MainActivity activity) : OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            if (GetMainPage(activity)?.HandleAndroidBack(activity, navigateWebHistory: true) != true)
                activity.ContinueAcceptedBack();
        }
    }

    internal static MainPage? GetMainPage(Android.App.Activity activity) =>
        Microsoft.Maui.Controls.Application.Current?.Windows
            .FirstOrDefault(window => window.Handler?.PlatformView is Android.App.Activity windowActivity &&
                                      windowActivity.Equals(activity))?.Page as MainPage;

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e?.KeyCode == Keycode.Back)
        {
            if (e.Action == KeyEventActions.Down)
            {
                if (_capturedHardwareBack)
                    return true;

                var decorView = Window?.DecorView;
                var keyboardVisible = decorView is not null &&
                    ViewCompat.GetRootWindowInsets(decorView)?.IsVisible(WindowInsetsCompat.Type.Ime()) == true;
                if (e.RepeatCount == 0 && !keyboardVisible &&
                    GetMainPage(this) is not null)
                {
                    // BlazorWebView handles Back on key-down, before the activity's
                    // normal Back callback. Capture both halves so it cannot navigate
                    // behind an open modal or act twice for one button press.
                    _capturedHardwareBack = true;
                    return true;
                }
            }
            else if (e.Action == KeyEventActions.Up && _capturedHardwareBack)
            {
                _capturedHardwareBack = false;
                if (!e.IsCanceled)
                {
                    var page = GetMainPage(this);
                    if (page?.HandleAndroidBack(this, navigateWebHistory: true) != true)
                    {
                        ContinueAcceptedBack();
                    }
                }
                return true;
            }
        }
        return base.DispatchKeyEvent(e);
    }

#if AURORA_ANDROID_DIAGNOSTICS
    private AndroidResponsivenessProbe? _probe;
#endif
    protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        Services.AndroidGoogleDriveAuthorization.OnActivityResult(requestCode, resultCode, data);
        Services.AndroidDocumentSaver.OnActivityResult(requestCode, resultCode, data);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Android 15 (SDK 35) forces edge-to-edge. Calling this explicitly tells the WebView
        // to report real system-bar heights via env(safe-area-inset-*) in CSS.
        WindowCompat.SetDecorFitsSystemWindows(Window!, false);
        // Android 16 no longer dispatches KEYCODE_BACK or Activity.OnBackPressed
        // for target API 36. Join the supported dispatcher for buttons and gestures.
        RegisterBackCallback();
#if AURORA_ANDROID_DIAGNOSTICS
        _probe = new AndroidResponsivenessProbe();
#endif
    }

#if AURORA_ANDROID_DIAGNOSTICS
    protected override void OnResume()
    {
        base.OnResume();
        _probe?.SetForeground(true);
    }

    protected override void OnPause()
    {
        _probe?.SetForeground(false);
        base.OnPause();
    }

#endif
    protected override void OnStop()
    {
        // Finish local checkpoints while Android still guarantees this callback time.
        // Do not rely on OnDestroy: background process reclamation may never call it.
        try
        {
            IPlatformApplication.Current?.Services.GetService<Services.CharacterDraftRecovery>()?.CheckpointOnBackground();
        }
        catch (Exception ex) { Services.DebugLogService.Instance.LogException(ex, "MainActivity.OnStop recovery"); }
        base.OnStop();
    }

    protected override void OnDestroy()
    {
        _backCallback?.Remove();
        _backCallback?.Dispose();
        _backCallback = null;
        Services.AndroidGoogleDriveAuthorization.OnActivityDestroyed(this);
        Services.AndroidDocumentSaver.OnActivityDestroyed(this);
#if AURORA_ANDROID_DIAGNOSTICS
        _probe?.Dispose();
#endif
        base.OnDestroy();
    }
}
