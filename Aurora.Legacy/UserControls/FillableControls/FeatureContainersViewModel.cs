// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.FillableControls.FeatureContainersViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Data;
using Builder.Presentation.Events.Character;
using Builder.Presentation.ViewModels.Base;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

#nullable disable
namespace Builder.Presentation.UserControls.FillableControls;

public class FeatureContainersViewModel : 
  ViewModelBase,
  ISubscriber<CharacterManagerElementRegistered>,
  ISubscriber<CharacterManagerElementUnregistered>
{
  private ElementContainer _selectedContainer;

  public FeatureContainersViewModel()
  {
    if (this.IsInDesignMode)
    {
      ElementContainer elementContainer1 = new ElementContainer((ElementBase) null)
      {
        Name = {
          OriginalContent = "Original Name"
        },
        Description = {
          OriginalContent = "A nice original description."
        },
        IsEnabled = true,
        IsNested = false
      };
      ElementContainer elementContainer2 = new ElementContainer((ElementBase) null)
      {
        Name = {
          OriginalContent = "Another Original Name"
        },
        Description = {
          OriginalContent = "A nice original description for the secondary item."
        },
        IsEnabled = true,
        IsNested = true
      };
      ElementContainer elementContainer3 = new ElementContainer((ElementBase) null)
      {
        Name = {
          OriginalContent = "Another Original Name"
        },
        Description = {
          OriginalContent = "A nice original description for the secondary item."
        },
        IsEnabled = false,
        IsNested = false
      };
      this.Containers.Add(elementContainer1);
      this.Containers.Add(elementContainer2);
      this.Containers.Add(elementContainer3);
      this.SelectedContainer = this.Containers.First<ElementContainer>();
    }
    else
      this.SubscribeWithEventAggregator();
  }

  public ObservableCollection<ElementContainer> Containers { get; set; } = new ObservableCollection<ElementContainer>();

  public ElementContainer SelectedContainer
  {
    get => this._selectedContainer;
    set
    {
      this.SetProperty<ElementContainer>(ref this._selectedContainer, value, nameof (SelectedContainer));
    }
  }

  private void Fill()
  {
    IEnumerable<ElementContainer> containers = new CharacterContentOrganizer().GetContainers(CharacterManager.Current.GetElements().ToList<ElementBase>());
    this.Containers.Clear();
    foreach (ElementContainer elementContainer in containers)
      this.Containers.Add(elementContainer);
  }

  public void OnHandleEvent(CharacterManagerElementRegistered args) => this.Fill();

  public void OnHandleEvent(CharacterManagerElementUnregistered args) => this.Fill();
}
