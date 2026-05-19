using Aurora.App.Services;
using Builder.Presentation;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Aurora.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // iTextSharp uses Windows legacy codepages (e.g. windows-1252) that .NET Core
        // doesn't register by default. This must be called before any PDF generation.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        // Wire up all Aurora.Logic static context seams before anything else.
        var appContext = new MauiApplicationContext();
        ApplicationContext.SetCurrent(appContext);

#if ANDROID
        // Default to external app-specific storage on Android so user data survives uninstalls.
        // /storage/emulated/0/Android/data/{package}/files — preserved across updates,
        // accessible via any file manager without root. Only applied on a fresh install
        // (empty setting); a configured path is always respected.
        if (string.IsNullOrWhiteSpace(appContext.Settings.DocumentsRootDirectory))
        {
            var externalBase = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
            if (externalBase != null)
            {
                var dir = Path.Combine(externalBase, "5e Character Builder");
                Directory.CreateDirectory(dir); // must exist before DataManager checks it
                appContext.Settings.DocumentsRootDirectory = dir;
            }
        }
#endif

        SelectionRuleExpanderContext.Current = new MauiSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current    = new MauiSpellcastingSectionHandler();
        MessageDialogContext.Current          = new MauiMessageDialogService();
        ExternalLauncherContext.Current       = new MauiExternalLauncher();
        CharacterSheetGeneratorContext.Current = new MauiCharacterSheetGenerator();

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        builder.Services.AddSingleton<MauiApplicationContext>(appContext);
        builder.Services.AddSingleton<AppIdentityService>();
        builder.Services.AddSingleton<CharacterService>();
        builder.Services.AddSingleton<CharacterTabService>();
        builder.Services.AddSingleton<UserPreferencesService>();
        builder.Services.AddSingleton<CompendiumService>();
        builder.Services.AddSingleton<ContentService>();
        builder.Services.AddSingleton<ContentDatabaseService>();
        builder.Services.AddSingleton<ContentDatabaseParityService>();
        builder.Services.AddSingleton<PdfImportService>();
        var debugLog = new DebugLogService();
        DebugLogService.Instance = debugLog;
        debugLog.InitializePersistentLog(FileSystem.Current.AppDataDirectory);
        debugLog.Info("MauiProgram.CreateMauiApp", $"Persistent log path: {debugLog.PersistentLogPath ?? "(disabled)"}");
        builder.Services.AddSingleton(debugLog);
        CharacterContext.ExceptionLogged += (ex, ctx) => debugLog.LogException(ex, ctx);

        // Capture truly unhandled exceptions (background threads, etc.) into the log.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            if (ex != null) debugLog.LogException(ex, "AppDomain.UnhandledException");
            else debugLog.Error($"AppDomain.UnhandledException: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            debugLog.LogException(e.Exception, "UnobservedTaskException");
            e.SetObserved();
        };

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
