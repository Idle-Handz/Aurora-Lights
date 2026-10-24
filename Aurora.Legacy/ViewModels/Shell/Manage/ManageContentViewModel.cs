// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Shell.Manage.ManageContentViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Models.NewFolder1;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using Builder.Presentation.Views.Sliders;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.ViewModels.Shell.Manage;

public sealed class ManageContentViewModel : ViewModelBase
{
  private string _characterName;
  private string _playerName;
  private string _experience;
  private string _deity;
  private string _spellcastingClass;
  private string _spellcastingAbility;
  private string _spellSaveDc;
  private string _spellAttackBonus;
  private string _age;
  private string _height;
  private string _weight;
  private string _eyes;
  private string _skin;
  private string _hair;
  private string _characterBackstory;
  private Random _rnd = new Random();
  private string _additionalFeaturesAndTraits;
  private string _treasure;
  private string _copper;
  private string _silver;
  private string _electrum;
  private string _gold;
  private string _platinum;
  private string _equipment;
  private string _cantripSlot1;

  public ManageContentViewModel()
  {
    this.GenerateCharacterNameCommand = (ICommand) new RelayCommand(new Action(this.GenerateCharacterName));
    this.GenerateCharacterSheetCommand = (ICommand) new RelayCommand(new Action(this.GenerateCharacterSheet));
    if (this.IsInDesignMode)
    {
      this.InitializeDesignData();
    }
    else
    {
      this.PlayerName = ApplicationContext.Current.Settings.PlayerName;
      this.Trinkets.Add("A mummified goblin hand");
      this.Trinkets.Add("A piece of crystal that faintly glows in the moonlight");
      this.Trinkets.Add("A gold coin minted in a fallen civilization");
      this.Trinkets.Add("A diary written in a language you don’t know");
      this.Trinkets.Add("A brass ring that never tarnishes");
      this.Trinkets.Add("An old chess piece made from glass");
      this.Trinkets.Add("A pair of knucklebone dice, each with a skull symbol on the side that would normally show six pips");
      this.Trinkets.Add("A small idol depicting a nightmarish creature that gives you unsettling dreams when you sleep near it");
      this.Trinkets.Add("A rope necklace from which dangles four mummified elf fingers");
      this.Trinkets.Add("The deed for a parcel of land in a realm unknown to you");
      this.Trinkets.Add("A 1-ounce block made from an unknown material");
      this.Trinkets.Add("A small cloth doll skewered with needles");
      this.Trinkets.Add("A tooth from an unknown beast");
      this.Trinkets.Add("An enormous scale, perhaps from a dragon");
      this.Trinkets.Add("A bright green feather");
      this.Trinkets.Add("An old divination card bearing your likeness");
      this.Trinkets.Add("A glass orb filled with moving smoke");
      this.Trinkets.Add("A 1-pound egg with a bright red shell");
      this.Trinkets.Add("A pipe that blows bubbles");
      this.Trinkets.Add("A glass jar containing a weird bit of flesh floating in pickling fluid");
      this.Trinkets.Add("A tiny gnome-crafted music box that plays a song you dimly remember from your childhood");
      this.Trinkets.Add("A small wooden statuette of a smug halfling");
      this.Trinkets.Add("A brass orb etched with strange runes");
      this.Trinkets.Add("A multicolored stone disk");
      this.Trinkets.Add("A tiny silver icon of a raven");
      this.Trinkets.Add("A bag containing forty-seven humanoid teeth, one of which is rotten");
      this.Trinkets.Add("A shard of obsidian that always feels warm to the touch");
      this.Trinkets.Add("A dragon’s bony talon hanging from a plain leather necklace");
      this.Trinkets.Add("A pair of old socks");
      this.Trinkets.Add("A blank book whose pages refuse to hold ink, chalk, graphite, or any other substance or marking");
      this.Trinkets.Add("A silver badge in the shape of a five-pointed star");
      this.Trinkets.Add("A knife that belonged to a relative");
      this.Trinkets.Add("A glass vial filled with nail clippings");
      this.Trinkets.Add("A rectangular metal device with two tiny metal cups on one end that throws sparks when wet");
      this.Trinkets.Add("A white, sequined glove sized for a human");
      this.Trinkets.Add("A vest with one hundred tiny pockets");
      this.Trinkets.Add("A small, weightless stone block");
      this.Trinkets.Add("A tiny sketch portrait of a goblin");
      this.Trinkets.Add("An empty glass vial that smells of perfume when opened");
      this.Trinkets.Add("A gemstone that looks like a lump of coal when examined by anyone but you");
      this.Trinkets.Add("A scrap of cloth from an old banner");
      this.Trinkets.Add("A rank insignia from a lost legionnaire");
      this.Trinkets.Add("A tiny silver bell without a clapper");
      this.Trinkets.Add("A mechanical canary inside a gnome-crafted lamp");
      this.Trinkets.Add("A tiny chest carved to look like it has numerous feet on the bottom");
      this.Trinkets.Add("A dead sprite inside a clear glass bottle");
      this.Trinkets.Add("A metal can that has no opening but sounds as if it is filled with liquid, sand, spiders, or broken glass (your choice)");
      this.Trinkets.Add("A glass orb filled with water, in which swims a clockwork goldfish");
      this.Trinkets.Add("A silver spoon with an M engraved on the handle");
      this.Trinkets.Add("A whistle made from the gold-colored wood");
      this.Trinkets.Add("A dead scarab beetle the size of your hand");
      this.Trinkets.Add("Two toy soldiers, one with a missing head");
      this.Trinkets.Add("A small box filled with different-sized buttons");
      this.Trinkets.Add("A candle that can’t be lit");
      this.Trinkets.Add("A tiny cage with no door");
      this.Trinkets.Add("An old key");
      this.Trinkets.Add("An indecipherable treasure map");
      this.Trinkets.Add("A hilt from a broken sword");
      this.Trinkets.Add("A rabbit’s foot");
      this.Trinkets.Add("A glass eye");
      this.Trinkets.Add("A cameo carved in the likeness of a hideous person");
      this.Trinkets.Add("A silver skull the size of a coin");
      this.Trinkets.Add("An alabaster mask");
      this.Trinkets.Add("A pyramid of sticky black incense that smells very bad");
      this.Trinkets.Add("A nightcap that, when worn, gives you pleasant dreams");
      this.Trinkets.Add("A single caltrop made from bone");
      this.Trinkets.Add("A gold monocle frame without the lens");
      this.Trinkets.Add("A 1 inch cube, each side painted a different color");
      this.Trinkets.Add("A crystal knob from a door");
      this.Trinkets.Add("A small packet filled with pink dust");
      this.Trinkets.Add("A fragment of a beautiful song, written as musical notes on two pieces of parchment");
      this.Trinkets.Add("A silver teardrop earring made from a real teardrop");
      this.Trinkets.Add("The shell of an egg painted with scenes of human misery in disturbing detail");
      this.Trinkets.Add("A fan that, when unfolded, shows a sleeping cat");
      this.Trinkets.Add("A set of bone pipes");
      this.Trinkets.Add("A four-leaf clover pressed inside a book discussing manners and etiquette");
      this.Trinkets.Add("A sheet of parchment upon which is drawn a complex mechanical contraption");
      this.Trinkets.Add("An ornate scabbard that fits no blade you have found so far");
      this.Trinkets.Add("An invitation to a party where a murder happened");
      this.Trinkets.Add("A bronze pentacle with an etching of a rat’s head in its center");
      this.Trinkets.Add("A purple handkerchief embroidered with the name of a powerful archmage");
      this.Trinkets.Add("Half of a floorplan for a temple, castle, or some other structure");
      this.Trinkets.Add("A bit of folded cloth that, when unfolded, turns into a stylish cap");
      this.Trinkets.Add("A receipt of deposit at a bank in a far-flung city");
      this.Trinkets.Add("A diary with seven missing pages");
      this.Trinkets.Add("An empty silver snuffbox bearing an inscription on the surface that says “dreams”");
      this.Trinkets.Add("An iron holy symbol devoted to an unknown god");
      this.Trinkets.Add("A book that tells the story of a legendary hero’s rise and fall, with the last chapter missing");
      this.Trinkets.Add("A vial of dragon blood");
      this.Trinkets.Add("An ancient arrow of elven design");
      this.Trinkets.Add("A needle that never bends");
      this.Trinkets.Add("An ornate brooch of dwarven design");
      this.Trinkets.Add("An empty wine bottle bearing a pretty label that says, “The Wizard of Wines Winery, Red Dragon Crush, 33142 - W”");
      this.Trinkets.Add("A mosaic tile with a multicolored, glazed surface");
      this.Trinkets.Add("A petrified mouse");
      this.Trinkets.Add("A black pirate flag adorned with a dragon’s skull");
      this.Trinkets.Add("A tiny mechanical crab or spider that moves about when it’s not being observed");
      this.Trinkets.Add("A glass jar containing lard with a label that reads, “Griffon Grease”");
      this.Trinkets.Add("A wooden box with a ceramic bottom that holds a living worm with a head on each end of its body");
      this.Trinkets.Add("A metal urn containing the ashes of a hero");
      this.Trinkets.Add("An hourglass decorated with emeralds which is filled with acid instead of sand");
      this.Organizations.Add("The Harpers");
      this.Organizations.Add("The Emerald Enclave");
      this.Organizations.Add("The Lords’ Alliance");
      this.Organizations.Add("The Order of the Gauntlet");
      this.Organizations.Add("The Zhentarim");
      this.Organizations.Add("House Cannith");
      this.Organizations.Add("House Deneith");
      this.Organizations.Add("House Ghallanda");
      this.Organizations.Add("House Jorasco");
      this.Organizations.Add("House Kundarak");
      this.Organizations.Add("House Lyrandar");
      this.Organizations.Add("House Medani");
      this.Organizations.Add("House Orien");
      this.Organizations.Add("House Phiarlan");
      this.Organizations.Add("House Sivis");
      this.Organizations.Add("House Tharashk");
      this.Organizations.Add("House Thuranni");
      this.Organizations.Add("House Vadalis");
      this.Organizations.Add("Azorius Senate");
      this.Organizations.Add("Boros Legion");
      this.Organizations.Add("Cult of Rakdos");
      this.Organizations.Add("Golgari Swarm");
      this.Organizations.Add("Gruul Clans");
      this.Organizations.Add("House Dimir");
      this.Organizations.Add("Izzet League");
      this.Organizations.Add("Orzhov Syndicate");
      this.Organizations.Add("Selesnya Conclave");
      this.Organizations.Add("Simic Combine");
      foreach (ElementBase elementBase in DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Organization"))))
      {
        if (!this.Organizations.Contains(elementBase.Name))
          this.Organizations.Add(elementBase.Name);
      }
      this.EventAggregator.Subscribe((object) this);
    }
  }

  public CharacterManager CharacterManager => CharacterManager.Current;

  public Builder.Presentation.Models.Character Character => this.CharacterManager.Character;

  public string CharacterName
  {
    get => this._characterName;
    set
    {
      this.SetProperty<string>(ref this._characterName, value, nameof (CharacterName));
      this.EventAggregator.Send<CharacterNameChangedEvent>(new CharacterNameChangedEvent(this._characterName));
    }
  }

  public string PlayerName
  {
    get => this._playerName;
    set => this.SetProperty<string>(ref this._playerName, value, nameof (PlayerName));
  }

  public string Experience
  {
    get => this._experience;
    set => this.SetProperty<string>(ref this._experience, value, nameof (Experience));
  }

  public string Deity
  {
    get => this._deity;
    set => this.SetProperty<string>(ref this._deity, value, nameof (Deity));
  }

  public string SpellcastingClass
  {
    get => this._spellcastingClass;
    set => this.SetProperty<string>(ref this._spellcastingClass, value, nameof (SpellcastingClass));
  }

  public string SpellcastingAbility
  {
    get => this._spellcastingAbility;
    set
    {
      this.SetProperty<string>(ref this._spellcastingAbility, value, nameof (SpellcastingAbility));
    }
  }

  public string SpellSaveDc
  {
    get => this._spellSaveDc;
    set => this.SetProperty<string>(ref this._spellSaveDc, value, nameof (SpellSaveDc));
  }

  public string SpellAttackBonus
  {
    get => this._spellAttackBonus;
    set => this.SetProperty<string>(ref this._spellAttackBonus, value, nameof (SpellAttackBonus));
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

  public ICommand ShowSymbolsGalleryCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ShowSymbolsGallery));
  }

  public ICommand ShowCompanionsGalleryCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.ShowCompanionsGallery));
  }

  public ICommand ClearSymbolCommand => (ICommand) new RelayCommand(new Action(this.ClearSymbol));

  private void ShowSymbolsGallery()
  {
    ApplicationManager.Current.EventAggregator.Send<ShowSliderEvent>(new ShowSliderEvent(Slider.OrganizationSymbolsGallery));
  }

  private void ShowCompanionsGallery()
  {
    ApplicationManager.Current.EventAggregator.Send<ShowSliderEvent>(new ShowSliderEvent(Slider.CompanionGallery));
  }

  private void ClearSymbol() => this.Character.OrganisationSymbol = string.Empty;

  public string CharacterBackstory
  {
    get => this._characterBackstory;
    set
    {
      this.SetProperty<string>(ref this._characterBackstory, value, nameof (CharacterBackstory));
    }
  }

  public FillableBackgroundCharacteristics FillableBackgroundCharacteristics { get; set; } = new FillableBackgroundCharacteristics();

  public ObservableCollection<string> Trinkets { get; } = new ObservableCollection<string>();

  public ObservableCollection<string> Organizations { get; } = new ObservableCollection<string>();

  public ICommand RandomizeTrinketCommand
  {
    get => (ICommand) new RelayCommand(new Action(this.RandomizeTrinket));
  }

  private void RandomizeTrinket()
  {
    int index = this._rnd.Next(this.Trinkets.Count);
    if (index >= this.Trinkets.Count)
    {
      if (Debugger.IsAttached)
        Debugger.Break();
      index = this._rnd.Next(this.Trinkets.Count);
    }
    this.Character.Trinket.Clear();
    this.Character.Trinket.OriginalContent = this.Trinkets[index];
  }

  public string AdditionalFeaturesAndTraits
  {
    get => this._additionalFeaturesAndTraits;
    set
    {
      this.SetProperty<string>(ref this._additionalFeaturesAndTraits, value, nameof (AdditionalFeaturesAndTraits));
    }
  }

  public string Treasure
  {
    get => this._treasure;
    set => this.SetProperty<string>(ref this._treasure, value, nameof (Treasure));
  }

  public ICommand GenerateCharacterNameCommand { get; }

  public ICommand GenerateCharacterSheetCommand { get; }

  private void GenerateCharacterName()
  {
    Random random = new Random(Environment.TickCount);
    if (CharacterManager.Current.Elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Race")) is Race race)
    {
      string lower = CharacterManager.Current.Character.Gender.ToLower();
      if (race.Names != null)
      {
        if (lower == "male" || lower == "female")
          this.CharacterName = race.Names.GenerateRandomName(lower);
        else
          this.CharacterName = race.Names.GenerateRandomName();
      }
      else
      {
        List<string> stringList = lower == "male" ? race.MaleNames : race.FemaleNames;
        this.CharacterName = stringList[random.Next(stringList.Count)];
      }
    }
    else
    {
      string[] strArray = new string[4]
      {
        "Dr. Ustabil",
        "Beguil the Bard",
        "Kurald Emurlahn",
        "Flaem"
      };
      this.CharacterName = strArray[random.Next(strArray.Length)];
    }
  }

  private void GenerateCharacterSheet()
  {
    try
    {
      CharacterManager.Current.GenerateCharacterSheet();
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  protected override void InitializeDesignData()
  {
    base.InitializeDesignData();
    this.CharacterName = "Dr. Ustabil";
    this.Experience = "6500";
    this.Age = "63";
    this.Eyes = "Blue";
    this.Hair = "White";
  }

  public string Copper
  {
    get => this._copper;
    set => this.SetProperty<string>(ref this._copper, value, nameof (Copper));
  }

  public string Silver
  {
    get => this._silver;
    set => this.SetProperty<string>(ref this._silver, value, nameof (Silver));
  }

  public string Electrum
  {
    get => this._electrum;
    set => this.SetProperty<string>(ref this._electrum, value, nameof (Electrum));
  }

  public string Gold
  {
    get => this._gold;
    set => this.SetProperty<string>(ref this._gold, value, nameof (Gold));
  }

  public string Platinum
  {
    get => this._platinum;
    set => this.SetProperty<string>(ref this._platinum, value, nameof (Platinum));
  }

  public string Equipment
  {
    get => this._equipment;
    set => this.SetProperty<string>(ref this._equipment, value, nameof (Equipment));
  }

  public string CantripSlot1
  {
    get => this._cantripSlot1;
    set => this.SetProperty<string>(ref this._cantripSlot1, value, nameof (CantripSlot1));
  }

  public SpellContentViewModel SpellContentViewModel { get; set; } = new SpellContentViewModel();
}
