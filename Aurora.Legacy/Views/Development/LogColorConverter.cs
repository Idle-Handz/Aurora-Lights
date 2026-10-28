// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.LogColorConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class LogColorConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    switch ((Log) value)
    {
      case Log.Debug:
        return (object) new SolidColorBrush(Colors.Gray);
      case Log.Info:
        return (object) new SolidColorBrush(Colors.SteelBlue);
      case Log.Warning:
        return (object) new SolidColorBrush(Colors.Orange);
      case Log.Exception:
        return (object) new SolidColorBrush(Colors.DarkRed);
      default:
        return (object) new SolidColorBrush(Colors.Black);
    }
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotImplementedException();
  }
}
