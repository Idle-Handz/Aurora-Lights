// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.AvailablePointsValueConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation.Converters;

public class AvailablePointsValueConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return System.Convert.ToInt32(value.ToString()) <= 0 ? (object) (Application.Current.Resources[(object) "DangerColorBrush"] as SolidColorBrush) : (object) (Application.Current.Resources[(object) "BlackBrush"] as SolidColorBrush);
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return Binding.DoNothing;
  }
}
