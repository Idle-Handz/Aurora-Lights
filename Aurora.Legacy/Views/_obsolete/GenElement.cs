// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views._obsolete.GenElement
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

#nullable disable
namespace Builder.Presentation.Views._obsolete;

public class GenElement
{
  public string Name { get; }

  public string Type { get; }

  public string Source { get; }

  public string ID { get; }

  public GenElement(string name, string type, string source, string id)
  {
    this.Name = name;
    this.Type = type;
    this.Source = source;
    this.ID = id;
  }

  public override string ToString() => this.Name;
}
