// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Dialogs.EntryInitializationArguments
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.ViewModels.Base;

#nullable disable
namespace Builder.Presentation.Views.Dialogs;

public class EntryInitializationArguments : InitializationArguments
{
  public string Title { get; set; }

  public string Header { get; set; }

  public string InitialText { get; set; }

  public EntryInitializationArguments()
    : base()
  {
  }

  public EntryInitializationArguments(string title, string header, string initialText)
    : base()
  {
    this.Title = title;
    this.Header = header;
    this.InitialText = initialText;
  }
}
