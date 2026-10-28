// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.SymbolsGallerySlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public partial class SymbolsGallerySlider : Flyout
{
  public SymbolsGallerySlider()
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
    CharacterManager.Current.Character.OrganisationSymbol = this.GetViewModel<SymbolsGalleryViewModel>().SelectedPortrait;
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
      string galleryDirectory = DataManager.Current.UserDocumentsSymbolsGalleryDirectory;
      FileInfo fileInfo = new FileInfo(openFileDialog2.FileName);
      string path2 = CharacterManager.Current.Character.OrganisationName.ToLower().Trim() + fileInfo.Name.ToLower();
      string str = Path.Combine(galleryDirectory, path2);
      if (!File.Exists(str))
      {
        using (Bitmap bitmap = new Bitmap(System.Drawing.Image.FromFile(fileInfo.FullName, false)))
          bitmap.Save(str);
      }
      CharacterManager.Current.Character.OrganisationSymbol = str;
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
