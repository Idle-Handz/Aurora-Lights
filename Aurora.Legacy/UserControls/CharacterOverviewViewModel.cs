// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.CharacterOverviewViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models;
using Builder.Presentation.Models.Collections;
using Builder.Presentation.ViewModels.Base;

#nullable disable
namespace Builder.Presentation.UserControls;

public class CharacterOverviewViewModel : ViewModelBase
{
  public CharacterOverviewViewModel() => this.SubscribeWithEventAggregator();

  public Character Character => CharacterManager.Current.Character;

  public SkillsCollection Skills => CharacterManager.Current.Character.Skills;
}
