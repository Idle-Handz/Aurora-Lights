// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Extensions.UserControlExtensions
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.ViewModels.Base;
using System.Windows.Controls;

#nullable disable
namespace Builder.Presentation.Extensions;

public static class UserControlExtensions
{
  public static ViewModelBase GetViewModel(this UserControl control)
  {
    return (ViewModelBase) control?.DataContext;
  }

  public static TViewModel GetViewModel<TViewModel>(this UserControl control) where TViewModel : ViewModelBase
  {
    return (TViewModel) control?.DataContext;
  }
}
