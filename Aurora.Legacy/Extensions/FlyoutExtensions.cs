// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Extensions.FlyoutExtensions
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.ViewModels.Base;
using MahApps.Metro.Controls;

#nullable disable
namespace Builder.Presentation.Extensions;

public static class FlyoutExtensions
{
  public static ViewModelBase GetViewModel(this Flyout window)
  {
    return (ViewModelBase) window.DataContext;
  }

  public static TViewModel GetViewModel<TViewModel>(this Flyout window) where TViewModel : ViewModelBase
  {
    return (TViewModel) window.DataContext;
  }

  public static void Close(this Flyout flyout) => flyout.IsOpen = false;
}
