// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.CampaignSourceConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models.Sources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Converters;

public class CampaignSourceConverter : IMultiValueConverter
{
  public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
  {
    if (!(parameter is string str))
      str = "";
    char[] chArray = new char[1]{ ',' };
    List<string> list = ((IEnumerable<string>) str.Split(chArray)).Select<string, string>((Func<string, string>) (f => f.Trim())).ToList<string>();
    while (values.Length > list.Count)
      list.Add(string.Empty);
    List<object> objectList = new List<object>();
    for (int index = 0; index < values.Length; ++index)
    {
      if (!(values[index] is IEnumerable enumerable1))
        enumerable1 = (IEnumerable) new List<object>()
        {
          values[index]
        };
      IEnumerable enumerable2 = enumerable1;
      string name = list[index];
      if (name != string.Empty)
      {
        SourcesGroup sourcesGroup = new SourcesGroup(name);
        foreach (object obj in enumerable2)
          sourcesGroup.Sources.Add(obj as SourceItem);
        objectList.Add((object) sourcesGroup);
      }
      else
      {
        foreach (object obj in enumerable2)
          objectList.Add(obj);
      }
    }
    return (object) objectList;
  }

  public object[] ConvertBack(
    object value,
    Type[] targetTypes,
    object parameter,
    CultureInfo culture)
  {
    throw new NotSupportedException("Cannot perform reverse-conversion");
  }
}
