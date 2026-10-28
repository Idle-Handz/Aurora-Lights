// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.CharacterLoadingSliderEventArgs
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public class CharacterLoadingSliderEventArgs(bool open) : SliderEventArgs(open)
{
  public string DisplayName { get; set; }

  public string DisplayBuild { get; set; }

  public string DisplayPortraitFilePath { get; set; }

  public string DisplayLevel { get; set; }
}
