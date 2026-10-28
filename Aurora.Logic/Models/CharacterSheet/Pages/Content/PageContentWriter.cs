using Aurora.Documents.Helpers;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.Pages.Content.PageContentWriter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using iTextSharp.text.pdf;
using System.Text;

#nullable disable
namespace Builder.Presentation.Models.CharacterSheet.Pages.Content;

public class PageContentWriter : IPageContentWriter
{
  private readonly PdfStamper _stamper;

  public PageContentWriter(PdfStamper stamper) => this._stamper = stamper;

  public void Write(string key, string content) => this._stamper.AcroFields.SetFieldValue(key, content);

  public void Write<T>(T item) where T : IPageContentItem
  {
    if ((object) item is LineContent)
    {
      LineContent lineContent = (object) item as LineContent;
      if ((double) lineContent.Fontsize > 0.0)
        this.SetFontSize(lineContent.Key, lineContent.Fontsize);
      this._stamper.AcroFields.SetFieldValue(lineContent.Key, lineContent.Content);
    }
    if (!((object) item is AreaContent))
      return;
    AreaContent areaContent = (object) item as AreaContent;
    StringBuilder stringBuilder = new StringBuilder();
    foreach (string str in areaContent.Content)
      stringBuilder.AppendLine(str);
    this._stamper.AcroFields.SetFieldValue(areaContent.Key, stringBuilder.ToString());
  }

  public void SetFontSize(string key, float size)
  {
    this._stamper.AcroFields.SetFieldProperty(key, "textsize", (object) size, (int[]) null);
  }
}
