// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Extensions.WindowExtensions
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.ViewModels.Base;
using System.Windows;

#nullable disable
namespace Builder.Presentation.Extensions;

public static class WindowExtensions
{
  public static ViewModelBase GetViewModel(this Window window)
  {
    return (ViewModelBase) window.DataContext;
  }

  public static TViewModel GetViewModel<TViewModel>(this Window window) where TViewModel : ViewModelBase
  {
    return (TViewModel) window.DataContext;
  }

  public static void ApplyTheme(this Window window)
  {
    ApplicationManager.Current.SetWindowTheme(window);
  }
}
