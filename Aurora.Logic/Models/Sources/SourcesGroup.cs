// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Sources.SourcesGroup
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Models.Sources;

public class SourcesGroup : ObservableObject
{
  private bool? _isChecked;

  public SourcesGroup(string name, bool allowUnchecking = true)
  {
    this.Name = name;
    this.AllowUnchecking = allowUnchecking;
    this.Sources = new ObservableCollection<SourceItem>();
    if (!allowUnchecking) this._isChecked = true;
  }

  public string Name { get; set; }

  public bool AllowUnchecking { get; }

  public ObservableCollection<SourceItem> Sources { get; }

  public bool? IsChecked
  {
    get => this._isChecked;
    set => this.SetIsChecked(value, true);
  }

  public void SetIsChecked(bool? value, bool updateChildren)
  {
    if (!this.AllowUnchecking) value = true;
    if (updateChildren && value.HasValue)
    {
      foreach (SourceItem source in this.Sources)
        source.SetIsChecked(value, true, false);

      // A mixed group still includes its required children after a bulk disable.
      if (this.AllowUnchecking && this.Sources.Count > 0)
        value = this.Sources.All(source => source.IsChecked == this.Sources[0].IsChecked)
          ? this.Sources[0].IsChecked : null;
    }
    this._isChecked = value;
    this.OnPropertyChanged("IsChecked", "Underline");
  }

  public void VerifyCheckState()
  {
    bool? nullable1 = new bool?();
    for (int index = 0; index < this.Sources.Count; ++index)
    {
      bool? isChecked = this.Sources[index].IsChecked;
      if (index == 0)
      {
        nullable1 = isChecked;
      }
      else
      {
        bool? nullable2 = nullable1;
        bool? nullable3 = isChecked;
        if (!(nullable2.GetValueOrDefault() == nullable3.GetValueOrDefault() & nullable2.HasValue == nullable3.HasValue))
        {
          nullable1 = new bool?();
          break;
        }
      }
    }
    this.SetIsChecked(nullable1, false);
    this.OnPropertyChanged("Underline");
  }

  public override string ToString() => $"{this.Name} ({this.Sources.Count})";

  public string Underline
  {
    get
    {
      int num = this.Sources.Count<SourceItem>((Func<SourceItem, bool>) (x =>
      {
        bool? isChecked = x.IsChecked;
        bool flag = true;
        return isChecked.GetValueOrDefault() == flag & isChecked.HasValue;
      }));
      int count = this.Sources.Count;
      if (num == 0)
        return $"All {count} Sources Excluded";
      return num == count ? $"All {count} Sources Included" : $"{num}/{count} Sources Included";
    }
  }
}
