// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.BooleanToSolidColorBrushConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation.Converters;

public class BooleanToSolidColorBrushConverter : IValueConverter
{
  public string TrueColor { get; set; }

  public string FalseColor { get; set; }

  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return value != null && (bool) value ? (object) new SolidColorBrush(Colors.DarkGreen) : (object) new SolidColorBrush(Colors.DarkRed);
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
