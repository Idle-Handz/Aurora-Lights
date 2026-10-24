// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.IsExpertiseAbilityConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models;
using System;
using System.Globalization;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Converters;

[Obsolete]
public class IsExpertiseAbilityConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return value is SkillItem skillItem && parameter is int proficiencyBonus ? (object) skillItem.IsExpertise(proficiencyBonus) : (object) false;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return Binding.DoNothing;
  }
}
