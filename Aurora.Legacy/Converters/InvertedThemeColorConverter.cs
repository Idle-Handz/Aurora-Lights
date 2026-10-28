// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.InvertedThemeColorConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using MahApps.Metro;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation.Converters;

public class InvertedThemeColorConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return value is AppTheme appTheme ? (object) (appTheme.Resources[(object) "BlackBrush"] as SolidColorBrush) : (object) new SolidColorBrush(Colors.Transparent);
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
