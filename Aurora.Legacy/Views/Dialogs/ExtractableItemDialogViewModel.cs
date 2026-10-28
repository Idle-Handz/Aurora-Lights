// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Dialogs.ExtractableItemDialogViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

#nullable disable
namespace Builder.Presentation.Views.Dialogs;

public class ExtractableItemDialogViewModel : ViewModelBase
{
  private Item _extractableItem;
  private string _title;
  private string _descriptions;

  public ExtractableItemDialogViewModel()
  {
    this.Extractables = new ObservableCollection<string>();
    if (!this.IsInDesignMode)
      return;
    this.InitializeDesignData();
  }

  public string Title
  {
    get => this._title;
    set => this.SetProperty<string>(ref this._title, value, nameof (Title));
  }

  public string Descriptions
  {
    get => this._descriptions;
    set => this.SetProperty<string>(ref this._descriptions, value, nameof (Descriptions));
  }

  public ObservableCollection<string> Extractables { get; }

  public override Task InitializeAsync(InitializationArguments args)
  {
    object[] objArray = args.Argument as object[];
    this._extractableItem = objArray[0] as Item;
    ElementBaseCollection source = objArray[1] as ElementBaseCollection;
    this.Title = this._extractableItem != null && this._extractableItem.IsExtractable ? this._extractableItem.Name : throw new ArgumentException("the element is not an extractable item");
    this.Descriptions = this._extractableItem.ExtractableDescription;
    foreach (KeyValuePair<string, int> extractable in this._extractableItem.Extractables)
    {
      KeyValuePair<string, int> items = extractable;
      ElementBase elementBase = source.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(items.Key)));
      if (elementBase != null)
        this.Extractables.Add($"{elementBase.Name} ({items.Value})");
    }
    return base.InitializeAsync(args);
  }

  protected override void InitializeDesignData()
  {
    this.Title = "Explorer's Pack";
    this.Descriptions = "desc";
    this.Extractables.Add("Backpack");
    this.Extractables.Add("Quiver");
    this.Extractables.Add("Rope (50 feet)");
  }
}
