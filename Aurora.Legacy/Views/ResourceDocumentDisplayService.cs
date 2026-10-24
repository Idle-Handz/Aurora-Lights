// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.ResourceDocumentDisplayService
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Events.Application;

#nullable disable
namespace Builder.Presentation.Views;

public class ResourceDocumentDisplayService
{
  private const string RaceDescriptionFile = "description-race.html";
  private readonly IEventAggregator _eventAggregator;
  private bool _showRaceIntroduction = true;
  private bool _showClassIntroduction = true;
  private bool _showBackgroundIntroduction = true;
  private bool _showAbilitiesIntroduction = true;
  private bool _showLanguagesIntroduction = true;
  private bool _showProficienciesIntroduction = true;
  private bool _showFeatsIntroduction = true;
  private bool _showEquipmentIntroduction = true;

  public ResourceDocumentDisplayService(IEventAggregator eventAggregator)
  {
    this._eventAggregator = eventAggregator;
  }

  public void DisplayRaceDescription(bool force = false)
  {
    if (force)
      this._showRaceIntroduction = true;
    this.Display("description-race.html", ref this._showRaceIntroduction);
  }

  public void DisplayClassDescription(bool force = false)
  {
    if (force)
      this._showClassIntroduction = true;
    this.Display("description-class.html", ref this._showClassIntroduction);
  }

  public void DisplayBackgroundDescription(bool force = false)
  {
    if (force)
      this._showBackgroundIntroduction = true;
    this.Display("description-background.html", ref this._showBackgroundIntroduction);
  }

  public void DisplayAbilitiesDescription(bool force = false)
  {
    if (force)
      this._showAbilitiesIntroduction = true;
    this.Display("description-abilities.html", ref this._showAbilitiesIntroduction);
  }

  public void DisplayLanguagesDescription(bool force = false)
  {
    if (force)
      this._showLanguagesIntroduction = true;
    this.Display("description-languages.html", ref this._showLanguagesIntroduction);
  }

  public void DisplayProficienciesDescription(bool force = false)
  {
    if (force)
      this._showProficienciesIntroduction = true;
    this.Display("description-proficiencies.html", ref this._showProficienciesIntroduction);
  }

  public void DisplayFeatsDescription(bool force = false)
  {
    if (force)
      this._showFeatsIntroduction = true;
    this.Display("description-feats.html", ref this._showFeatsIntroduction);
  }

  public void DisplayEquipmentDescription(bool force = false)
  {
    if (force)
      this._showEquipmentIntroduction = true;
    this.Display("description-equipment.html", ref this._showEquipmentIntroduction);
  }

  private void Display(string filename, ref bool show)
  {
    if (!show || string.IsNullOrWhiteSpace(filename))
      return;
    this._eventAggregator.Send<ResourceDocumentDisplayRequestEvent>(new ResourceDocumentDisplayRequestEvent(filename));
    show = false;
  }
}
