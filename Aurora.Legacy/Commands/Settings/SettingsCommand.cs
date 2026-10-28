// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Commands.Settings.SettingsCommand
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null

using System;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Commands.Settings;

internal abstract class SettingsCommand : ICommand
{
  protected AppSettingsStore Settings { get; }

  protected SettingsCommand() => this.Settings = ApplicationManager.Current.Settings.Settings;

  public abstract bool CanExecute(object parameter);

  public abstract void Execute(object parameter);

  public event EventHandler CanExecuteChanged
  {
    add => CommandManager.RequerySuggested += value;
    remove => CommandManager.RequerySuggested -= value;
  }
}
