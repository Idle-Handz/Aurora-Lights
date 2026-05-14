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
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Android 15 (SDK 35) forces edge-to-edge. Calling this explicitly tells the WebView
        // to report real system-bar heights via env(safe-area-inset-*) in CSS.
        WindowCompat.SetDecorFitsSystemWindows(Window!, false);
    }
}
