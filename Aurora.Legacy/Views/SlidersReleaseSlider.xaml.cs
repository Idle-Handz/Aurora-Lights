// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.ReleaseSlider
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Controls;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Models;
using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public partial class ReleaseSlider : 
  Flyout,
  ISubscriber<CharacterLoadingSliderStatusUpdateEvent>,
  ISubscriber<CharacterLoadingSliderProgressEvent>
{
  public ReleaseSlider()
  {
    this.InitializeComponent();
    this.Initialize();
    ApplicationManager.Current.EventAggregator.Subscribe((object) this);
    this.DataContextChanged += new DependencyPropertyChangedEventHandler(this.ReleaseSlider_DataContextChanged);
    this.IsOpenChanged += new RoutedEventHandler(this.ReleaseSlider_IsOpenChanged);
  }

  private async void ReleaseSlider_IsOpenChanged(object sender, RoutedEventArgs e)
  {
    ReleaseSlider releaseSlider = this;
    if (releaseSlider.IsOpen)
      return;
    await Task.Delay(1000);
    releaseSlider.StatusMessage.Foreground = (Brush) new SolidColorBrush(Color.FromRgb((byte) 29, (byte) 83, (byte) 56));
    releaseSlider.StatusMessage.Text = "INITIALIZING";
    releaseSlider.StatusMessage.FontSize = 13.0;
    releaseSlider.CircularProgressBar.Value = 0.0;
  }

  private void ReleaseSlider_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
  {
    this.StatusMessage.Foreground = (Brush) new SolidColorBrush(Color.FromRgb((byte) 29, (byte) 83, (byte) 56));
    this.StatusMessage.Text = "INITIALIZING";
    this.StatusMessage.FontSize = 13.0;
    this.CircularProgressBar.Value = 0.0;
  }

  public void Initialize(string name, string build, string portrait, string level)
  {
    this.DataContext = (object) new CharacterFile("")
    {
      DisplayName = name,
      DisplayLevel = level,
      DisplayPortraitFilePath = portrait
    };
  }

  public void Initialize()
  {
    this.DataContext = (object) new CharacterFile("")
    {
      DisplayName = "New Character",
      DisplayLevel = "1"
    };
  }

  public void OnHandleEvent(CharacterLoadingSliderStatusUpdateEvent args)
  {
    this.Dispatcher.Invoke((Action) (() =>
    {
      this.StatusMessage.FontSize = args.StatusMessage == "\uE10B" ? 36.0 : 13.0;
      if (args.StatusMessage.Length == 1 && args.StatusMessage != "\uE10B")
        this.StatusMessage.FontSize = 36.0;
      this.StatusMessage.Text = args.StatusMessage.ToUpper();
      this.StatusMessage.Foreground = args.Success ? (Brush) new SolidColorBrush(Color.FromRgb((byte) 29, (byte) 83, (byte) 56)) : (Brush) new SolidColorBrush(Color.FromRgb((byte) 117, (byte) 55, (byte) 55));
    }));
  }

  public void OnHandleEvent(CharacterLoadingSliderProgressEvent args)
  {
    this.CircularProgressBar.Value = (double) args.ProgressPercentage;
  }





}
