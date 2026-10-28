// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.PortraitImageSourceConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Presentation.Services.Data;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

#nullable disable
namespace Builder.Presentation.Converters;

public class PortraitImageSourceConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (value != null)
    {
      try
      {
        BitmapImage bitmapImage = new BitmapImage();
        string str = value.ToString();
        if (!File.Exists(str))
          str = Path.Combine(DataManager.Current.UserDocumentsPortraitsDirectory, Path.GetFileName(str));
        if (File.Exists(str))
        {
          bitmapImage.BeginInit();
          bitmapImage.UriSource = new Uri(str, UriKind.RelativeOrAbsolute);
          bitmapImage.EndInit();
        }
        return (object) bitmapImage;
      }
      catch (Exception ex)
      {
        Logger.Exception(ex, nameof (Convert));
      }
    }
    return (object) null;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return Binding.DoNothing;
  }
}
