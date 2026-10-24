// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.InverterBooleanToVisibilityConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Converters;

public class InverterBooleanToVisibilityConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (value == null)
    {
      Logger.Warning("InverterBooleanToVisibilityConverter tried to convert from null, setting to visible [param: {0}]", parameter);
      return (object) Visibility.Visible;
    }
    return System.Convert.ToBoolean(value) ? (object) Visibility.Collapsed : (object) Visibility.Visible;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return Binding.DoNothing;
  }
}
