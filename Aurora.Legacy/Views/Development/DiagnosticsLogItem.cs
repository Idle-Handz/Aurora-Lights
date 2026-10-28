// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.DiagnosticsLogItem
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class DiagnosticsLogItem
{
  public Log Log { get; set; }

  public string Content { get; set; }

  public DiagnosticsLogItem(Log log, string content)
  {
    this.Log = log;
    this.Content = content;
  }

  public bool IsDebug => this.Log == Log.Debug;

  public bool IsInfo => this.Log == Log.Info;

  public bool IsWarning => this.Log == Log.Warning;

  public bool IsException => this.Log == Log.Exception;
}
