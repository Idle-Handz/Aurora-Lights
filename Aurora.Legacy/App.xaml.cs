using System.IO;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.App
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Presentation.Telemetry;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;

#nullable disable
namespace Builder.Presentation;

public partial class App : Application
{
  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);
    if (Debugger.IsAttached)
    {
      Logger.RegisterLogger((ILogger) new DebugLogger(true)
      {
        SurpressDebugLogging = true,
        SurpressInfoLogging = true
      });
    }
    else
    {
      Microsoft.AppCenter.AppCenter.SetCountryCode(RegionInfo.CurrentRegion.TwoLetterISORegionName);
      Microsoft.AppCenter.AppCenter.Start("bf10d9a7-8fdc-4617-964f-d2e34b0b9632", typeof (Microsoft.AppCenter.Analytics.Analytics), typeof (Microsoft.AppCenter.Crashes.Crashes));
      this.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
    ApplicationManager current = ApplicationManager.Current;
    current.IsInDeveloperMode = ((IEnumerable<string>) e.Args).Contains<string>("dev") || ((IEnumerable<string>) e.Args).Contains<string>("developer");
    current.EnableDiagnostics = ((IEnumerable<string>) e.Args).Contains<string>("diagnostic") || ((IEnumerable<string>) e.Args).Contains<string>("diagnostics");
    if (((IEnumerable<string>) e.Args).Any<string>((Func<string, bool>) (x => x.EndsWith(".dnd5e"))))
    {
      current.LoadedCharacterFilePath = ((IEnumerable<string>) e.Args).FirstOrDefault<string>((Func<string, bool>) (x => x.EndsWith(".dnd5e")));
      Logger.Info("[character file] " + current.LoadedCharacterFilePath);
    }
    if (current.IsInDeveloperMode)
    {
      Logger.Info("[dev] enabled");
      AnalyticsEventHelper.ApplicationStartupEvent("dev");
    }
    if (current.EnableDiagnostics)
    {
      Logger.Info("[diagnostic] enabled");
      AnalyticsEventHelper.ApplicationStartupEvent("diagnostic");
    }
    current.ValidateConfiguration(true);
    current.UpgradeConfigurationCheck();
    current.LoadThemes();
    new Builder.Presentation.Views.Splash().Show();
  }
}

