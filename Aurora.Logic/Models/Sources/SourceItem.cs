// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Sources.SourceItem
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services.Sources;
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Models.Sources;

public class SourceItem : ObservableObject
{
  private bool? _isChecked;
  private bool _allowUnchecking = true;

  public Source Source { get; }

  public bool AllowUnchecking
  {
    get => this._allowUnchecking && this.Parent?.AllowUnchecking != false
      && !RequiredContentPolicy.IsRequiredSource(this.Source.Name, this.Source.Id);
    set
    {
      this._allowUnchecking = value;
      if (!this.AllowUnchecking) this.SetIsChecked(true, false, true);
      this.OnPropertyChanged(nameof(AllowUnchecking));
    }
  }

  public List<ElementHeader> Elements { get; set; }

  public SourceItem(Source source)
  {
    this.Source = source;
    this.Elements = new List<ElementHeader>();
    if (!this.AllowUnchecking) this._isChecked = true;
  }

  public void SetParent(SourcesGroup parent)
  {
    this.Parent = parent;
    if (!this.AllowUnchecking) this.SetIsChecked(true, false, true);
    this.OnPropertyChanged("AllowUnchecking");
  }

  public bool? IsChecked
  {
    get => this._isChecked;
    set => this.SetIsChecked(value, true, true);
  }

  public SourcesGroup Parent { get; private set; }

  public void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
  {
    if (!this.AllowUnchecking) value = true;
    bool? nullable = value;
    bool? isChecked = this._isChecked;
    if (nullable.GetValueOrDefault() == isChecked.GetValueOrDefault() & nullable.HasValue == isChecked.HasValue)
      return;
    this._isChecked = value;
    if (updateChildren)
    {
      int num = this._isChecked.HasValue ? 1 : 0;
    }
    if (updateParent)
      this.Parent?.VerifyCheckState();
    this.OnPropertyChanged("IsChecked");
  }

  public bool HasElements => this.Elements != null && this.Elements.Any<ElementHeader>();

  public override string ToString() => this.Source.Name ?? "";
}
