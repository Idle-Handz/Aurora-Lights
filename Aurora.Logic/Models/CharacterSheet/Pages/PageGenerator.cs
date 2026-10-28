// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.Pages.PageGenerator
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;

#nullable disable
namespace Builder.Presentation.Models.CharacterSheet.Pages;

public abstract class PageGenerator : IDisposable
{
  protected PageGenerator(int pageFollowNumber = 1)
  {
    this.PageFollowNumber = pageFollowNumber;
    this.PageWidth = 612;
    this.PageHeight = 792;
    this.PageMargin = 26;
    this.PageGutter = 9;
    this.Flatten = false;
    this.PartialFlatteningNames = new List<string>();
    this.ResourceReaders = new Dictionary<string, PdfReader>();
    this.Stream = new MemoryStream();
    this.Document = new Document();
    this.Document.SetPageSize(new Rectangle(0.0f, 0.0f, (float) this.PageWidth, (float) this.PageHeight));
    this.Writer = PdfWriter.GetInstance(this.Document, (System.IO.Stream) this.Stream);
    this.Document.Open();
    this.Document.NewPage();
  }

  protected MemoryStream Stream { get; }

  protected Document Document { get; }

  protected PdfWriter Writer { get; }

  protected Dictionary<string, PdfReader> ResourceReaders { get; }

  public int PageFollowNumber { get; set; }

  public int PageWidth { get; protected set; }

  public int PageHeight { get; protected set; }

  public int PageMargin { get; protected set; }

  public int PageGutter { get; protected set; }

  public bool Flatten { get; set; }

  public List<string> PartialFlatteningNames { get; }

  public MemoryStream GetStream()
  {
    this.Document.Close();
    this.Stream.Flush();
    return this.Stream;
  }

  public PdfReader AsReader() => new PdfReader(this.GetStream().ToArray());

  public void Save(string path, bool open = false)
  {
    this.Document.Close();
    this.Stream.Flush();
    PdfReader reader = new PdfReader(this.Stream.ToArray());
    MemoryStream os = new MemoryStream();
    PdfStamper pdfStamper = new PdfStamper(reader, (System.IO.Stream) os);
    if (this.Flatten)
    {
      pdfStamper.FormFlattening = true;
      foreach (string partialFlatteningName in this.PartialFlatteningNames)
        pdfStamper.PartialFormFlattening(partialFlatteningName);
    }
    pdfStamper.Close();
    reader.Close();
    File.WriteAllBytes(path, os.ToArray());
    os.Close();
    os.Dispose();
    if (open)
      ExternalLauncherContext.Current?.OpenPath(path);
  }

  protected void PlacePage(PdfReader reader, Rectangle area)
  {
    this.Writer.DirectContentUnder.AddTemplate((PdfTemplate) this.Writer.GetImportedPage(reader, 1), area.Left, area.Bottom);
  }

  public virtual void StartNewPage()
  {
    this.Document.NewPage();
    ++this.PageFollowNumber;
  }

  protected PdfReader GetResourceReader(string resourcePath)
  {
    if (!this.ResourceReaders.ContainsKey(resourcePath))
    {
      CharacterSheetResourcePage sheetResourcePage = new CharacterSheetResourcePage(resourcePath);
      this.ResourceReaders.Add(resourcePath, sheetResourcePage.CreateReader());
    }
    return this.ResourceReaders[resourcePath];
  }

  public virtual void Dispose()
  {
    // The document/writer may still need the stream when finalizing a page.
    this.Document?.Dispose();
    this.Writer?.Dispose();
    this.Stream?.Dispose();
    foreach (KeyValuePair<string, PdfReader> resourceReader in this.ResourceReaders)
      resourceReader.Value?.Dispose();
  }
}
