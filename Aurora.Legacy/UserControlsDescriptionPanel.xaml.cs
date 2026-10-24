// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.DescriptionPanel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data.Extensions;
using Builder.Presentation.Extensions;
using Builder.Presentation.Services;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;
using TheArtOfDev.HtmlRenderer.WPF;

#nullable disable
namespace Builder.Presentation.UserControls;

public partial class DescriptionPanel : UserControl
{
  public DescriptionPanel()
  {
    this.InitializeComponent();
    SpeechService.Default.SpeechStopped += new EventHandler(this.Default_SpeechStopped);
    this.GetViewModel().PropertyChanged += new PropertyChangedEventHandler(this.ViewModelPropertyChanged);
  }

  private void Default_SpeechStopped(object sender, EventArgs e)
  {
    this.speak.Visibility = Visibility.Visible;
    this.cancel.Visibility = Visibility.Collapsed;
  }

  private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    if (!e.PropertyName.Equals("ElementDescription") || !(this.Parent is ScrollViewer parent))
      return;
    parent.ScrollToHome();
  }

  private void Speech(object sender, RoutedEventArgs e)
  {
    try
    {
      string selectedText;
      if (this.ContentPanel.SelectedText.Length > 0)
      {
        selectedText = this.ContentPanel.SelectedText;
      }
      else
      {
        string text = this.ContentPanel.Text;
        XmlElement element = new XmlDocument().CreateElement("x");
        element.InnerXml = text.Replace("</h1>", "</h1> " + Environment.NewLine).Replace("</h2>", "</h2> " + Environment.NewLine).Replace("</h3>", "</h3> " + Environment.NewLine).Replace("</h4>", "</h4> " + Environment.NewLine).Replace("</h5>", "</h5> " + Environment.NewLine).Replace("</h6>", "</h6> " + Environment.NewLine).Replace("</p>", "</p> " + Environment.NewLine);
        StringBuilder stringBuilder = new StringBuilder();
        foreach (string str in Regex.Split(element.GetInnerText(), Environment.NewLine))
        {
          if (!str.Trim().Equals("SOURCE"))
            stringBuilder.AppendLine(str.Trim());
          else
            break;
        }
        selectedText = stringBuilder.ToString();
      }
      SpeechService.Default.StartSpeech(selectedText.Replace("d10", "d 10").Replace("d12", "d 12").Replace("d20", "d 20").Replace("—", " , ").Replace(" gp ", " gold pieces ").Replace(" gp)", " gold pieces)").Replace(" gp.", " gold pieces."));
      this.speak.Visibility = Visibility.Collapsed;
      this.cancel.Visibility = Visibility.Visible;
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  private void Speech_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
  {
  }

  private void StopSpeech(object sender, RoutedEventArgs e)
  {
    SpeechService.Default.StopSpeech();
    this.speak.Visibility = Visibility.Visible;
    this.cancel.Visibility = Visibility.Collapsed;
  }




}
