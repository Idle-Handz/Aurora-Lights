// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.ExperienceProgressBar
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Extensions;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls;

public partial class ExperienceProgressBar : UserControl
{
  public static readonly DependencyProperty StartingExperienceProperty = DependencyProperty.Register(nameof (StartingExperience), typeof (int), typeof (ExperienceProgressBar), new PropertyMetadata((object) 0));
  public static readonly DependencyProperty CurrentExperienceProperty = DependencyProperty.Register(nameof (CurrentExperience), typeof (int), typeof (ExperienceProgressBar), new PropertyMetadata((object) 0));
  public static readonly DependencyProperty TargetExperienceProperty = DependencyProperty.Register(nameof (TargetExperience), typeof (int), typeof (ExperienceProgressBar), new PropertyMetadata((object) 0));
  public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof (Progress), typeof (int), typeof (ExperienceProgressBar), new PropertyMetadata((object) 0));
  public static readonly DependencyProperty TargetVisibilityProperty = DependencyProperty.Register(nameof (TargetVisibility), typeof (Visibility), typeof (ExperienceProgressBar), new PropertyMetadata((object) Visibility.Visible));
  public static readonly DependencyProperty CurrentVisibilityProperty = DependencyProperty.Register(nameof (CurrentVisibility), typeof (Visibility), typeof (ExperienceProgressBar), new PropertyMetadata((object) Visibility.Visible));
  public ExperienceProgressBar() => this.InitializeComponent();

  public int StartingExperience
  {
    get => (int) this.GetValue(ExperienceProgressBar.StartingExperienceProperty);
    set
    {
      this.SetValue(ExperienceProgressBar.StartingExperienceProperty, (object) value);
      this.CalculateProgress();
    }
  }

  public int CurrentExperience
  {
    get => (int) this.GetValue(ExperienceProgressBar.CurrentExperienceProperty);
    set
    {
      this.SetValue(ExperienceProgressBar.CurrentExperienceProperty, (object) value);
      this.CalculateProgress();
    }
  }

  public int TargetExperience
  {
    get => (int) this.GetValue(ExperienceProgressBar.TargetExperienceProperty);
    set
    {
      this.SetValue(ExperienceProgressBar.TargetExperienceProperty, (object) value);
      this.CalculateProgress();
    }
  }

  public int Progress
  {
    get => (int) this.GetValue(ExperienceProgressBar.ProgressProperty);
    set => this.SetValue(ExperienceProgressBar.ProgressProperty, (object) value);
  }

  private void CalculateProgress()
  {
    this.Progress = (this.CurrentExperience - this.StartingExperience).IsPercetageOf(this.TargetExperience - this.StartingExperience);
  }

  public Visibility TargetVisibility
  {
    get => (Visibility) this.GetValue(ExperienceProgressBar.TargetVisibilityProperty);
    set => this.SetValue(ExperienceProgressBar.TargetVisibilityProperty, (object) value);
  }

  public Visibility CurrentVisibility
  {
    get => (Visibility) this.GetValue(ExperienceProgressBar.CurrentVisibilityProperty);
    set => this.SetValue(ExperienceProgressBar.CurrentVisibilityProperty, (object) value);
  }




}
