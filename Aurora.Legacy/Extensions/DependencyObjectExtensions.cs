// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Extensions.DependencyObjectExtensions
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System.Windows;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation.Extensions;

public static class DependencyObjectExtensions
{
  public static T GetVisualParent<T>(this DependencyObject child) where T : Visual
  {
    while (true)
    {
      switch (child)
      {
        case null:
        case T _:
          goto label_3;
        default:
          child = VisualTreeHelper.GetParent(child);
          continue;
      }
    }
label_3:
    return child as T;
  }
}
