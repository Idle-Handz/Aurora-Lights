// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Controls.PortraitSizeToScaleValueConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Globalization;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Controls;

public class PortraitSizeToScaleValueConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return value == null ? (object) 0.5 : (object) Math.Round(double.Parse(value.ToString()) / 4.0 / 100.0, 2);
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
