// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.BonusStringValueConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using System;
using System.Globalization;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Converters;

public class BonusStringValueConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (value == null)
      Logger.Warning("BonusStringValueConverter tried to convert from null");
    int int32 = System.Convert.ToInt32(value);
    return int32 >= 0 ? (object) $"+{int32}" : value;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
