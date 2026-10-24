// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.ExperienceProgressBarViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Extensions;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

#nullable disable
namespace Builder.Presentation.UserControls;

public class ExperienceProgressBarViewModel : 
  ViewModelBase,
  ISubscriber<CharacterManagerElementRegistered>,
  ISubscriber<CharacterManagerElementUnregistered>
{
  private int _startingExperience;
  private int _currentExperience;
  private int _targetExperience;
  private int _currentProgress;

  public ExperienceProgressBarViewModel()
  {
    this._startingExperience = 0;
    this._currentExperience = 0;
    this._targetExperience = 100;
    if (this.IsInDesignMode)
    {
      this._startingExperience = 100;
      this._targetExperience = 300;
      this._currentExperience = 125;
      this.CalculateProgress();
    }
    else
    {
      this.SubscribeWithEventAggregator();
      CharacterManager.Current.Character.PropertyChanged += new PropertyChangedEventHandler(this.Character_PropertyChanged);
    }
  }

  private void Character_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    if (!e.PropertyName.Equals("Experience"))
      return;
    this.Update();
  }

  public int StartingExperience
  {
    get => this._startingExperience;
    set
    {
      this.SetProperty<int>(ref this._startingExperience, value, nameof (StartingExperience));
      this.CalculateProgress();
    }
  }

  public int CurrentExperience
  {
    get => this._currentExperience;
    set
    {
      this.SetProperty<int>(ref this._currentExperience, value, nameof (CurrentExperience));
      this.CalculateProgress();
    }
  }

  public int TargetExperience
  {
    get => this._targetExperience;
    set
    {
      this.SetProperty<int>(ref this._targetExperience, value, nameof (TargetExperience));
      this.CalculateProgress();
    }
  }

  public int CurrentProgress
  {
    get => this._currentProgress;
    set => this.SetProperty<int>(ref this._currentProgress, value, nameof (CurrentProgress));
  }

  private void CalculateProgress()
  {
    this.CurrentProgress = (this.CurrentExperience - this.StartingExperience).IsPercetageOf(this.TargetExperience - this.StartingExperience);
  }

  private void Update()
  {
    try
    {
      List<LevelElement> list1 = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level"))).Cast<LevelElement>().ToList<LevelElement>();
      CharacterManager current = CharacterManager.Current;
      List<LevelElement> list2 = current.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level"))).Cast<LevelElement>().ToList<LevelElement>();
      if (list2.Count == 0)
      {
        this.TargetExperience = 100;
        this.CurrentExperience = 0;
        this.StartingExperience = 0;
      }
      else if (list2.Count == 20)
      {
        this.TargetExperience = list2.Last<LevelElement>().RequiredExperience;
        this.CurrentExperience = current.Character.Experience;
        this.StartingExperience = list2.Last<LevelElement>().RequiredExperience;
        this.CurrentProgress = 100;
      }
      else
      {
        this.TargetExperience = list1[list2.IndexOf(list2.Last<LevelElement>()) + 1].RequiredExperience;
        this.CurrentExperience = current.Character.Experience;
        this.StartingExperience = list2.Last<LevelElement>().RequiredExperience;
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (Update));
    }
  }

  public void OnHandleEvent(CharacterManagerElementRegistered args) => this.Update();

  public void OnHandleEvent(CharacterManagerElementUnregistered args) => this.Update();
}
