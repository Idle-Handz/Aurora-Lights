// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ApplicationManager
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services;
using Builder.Presentation.Telemetry;
using Builder.Presentation.Views.Development;
using MahApps.Metro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation;

public sealed class ApplicationManager : IApplicationContext
{
  private DiagnosticsWindow _diagnosticsWindow;

  public static ApplicationManager Current { get; } = new ApplicationManager();

  private ApplicationManager()
  {
    Logger.Initializing((object) this);
    ThemeManager.IsThemeChanged += new EventHandler<OnThemeChangedEventArgs>(this.ThemeManagerIsThemeChanged);
    this.EventAggregator = (IEventAggregator) new Builder.Core.Events.EventAggregator();
    this.Settings = new ApplicationSettings(this.EventAggregator);
    // Register this instance as the cross-platform context so Aurora.Logic can resolve it.
    ApplicationContext.SetCurrent(this);
    // Wire up dialog service so Aurora.Logic code can show dialogs.
    MessageDialogContext.Current = new MessageDialogServiceAdapter();
    // Wire up platform launcher so Aurora.Logic can open files/folders/URLs without Process.Start.
    ExternalLauncherContext.Current = new ExternalLauncherService();
    // Force SelectionRuleExpanderHandler to initialise now so that
    // SelectionRuleExpanderContext.Current is non-null before any
    // SupportExpanderViewModel subclass is constructed during ShellWindow init.
    _ = Services.SelectionRuleExpanderHandler.Current;
  }

  public IEventAggregator EventAggregator { get; }

  /// <summary>WPF-specific settings wrapper with ICommand properties.</summary>
  public ApplicationSettings Settings { get; }

  // IApplicationContext explicit implementation — exposes the JSON store directly
  // so Aurora.Logic ViewModels receive AppSettingsStore without a WPF dep.
  AppSettingsStore IApplicationContext.Settings => this.Settings.Settings;

  public bool IsInDeveloperMode { get; set; }

  public bool EnableDiagnostics { get; set; }

  public string LoadedCharacterFilePath { get; set; }

  public bool HasCharacterFileRequest => !string.IsNullOrWhiteSpace(this.LoadedCharacterFilePath);

  public void SendStatusMessage(string statusMessage)
  {
    this.EventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent(statusMessage));
  }

  public void RestartApplication(bool killCurrentProcess = true)
  {
    string exe = Environment.ProcessPath
      ?? Application.ResourceAssembly.Location;
    Process.Start(exe);
    if (!killCurrentProcess)
      return;
    Process.GetCurrentProcess().Kill();
  }

  private void ThemeManagerIsThemeChanged(object sender, OnThemeChangedEventArgs e)
  {
    if (!Debugger.IsAttached)
      return;
    this.SendStatusMessage($"ThemeManagerIsThemeChanged {e.Accent.Name}/{e.AppTheme.Name}");
  }

  public IEnumerable<string> GetAccentNames()
  {
    return (IEnumerable<string>) new string[10]
    {
      "Default",
      "Black",
      "Brown",
      "Purple",
      "Green",
      "Aqua",
      "Blue",
      "Mauve",
      "Pink",
      "Red"
    };
  }

  public void SetDefaultAccent(bool saveSettings)
  {
    Accent accent = ThemeManager.GetAccent("Aurora Default");
    foreach (Window window in Application.Current.Windows)
      ThemeManager.ChangeAppStyle(window, accent, ThemeManager.DetectAppStyle(Application.Current.MainWindow).Item1);
    if (!saveSettings)
      return;
    this.Settings.Settings.Accent = "Aurora Default";
    this.Settings.Save();
  }

  public void SetLightTheme(bool saveSettings)
  {
    foreach (Window window in Application.Current.Windows)
      ThemeManager.ChangeAppTheme(window, "Aurora Light");
    this.Settings.Settings.Theme = "Aurora Light";
    this.Settings.Save(saveSettings);
  }

  public void SetDarkTheme(bool saveSettings)
  {
    foreach (Window window in Application.Current.Windows)
      ThemeManager.ChangeAppTheme(window, "Aurora Dark");
    this.Settings.Settings.Theme = "Aurora Dark";
    this.Settings.Save(saveSettings);
  }

  public void LoadThemes()
  {
    try
    {
      foreach (string accentName in this.GetAccentNames())
        ThemeManager.AddAccent("Aurora " + accentName, new Uri($"pack://application:,,,/Aurora.Presentation;component/Styles/Accents/{accentName}.xaml"));
      ThemeManager.AddAppTheme("Aurora Light", new Uri("pack://application:,,,/Aurora.Presentation;component/Styles/Theme/AuroraLight.xaml"));
      ThemeManager.AddAppTheme("Aurora Dark", new Uri("pack://application:,,,/Aurora.Presentation;component/Styles/Theme/AuroraDark.xaml"));
    }
    catch (Exception ex)
    {
      ex.Data[(object) "warning"] = (object) "Unable to load themes.";
      AnalyticsErrorHelper.Exception(ex, attachmentContent: "unable to load themes", method: nameof (LoadThemes), line: 131);
      MessageDialogService.ShowException(ex);
      Application.Current.Shutdown();
    }
  }

  public void SetAccent(string accentName)
  {
    try
    {
      Accent accent = ThemeManager.GetAccent(accentName);
      AppTheme newTheme = ThemeManager.DetectAppStyle(Application.Current.MainWindow).Item1;
      foreach (Window window in Application.Current.Windows)
        ThemeManager.ChangeAppStyle(window, accent, newTheme);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (SetAccent));
      MessageDialogService.ShowException(ex);
    }
  }

  public void SetTheme(string name, bool save)
  {
    try
    {
      if (name.Contains("Dark"))
      {
        this.SetDarkTheme(save);
      }
      else
      {
        if (!name.Contains("Light"))
          throw new ArgumentNullException(nameof (name));
        this.SetLightTheme(save);
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (SetTheme));
      MessageDialogService.ShowException(ex);
    }
  }

  public void SetWindowTheme(Window window)
  {
    try
    {
      string accent = ApplicationManager.Current.Settings.Settings.Accent;
      string theme = ApplicationManager.Current.Settings.Settings.Theme;
      Accent newAccent = ThemeManager.GetAccent(accent) ?? ThemeManager.GetAccent("Aurora Default");
      AppTheme newTheme = ThemeManager.GetAppTheme(theme) ?? ThemeManager.GetAppTheme("Aurora Light");
      ThemeManager.ChangeAppStyle(window, newAccent, newTheme);
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  private static void OpenDirectory(string? path)
  {
      if (string.IsNullOrWhiteSpace(path)) return;
      if (OperatingSystem.IsWindows())
          System.Diagnostics.Process.Start("explorer.exe", path);
      else if (OperatingSystem.IsMacOS())
          System.Diagnostics.Process.Start("open", path);
      else
          System.Diagnostics.Process.Start("xdg-open", path);
  }

  public void UpgradeConfigurationCheck()
  {
      AppSettingsStore store = this.Settings.Settings;
      if (!store.ConfigurationUpgradeRequired)
          return;
      AppSettingsStore.MigrateFromLegacyConfig(store);
  }

  public void ValidateConfiguration(bool openDirectory)
  {
    try
    {
      ApplicationManager.Current.Settings.Settings.Reload();
      string accent = ApplicationManager.Current.Settings.Settings.Accent;
    }
    catch (ConfigurationException ex)
    {
      AnalyticsErrorHelper.Exception((Exception) ex, method: nameof (ValidateConfiguration), line: 241);
      if (MessageBox.Show($"Aurora has detected that your user settings file has become corrupted. This may be due to a previous crash.{Environment.NewLine}{Environment.NewLine}Do you want to reset your user settings? Click no to try to open the folder containing your user settings to manually fix or remove it.", "Corrupted Configuration", MessageBoxButton.YesNo, MessageBoxImage.Hand) == MessageBoxResult.Yes)
      {
        if (ex.InnerException is ConfigurationException innerException3)
        {
          if (!System.IO.File.Exists(innerException3.Filename))
            return;
          System.IO.File.Delete(innerException3.Filename);
          this.RestartApplication();
        }
        else if (ex.InnerException is ConfigurationException innerException2)
        {
          if (!System.IO.File.Exists(innerException2.Filename))
            return;
          System.IO.File.Delete(innerException2.Filename);
          this.RestartApplication();
        }
        else if (ex is ConfigurationErrorsException configurationErrorsException)
        {
          if (!System.IO.File.Exists(configurationErrorsException.Filename))
            return;
          System.IO.File.Delete(configurationErrorsException.Filename);
          this.RestartApplication();
        }
        else if (ex.InnerException is ConfigurationErrorsException innerException1)
        {
          if (!System.IO.File.Exists(innerException1.Filename))
            return;
          System.IO.File.Delete(innerException1.Filename);
          this.RestartApplication();
        }
        else
        {
          int num = (int) MessageBox.Show($"Unable to detect user settings file.{Environment.NewLine}{Environment.NewLine}{ex.Message}", ex.ToString());
        }
      }
      else
      {
        if (openDirectory)
        {
          if (ex.InnerException is ConfigurationException innerException4)
          {
            FileInfo fileInfo = new FileInfo(innerException4.Filename);
            if (fileInfo.DirectoryName != null)
              OpenDirectory(fileInfo.DirectoryName);
          }
          if (ex != null)
          {
            FileInfo fileInfo = new FileInfo(ex.Filename);
            if (fileInfo.DirectoryName != null)
              OpenDirectory(fileInfo.DirectoryName);
          }
        }
        Process.GetCurrentProcess().Kill();
      }
    }
  }

  public void InitializeDiagnosticsWindow()
  {
    if (this._diagnosticsWindow != null)
      return;
    this._diagnosticsWindow = new DiagnosticsWindow();
  }

  public void ShowDiagnosticsWindow()
  {
    if (this._diagnosticsWindow == null)
      this._diagnosticsWindow = new DiagnosticsWindow();
    this._diagnosticsWindow.Show();
  }

  public ObservableCollection<string> CharacterGroups { get; } = new ObservableCollection<string>();

  public bool UpdateAvailable { get; set; }

  [Obsolete("legacy - not used")]
  public Color GetHighlightColor()
  {
    return (Color) ThemeManager.GetAccent(ApplicationManager.Current.Settings.Settings.Accent).Resources[(object) "HighlightColor"];
  }

  [Obsolete("legacy - not used")]
  public Color GetAccentColor()
  {
    return (Color) ThemeManager.GetAccent(ApplicationManager.Current.Settings.Settings.Accent).Resources[(object) "AccentColor"];
  }
}
