// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Commands.GenerateCharacterSheetPreviewCommand
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Services;
using Builder.Presentation.Views;
using System;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Commands;

public class GenerateCharacterSheetPreviewCommand : ICommand
{
  private IEventAggregator _eventAggregator;
  private readonly CharacterSheetGenerator _generator;

  public GenerateCharacterSheetPreviewCommand()
  {
    this._eventAggregator = ApplicationManager.Current.EventAggregator;
    this._generator = new CharacterSheetGenerator(CharacterManager.Current);
  }

  public string DestinationPath { get; set; }

  public bool CanExecute(object parameter) => true;

  public void Execute(object parameter)
  {
    this._eventAggregator.Send<CharacterSheetPreviewEvent>(new CharacterSheetPreviewEvent(this._generator.GenerateNewSheet(this.DestinationPath, true)));
  }

  public event EventHandler CanExecuteChanged;
}
