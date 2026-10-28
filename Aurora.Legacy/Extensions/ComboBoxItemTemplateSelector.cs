// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Extensions.ComboBoxItemTemplateSelector
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System.Windows;
using System.Windows.Controls;

#nullable disable
namespace Builder.Presentation.Extensions;

public class ComboBoxItemTemplateSelector : DataTemplateSelector
{
  public DataTemplate SelectedItemTemplate { get; set; }

  public DataTemplate ItemTemplate { get; set; }

  public override DataTemplate SelectTemplate(object item, DependencyObject container)
  {
    bool flag = false;
    if (container is FrameworkElement frameworkElement)
    {
      DependencyObject templatedParent = frameworkElement.TemplatedParent;
      if (templatedParent != null && templatedParent is ComboBox)
        flag = true;
    }
    return !flag ? this.ItemTemplate : this.SelectedItemTemplate;
  }
}
