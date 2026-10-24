// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.SourceListingNameReplaceConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models.Sources;
using System;
using System.Globalization;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Converters;

public class SourceListingNameReplaceConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (value == null)
      return (object) null;
    if (!(value is SourceItem sourceItem))
      return (object) value.ToString();
    if (sourceItem.Source.IsOfficialContent && sourceItem.Source.IsPlaytestContent && sourceItem.Source.HasReleaseDate)
      return (object) sourceItem.ToString().Replace("Unearthed Arcana:", "UA:").Trim();
    return sourceItem.Source.IsOfficialContent && sourceItem.Source.IsAdventureLeagueContent ? (object) sourceItem.ToString().Replace("Adventurers League:", "AL:").Trim() : (object) sourceItem.ToString();
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return Binding.DoNothing;
  }
}
