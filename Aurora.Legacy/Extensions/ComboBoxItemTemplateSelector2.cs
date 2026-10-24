// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Extensions.ComboBoxItemTemplateSelector2
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System.Windows;
using System.Windows.Controls;

#nullable disable
namespace Builder.Presentation.Extensions;

public class ComboBoxItemTemplateSelector2 : DataTemplateSelector
{
  public DataTemplate SelectedTemplate { get; set; }

  public DataTemplate DropDownTemplate { get; set; }

  public override DataTemplate SelectTemplate(object item, DependencyObject container)
  {
    return container.GetVisualParent<ComboBoxItem>() == null ? this.SelectedTemplate : this.DropDownTemplate;
  }
}
