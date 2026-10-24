// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Commands.ManageRestrictionsCommand
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Services;
using System;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Commands;

public class ManageRestrictionsCommand : ICommand
{
  private readonly IEventAggregator _eventAggregator;

  public ManageRestrictionsCommand(IEventAggregator eventAggregator)
  {
    this._eventAggregator = eventAggregator;
  }

  public bool CanExecute(object parameter) => true;

  public void Execute(object parameter)
  {
    this._eventAggregator.Send<SelectionRuleNavigationArgs>(new SelectionRuleNavigationArgs(NavigationLocation.StartSources));
  }

  public event EventHandler CanExecuteChanged
  {
    add => CommandManager.RequerySuggested += value;
    remove => CommandManager.RequerySuggested -= value;
  }
}
