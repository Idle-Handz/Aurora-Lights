using Aurora.App.Services;
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
        blazorWebView.BlazorWebViewInitialized += (_, _) =>
            DebugLogService.Instance.Info("BlazorWebView initialized.");
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
}
