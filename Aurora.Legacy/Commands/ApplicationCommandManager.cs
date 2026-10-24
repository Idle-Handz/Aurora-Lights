// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Commands.ApplicationCommandManager
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Commands;

internal class ApplicationCommandManager
{
  private readonly IEventAggregator _eventAggregator;
  private readonly CharacterManager _manager;

  public ApplicationCommandManager(IEventAggregator eventAggregator, CharacterManager manager)
  {
    this._eventAggregator = eventAggregator;
    this._manager = manager;
    this.SourceCommands = new SourceApplicationCommands(this._eventAggregator, this._manager);
  }

  public SourceApplicationCommands SourceCommands { get; set; }

  public void InvalidateRequerySuggested() => CommandManager.InvalidateRequerySuggested();
}
