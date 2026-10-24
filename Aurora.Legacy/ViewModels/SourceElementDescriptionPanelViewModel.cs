// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.SourceElementDescriptionPanelViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Events.Application;
using System.Text;

#nullable disable
namespace Builder.Presentation.ViewModels;

public class SourceElementDescriptionPanelViewModel : 
  DescriptionPanelViewModelBase,
  ISubscriber<SourceElementDescriptionDisplayRequestEvent>
{
  public SourceElementDescriptionPanelViewModel()
  {
    this.IncludeSource = false;
    this.SupportedTypes.Add("Source");
  }

  public void OnHandleEvent(SourceElementDescriptionDisplayRequestEvent args)
  {
    args.IgnoreGeneratedDescription = true;
    this.HandleDisplayRequest((ElementDescriptionDisplayRequestEvent) args);
  }

  protected override string GenerateHeader(ElementBase element)
  {
    StringBuilder stringBuilder1 = new StringBuilder();
    StringBuilder stringBuilder2 = new StringBuilder();
    if (element is Source source1 && source1.HasInformationMessage)
    {
      stringBuilder1.Append($"<p class=\"underline\"><em class=\"info\">{source1.InformationMessage}</em>");
      if (source1.HasInformationUrl)
        stringBuilder1.Append($" - <a href=\"{source1.InformationUrl}\" style=\"color:#3aa6ae\">Learn More...</a>");
      stringBuilder1.Append("</p>");
    }
    if (element is Source source2)
    {
      if (source2.IsIncomplete)
      {
        if (!string.IsNullOrWhiteSpace(stringBuilder2.ToString()))
          stringBuilder2.Append(" - ");
        stringBuilder2.Append("<strong><em class=\"danger\">Incomplete Source</em></strong>");
      }
      if (source2.IsWorkInProgress)
      {
        if (!string.IsNullOrWhiteSpace(stringBuilder2.ToString()))
          stringBuilder2.Append(" - ");
        stringBuilder2.Append("<strong><em class=\"danger\">Work in Progress</em></strong>");
      }
      if (source2.IsPlaytestContent)
      {
        if (!string.IsNullOrWhiteSpace(stringBuilder2.ToString()))
          stringBuilder2.Append(" - ");
        stringBuilder2.Append("<strong><em class=\"warning\">Playtest Material</em></strong>");
      }
    }
    if (string.IsNullOrWhiteSpace(stringBuilder1.ToString()) && string.IsNullOrWhiteSpace(stringBuilder2.ToString()))
      return string.Empty;
    string header = "";
    if (!string.IsNullOrWhiteSpace(stringBuilder1.ToString()))
      header += stringBuilder1.ToString();
    if (!string.IsNullOrWhiteSpace(stringBuilder2.ToString()))
      header += $"<p class=\"underline\">{stringBuilder2}</p>";
    return header;
  }

  protected override void AppendBeforeSource(
    StringBuilder descriptionBuilder,
    ElementBase currentElement)
  {
    if (!(currentElement is Source source))
      return;
    StringBuilder stringBuilder1 = descriptionBuilder;
    string str1;
    if (!source.HasSourceUrl)
      str1 = $"<h6>SOURCE</h6><p><em>{source.Name}</em></p>";
    else
      str1 = $"<h6>SOURCE</h6><p><em><a href=\"{source.Url}\">{source.Name}</a></em></p>";
    stringBuilder1.Append(str1);
    if (source.HasErrataUrl)
      descriptionBuilder.Append($"<h6>ERRATA</h6><p><em><a href=\"{source.ErrataUrl}\">{source.Name} (Errata)</a></em></p>");
    StringBuilder stringBuilder2 = descriptionBuilder;
    string str2;
    if (!source.HasAuthorUrl)
      str2 = $"<h6>AUTHOR</h6><p><em>{source.Author}</em></p>";
    else
      str2 = $"<h6>AUTHOR</h6><p><em><a href=\"{source.AuthorUrl}\">{source.Author}</a></em></p>";
    stringBuilder2.Append(str2);
  }

  protected override void AppendAfterSource(
    StringBuilder descriptionBuilder,
    ElementBase currentElement)
  {
    if (!(currentElement is Source source) || !source.HasImageUrl)
      return;
    descriptionBuilder.Append($"<br><br><center style=\"height: 300px;max-height: 300px;max-width: 350px;\"><img src=\"{source.ImageUrl}\" style=\"height: 300px;max-height: 300px;max-width: 350px;\"></center><br><br>");
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
