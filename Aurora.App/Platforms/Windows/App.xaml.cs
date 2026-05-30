using Microsoft.UI.Xaml;
using Velopack;

namespace Aurora.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        // VelopackApp.Build().Run() MUST be the very first statement before any other app code.
        // If this launch was triggered by the Velopack bootstrapper (e.g. after downloading an
        // update), Run() handles the install/uninstall/restart sequence internally and calls
        // Environment.Exit() — meaning the code below never runs. On a normal launch it's a
        // fast no-op. Placing it anywhere later (after DI setup, after InitializeComponent)
        // would cause update installs to silently fail.
        VelopackApp.Build().Run();

        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
