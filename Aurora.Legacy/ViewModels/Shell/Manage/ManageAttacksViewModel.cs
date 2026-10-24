// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Shell.Manage.ManageAttacksViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Models.Helpers;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.Views.Content.Manage;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.ViewModels.Shell.Manage;

public sealed class ManageAttacksViewModel : 
  ViewModelBase,
  ISubscriber<CharacterManagerElementsUpdated>,
  ISubscriber<ReprocessCharacterEvent>
{
  private AttackSectionItem _selectedAttackItem;

  public ManageAttacksViewModel()
  {
    if (this.IsInDesignMode)
    {
      this.InitializeDesignData();
    }
    else
    {
      this.MoveAttackUpCommand = new RelayCommand<AttackSectionItem>(new Action<AttackSectionItem>(this.MoveAttackUp), new Func<AttackSectionItem, bool>(this.CanMoveAttackUp));
      this.MoveAttackDownCommand = new RelayCommand<AttackSectionItem>(new Action<AttackSectionItem>(this.MoveAttackDown), new Func<AttackSectionItem, bool>(this.CanMoveAttackDown));
      this.SubscribeWithEventAggregator();
    }
  }

  public CharacterManager Manager => CharacterManager.Current;

  public AttacksSection Attacks => this.Manager.Character.AttacksSection;

  public AttackSectionItem SelectedAttackItem
  {
    get => this._selectedAttackItem;
    set
    {
      this.SetProperty<AttackSectionItem>(ref this._selectedAttackItem, value, nameof (SelectedAttackItem));
      this.MoveAttackUpCommand.RaiseCanExecuteChanged();
      this.MoveAttackDownCommand.RaiseCanExecuteChanged();
    }
  }

  public ICommand AddAttackCommand => (ICommand) new RelayCommand(new Action(this.AddAttack));

  public ICommand EditAttackCommand
  {
    get
    {
      return (ICommand) new RelayCommand<AttackSectionItem>(new Action<AttackSectionItem>(this.EditAttack));
    }
  }

  public ICommand RemoveAttackCommand
  {
    get
    {
      return (ICommand) new RelayCommand<AttackSectionItem>(new Action<AttackSectionItem>(this.RemoveAttack));
    }
  }

  public ICommand ToggleDisplayAttackCommand
  {
    get
    {
      return (ICommand) new RelayCommand<AttackSectionItem>(new Action<AttackSectionItem>(this.ToggleDisplayAttack));
    }
  }

  public RelayCommand<AttackSectionItem> MoveAttackUpCommand { get; }

  public RelayCommand<AttackSectionItem> MoveAttackDownCommand { get; }

  private void MoveAttackUp(AttackSectionItem parameter)
  {
    if (parameter == null || parameter.Equals((object) this.Attacks.Items.FirstOrDefault<AttackSectionItem>()))
      return;
    int oldIndex = this.Attacks.Items.IndexOf(parameter);
    this.Attacks.Items.Move(oldIndex, oldIndex - 1);
    this.MoveAttackUpCommand.RaiseCanExecuteChanged();
    this.MoveAttackDownCommand.RaiseCanExecuteChanged();
  }

  private void MoveAttackDown(AttackSectionItem parameter)
  {
    if (parameter == null || parameter.Equals((object) this.Attacks.Items.LastOrDefault<AttackSectionItem>()))
      return;
    int oldIndex = this.Attacks.Items.IndexOf(parameter);
    this.Attacks.Items.Move(oldIndex, oldIndex + 1);
    this.MoveAttackUpCommand.RaiseCanExecuteChanged();
    this.MoveAttackDownCommand.RaiseCanExecuteChanged();
  }

  private bool CanMoveAttackUp(AttackSectionItem parameter)
  {
    return parameter != null && !parameter.Equals((object) this.Attacks.Items.FirstOrDefault<AttackSectionItem>()) && this.Attacks.Items.IndexOf(parameter) != 0;
  }

  private bool CanMoveAttackDown(AttackSectionItem parameter)
  {
    return parameter != null && !parameter.Equals((object) this.Attacks.Items.LastOrDefault<AttackSectionItem>()) && this.Attacks.Items.IndexOf(parameter) != this.Attacks.Items.Count - 1;
  }

  private void AddAttack()
  {
    AttackSectionItem attackSectionItem = new AttackSectionItem("New Attack");
    CreateAttackWindow createAttackWindow = new CreateAttackWindow();
    createAttackWindow.DataContext = (object) attackSectionItem;
    bool? nullable = createAttackWindow.ShowDialog();
    if (!nullable.HasValue || !nullable.Value)
      return;
    this.Attacks.Items.Add(attackSectionItem);
  }

  private void EditAttack(AttackSectionItem parameter)
  {
    if (parameter == null)
      return;
    CreateAttackWindow createAttackWindow = new CreateAttackWindow();
    createAttackWindow.DataContext = (object) parameter;
    createAttackWindow.ShowDialog();
  }

  private void RemoveAttack(AttackSectionItem parameter)
  {
    if (parameter == null)
      return;
    this.Attacks.Items.Remove(parameter);
  }

  private void ToggleDisplayAttack(AttackSectionItem parameter)
  {
    if (parameter == null)
      return;
    parameter.IsDisplayed = !parameter.IsDisplayed;
  }

  protected override void InitializeDesignData()
  {
    base.InitializeDesignData();
    this.Attacks.Items.Add(new AttackSectionItem("Attack 1", "Description 1"));
    this.Attacks.Items.Add(new AttackSectionItem("Attack 2", "Description 2")
    {
      IsDisplayed = false
    });
    this.Attacks.Items.Add(new AttackSectionItem("Attack 3", "Description 3"));
    this.Attacks.Items.Add(new AttackSectionItem("Attack 4", "Description 4")
    {
      IsDisplayed = false
    });
    this.SelectedAttackItem = this.Attacks.Items.FirstOrDefault<AttackSectionItem>();
  }

  private void UpdateLinkedAttacks()
  {
    if (!this.Manager.Status.IsLoaded)
      return;
    foreach (AttackSectionItem attackSectionItem in (Collection<AttackSectionItem>) this.Attacks.Items)
    {
      if (attackSectionItem.EquipmentItem != null && attackSectionItem.IsAutomaticAddition)
        attackSectionItem.UpdateCalculations();
    }
  }

  public void OnHandleEvent(CharacterManagerElementsUpdated args) => this.UpdateLinkedAttacks();

  public void OnHandleEvent(ReprocessCharacterEvent args) => this.UpdateLinkedAttacks();
}
