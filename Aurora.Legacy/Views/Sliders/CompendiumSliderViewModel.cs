// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.CompendiumSliderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.ViewModels.Base;
using System.Threading.Tasks;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public sealed class CompendiumSliderViewModel : ViewModelBase
{
  public CompendiumSliderViewModel()
  {
    if (!this.IsInDesignMode)
      return;
    this.InitializeDesignData();
  }

  public CharacterManager Manager => CharacterManager.Current;

  public override Task InitializeAsync(InitializationArguments args) => base.InitializeAsync(args);
}
