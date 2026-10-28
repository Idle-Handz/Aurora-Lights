// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Controls.AngleToPointConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Controls;

internal class AngleToPointConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    double num1 = (double) value;
    double num2 = 50.0;
    double num3 = num1 * Math.PI / 180.0;
    return (object) new Point(Math.Sin(num3) * num2 + num2, -Math.Cos(num3) * num2 + num2);
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
