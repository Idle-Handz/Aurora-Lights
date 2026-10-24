// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Commands.Settings.ActivateDarkThemeCommand
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Events.Application;
using MahApps.Metro;
using System.Windows;

#nullable disable
namespace Builder.Presentation.Commands.Settings;

internal class ActivateDarkThemeCommand : SettingsCommand
{
  public override bool CanExecute(object parameter) => true;

  public override void Execute(object parameter)
  {
    this.Settings.Theme = "Aurora Dark";
    this.Settings.Save();
    foreach (Window window in System.Windows.Application.Current.Windows)
      ThemeManager.ChangeAppTheme(window, this.Settings.Theme);
    ApplicationManager.Current.EventAggregator.Send<SettingsChangedEvent>(new SettingsChangedEvent());
  }
}
