// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.BuildElementDescriptionPanelViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

#nullable disable
namespace Builder.Presentation.ViewModels;

public class BuildElementDescriptionPanelViewModel : DescriptionPanelViewModelBase
{
  public override void OnHandleEvent(ElementDescriptionDisplayRequestEvent args)
  {
    this.CurrentElement = args.Element;
    if (this.CurrentElement == null)
      return;
    switch (args.Element.Type)
    {
      case "Item":
        break;
      case "Item Pack":
        break;
      case "Magic Item":
        break;
      case "Spell":
        break;
      default:
        base.OnHandleEvent(args);
        break;
    }
  }
}
