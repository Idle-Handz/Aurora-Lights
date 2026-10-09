using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

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
#pragma warning disable CS0618, CA1422
                        OnBackPressed();
#pragma warning restore CS0618, CA1422
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
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Android 15 (SDK 35) forces edge-to-edge. Calling this explicitly tells the WebView
        // to report real system-bar heights via env(safe-area-inset-*) in CSS.
        WindowCompat.SetDecorFitsSystemWindows(Window!, false);
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
    protected override void OnDestroy()
    {
        Services.AndroidGoogleDriveAuthorization.OnActivityDestroyed(this);
#if AURORA_ANDROID_DIAGNOSTICS
        _probe?.Dispose();
#endif
        base.OnDestroy();
    }
}
