// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Commands.SourceApplicationCommands
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Commands;

internal class SourceApplicationCommands : ApplicationCommands
{
  public SourceApplicationCommands(IEventAggregator eventAggregator, CharacterManager manager)
    : base(eventAggregator, manager)
  {
    this.ManageRestrictionsCommand = (ICommand) new Builder.Presentation.Commands.ManageRestrictionsCommand(this.EventAggregator);
    this.SaveRestrictionsCommand = (ICommand) new SaveDefaultRestrictionsCommand(this.Manager);
    this.LoadRestrictionsCommand = (ICommand) new LoadDefaultRestrictionsCommand(this.Manager);
    this.ApplyRestrictionsCommand = (ICommand) new Builder.Presentation.Commands.ApplyRestrictionsCommand(this.Manager);
  }

  public ICommand ManageRestrictionsCommand { get; }

  public ICommand SaveRestrictionsCommand { get; }

  public ICommand LoadRestrictionsCommand { get; }

  public ICommand ApplyRestrictionsCommand { get; }
}
