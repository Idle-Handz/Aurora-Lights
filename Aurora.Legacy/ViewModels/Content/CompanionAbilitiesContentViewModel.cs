// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Content.CompanionAbilitiesContentViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.Models.Collections;

#nullable disable
namespace Builder.Presentation.ViewModels.Content;

public class CompanionAbilitiesContentViewModel : AbilitiesContentViewModel
{
  public CompanionAbilitiesContentViewModel()
  {
    this.IsPointsGeneration = false;
    this.IsRandomizeGeneration = false;
  }

  public override AbilitiesCollection Abilities
  {
    get => CharacterManager.Current.Character.Companion.Abilities;
  }
}
