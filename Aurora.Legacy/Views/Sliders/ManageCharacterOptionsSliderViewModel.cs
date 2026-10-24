// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Sliders.ManageCharacterOptionsSliderViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Views.Sliders;

public sealed class ManageCharacterOptionsSliderViewModel : ViewModelBase
{
  public ManageCharacterOptionsSliderViewModel()
  {
    if (this.IsInDesignMode)
    {
      this.InitializeDesignData();
      this.CharacterOptions.Add(new CharacterOption((ElementBase) null)
      {
        Header = "Header 1",
        Description = "A first short description of the item."
      });
      this.CharacterOptions.Add(new CharacterOption((ElementBase) null)
      {
        Header = "Header 2",
        Description = "Another short description of the item. Another short description of the item. Another short description of the item. Another short description of the item. Another short description of the item.",
        IsSelected = true,
        IsCustom = true,
        IsEnabled = true
      });
      this.CharacterOptions.Add(new CharacterOption((ElementBase) null)
      {
        Header = "Awesome Header 3",
        Description = "A 3rd description of the item.",
        IsSelected = false,
        IsCustom = true,
        IsEnabled = true
      });
    }
    else
      this.LoadCharacterOptions();
  }

  public CharacterManager Manager => CharacterManager.Current;

  public ICommand SaveCommand => (ICommand) new RelayCommand(new Action(this.Save));

  private void Save()
  {
    bool flag = false;
    foreach (CharacterOption characterOption in (Collection<CharacterOption>) this.CharacterOptions)
    {
      if (characterOption.IsSelected)
      {
        if (!characterOption.IsActive)
        {
          CharacterManager.Current.RegisterElement(characterOption.Element);
          flag = true;
        }
      }
      else if (characterOption.IsActive)
      {
        CharacterManager.Current.UnregisterElement(characterOption.Element);
        flag = true;
      }
    }
    if (!flag)
      return;
    this.EventAggregator.Send<CharacterOptionsChangedEvent>(new CharacterOptionsChangedEvent(new List<CharacterOption>((IEnumerable<CharacterOption>) this.CharacterOptions)));
  }

  public ObservableCollection<CharacterOption> CharacterOptions { get; } = new ObservableCollection<CharacterOption>();

  public override Task InitializeAsync(InitializationArguments args)
  {
    this.LoadCharacterOptions();
    return base.InitializeAsync(args);
  }

  private void LoadCharacterOptions()
  {
    List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Option"))).OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)).ToList<ElementBase>();
    this.CharacterOptions.Clear();
    foreach (ElementBase element in list.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Source.Equals("Internal"))))
      this.CharacterOptions.Add(new CharacterOption(element)
      {
        IsSelected = false
      });
    foreach (ElementBase element in list.Where<ElementBase>((Func<ElementBase, bool>) (x => !x.Source.Equals("Internal"))))
      this.CharacterOptions.Add(new CharacterOption(element)
      {
        IsSelected = false,
        IsEnabled = true,
        IsCustom = true
      });
    foreach (ElementBase elementBase in this.Manager.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Option"))))
    {
      ElementBase registeredOption = elementBase;
      CharacterOption characterOption = this.CharacterOptions.FirstOrDefault<CharacterOption>((Func<CharacterOption, bool>) (x => x.Element == registeredOption));
      if (characterOption != null)
      {
        characterOption.IsActive = true;
        characterOption.IsSelected = true;
      }
    }
  }
}
