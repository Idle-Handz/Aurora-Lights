using Android.App;
using Android.Content.PM;
using Android.OS;
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
#if AURORA_ANDROID_DIAGNOSTICS
    private AndroidResponsivenessProbe? _probe;
#endif
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

    protected override void OnDestroy()
    {
        _probe?.Dispose();
        base.OnDestroy();
    }
#endif
}
