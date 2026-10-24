// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.NotificationSliderTopMarginConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public class NotificationSliderTopMarginConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    double num1 = System.Convert.ToDouble(value);
    int num2 = 70;
    int bottom = 75;
    double num3 = (double) (num2 + bottom);
    return (object) new Thickness(0.0, num1 - num3, 0.0, (double) bottom);
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
