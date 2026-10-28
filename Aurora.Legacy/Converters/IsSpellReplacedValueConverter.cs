// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.IsSpellReplacedValueConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Converters;

public class IsSpellReplacedValueConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (value == null)
      return (object) Visibility.Collapsed;
    return string.IsNullOrWhiteSpace(value.ToString()) || value.ToString().Equals("0") ? (object) Visibility.Collapsed : (object) Visibility.Visible;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
