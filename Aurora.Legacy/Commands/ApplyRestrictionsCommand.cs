// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Commands.ApplyRestrictionsCommand
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Services.Sources;
using System;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Commands;

public class ApplyRestrictionsCommand : ICommand
{
  private readonly CharacterManager _manager;
  private readonly SourcesManager _sourcesManager;

  public ApplyRestrictionsCommand(CharacterManager manager)
  {
    this._manager = manager;
    this._sourcesManager = manager.SourcesManager;
  }

  public bool CanExecute(object parameter) => this._manager.Status.IsLoaded;

  public void Execute(object parameter) => this._sourcesManager.ApplyRestrictions(true);

  public event EventHandler CanExecuteChanged
  {
    add => CommandManager.RequerySuggested += value;
    remove => CommandManager.RequerySuggested -= value;
  }
}
