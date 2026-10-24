// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.NotificationElementDescriptionPanelViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Presentation.Events.Application;

using System;
using System.Net.Http;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.ViewModels;

public class NotificationElementDescriptionPanelViewModel : 
  DescriptionPanelViewModelBase,
  ISubscriber<NotificationElementDescriptionDisplayRequestEvent>
{
  public NotificationElementDescriptionPanelViewModel()
  {
    this.IsSpeechEnabled = false;
    this.IsDarkStyle = true;
    if (this.IsInDesignMode)
      return;
    this.DownloadUpdateNotificationContent(Resources.NotificationUrl);
  }

  private async void DownloadUpdateNotificationContent(string url)
  {
    try
    {
      using (HttpClient client = new HttpClient())
      {
        base.OnHandleEvent(new HtmlDisplayRequestEvent(await client.GetStringAsync(url)));
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (DownloadUpdateNotificationContent));
    }
  }

  public void OnHandleEvent(
    NotificationElementDescriptionDisplayRequestEvent args)
  {
    this.HandleHtmlDisplayRequest((HtmlDisplayRequestEvent) args);
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
