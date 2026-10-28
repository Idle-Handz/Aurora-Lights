// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Controls.ElementDescriptionPanel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Presentation.Extensions;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Telemetry;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.WPF;

#nullable disable
namespace Builder.Presentation.Controls;

[TemplatePart(Name = "PART_HtmlPanel", Type = typeof(HtmlPanel))]
[TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
[TemplatePart(Name = "PART_SnapShotButton", Type = typeof(ScrollViewer))]
public class ElementDescriptionPanel : Control
{
    private ScrollViewer _scrollViewer;
    private HtmlPanel _panel;
    private Image _image;
    private Button _snapButton;
    public static readonly DependencyProperty ElementProperty = DependencyProperty.Register(nameof(Element), typeof(ElementBase), typeof(ElementDescriptionPanel), new PropertyMetadata((object)null));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(ElementDescriptionPanel), new PropertyMetadata((object)null));
    public static readonly DependencyProperty StyleSheetProperty = DependencyProperty.Register(nameof(StyleSheet), typeof(string), typeof(ElementDescriptionPanel), new PropertyMetadata((object)null));
    public static readonly DependencyProperty StartAudioCommandProperty = DependencyProperty.Register(nameof(StartAudioCommand), typeof(ICommand), typeof(ElementDescriptionPanel), new PropertyMetadata((object)null));
    public static readonly DependencyProperty StopAudioCommandProperty = DependencyProperty.Register(nameof(StopAudioCommand), typeof(ICommand), typeof(ElementDescriptionPanel), new PropertyMetadata((object)null));
    public static readonly DependencyProperty StartAudioVisibleProperty = DependencyProperty.Register(nameof(StartAudioVisible), typeof(Visibility), typeof(ElementDescriptionPanel), new PropertyMetadata((object)Visibility.Visible));
    public static readonly DependencyProperty StopAudioVisibleProperty = DependencyProperty.Register(nameof(StopAudioVisible), typeof(Visibility), typeof(ElementDescriptionPanel), new PropertyMetadata((object)Visibility.Visible));
    public static readonly DependencyProperty SnapShotCommandProperty = DependencyProperty.Register(nameof(SnapShotCommand), typeof(ICommand), typeof(ElementDescriptionPanel), new PropertyMetadata((object)null));
    public static readonly DependencyProperty SelectedDescriptionTextProperty = DependencyProperty.Register(nameof(SelectedDescriptionText), typeof(string), typeof(ElementDescriptionPanel), new PropertyMetadata((object)null));

    static ElementDescriptionPanel()
    {
        FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ElementDescriptionPanel), (PropertyMetadata)new FrameworkPropertyMetadata((object)typeof(ElementDescriptionPanel)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (this._snapButton != null)
            this._snapButton.Click -= new RoutedEventHandler(this._snapButton_Click);
        if (this._panel != null)
            this._panel.LinkClicked -= new TheArtOfDev.HtmlRenderer.WPF.RoutedEventHandler<HtmlLinkClickedEventArgs>(this._panel_LinkClicked);
        this._scrollViewer = this.Template.FindName("PART_ScrollViewer", (FrameworkElement)this) as ScrollViewer;
        this._panel = this.Template.FindName("PART_HtmlPanel", (FrameworkElement)this) as HtmlPanel;
        this._image = this.Template.FindName("PART_Image", (FrameworkElement)this) as Image;
        this._snapButton = this.Template.FindName("PART_SnapShotButton", (FrameworkElement)this) as Button;
        if (this._snapButton != null)
            this._snapButton.Click += new RoutedEventHandler(this._snapButton_Click);
        if (this._panel != null)
            this._panel.LinkClicked += new TheArtOfDev.HtmlRenderer.WPF.RoutedEventHandler<HtmlLinkClickedEventArgs>(this._panel_LinkClicked);
        if (this.StartAudioCommand == null)
            this.StartAudioCommand = (ICommand)new RelayCommand(new Action(this.StartSpeech));
        if (this.StopAudioCommand != null)
            return;
        this.StopAudioCommand = (ICommand)new RelayCommand(new Action(this.StopSpeech));
    }

    private void DisplayLinkedDescription(HtmlLinkClickedEventArgs args)
    {
        try
        {
            if (args == null || string.IsNullOrWhiteSpace(args.Link))
                return;
            string str = args.Attributes != null && args.Attributes.ContainsKey("href") ? args.Attributes["href"] : args.Link;
            string id = str.Trim('#').Trim();
            if (id.Contains("ID_"))
            {
                ElementBase element = DataManager.Current.ElementsCollection.GetElement(id);
                if (element != null)
                {
                    this.Element = element;
                    this.Description = element.HasGeneratedDescription ? element.GeneratedDescription : element.Description;
                    args.Handled = true;
                    return;
                }
            }
            if (Uri.TryCreate(args.Link, UriKind.Absolute, out Uri result))
            {
                Process.Start(new ProcessStartInfo(result.AbsoluteUri)
                {
                    UseShellExecute = true
                });
                args.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(DisplayLinkedDescription));
        }
    }

    private void _panel_LinkClicked(object sender, TheArtOfDev.HtmlRenderer.WPF.RoutedEventArgs<HtmlLinkClickedEventArgs> args)
    {
        if (args == null)
            return;
        this.DisplayLinkedDescription(args.Data);
        args.Handled = args.Data != null && args.Data.Handled;
    }

    private void _snapButton_Click(object sender, RoutedEventArgs e) => this.GenerateImage();

    [Category("Aurora")]
    public ElementBase Element
    {
        get => (ElementBase)this.GetValue(ElementDescriptionPanel.ElementProperty);
        set => this.SetValue(ElementDescriptionPanel.ElementProperty, (object)value);
    }

    [Category("Aurora")]
    public string Description
    {
        get => (string)this.GetValue(ElementDescriptionPanel.DescriptionProperty);
        set
        {
            this.SetValue(ElementDescriptionPanel.DescriptionProperty, (object)value);
            this._scrollViewer.ScrollToHome();
        }
    }

    [Category("Aurora")]
    public string StyleSheet
    {
        get => (string)this.GetValue(ElementDescriptionPanel.StyleSheetProperty);
        set => this.SetValue(ElementDescriptionPanel.StyleSheetProperty, (object)value);
    }

    [Category("Aurora")]
    public ICommand StartAudioCommand
    {
        get => (ICommand)this.GetValue(ElementDescriptionPanel.StartAudioCommandProperty);
        set => this.SetValue(ElementDescriptionPanel.StartAudioCommandProperty, (object)value);
    }

    [Category("Aurora")]
    public ICommand StopAudioCommand
    {
        get => (ICommand)this.GetValue(ElementDescriptionPanel.StopAudioCommandProperty);
        set => this.SetValue(ElementDescriptionPanel.StopAudioCommandProperty, (object)value);
    }

    [Category("Aurora")]
    public Visibility StartAudioVisible
    {
        get => (Visibility)this.GetValue(ElementDescriptionPanel.StartAudioVisibleProperty);
        set => this.SetValue(ElementDescriptionPanel.StartAudioVisibleProperty, (object)value);
    }

    [Category("Aurora")]
    public Visibility StopAudioVisible
    {
        get => (Visibility)this.GetValue(ElementDescriptionPanel.StopAudioVisibleProperty);
        set => this.SetValue(ElementDescriptionPanel.StopAudioVisibleProperty, (object)value);
    }

    [Category("Aurora")]
    public ICommand SnapShotCommand
    {
        get => (ICommand)this.GetValue(ElementDescriptionPanel.SnapShotCommandProperty);
        set => this.SetValue(ElementDescriptionPanel.SnapShotCommandProperty, (object)value);
    }

    [Category("Aurora")]
    public string SelectedDescriptionText
    {
        get => this._panel.SelectedText;
        set => this.SetValue(ElementDescriptionPanel.SelectedDescriptionTextProperty, (object)value);
    }

    private void StartSpeech()
    {
        try
        {
            if (this.SelectedDescriptionText.Length > 0)
            {
                SpeechService.Default.StartSpeech(this.SelectedDescriptionText);
            }
            else
            {
                string xml = this.Description.Replace("</h1>", "</h1>__ENTER__").Replace("</h2>", "</h2>__ENTER__").Replace("</h3>", "</h3>__ENTER__").Replace("</h4>", "</h4>__ENTER__").Replace("</h5>", "</h5>__ENTER__").Replace("</h6>", "</h6>__ENTER__").Replace("<br/>", "<br/>__ENTER__").Replace("</p>", "</p>__ENTER__").Replace("d10", "d 10").Replace("d12", "d 12").Replace("d20", "d 20").Replace("—", " , ").Replace(" cp ", " copper pieces ").Replace(" cp)", " copper pieces)").Replace(" cp.", " copper pieces.").Replace(" sp ", " silver pieces ").Replace(" sp)", " silver pieces)").Replace(" sp.", " silver pieces.").Replace(" ep ", " electrum pieces ").Replace(" ep)", " electrum pieces)").Replace(" ep.", " electrum pieces.").Replace(" gp ", " gold pieces ").Replace(" gp)", " gold pieces)").Replace(" gp.", " gold pieces.").Replace(" pp ", " platinum pieces ").Replace(" pp)", " platinum pieces)").Replace(" pp.", " platinum pieces.");
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(xml);
                SpeechService.Default.StartSpeech(xmlDocument.InnerText.Replace("__ENTER__", Environment.NewLine));
                if (this.Element == null)
                    return;
                AnalyticsEventHelper.DescriptionPanelReadAloud(this.Element.Name);
            }
        }
        catch (Exception ex)
        {
            MessageDialogService.ShowException(ex);
        }
    }

    private void StopSpeech() => SpeechService.Default.StopSpeech();

    public void GenerateImage()
    {
        try
        {
            string str = this.Element == null ? "snap" : this.Element.Name.ToSafeFilename();
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "Image (*.png)|*.png";
            saveFileDialog1.FileName = str;
            saveFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            saveFileDialog1.AddExtension = true;
            saveFileDialog1.DefaultExt = "png";
            saveFileDialog1.Title = "Save Snapshot";
            SaveFileDialog saveFileDialog2 = saveFileDialog1;
            bool? nullable = saveFileDialog2.ShowDialog();
            bool flag = true;
            if (!(nullable.GetValueOrDefault() == flag & nullable.HasValue))
                return;
            BitmapFrame image = HtmlRender.RenderToImage(this._panel.GetHtml(), (int)this._panel.ActualWidth);
            PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
            pngBitmapEncoder.Frames.Add(image);
            using (FileStream fileStream = new FileStream(saveFileDialog2.FileName, FileMode.Create))
                pngBitmapEncoder.Save((Stream)fileStream);
            Process.Start(saveFileDialog2.FileName);
            if (this.Element == null)
                return;
            AnalyticsEventHelper.DescriptionPanelSnap(this.Element.Name);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(GenerateImage));
            MessageDialogService.ShowException(ex);
        }
    }
}
