// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Converters.InventoryEquipmentItemUnderlineVisibilityConverter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.ViewModels.Shell.Items;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

#nullable disable
namespace Builder.Presentation.Converters;

public class InventoryEquipmentItemUnderlineVisibilityConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if (!(value is RefactoredEquipmentItem refactoredEquipmentItem) || refactoredEquipmentItem.Item.HideFromInventory || refactoredEquipmentItem.IsAdorned && refactoredEquipmentItem.AdornerItem.HideFromInventory)
      return (object) Visibility.Collapsed;
    return refactoredEquipmentItem.IsActivated && !string.IsNullOrWhiteSpace(refactoredEquipmentItem.EquippedLocation) ? (object) Visibility.Visible : (object) Visibility.Collapsed;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return Binding.DoNothing;
  }
}
