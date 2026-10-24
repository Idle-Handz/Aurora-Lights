// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.ElementProficiencyBrushValueConvereter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Data.Elements;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation.Converters;

public class ElementProficiencyBrushValueConvereter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    ElementSetters.Setter setter;
    if (!(value is Item obj) || !obj.AttemptGetSetterValue("proficiency", out setter))
      return (object) (Application.Current.Resources[(object) "SuccessColorBrush"] as SolidColorBrush);
    return CharacterManager.Current.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Proficiency"))).Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id)).Contains<string>(setter.Value) ? (object) (Application.Current.Resources[(object) "SuccessColorBrush"] as SolidColorBrush) : (object) (Application.Current.Resources[(object) "DangerColorBrush"] as SolidColorBrush);
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
