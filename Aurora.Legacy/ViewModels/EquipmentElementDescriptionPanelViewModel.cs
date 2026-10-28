// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.EquipmentElementDescriptionPanelViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Data;
using Builder.Presentation.Events.Application;
using System.Text;

#nullable disable
namespace Builder.Presentation.ViewModels;

public class EquipmentElementDescriptionPanelViewModel : 
  DescriptionPanelViewModelBase,
  ISubscriber<EquipmentElementDescriptionDisplayRequestEvent>
{
  private ElementBase _parent;

  public EquipmentElementDescriptionPanelViewModel()
  {
    this.SupportedTypes.Add("Item");
    this.SupportedTypes.Add("Item Pack");
    this.SupportedTypes.Add("Magic Item");
    this.SupportedTypes.Add("Armor");
    this.SupportedTypes.Add("Weapon");
  }

  public void OnHandleEvent(
    EquipmentElementDescriptionDisplayRequestEvent args)
  {
    this._parent = args.Parent;
    this.HandleDisplayRequest((ElementDescriptionDisplayRequestEvent) args);
  }

  protected override void AppendBeforeSource(
    StringBuilder descriptionBuilder,
    ElementBase currentElement)
  {
    string.IsNullOrWhiteSpace(this._parent?.Description);
  }

  public override void OnHandleEvent(ElementDescriptionDisplayRequestEvent args)
  {
  }

  public override void OnHandleEvent(HtmlDisplayRequestEvent args)
  {
  }

  public override void OnHandleEvent(ResourceDocumentDisplayRequestEvent args)
  {
  }
}
