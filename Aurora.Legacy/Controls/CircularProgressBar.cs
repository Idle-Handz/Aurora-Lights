// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Controls.CircularProgressBar
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

#nullable disable
namespace Builder.Presentation.Controls;

public class CircularProgressBar : ProgressBar
{
  public static readonly DependencyProperty AngleProperty = DependencyProperty.Register(nameof (Angle), typeof (double), typeof (CircularProgressBar), new PropertyMetadata((object) 0.0));
  public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(nameof (StrokeThickness), typeof (double), typeof (CircularProgressBar), new PropertyMetadata((object) 10.0));

  public CircularProgressBar()
  {
    this.ValueChanged += new RoutedPropertyChangedEventHandler<double>(this.CircularProgressBar_ValueChanged);
  }

  private void CircularProgressBar_ValueChanged(
    object sender,
    RoutedPropertyChangedEventArgs<double> e)
  {
    CircularProgressBar circularProgressBar = sender as CircularProgressBar;
    DoubleAnimation animation = new DoubleAnimation(circularProgressBar.Angle, e.NewValue / circularProgressBar.Maximum * 359.999, (Duration) TimeSpan.FromMilliseconds(500.0));
    circularProgressBar.BeginAnimation(CircularProgressBar.AngleProperty, (AnimationTimeline) animation, HandoffBehavior.SnapshotAndReplace);
  }

  public double Angle
  {
    get => (double) this.GetValue(CircularProgressBar.AngleProperty);
    set => this.SetValue(CircularProgressBar.AngleProperty, (object) value);
  }

  public double StrokeThickness
  {
    get => (double) this.GetValue(CircularProgressBar.StrokeThicknessProperty);
    set => this.SetValue(CircularProgressBar.StrokeThicknessProperty, (object) value);
  }
}
