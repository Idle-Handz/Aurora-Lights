// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.PortraitsGallerySlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data;
using Builder.Presentation.Extensions;
using Builder.Presentation.Services.Data;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public partial class PortraitsGallerySlider : Flyout
{
  public PortraitsGallerySlider()
  {
    this.InitializeComponent();
    this.IsOpenChanged += new RoutedEventHandler(this.SelectPortraitSliderIsOpenChanged);
  }

  private void SelectPortraitSliderIsOpenChanged(object sender, RoutedEventArgs e)
  {
    this.GetViewModel().InitializeAsync();
  }

  private void SelectPortraitClick(object sender, RoutedEventArgs e)
  {
    CharacterManager.Current.Character.PortraitFilename = this.GetViewModel<PortraitsGalleryViewModel>().SelectedPortrait;
    this.IsOpen = false;
  }

  private void BrowseClick(object sender, RoutedEventArgs e)
  {
    try
    {
      OpenFileDialog openFileDialog1 = new OpenFileDialog();
      openFileDialog1.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
      OpenFileDialog openFileDialog2 = openFileDialog1;
      bool? nullable = openFileDialog2.ShowDialog();
      if (!nullable.HasValue || !nullable.Value)
        return;
      string portraitsDirectory = DataManager.Current.UserDocumentsPortraitsDirectory;
      FileInfo fileInfo = new FileInfo(openFileDialog2.FileName);
      string str1 = "";
      ElementBaseCollection elements = CharacterManager.Current.GetElements();
      string lower1 = elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Race")))?.Name.ToLower();
      string lower2 = elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Sub Race")))?.Name.ToLower();
      if (lower1 != null)
        str1 = $"{str1}{lower1} ";
      if (lower2 != null)
        str1 = $"{str1}{lower2} ";
      string path2 = str1 + fileInfo.Name.ToLower();
      string str2 = Path.Combine(portraitsDirectory, path2);
      if (!File.Exists(str2))
      {
        using (Bitmap bitmap = new Bitmap(System.Drawing.Image.FromFile(fileInfo.FullName, false)))
          bitmap.Save(str2);
      }
      CharacterManager.Current.Character.PortraitFilename = str2;
      CharacterManager.Current.Status.IsUserPortrait = true;
      this.IsOpen = false;
    }
    catch (IOException ex)
    {
      Logger.Exception((Exception) ex, nameof (BrowseClick));
      int num = (int) MessageBox.Show(ex.Message, "IO Exception @ ChangePortrait");
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (BrowseClick));
      int num = (int) MessageBox.Show(ex.Message, "Exception @ ChangePortrait");
    }
  }

  private void CancelClick(object sender, RoutedEventArgs e) => this.IsOpen = false;





}
