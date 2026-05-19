// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Syndication.SyndicationService
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Syndication.Posts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

#nullable disable
namespace Builder.Presentation.Syndication;

public class SyndicationService
{
  public SyndicationService(string syndicationUrl, string storageDirectory)
  {
    this.SyndicationUrl = syndicationUrl;
    if (Directory.Exists(storageDirectory))
    {
      this.StorageDirectory = storageDirectory;
    }
    else
    {
      Directory.CreateDirectory(storageDirectory);
      this.StorageDirectory = storageDirectory;
    }
    try
    {
      if (this.Load())
        return;
      this.Feed = new Feed();
    }
    catch (Exception)
    {
      if (System.IO.File.Exists(this.GetFilepath()))
        System.IO.File.Delete(this.GetFilepath());
      this.Feed = new Feed();
    }
  }

  public string SyndicationUrl { get; set; }

  public string StorageDirectory { get; set; }

  public Feed Feed { get; private set; }

  public event EventHandler Updating;

  public event EventHandler<SyndicationUpdateProgressEventArgs> UpdateProgress;

  public event EventHandler<SyndicationUpdateResultEventArgs> UpdateCompleted;

  public bool Update(bool forced = false, bool storeThumbnail = false)
  {
    this.OnUpdating();
    if (forced)
    {
      this.Feed.Updated = string.Empty;
      this.Feed.Collection.Clear();
    }
    List<Post> newPosts = new List<Post>();
    try
    {
      string syndicationUrl = this.SyndicationUrl;
      using (XmlReader reader = XmlReader.Create(syndicationUrl, new XmlReaderSettings()
      {
        Async = true
      }))
      {
        SyndicationFeed syndicationFeed = SyndicationFeed.Load(reader);
        int num1 = syndicationFeed.Items.Count<SyndicationItem>();
        int num2 = 0;
        this.Feed.Updated = syndicationFeed.LastUpdatedTime.ToString();
        SyndicationUpdateProgressEventArgs e = new SyndicationUpdateProgressEventArgs(0);
        foreach (SyndicationItem syndicationItem in syndicationFeed.Items.Reverse<SyndicationItem>())
        {
          Post post = this.ParseSyndicationItem(syndicationItem);
          if (storeThumbnail)
            this.StoreThumbnail(post);
          ++num2;
          e.ProgressPercentage = (int) ((double) num2 / (double) num1 * 100.0);
          if (!this.Feed.Collection.Any<Post>((Func<Post, bool>) (x => x.Identifier.Equals(post.Identifier))))
          {
            this.Feed.Collection.Insert(0, post);
            newPosts.Add(post);
          }
          this.OnUpdateProgress(e);
        }
      }
    }
    catch (Exception ex)
    {
      Trace.WriteLine(ex.Message);
    }
    this.OnUpdateCompleted(new SyndicationUpdateResultEventArgs((IEnumerable<Post>) newPosts));
    return true;
  }

  private Post ParseSyndicationItem(SyndicationItem syndicationItem)
  {
    if (syndicationItem == null)
      throw new ArgumentNullException(nameof (syndicationItem));
    Post syndicationItem1 = new Post();
    if (!string.IsNullOrWhiteSpace(syndicationItem.Id) && syndicationItem.Id.Contains("?p="))
    {
      syndicationItem1.Identifier = ((IEnumerable<string>) syndicationItem.Id.Split('?')).Last<string>().TrimStart('p', '=');
    }
    else
    {
      syndicationItem1.Identifier = Guid.NewGuid().ToString();
      Trace.WriteLine("post assigned with a guid: " + syndicationItem1.Identifier);
    }
    syndicationItem1.Title = WebUtility.HtmlDecode(syndicationItem.Title.Text);
    syndicationItem1.Content = WebUtility.HtmlDecode(syndicationItem.Summary.Text);
    syndicationItem1.Date = syndicationItem.PublishDate.ToString();
    syndicationItem1.Meta.Categories = string.Join(", ", syndicationItem.Categories.Select<SyndicationCategory, string>((Func<SyndicationCategory, string>) (x => x.Name)));
    syndicationItem1.Url = syndicationItem.Links.First<SyndicationLink>().Uri.AbsoluteUri;
    foreach (SyndicationElementExtension elementExtension in (Collection<SyndicationElementExtension>) syndicationItem.ElementExtensions)
    {
      XElement xelement = elementExtension.GetObject<XElement>();
      if (elementExtension.OuterName.Equals("post-tags") && !string.IsNullOrWhiteSpace(xelement.Value))
        syndicationItem1.Meta.Tags = xelement.Value;
      if (elementExtension.OuterName.Equals("post-thumbnail"))
        syndicationItem1.Image = xelement.Element((XName) "url")?.Value;
    }
    return syndicationItem1;
  }

  private void StoreThumbnail(Post post)
  {
    string extension = Path.GetExtension(post.Image);
    string fileName = Path.Combine(this.StorageDirectory, post.Identifier + extension);
#pragma warning disable SYSLIB0014
    using (WebClient webClient = new WebClient())
      webClient.DownloadFileAsync(new Uri(post.Image), fileName);
#pragma warning restore SYSLIB0014
  }

  public string GetFilepath() => Path.Combine(this.StorageDirectory, "posts.dat");

  public void Save()
  {
    string outputFileName = Path.Combine(this.StorageDirectory, "posts.dat");
    XmlWriterSettings settings = new XmlWriterSettings()
    {
      IndentChars = "\t",
      Indent = true,
      OmitXmlDeclaration = false,
      NamespaceHandling = NamespaceHandling.OmitDuplicates
    };
    using (XmlWriter xmlWriter = XmlWriter.Create(outputFileName, settings))
    {
      XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
      namespaces.Add(string.Empty, string.Empty);
      new XmlSerializer(typeof (Feed)).Serialize(xmlWriter, (object) this.Feed, namespaces);
    }
  }

  public bool Load()
  {
    string path = Path.Combine(this.StorageDirectory, "posts.dat");
    if (!System.IO.File.Exists(path))
      return false;
    using (FileStream fileStream = new FileStream(path, FileMode.Open))
      this.Feed = new XmlSerializer(typeof (Feed)).Deserialize((Stream) fileStream) as Feed;
    return this.Feed != null;
  }

  public void Reload()
  {
    if (System.IO.File.Exists(Path.Combine(this.StorageDirectory, "posts.dat")))
      return;
    this.Update(true);
    this.Save();
  }

  protected virtual void OnUpdating()
  {
    EventHandler updating = this.Updating;
    if (updating == null)
      return;
    updating((object) this, EventArgs.Empty);
  }

  protected virtual void OnUpdateCompleted(SyndicationUpdateResultEventArgs args)
  {
    EventHandler<SyndicationUpdateResultEventArgs> updateCompleted = this.UpdateCompleted;
    if (updateCompleted == null)
      return;
    updateCompleted((object) this, args);
  }

  protected virtual void OnUpdateProgress(SyndicationUpdateProgressEventArgs e)
  {
    EventHandler<SyndicationUpdateProgressEventArgs> updateProgress = this.UpdateProgress;
    if (updateProgress == null)
      return;
    updateProgress((object) this, e);
  }
}
