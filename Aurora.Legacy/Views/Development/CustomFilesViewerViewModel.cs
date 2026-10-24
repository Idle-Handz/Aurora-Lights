// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.CustomFilesViewerViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data.Files;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class CustomFilesViewerViewModel : ViewModelBase
{
  public CustomFilesViewerViewModel()
  {
    if (this.IsInDesignMode)
      return;
    foreach (IndexFile indexFile in ((IEnumerable<string>) Directory.GetFiles(DataManager.Current.UserDocumentsCustomElementsDirectory, "*.*", SearchOption.AllDirectories)).Select<string, IndexFile>((Func<string, IndexFile>) (x => new IndexFile(new FileInfo(x)))))
      this.IndexFiles.Add(indexFile);
  }

  public ObservableCollection<IndexFile> IndexFiles { get; set; } = new ObservableCollection<IndexFile>();
}
