using System.IO;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.ContentFileContainer
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data.Files;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public class ContentFileContainer
{
  private readonly IndexFile _file;

  public ContentFileContainer(IndexFile file) => this._file = file;

  public string Name => this._file.Info.DisplayName;

  public string Description => this._file.Info.Description;
}
