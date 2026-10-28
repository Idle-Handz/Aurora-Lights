// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.CompendiumElementDescriptionPanelViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Events.Application;

#nullable disable
namespace Builder.Presentation.ViewModels;

public class CompendiumElementDescriptionPanelViewModel : 
  DescriptionPanelViewModelBase,
  ISubscriber<CompendiumElementDescriptionDisplayRequestEvent>
{
  public void OnHandleEvent(
    CompendiumElementDescriptionDisplayRequestEvent args)
  {
    this.HandleDisplayRequest((ElementDescriptionDisplayRequestEvent) args);
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
