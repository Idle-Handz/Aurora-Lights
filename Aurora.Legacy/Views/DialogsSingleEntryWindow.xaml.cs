// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Dialogs.SingleEntryWindow
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using Builder.Presentation.ViewModels.Base;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Dialogs;

public partial class SingleEntryWindow : MetroWindow
{
  public SingleEntryWindow() => this.InitializeComponent();

  public async Task<string> Show(EntryInitializationArguments args)
  {
    SingleEntryWindow window = this;
    if (args == null)
      throw new ArgumentNullException(nameof (args));
    await window.GetViewModel().InitializeAsync((InitializationArguments) args);
    bool? nullable = window.ShowDialog();
    if (!nullable.HasValue || !nullable.Value)
      return (string) null;
    window.Close();
    return window.GetViewModel<SingleEntryWindowViewModel>().Text;
  }

  private void AcceptClick(object sender, RoutedEventArgs e) => this.DialogResult = new bool?(true);




}
