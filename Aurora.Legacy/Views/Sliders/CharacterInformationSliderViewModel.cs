// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.CharacterInformationSliderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Presentation.Models;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.ViewModels.Shell;
using System;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public sealed class CharacterInformationSliderViewModel : ViewModelBase
{
  private bool _isCharacterContext;
  private bool _isCompanionContext;

  public ShellWindowViewModel MainViewModel { get; }

  public CharacterInformationSliderViewModel(ShellWindowViewModel mainViewModel)
  {
    this.MainViewModel = mainViewModel;
    mainViewModel.CharacterManager.Status.StatusChanged += new EventHandler<CharacterStatusChangedEventArgs>(this.Status_StatusChanged);
    this.IsCharacterContext = true;
    this.IsCompanionContext = false;
    this.SwitchContextCommand = (ICommand) new RelayCommand(new Action(this.SwitchContext));
  }

  private void Status_StatusChanged(object sender, CharacterStatusChangedEventArgs e)
  {
    if (e.Status.HasCompanion || !this.IsCompanionContext)
      return;
    this.SwitchContext();
  }

  public CharacterManager Manager => this.MainViewModel.CharacterManager;

  public Character Character => this.Manager.Character;

  public Companion Companion => this.Manager.Character.Companion;

  public ICommand SwitchContextCommand { get; }

  public bool IsCharacterContext
  {
    get => this._isCharacterContext;
    set => this.SetProperty<bool>(ref this._isCharacterContext, value, nameof (IsCharacterContext));
  }

  public bool IsCompanionContext
  {
    get => this._isCompanionContext;
    set => this.SetProperty<bool>(ref this._isCompanionContext, value, nameof (IsCompanionContext));
  }

  private void SwitchContext()
  {
    this.IsCharacterContext = !this.IsCharacterContext;
    this.IsCompanionContext = !this.IsCompanionContext;
  }
}
