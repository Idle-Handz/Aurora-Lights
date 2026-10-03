// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.Character
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Data.Elements;
using System.Collections.Generic;
using System.Linq;
using Builder.Presentation.Models.Collections;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.Models.Helpers;
using Builder.Presentation.Models.NewFolder1;
using Builder.Presentation.UserControls.Spellcasting;
using Builder.Presentation.ViewModels.Shell.Manage;

#nullable disable
namespace Builder.Presentation.Models;

public sealed class Character : ObservableObject
{
  private string _name;
  private string _playerName;
  private int _level;
  private int _experience;
  private string _class;
  private string _archetype;
  private string _race;
  private string _background;
  private string _alignment;
  private int _proficiency;
  private string _age;
  private string _height;
  private string _weight;
  private string _eyes;
  private string _skin;
  private string _hair;
  private int _speed;
  private int _armorClass;
  private int _initiative;
  private int _maxHp;
  private string _backstory;
  private string _portraitFilename;
  private string _gender;
  private string _deity;
  private string _dragonmark;
  private string _allies;
  private string _organisationName;
  private string _organisationSymbol;
  private string _additonalFeatures;
  private int _multiclassSpellcasterLevel;
  private string _notes1;
  private string _notes2;

  public Character()
  {
    this.Abilities = new AbilitiesCollection();
    this.Skills = new SkillsCollection(this.Abilities);
    this.SavingThrows = new SavingThrowCollection(this.Abilities);
    this.Inventory = new CharacterInventory();
    this.AttacksSection = new AttacksSection();
  }

  public AbilitiesCollection Abilities { get; }

  public SkillsCollection Skills { get; }

  public SavingThrowCollection SavingThrows { get; }

  public CharacterInventory Inventory { get; }

  private readonly Companion _emptyCompanion = new Companion();
  private readonly List<Companion> _companions = new List<Companion>();

  public IReadOnlyList<Companion> Companions => _companions;

  // The legacy sheet/view models still use the last companion as their primary.
  public Companion Companion => _companions.LastOrDefault() ?? _emptyCompanion;

  internal void SynchronizeCompanions(IEnumerable<ElementBase> elements)
  {
    var active = elements.OfType<CompanionElement>().ToList();
    var existing = _companions.ToArray();
    _companions.Clear();
    foreach (var element in active)
      _companions.Add(existing.FirstOrDefault(c => ReferenceEquals(c.Element, element)) ?? new Companion(element));
    // Companion is a computed property, so bindings to Character.Companion.* resolve once and
    // then keep whichever instance they first saw. Without this they stay on the empty
    // companion for the life of the view when a creature is acquired or replaced.
    if (!existing.SequenceEqual(_companions))
      this.OnPropertyChanged(nameof(Companions), nameof(Companion));
  }

  public string PlayerName
  {
    get => this._playerName;
    set => this.SetProperty<string>(ref this._playerName, value, nameof (PlayerName));
  }

  public string Name
  {
    get => this._name;
    set => this.SetProperty<string>(ref this._name, value, nameof (Name));
  }

  public int Level
  {
    get => this._level;
    set => this.SetProperty<int>(ref this._level, value, nameof (Level));
  }

  public int Experience
  {
    get => this._experience;
    set => this.SetProperty<int>(ref this._experience, value, nameof (Experience));
  }

  public string Class
  {
    get => this._class;
    set => this.SetProperty<string>(ref this._class, value, nameof (Class));
  }

  public string Archetype
  {
    get => this._archetype;
    set => this.SetProperty<string>(ref this._archetype, value, nameof (Archetype));
  }

  public string Race
  {
    get => this._race;
    set => this.SetProperty<string>(ref this._race, value, nameof (Race));
  }

  public string Background
  {
    get => this._background;
    set => this.SetProperty<string>(ref this._background, value, nameof (Background));
  }

  public string Alignment
  {
    get => this._alignment;
    set => this.SetProperty<string>(ref this._alignment, value, nameof (Alignment));
  }

  public int Proficiency
  {
    get => this._proficiency;
    set => this.SetProperty<int>(ref this._proficiency, value, nameof (Proficiency));
  }

  public string Age
  {
    get => this._age;
    set => this.SetProperty<string>(ref this._age, value, nameof (Age));
  }

  public string Height
  {
    get => this._height;
    set => this.SetProperty<string>(ref this._height, value, nameof (Height));
  }

  public string Weight
  {
    get => this._weight;
    set => this.SetProperty<string>(ref this._weight, value, nameof (Weight));
  }

  public string Eyes
  {
    get => this._eyes;
    set => this.SetProperty<string>(ref this._eyes, value, nameof (Eyes));
  }

  public string Skin
  {
    get => this._skin;
    set => this.SetProperty<string>(ref this._skin, value, nameof (Skin));
  }

  public string Hair
  {
    get => this._hair;
    set => this.SetProperty<string>(ref this._hair, value, nameof (Hair));
  }

  public int Speed
  {
    get => this._speed;
    set => this.SetProperty<int>(ref this._speed, value, nameof (Speed));
  }

  public int ArmorClass
  {
    get => this._armorClass;
    set => this.SetProperty<int>(ref this._armorClass, value, nameof (ArmorClass));
  }

  public int Initiative
  {
    get => this._initiative;
    set => this.SetProperty<int>(ref this._initiative, value, nameof (Initiative));
  }

  public int MaxHp
  {
    get => this._maxHp;
    set => this.SetProperty<int>(ref this._maxHp, value, nameof (MaxHp));
  }

  public string PortraitFilename
  {
    get => this._portraitFilename;
    set => this.SetProperty<string>(ref this._portraitFilename, value, nameof (PortraitFilename));
  }

  public string Backstory
  {
    get => this._backstory;
    set => this.SetProperty<string>(ref this._backstory, value, nameof (Backstory));
  }

  public string Gender
  {
    get => this._gender;
    set => this.SetProperty<string>(ref this._gender, value, nameof (Gender));
  }

  public bool MeetsLevelRequirement(int requiredLevel) => this.Level >= requiredLevel;

  public void ResetEntryFields()
  {
    this.Name = "";
    this.PortraitFilename = "";
    this.Experience = 0;
    this.PlayerName = "";
    this.Level = 0;
    this.Class = "";
    this.Archetype = "";
    this.Race = "";
    this.Background = "";
    this.Alignment = "";
    this.Age = "";
    this.Height = "";
    this.Weight = "";
    this.Eyes = "";
    this.Skin = "";
    this.Hair = "";
    this.Backstory = "";
    this.BackgroundStory.Clear(true);
    this.Trinket.Clear();
    this.FillableBackgroundCharacteristics.Clear(true);
    this.BackgroundFeatureName.OriginalContent = "";
    this.BackgroundFeatureDescription.OriginalContent = "";
    this.BackgroundFeatureName.Clear();
    this.BackgroundFeatureDescription.Clear();
    this.OrganisationName = "";
    this.OrganisationSymbol = "";
    this.Allies = "";
    this.AdditionalFeatures = "";
    this.Gender = "Male";
    this.Deity = "";
    this.Dragonmark = "";
    this.Abilities.Reset();
    this.Inventory.ClearInventory();
    this.AttacksSection.Reset();
    this.SpellcastingCollection.Reset();
    this.AgeField.Clear();
    this.HeightField.Clear();
    this.WeightField.Clear();
    this.ConditionalArmorClassField.Clear(true);
    this.ConditionalSavingThrowsField.Clear(true);
    this.MulticlassSpellcasterLevel = 0;
    this.MulticlassSpellSlots.Clear();
    bool hadCompanions = _companions.Count > 0;
    _companions.Clear();
    _emptyCompanion.Reset();
    if (hadCompanions)
      this.OnPropertyChanged(nameof(Companions), nameof(Companion));
    this.Notes1 = "";
    this.Notes2 = "";
  }

  public string Deity
  {
    get => this._deity;
    set => this.SetProperty<string>(ref this._deity, value, nameof (Deity));
  }

  public string Dragonmark
  {
    get => this._dragonmark;
    set => this.SetProperty<string>(ref this._dragonmark, value, nameof (Dragonmark));
  }

  public string Allies
  {
    get => this._allies;
    set => this.SetProperty<string>(ref this._allies, value, nameof (Allies));
  }

  public string OrganisationName
  {
    get => this._organisationName;
    set => this.SetProperty<string>(ref this._organisationName, value, nameof (OrganisationName));
  }

  public string OrganisationSymbol
  {
    get => this._organisationSymbol;
    set
    {
      this.SetProperty<string>(ref this._organisationSymbol, value, nameof (OrganisationSymbol));
    }
  }

  public string AdditionalFeatures
  {
    get => this._additonalFeatures;
    set
    {
      this.SetProperty<string>(ref this._additonalFeatures, value, nameof (AdditionalFeatures));
    }
  }

  public SpellcastingCollection SpellcastingCollection { get; } = new SpellcastingCollection();

  public AttacksSection AttacksSection { get; set; }

  public string CharacterBuildString => this.ToBuildString();

  public override string ToString()
  {
    return $"{this._name}, Level {this._level} {this._race} {this._class}";
  }

  public string ToBuildString()
  {
    if (string.IsNullOrWhiteSpace(this.Race) && !string.IsNullOrWhiteSpace(this.Class))
      return $"Level {this.Level} {this.Class}";
    return !string.IsNullOrWhiteSpace(this.Race) && string.IsNullOrWhiteSpace(this.Class) ? $"Level {this.Level} {this.Race}" : $"Level {this.Level} {this.Race} {this.Class}";
  }

  public FillableField HeightField { get; set; } = new FillableField();

  public FillableField WeightField { get; set; } = new FillableField();

  public FillableField AgeField { get; set; } = new FillableField();

  public FillableField BackgroundStory { get; set; } = new FillableField();

  public FillableField Trinket { get; set; } = new FillableField();

  public FillableBackgroundCharacteristics FillableBackgroundCharacteristics { get; set; } = new FillableBackgroundCharacteristics();

  public FillableField BackgroundFeatureName { get; set; } = new FillableField();

  public FillableField BackgroundFeatureDescription { get; set; } = new FillableField();

  public FillableField ConditionalArmorClassField { get; set; } = new FillableField();

  public FillableField ConditionalSavingThrowsField { get; set; } = new FillableField();

  public int MulticlassSpellcasterLevel
  {
    get => this._multiclassSpellcasterLevel;
    set
    {
      this.SetProperty<int>(ref this._multiclassSpellcasterLevel, value, nameof (MulticlassSpellcasterLevel));
    }
  }

  public SpellcastingSectionSlots MulticlassSpellSlots { get; } = new SpellcastingSectionSlots();

  public string Notes1
  {
    get => this._notes1;
    set => this.SetProperty<string>(ref this._notes1, value, nameof (Notes1));
  }

  public string Notes2
  {
    get => this._notes2;
    set => this.SetProperty<string>(ref this._notes2, value, nameof (Notes2));
  }
}
