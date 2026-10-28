// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.CharacterSheetGenerator
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Aurora.Documents.ExportContent;
using Aurora.Documents.Sheets;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Rules;
using Builder.Data.Strings;
using Builder.Presentation.Documents;
using Builder.Presentation.Extensions;
using Builder.Presentation.Models;
using Builder.Presentation.Models.CharacterSheet;
using Builder.Presentation.Models.CharacterSheet.Content;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.Models.Helpers;
using Builder.Presentation.Models.Sheet;

using Builder.Presentation.Services.Calculator;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Telemetry;
using Builder.Presentation.UserControls.Spellcasting;
using Builder.Presentation.Utilities;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Shell.Items;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#nullable disable
namespace Builder.Presentation.Services;

public class CharacterSheetGenerator
{
  private IExportContentProvider _contentProvider;
  private readonly IEventAggregator _eventAggregator;
  private readonly CharacterManager _manager;
  private readonly Character _character;
  private readonly ElementBaseCollection _elements;

  public CharacterSheetGenerator(CharacterManager manager)
  {
    this._eventAggregator = ApplicationManager.Current.EventAggregator;
    this._manager = manager;
    this._character = this._manager.Character;
    this._elements = this._manager.GetElements();
  }

  private SpellcastingSheetInfo GenerateSpellcastingSheetInfo(SpellcastingInformation information)
  {
    SpellcasterSelectionControlViewModel sectionViewModel = SpellcastingSectionHandler.Current.GetSpellcasterSectionViewModel(information.UniqueIdentifier);
    IOrderedEnumerable<SelectionElement> spells = SpellcastingSectionHandler.Current.GetSpells(information);
    SpellcastingSheetInfo spellcastingSheetInfo = new SpellcastingSheetInfo()
    {
      SpellcastingClass = information.Name,
      SpellcastingAbility = information.AbilityName,
      SpellAttackBonus = sectionViewModel.InformationHeader.SpellAttackModifier.ToString(),
      SpellSaveDifficultyClass = sectionViewModel.InformationHeader.SpellSaveDc.ToString(),
      Spells1st = {
        TotalSlots = sectionViewModel.InformationHeader.Slot1
      },
      Spells2nd = {
        TotalSlots = sectionViewModel.InformationHeader.Slot2
      },
      Spells3rd = {
        TotalSlots = sectionViewModel.InformationHeader.Slot3
      },
      Spells4th = {
        TotalSlots = sectionViewModel.InformationHeader.Slot4
      },
      Spells5th = {
        TotalSlots = sectionViewModel.InformationHeader.Slot5
      },
      Spells6th = {
        TotalSlots = sectionViewModel.InformationHeader.Slot6
      },
      Spells7th = {
        TotalSlots = sectionViewModel.InformationHeader.Slot7
      },
      Spells8th = {
        TotalSlots = sectionViewModel.InformationHeader.Slot8
      },
      Spells9th = {
        TotalSlots = sectionViewModel.InformationHeader.Slot9
      }
    };
    foreach (SelectionElement selectionElement in (IEnumerable<SelectionElement>) spells)
    {
      Spell spell = selectionElement.Element.AsElement<Spell>();
      switch (spell.Level)
      {
        case 0:
          spellcastingSheetInfo.Cantrips.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, true));
          continue;
        case 1:
          spellcastingSheetInfo.Spells1st.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        case 2:
          spellcastingSheetInfo.Spells2nd.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        case 3:
          spellcastingSheetInfo.Spells3rd.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        case 4:
          spellcastingSheetInfo.Spells4th.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        case 5:
          spellcastingSheetInfo.Spells5th.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        case 6:
          spellcastingSheetInfo.Spells6th.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        case 7:
          spellcastingSheetInfo.Spells7th.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        case 8:
          spellcastingSheetInfo.Spells8th.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        case 9:
          spellcastingSheetInfo.Spells9th.Add(new SpellcastingSheetInfo.SpellsContainer.SpellPlaceholder(spell.Name, selectionElement.IsChosen));
          continue;
        default:
          continue;
      }
    }
    return spellcastingSheetInfo;
  }

  public FileInfo GenerateNewSheet(string destinationPath, bool generateForPreview)
  {
    // ISSUE: variable of a compiler-generated type
    AppSettingsStore settings = ApplicationManager.Current.Settings.Settings;
    List<SpellcastingInformation> list = CharacterManager.Current.GetSpellcastingInformations().Where<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => !x.IsExtension)).ToList<SpellcastingInformation>();
    CharacterSheetEx characterSheetEx = new CharacterSheetEx()
    {
      Configuration = {
        IsAttributeDisplayFlipped = settings.CharacterSheetAbilitiesFlipped,
        IncludeBackgroundPage = true,
        IncludeEquipmentPage = CharacterManager.Current.Character.Inventory.Items.Count <= 15 || true,
        IncludeNotesPage = CharacterManager.Current.Character.Notes1.Length > 0 || CharacterManager.Current.Character.Notes2.Length > 0,
        IncludeCompanionPage = CharacterManager.Current.Status.HasCompanion,
        IncludeSpellcastingPage = list.Any<SpellcastingInformation>(),
        IsEditable = settings.AllowEditableSheet,
        IncludeSpellcards = settings.IncludeSpellcards,
        IncludeItemcards = settings.IncludeItemcards,
        IncludeAttackCards = settings.SheetIncludeAttackCards,
        IncludeFeatureCards = settings.SheetIncludeFeatureCards,
        IncludeFormatting = settings.SheetIncludeFormatting,
        StartNewSpellCardsPage = settings.SheetStartSpellCardsOnNewPage,
        StartNewAttackCardsPage = settings.SheetStartAttackCardsOnNewPage,
        StartNewItemCardsPage = settings.SheetStartItemCardsOnNewPage,
        StartNewFeatureCardsPage = settings.SheetStartFeatureCardsOnNewPage,
        UseLegacyDetailsPage = settings.UseLegacyDetailsPage,
        UseLegacyBackgroundPage = settings.UseLegactBackgroundPage,
        UseLegacySpellcastingPage = settings.UseLegacySpellcastingPage
      }
    };
    characterSheetEx.ExportContent = this.GenerateExportContent(CharacterManager.Current, characterSheetEx.Configuration);
    this._contentProvider = (IExportContentProvider) new ExportContentGenerator(this._manager, characterSheetEx.Configuration);
    if (characterSheetEx.Configuration.IncludeEquipmentPage)
      characterSheetEx.EquipmentSheetExportContent = this._contentProvider.GetEquipmentContent();
    if (characterSheetEx.Configuration.IncludeNotesPage)
      characterSheetEx.NotesExportContent = this._contentProvider.GetNotesContent();
    if (characterSheetEx.Configuration.IncludeCompanionPage)
      this.GenerateCompanionSheetExportContent(CharacterManager.Current, characterSheetEx.Configuration, characterSheetEx.ExportContent);
    List<string> stringList = new List<string>();
    foreach (SpellcastingInformation information in list)
      characterSheetEx.SpellcastingPageExportContentCollection.Add(this.GenerateSpellcastingExportContent(CharacterManager.Current, information, stringList));
    try
    {
      if (list.Any<SpellcastingInformation>())
      {
        CharacterSheetSpellcastingPageExportContent spellcastingExportContent = this.GenerateOtherSpellcastingExportContent(CharacterManager.Current, stringList);
        if (spellcastingExportContent != null)
          characterSheetEx.SpellcastingPageExportContentCollection.Add(spellcastingExportContent);
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (GenerateNewSheet));
      AnalyticsErrorHelper.Exception(ex, method: nameof (GenerateNewSheet), line: 813);
    }
    if (characterSheetEx.Configuration.IncludeSpellcards)
    {
      IEnumerable<Spell> collection = CharacterManager.Current.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (e => e.Type.Equals("Spell"))).Cast<Spell>().Concat<Spell>(CharacterManager.Current.GetPreparedSpells()).OrderBy<Spell, int>((Func<Spell, int>) (x => x.Level)).ThenBy<Spell, string>((Func<Spell, string>) (x => x.Name)).Distinct<Spell>();
      characterSheetEx.Spells.AddRange(collection);
    }
    if (characterSheetEx.Configuration.IncludeItemcards)
    {
      foreach (RefactoredEquipmentItem refactoredEquipmentItem in (Collection<RefactoredEquipmentItem>) CharacterManager.Current.Character.Inventory.Items)
      {
        if (refactoredEquipmentItem.ShowCard)
          characterSheetEx.Items.Add(refactoredEquipmentItem);
      }
    }
    int num = characterSheetEx.Configuration.IncludeAttackCards ? 1 : 0;
    if (characterSheetEx.Configuration.IncludeFeatureCards)
    {
      foreach (ElementBase sortedFeature in new ElementsOrganizer((IEnumerable<ElementBase>) this._manager.GetElements()).GetSortedFeatures(new List<ElementBase>()))
      {
        if (sortedFeature.SheetDescription.DisplayOnSheet)
          characterSheetEx.Features.Add(sortedFeature);
      }
    }
    if (generateForPreview)
      AnalyticsEventHelper.CharacterSheetPreview(characterSheetEx.Configuration.IsFormFillable, characterSheetEx.Configuration.IncludeFormatting, characterSheetEx.Configuration.IncludeItemcards, characterSheetEx.Configuration.IncludeAttackCards, characterSheetEx.Configuration.IncludeSpellcards, characterSheetEx.Configuration.IncludeFeatureCards, characterSheetEx.Configuration.UseLegacySpellcastingPage);
    else
      AnalyticsEventHelper.CharacterSheetSave(characterSheetEx.Configuration.IsFormFillable, characterSheetEx.Configuration.IncludeFormatting, characterSheetEx.Configuration.IncludeItemcards, characterSheetEx.Configuration.IncludeAttackCards, characterSheetEx.Configuration.IncludeSpellcards, characterSheetEx.Configuration.IncludeFeatureCards, characterSheetEx.Configuration.UseLegacySpellcastingPage);
    return characterSheetEx.Save(destinationPath);
  }

  private MainSheetContent GenerateInformationSheetContent(
    CharacterSheetConfiguration configuration,
    Character character)
  {
    MainSheetContent informationSheetContent = new MainSheetContent()
    {
      CharacterName = character.Name,
      Level = character.Level.ToString(),
      Class = character.Class
    };
    informationSheetContent.ClassLevel = $"{informationSheetContent.Class} ({informationSheetContent.Level})";
    if (this._manager.Status.HasMulticlass)
      informationSheetContent.ClassLevel = informationSheetContent.Class ?? "";
    informationSheetContent.Background = character.Background;
    informationSheetContent.PlayerName = character.PlayerName;
    informationSheetContent.Race = character.Race;
    informationSheetContent.Alignment = character.Alignment;
    informationSheetContent.Experience = character.Experience.ToString();
    informationSheetContent.Strength = character.Abilities.Strength.FinalScore.ToString();
    MainSheetContent mainSheetContent1 = informationSheetContent;
    int num1 = character.Abilities.Dexterity.FinalScore;
    string str1 = num1.ToString();
    mainSheetContent1.Dexterity = str1;
    MainSheetContent mainSheetContent2 = informationSheetContent;
    num1 = character.Abilities.Constitution.FinalScore;
    string str2 = num1.ToString();
    mainSheetContent2.Constitution = str2;
    MainSheetContent mainSheetContent3 = informationSheetContent;
    num1 = character.Abilities.Intelligence.FinalScore;
    string str3 = num1.ToString();
    mainSheetContent3.Intelligence = str3;
    MainSheetContent mainSheetContent4 = informationSheetContent;
    num1 = character.Abilities.Wisdom.FinalScore;
    string str4 = num1.ToString();
    mainSheetContent4.Wisdom = str4;
    MainSheetContent mainSheetContent5 = informationSheetContent;
    num1 = character.Abilities.Charisma.FinalScore;
    string str5 = num1.ToString();
    mainSheetContent5.Charisma = str5;
    informationSheetContent.StrengthModifier = character.Abilities.Strength.ModifierString;
    informationSheetContent.DexterityModifier = character.Abilities.Dexterity.ModifierString;
    informationSheetContent.ConstitutionModifier = character.Abilities.Constitution.ModifierString;
    informationSheetContent.IntelligenceModifier = character.Abilities.Intelligence.ModifierString;
    informationSheetContent.WisdomModifier = character.Abilities.Wisdom.ModifierString;
    informationSheetContent.CharismaModifier = character.Abilities.Charisma.ModifierString;
    informationSheetContent.ProficiencyBonus = character.Proficiency.ToValueString();
    informationSheetContent.StrengthSavingThrow = character.SavingThrows.Strength.FinalBonus.ToValueString();
    informationSheetContent.DexteritySavingThrow = character.SavingThrows.Dexterity.FinalBonus.ToValueString();
    informationSheetContent.ConstitutionSavingThrow = character.SavingThrows.Constitution.FinalBonus.ToValueString();
    informationSheetContent.IntelligenceSavingThrow = character.SavingThrows.Intelligence.FinalBonus.ToValueString();
    informationSheetContent.WisdomSavingThrow = character.SavingThrows.Wisdom.FinalBonus.ToValueString();
    informationSheetContent.CharismaSavingThrow = character.SavingThrows.Charisma.FinalBonus.ToValueString();
    informationSheetContent.StrengthSavingThrowProficient = character.SavingThrows.Strength.IsProficient;
    informationSheetContent.DexteritySavingThrowProficient = character.SavingThrows.Dexterity.IsProficient;
    informationSheetContent.ConstitutionSavingThrowProficient = character.SavingThrows.Constitution.IsProficient;
    informationSheetContent.IntelligenceSavingThrowProficient = character.SavingThrows.Intelligence.IsProficient;
    informationSheetContent.WisdomSavingThrowProficient = character.SavingThrows.Wisdom.IsProficient;
    informationSheetContent.CharismaSavingThrowProficient = character.SavingThrows.Charisma.IsProficient;
    informationSheetContent.Acrobatics = character.Skills.Acrobatics.FinalBonus.ToValueString();
    informationSheetContent.AnimalHandling = character.Skills.AnimalHandling.FinalBonus.ToValueString();
    informationSheetContent.Arcana = character.Skills.Arcana.FinalBonus.ToValueString();
    informationSheetContent.Athletics = character.Skills.Athletics.FinalBonus.ToValueString();
    informationSheetContent.Deception = character.Skills.Deception.FinalBonus.ToValueString();
    informationSheetContent.History = character.Skills.History.FinalBonus.ToValueString();
    informationSheetContent.Insight = character.Skills.Insight.FinalBonus.ToValueString();
    informationSheetContent.Intimidation = character.Skills.Intimidation.FinalBonus.ToValueString();
    informationSheetContent.Investigation = character.Skills.Investigation.FinalBonus.ToValueString();
    informationSheetContent.Medicine = character.Skills.Medicine.FinalBonus.ToValueString();
    informationSheetContent.Nature = character.Skills.Nature.FinalBonus.ToValueString();
    informationSheetContent.Perception = character.Skills.Perception.FinalBonus.ToValueString();
    informationSheetContent.Performance = character.Skills.Performance.FinalBonus.ToValueString();
    informationSheetContent.Persuasion = character.Skills.Persuasion.FinalBonus.ToValueString();
    informationSheetContent.Religion = character.Skills.Religion.FinalBonus.ToValueString();
    informationSheetContent.SleightOfHand = character.Skills.SleightOfHand.FinalBonus.ToValueString();
    informationSheetContent.Stealth = character.Skills.Stealth.FinalBonus.ToValueString();
    informationSheetContent.Survival = character.Skills.Survival.FinalBonus.ToValueString();
    informationSheetContent.AcrobaticsProficient = character.Skills.Acrobatics.IsProficient;
    informationSheetContent.AnimalHandlingProficient = character.Skills.AnimalHandling.IsProficient;
    informationSheetContent.ArcanaProficient = character.Skills.Arcana.IsProficient;
    informationSheetContent.AthleticsProficient = character.Skills.Athletics.IsProficient;
    informationSheetContent.DeceptionProficient = character.Skills.Deception.IsProficient;
    informationSheetContent.HistoryProficient = character.Skills.History.IsProficient;
    informationSheetContent.InsightProficient = character.Skills.Insight.IsProficient;
    informationSheetContent.IntimidationProficient = character.Skills.Intimidation.IsProficient;
    informationSheetContent.InvestigationProficient = character.Skills.Investigation.IsProficient;
    informationSheetContent.MedicineProficient = character.Skills.Medicine.IsProficient;
    informationSheetContent.NatureProficient = character.Skills.Nature.IsProficient;
    informationSheetContent.PerceptionProficient = character.Skills.Perception.IsProficient;
    informationSheetContent.PerformanceProficient = character.Skills.Performance.IsProficient;
    informationSheetContent.PersuasionProficient = character.Skills.Persuasion.IsProficient;
    informationSheetContent.ReligionProficient = character.Skills.Religion.IsProficient;
    informationSheetContent.SleightOfHandProficient = character.Skills.SleightOfHand.IsProficient;
    informationSheetContent.StealthProficient = character.Skills.Stealth.IsProficient;
    informationSheetContent.SurvivalProficient = character.Skills.Survival.IsProficient;
    informationSheetContent.AcrobaticsExpertise = character.Skills.Acrobatics.ProficiencyBonus == character.Proficiency * 2;
    int num2 = this._manager.StatisticsCalculator.StatisticValues.GetValue(this._manager.StatisticsCalculator.Names.PerceptionPassive);
    MainSheetContent mainSheetContent6 = informationSheetContent;
    num1 = character.Skills.Perception.FinalBonus + num2;
    string str6 = num1.ToString();
    mainSheetContent6.PassiveWisdomPerception = str6;
    if (character.Inventory.EquippedArmor != null)
      informationSheetContent.EquippedArmor = character.Inventory.EquippedArmor.ToString();
    if (character.Inventory.EquippedSecondary != null && character.Inventory.IsEquippedShield())
      informationSheetContent.EquippedShield = character.Inventory.EquippedSecondary.ToString();
    informationSheetContent.ConditionalArmorClass = "conditional armor class field";
    MainSheetContent mainSheetContent7 = informationSheetContent;
    num1 = character.ArmorClass;
    string str7 = num1.ToString();
    mainSheetContent7.ArmorClass = str7;
    MainSheetContent mainSheetContent8 = informationSheetContent;
    num1 = character.Initiative;
    string str8 = num1.ToString();
    mainSheetContent8.Initiative = str8;
    MainSheetContent mainSheetContent9 = informationSheetContent;
    num1 = character.Speed;
    string str9 = num1.ToString();
    mainSheetContent9.Speed = str9;
    MainSheetContent mainSheetContent10 = informationSheetContent;
    num1 = this._manager.StatisticsCalculator.StatisticValues.GetValue("speed:fly");
    string str10 = num1.ToString();
    mainSheetContent10.FlySpeed = str10;
    MainSheetContent mainSheetContent11 = informationSheetContent;
    num1 = this._manager.StatisticsCalculator.StatisticValues.GetValue("speed:climb");
    string str11 = num1.ToString();
    mainSheetContent11.ClimbSpeed = str11;
    MainSheetContent mainSheetContent12 = informationSheetContent;
    num1 = this._manager.StatisticsCalculator.StatisticValues.GetValue("speed:swim");
    string str12 = num1.ToString();
    mainSheetContent12.SwimSpeed = str12;
    MainSheetContent mainSheetContent13 = informationSheetContent;
    num1 = character.MaxHp;
    string str13 = num1.ToString();
    mainSheetContent13.MaximumHitPoints = str13;
    MainSheetContent mainSheetContent14 = informationSheetContent;
    num1 = character.MaxHp;
    string str14 = num1.ToString();
    mainSheetContent14.CurrentHitPoints = str14;
    informationSheetContent.TemporaryHitPoints = "0";
    string str15 = "";
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) CharacterManager.Current.ClassProgressionManagers)
      str15 += $"/{progressionManager.ProgressionLevel}{progressionManager.HD}";
    informationSheetContent.TotalHitDice = str15.TrimStart('/').Trim();
    informationSheetContent.HitDice = "";
    informationSheetContent.DeathSavingThrowSuccess1 = true;
    informationSheetContent.DeathSavingThrowSuccess2 = false;
    informationSheetContent.DeathSavingThrowSuccess3 = false;
    informationSheetContent.DeathSavingThrowFailure1 = false;
    informationSheetContent.DeathSavingThrowFailure2 = false;
    informationSheetContent.DeathSavingThrowFailure3 = false;
    informationSheetContent.Name1 = character.AttacksSection.AttackObject1.Name;
    informationSheetContent.AttackBonus1 = character.AttacksSection.AttackObject1.Bonus;
    informationSheetContent.DamageType1 = character.AttacksSection.AttackObject1.Damage;
    informationSheetContent.Name2 = character.AttacksSection.AttackObject2.Name;
    informationSheetContent.AttackBonus2 = character.AttacksSection.AttackObject2.Bonus;
    informationSheetContent.DamageType2 = character.AttacksSection.AttackObject2.Damage;
    informationSheetContent.Name3 = character.AttacksSection.AttackObject3.Name;
    informationSheetContent.AttackBonus3 = character.AttacksSection.AttackObject3.Bonus;
    informationSheetContent.DamageType3 = character.AttacksSection.AttackObject3.Damage;
    informationSheetContent.AttackAndSpellcastingField = character.AttacksSection.AttacksAndSpellcasting;
    MainSheetContent mainSheetContent15 = informationSheetContent;
    long num3 = character.Inventory.Coins.Copper;
    string str16 = num3.ToString();
    mainSheetContent15.Copper = str16;
    MainSheetContent mainSheetContent16 = informationSheetContent;
    num3 = character.Inventory.Coins.Silver;
    string str17 = num3.ToString();
    mainSheetContent16.Silver = str17;
    MainSheetContent mainSheetContent17 = informationSheetContent;
    num3 = character.Inventory.Coins.Electrum;
    string str18 = num3.ToString();
    mainSheetContent17.Electrum = str18;
    MainSheetContent mainSheetContent18 = informationSheetContent;
    num3 = character.Inventory.Coins.Gold;
    string str19 = num3.ToString();
    mainSheetContent18.Gold = str19;
    MainSheetContent mainSheetContent19 = informationSheetContent;
    num3 = character.Inventory.Coins.Platinum;
    string str20 = num3.ToString();
    mainSheetContent19.Platinum = str20;
    StringBuilder stringBuilder1 = new StringBuilder();
    foreach (RefactoredEquipmentItem refactoredEquipmentItem in (Collection<RefactoredEquipmentItem>) character.Inventory.Items)
    {
      string str21 = !string.IsNullOrWhiteSpace(refactoredEquipmentItem.AlternativeName) ? refactoredEquipmentItem.AlternativeName : refactoredEquipmentItem.DisplayName;
      string str22 = !refactoredEquipmentItem.IsStackable || string.IsNullOrWhiteSpace(refactoredEquipmentItem.AlternativeName) ? "" : $" ({refactoredEquipmentItem.Amount})";
      string str23 = refactoredEquipmentItem.IsEquipped ? " (E)" : "";
      stringBuilder1.AppendLine(str21 + str22 + str23);
    }
    informationSheetContent.Equipment = stringBuilder1.ToString();
    informationSheetContent.PersonalityTraits = character.FillableBackgroundCharacteristics.Traits.Content;
    informationSheetContent.Ideals = character.FillableBackgroundCharacteristics.Ideals.Content;
    informationSheetContent.Bonds = character.FillableBackgroundCharacteristics.Bonds.Content;
    informationSheetContent.Flaws = character.FillableBackgroundCharacteristics.Flaws.Content;
    ElementsOrganizer elementsOrganizer = new ElementsOrganizer((IEnumerable<ElementBase>) CharacterManager.Current.GetElements());
    List<Language> list1 = elementsOrganizer.GetLanguages(false).ToList<Language>();
    List<ElementBase> list2 = this._manager.GetProficiencyList((IEnumerable<ElementBase>) elementsOrganizer.GetArmorProficiencies(false)).ToList<ElementBase>();
    List<ElementBase> list3 = this._manager.GetProficiencyList((IEnumerable<ElementBase>) elementsOrganizer.GetWeaponProficiencies(false)).ToList<ElementBase>();
    List<ElementBase> list4 = this._manager.GetProficiencyList((IEnumerable<ElementBase>) elementsOrganizer.GetToolProficiencies(false)).ToList<ElementBase>();
    ContentField contentField = new ContentField();
    if (list1.Count > 0)
      contentField.Lines.Add(new ContentLine("Languages", string.Join(", ", list1.Select<Language, string>((Func<Language, string>) (x => x.Name)))));
    if (list2.Count > 0)
    {
      IEnumerable<string> values = list2.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Armor Proficiency", "").Replace("(", "").Replace(")", "").Trim()));
      contentField.Lines.Add(new ContentLine("Armor Proficiencies", string.Join(", ", values), true));
    }
    if (list3.Count > 0)
    {
      IEnumerable<string> values = list3.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Weapon Proficiency", "").Replace("(", "").Replace(")", "").Trim()));
      contentField.Lines.Add(new ContentLine("Weapon Proficiencies", string.Join(", ", values), true));
    }
    if (list4.Count > 0)
    {
      IEnumerable<string> values = list4.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Tool Proficiency", "").Replace("(", "").Replace(")", "").Trim()));
      contentField.Lines.Add(new ContentLine("Tool Proficiencies", string.Join(", ", values), true));
    }
    informationSheetContent.ProficienciesAndLanguagesFieldContent = contentField;
    if (!configuration.IncludeFormatting)
    {
      StringBuilder stringBuilder2 = new StringBuilder();
      foreach (ContentLine line in informationSheetContent.ProficienciesAndLanguagesFieldContent.Lines)
      {
        if (line.NewLineBefore)
          stringBuilder2.AppendLine();
        stringBuilder2.AppendLine($"{line.Name}. {line.Content}");
      }
      informationSheetContent.ProficienciesAndLanguages = stringBuilder2.ToString();
    }
    List<ElementBase> list5 = this._manager.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Vision" || x.Type == "Race" || x.Type == "Sub Race" || x.Type == "Race Variant" || x.Type == "Racial Trait" || x.Type == "Class" || x.Type == "Class Feature" || x.Type == "Archetype" || x.Type == "Archetype Feature" || x.Type == "Feat" || x.Type == "Feat Feature")).Where<ElementBase>((Func<ElementBase, bool>) (x => !x.Name.StartsWith("Ability Score Increase"))).Where<ElementBase>((Func<ElementBase, bool>) (x => !x.Name.StartsWith("Ability Score Improvement"))).ToList<ElementBase>();
    Logger.Info("====================features post sorting====================");
    List<ElementBase> elementBaseList1 = new List<ElementBase>();
    List<ElementBase> elementBaseList2 = new List<ElementBase>();
    foreach (ElementBase elementBase1 in list5)
    {
      if (!elementBaseList1.Contains(elementBase1))
      {
        elementBaseList1.Add(elementBase1);
        Logger.Info($"{elementBase1}");
      }
      if (elementBase1.ContainsSelectRules)
      {
        foreach (SelectRule selectRule in elementBase1.GetSelectRules())
        {
          SelectRule rule = selectRule;
          for (int number = 1; number <= rule.Attributes.Number; ++number)
          {
            if (SelectionRuleExpanderHandler.Current.HasExpander(rule.UniqueIdentifier, number))
            {
              ElementBase registeredElement = SelectionRuleExpanderHandler.Current.GetRegisteredElement(rule, number) as ElementBase;
              if (list5.Contains(registeredElement))
              {
                ElementBase elementBase2 = list5.First<ElementBase>((Func<ElementBase, bool>) (x => x.Id == registeredElement.Id));
                if (!elementBaseList1.Contains(elementBase2))
                {
                  ElementBase elementBase3 = CharacterManager.Current.GetElements().Single<ElementBase>((Func<ElementBase, bool>) (x => x.Id == rule.ElementHeader.Id));
                  elementBaseList1.Add(elementBase2);
                  if (!rule.ElementHeader.Name.StartsWith("Ability Score Increase") && rule.ElementHeader.Type != "Race" || !rule.ElementHeader.Id.StartsWith("ID_CLASS_FEATURE_FEAT_") && rule.ElementHeader.Type != "Class Feature")
                  {
                    if (elementBase3.SheetDescription.DisplayOnSheet)
                    {
                      elementBaseList2.Add(elementBase2);
                      Logger.Info($"\t{elementBase2}");
                    }
                    else
                      Logger.Info($"\t{elementBase2}");
                  }
                  else
                    Logger.Info($"{elementBase2}");
                }
              }
            }
          }
        }
      }
    }
    foreach (ElementBase elementBase in elementBaseList1)
    {
      ElementBase element = elementBase;
      int num4 = this._manager.Character.Level;
      ClassProgressionManager progressionManager = this._manager.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.GetElements().Contains(element)));
      if (progressionManager != null)
        num4 = progressionManager.ProgressionLevel;
      if (element.SheetDescription.DisplayOnSheet)
      {
        string str24 = "";
        foreach (ElementSheetDescriptions.SheetDescription sheetDescription in (IEnumerable<ElementSheetDescriptions.SheetDescription>) element.SheetDescription.OrderBy<ElementSheetDescriptions.SheetDescription, int>((Func<ElementSheetDescriptions.SheetDescription, int>) (x => x.Level)))
        {
          if (sheetDescription.Level <= num4)
            str24 = sheetDescription.Description;
        }
        string pattern = "\\$\\((.*?)\\)";
        foreach (Match match in Regex.Matches(str24, pattern))
        {
          string oldValue = match.Value;
          string str25 = match.Value.Substring(2, match.Value.Length - 3);
          string newValue = "==UNKNOWN VALUE==";
          if (this._manager.StatisticsCalculator.StatisticValues.ContainsGroup(str25))
            newValue = this._manager.StatisticsCalculator.StatisticValues.GetValue(str25).ToString();
          else if (this._manager.StatisticsCalculator.InlineValues.ContainsKey(str25))
            newValue = this._manager.StatisticsCalculator.InlineValues[str25];
          else
            Logger.Warning($"UNKNOWN REPLACE STRING: {oldValue} in {element}");
          str24 = str24.Replace(oldValue, newValue);
        }
        string oldValue1;
        string newValue1;
        if (str24.Contains("%"))
        {
          for (; str24.Contains("%"); str24 = str24.Replace(oldValue1, newValue1))
          {
            int startIndex = str24.IndexOf("%", StringComparison.Ordinal);
            string str26 = str24.Substring(startIndex + 1, str24.Length - startIndex - 1);
            int num5 = startIndex + str26.IndexOf("%", StringComparison.Ordinal);
            oldValue1 = str24.Substring(startIndex, num5 - startIndex + 2);
            newValue1 = "UNKNOWN VALUE";
            if (this._manager.StatisticsCalculator.InlineValues.ContainsKey(oldValue1.Replace("%", "")))
              newValue1 = this._manager.StatisticsCalculator.InlineValues[oldValue1.Replace("%", "")];
            else if (this._manager.StatisticsCalculator.StatisticValues.ContainsGroup(oldValue1.Replace("%", "")))
              newValue1 = this._manager.StatisticsCalculator.StatisticValues.GetValue(oldValue1.Replace("%", "")).ToString();
            else
              Logger.Warning($"UNKNOWN REPLACESTRING:{oldValue1} in {element}");
          }
        }
        bool flag = false;
        if (informationSheetContent.FeaturesFieldContent.Lines.Any<ContentLine>())
          flag = informationSheetContent.FeaturesFieldContent.Lines.Last<ContentLine>().Indent;
        string name = element.SheetDescription.HasAlternateName ? element.SheetDescription.AlternateName : element.Name;
        informationSheetContent.FeaturesFieldContent.Lines.Add(new ContentLine(name, str24, flag || !elementBaseList2.Contains(element), elementBaseList2.Contains(element)));
      }
    }
    if (!configuration.IncludeFormatting)
    {
      StringBuilder stringBuilder3 = new StringBuilder();
      foreach (ContentLine line in informationSheetContent.FeaturesFieldContent.Lines)
      {
        if (line.NewLineBefore)
          stringBuilder3.AppendLine();
        stringBuilder3.AppendLine($"{(line.Indent ? "    " : "")}{line.Name}. {line.Content}");
      }
      informationSheetContent.FeaturesAndTraitsField = stringBuilder3.ToString().Trim();
    }
    return informationSheetContent;
  }

  private DetailsSheetContent GenerateDetailsSheetContent(Character character)
  {
    DetailsSheetContent detailsSheetContent = new DetailsSheetContent();
    detailsSheetContent.CharacterName = character.Name;
    detailsSheetContent.Age = character.AgeField.Content;
    detailsSheetContent.Height = character.HeightField.Content;
    detailsSheetContent.Weight = character.WeightField.Content;
    detailsSheetContent.Eyes = character.Eyes;
    detailsSheetContent.Skin = character.Skin;
    detailsSheetContent.Hair = character.Hair;
    detailsSheetContent.CharacterAppearance = character.PortraitFilename;
    detailsSheetContent.AlliesAndOrganizations = character.Allies;
    detailsSheetContent.OrganizationName = character.OrganisationName;
    detailsSheetContent.OrganizationSymbol = character.OrganisationSymbol;
    detailsSheetContent.AdditionalFeaturesAndTraits = character.AdditionalFeatures;
    detailsSheetContent.CharacterBackstory = character.BackgroundStory.Content;
    detailsSheetContent.Treasure = character.Inventory.Treasure;
    detailsSheetContent.Trinket = character.Trinket.ToString();
    ElementBase elementBase = CharacterManager.Current.GetElements().FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Background Feature")));
    if (elementBase != null)
    {
      detailsSheetContent.BackgroundFeatureName = elementBase.SheetDescription.HasAlternateName ? elementBase.SheetDescription.AlternateName : elementBase.Name;
      detailsSheetContent.BackgroundFeature = elementBase.SheetDescription.Any<ElementSheetDescriptions.SheetDescription>() ? elementBase.SheetDescription[0].Description : ElementDescriptionGenerator.GeneratePlainDescription(elementBase.Description);
    }
    return detailsSheetContent;
  }

  private SpellcastingSheetContent GenerateSpellcastingSheetContent(
    SpellcastingInformation information)
  {
    SpellcasterSelectionControlViewModel sectionViewModel = SpellcastingSectionHandler.Current.GetSpellcasterSectionViewModel(information.UniqueIdentifier);
    IOrderedEnumerable<SelectionElement> spells = SpellcastingSectionHandler.Current.GetSpells(information);
    SpellcastingSheetContent spellcastingSheetContent = new SpellcastingSheetContent()
    {
      SpellcastingClass = information.Name,
      SpellcastingAbility = information.AbilityName,
      SpellcastingAttackModifier = sectionViewModel.InformationHeader.SpellAttackModifier.ToString(),
      SpellcastingSave = sectionViewModel.InformationHeader.SpellSaveDc.ToString(),
      Spells1 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot1
      },
      Spells2 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot2
      },
      Spells3 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot3
      },
      Spells4 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot4
      },
      Spells5 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot5
      },
      Spells6 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot6
      },
      Spells7 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot7
      },
      Spells8 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot8
      },
      Spells9 = {
        SlotsCount = sectionViewModel.InformationHeader.Slot9
      }
    };
    if (information.Prepare)
    {
      string amountStatisticName = information.GetPrepareAmountStatisticName();
      spellcastingSheetContent.SpellcastingPrepareCount = this._manager.StatisticsCalculator.StatisticValues.GetValue(amountStatisticName).ToString();
    }
    else
      spellcastingSheetContent.SpellcastingPrepareCount = "N/A";
    spellcastingSheetContent.SpellcastingNotes = "implement spellcasting notes";
    foreach (SelectionElement selectionElement in (IEnumerable<SelectionElement>) spells)
    {
      Spell spell = selectionElement.Element.AsElement<Spell>();
      switch (spell.Level)
      {
        case 0:
          spellcastingSheetContent.Cantrips.Collection.Add(new SpellcastingSpellContent(spell.Name, true));
          continue;
        case 1:
          spellcastingSheetContent.Spells1.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        case 2:
          spellcastingSheetContent.Spells2.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        case 3:
          spellcastingSheetContent.Spells3.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        case 4:
          spellcastingSheetContent.Spells4.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        case 5:
          spellcastingSheetContent.Spells5.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        case 6:
          spellcastingSheetContent.Spells6.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        case 7:
          spellcastingSheetContent.Spells7.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        case 8:
          spellcastingSheetContent.Spells8.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        case 9:
          spellcastingSheetContent.Spells9.Collection.Add(new SpellcastingSpellContent(spell.Name, selectionElement.IsChosen));
          continue;
        default:
          continue;
      }
    }
    return spellcastingSheetContent;
  }

  private CharacterSheetExportContent GenerateExportContent(
    CharacterManager manager,
    CharacterSheetConfiguration configuration)
  {
    CharacterSheetExportContent export = new CharacterSheetExportContent();
    ElementBaseCollection elements = manager.GetElements();
    Character character = manager.Character;
    CharacterInventory inventory = manager.Character.Inventory;
    StatisticValuesGroupCollection statisticValues = manager.StatisticsCalculator.StatisticValues;
    AuroraStatisticStrings names = manager.StatisticsCalculator.Names;
    CharacterStatus status = manager.Status;
    export.PlayerName = character.PlayerName;
    export.CharacterName = character.Name;
    export.Race = character.Race;
    ElementBase elementBase1 = elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Race Variant")));
    if (elementBase1 != null)
      export.Race = elementBase1.GetAlternateName();
    export.Alignment = character.Alignment;
    export.Deity = character.Deity;
    export.Experience = character.Experience.ToString();
    export.ProficiencyBonus = character.Proficiency.ToValueString();
    export.Level = character.Level.ToString();
    CharacterSheetExportContent sheetExportContent = export;
    StatisticValuesGroup group1 = statisticValues.GetGroup("extra attack:count", false);
    int num1;
    string str1;
    if (group1 == null)
    {
      str1 = (string) null;
    }
    else
    {
      num1 = group1.Sum();
      str1 = num1.ToString();
    }
    if (str1 == null)
      str1 = "1";
    sheetExportContent.AttacksCount = str1;
    Dictionary<string, int> source1 = new Dictionary<string, int>();
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) manager.ClassProgressionManagers)
    {
      if (progressionManager.IsMainClass)
      {
        CharacterSheetExportContent.ClassExportContent mainClass = export.MainClass;
        num1 = progressionManager.ProgressionLevel;
        string str2 = num1.ToString();
        mainClass.Level = str2;
        export.MainClass.Name = progressionManager.ClassElement.GetAlternateName();
        export.MainClass.Archetype = progressionManager.Elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Archetype")))?.GetAlternateName();
        export.MainClass.HitDie = progressionManager.HD;
        if (source1.ContainsKey(progressionManager.HD))
          source1[progressionManager.HD] += progressionManager.ProgressionLevel;
        else
          source1.Add(progressionManager.HD, progressionManager.ProgressionLevel);
      }
      else
      {
        CharacterSheetExportContent.ClassExportContent classExportContent1 = new CharacterSheetExportContent.ClassExportContent();
        num1 = progressionManager.ProgressionLevel;
        classExportContent1.Level = num1.ToString();
        classExportContent1.Name = progressionManager.ClassElement.GetAlternateName();
        classExportContent1.Archetype = progressionManager.Elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Archetype")))?.GetAlternateName();
        classExportContent1.HitDie = progressionManager.HD;
        CharacterSheetExportContent.ClassExportContent classExportContent2 = classExportContent1;
        export.Multiclass.Add(classExportContent2);
        if (source1.ContainsKey(progressionManager.HD))
          source1[progressionManager.HD] += progressionManager.ProgressionLevel;
        else
          source1.Add(progressionManager.HD, progressionManager.ProgressionLevel);
      }
    }
    export.IsMulticlass = status.HasMulticlass;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent1 = export.AbilitiesContent;
    int num2 = character.Abilities.Strength.FinalScore;
    string str3 = num2.ToString();
    abilitiesContent1.Strength = str3;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent2 = export.AbilitiesContent;
    num2 = character.Abilities.Dexterity.FinalScore;
    string str4 = num2.ToString();
    abilitiesContent2.Dexterity = str4;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent3 = export.AbilitiesContent;
    num2 = character.Abilities.Constitution.FinalScore;
    string str5 = num2.ToString();
    abilitiesContent3.Constitution = str5;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent4 = export.AbilitiesContent;
    num2 = character.Abilities.Intelligence.FinalScore;
    string str6 = num2.ToString();
    abilitiesContent4.Intelligence = str6;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent5 = export.AbilitiesContent;
    num2 = character.Abilities.Wisdom.FinalScore;
    string str7 = num2.ToString();
    abilitiesContent5.Wisdom = str7;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent6 = export.AbilitiesContent;
    num2 = character.Abilities.Charisma.FinalScore;
    string str8 = num2.ToString();
    abilitiesContent6.Charisma = str8;
    export.AbilitiesContent.StrengthModifier = character.Abilities.Strength.ModifierString;
    export.AbilitiesContent.DexterityModifier = character.Abilities.Dexterity.ModifierString;
    export.AbilitiesContent.ConstitutionModifier = character.Abilities.Constitution.ModifierString;
    export.AbilitiesContent.IntelligenceModifier = character.Abilities.Intelligence.ModifierString;
    export.AbilitiesContent.WisdomModifier = character.Abilities.Wisdom.ModifierString;
    export.AbilitiesContent.CharismaModifier = character.Abilities.Charisma.ModifierString;
    export.AbilitiesContent.StrengthSave = character.SavingThrows.Strength.FinalBonus.ToValueString();
    export.AbilitiesContent.DexteritySave = character.SavingThrows.Dexterity.FinalBonus.ToValueString();
    export.AbilitiesContent.ConstitutionSave = character.SavingThrows.Constitution.FinalBonus.ToValueString();
    export.AbilitiesContent.IntelligenceSave = character.SavingThrows.Intelligence.FinalBonus.ToValueString();
    export.AbilitiesContent.WisdomSave = character.SavingThrows.Wisdom.FinalBonus.ToValueString();
    export.AbilitiesContent.CharismaSave = character.SavingThrows.Charisma.FinalBonus.ToValueString();
    export.AbilitiesContent.StrengthSaveProficient = character.SavingThrows.Strength.IsProficient;
    export.AbilitiesContent.DexteritySaveProficient = character.SavingThrows.Dexterity.IsProficient;
    export.AbilitiesContent.ConstitutionSaveProficient = character.SavingThrows.Constitution.IsProficient;
    export.AbilitiesContent.IntelligenceSaveProficient = character.SavingThrows.Intelligence.IsProficient;
    export.AbilitiesContent.WisdomSaveProficient = character.SavingThrows.Wisdom.IsProficient;
    export.AbilitiesContent.CharismaSaveProficient = character.SavingThrows.Charisma.IsProficient;
    export.SkillsContent.Acrobatics = character.Skills.Acrobatics.FinalBonus.ToValueString();
    export.SkillsContent.AnimalHandling = character.Skills.AnimalHandling.FinalBonus.ToValueString();
    export.SkillsContent.Arcana = character.Skills.Arcana.FinalBonus.ToValueString();
    export.SkillsContent.Athletics = character.Skills.Athletics.FinalBonus.ToValueString();
    export.SkillsContent.Deception = character.Skills.Deception.FinalBonus.ToValueString();
    export.SkillsContent.History = character.Skills.History.FinalBonus.ToValueString();
    export.SkillsContent.Insight = character.Skills.Insight.FinalBonus.ToValueString();
    export.SkillsContent.Intimidation = character.Skills.Intimidation.FinalBonus.ToValueString();
    export.SkillsContent.Investigation = character.Skills.Investigation.FinalBonus.ToValueString();
    export.SkillsContent.Medicine = character.Skills.Medicine.FinalBonus.ToValueString();
    export.SkillsContent.Nature = character.Skills.Nature.FinalBonus.ToValueString();
    export.SkillsContent.Perception = character.Skills.Perception.FinalBonus.ToValueString();
    export.SkillsContent.Performance = character.Skills.Performance.FinalBonus.ToValueString();
    export.SkillsContent.Persuasion = character.Skills.Persuasion.FinalBonus.ToValueString();
    export.SkillsContent.Religion = character.Skills.Religion.FinalBonus.ToValueString();
    export.SkillsContent.SleightOfHand = character.Skills.SleightOfHand.FinalBonus.ToValueString();
    export.SkillsContent.Stealth = character.Skills.Stealth.FinalBonus.ToValueString();
    export.SkillsContent.Survival = character.Skills.Survival.FinalBonus.ToValueString();
    export.SkillsContent.AcrobaticsProficient = character.Skills.Acrobatics.IsProficient;
    export.SkillsContent.AnimalHandlingProficient = character.Skills.AnimalHandling.IsProficient;
    export.SkillsContent.ArcanaProficient = character.Skills.Arcana.IsProficient;
    export.SkillsContent.AthleticsProficient = character.Skills.Athletics.IsProficient;
    export.SkillsContent.DeceptionProficient = character.Skills.Deception.IsProficient;
    export.SkillsContent.HistoryProficient = character.Skills.History.IsProficient;
    export.SkillsContent.InsightProficient = character.Skills.Insight.IsProficient;
    export.SkillsContent.IntimidationProficient = character.Skills.Intimidation.IsProficient;
    export.SkillsContent.InvestigationProficient = character.Skills.Investigation.IsProficient;
    export.SkillsContent.MedicineProficient = character.Skills.Medicine.IsProficient;
    export.SkillsContent.NatureProficient = character.Skills.Nature.IsProficient;
    export.SkillsContent.PerceptionProficient = character.Skills.Perception.IsProficient;
    export.SkillsContent.PerformanceProficient = character.Skills.Performance.IsProficient;
    export.SkillsContent.PersuasionProficient = character.Skills.Persuasion.IsProficient;
    export.SkillsContent.ReligionProficient = character.Skills.Religion.IsProficient;
    export.SkillsContent.SleightOfHandProficient = character.Skills.SleightOfHand.IsProficient;
    export.SkillsContent.StealthProficient = character.Skills.Stealth.IsProficient;
    export.SkillsContent.SurvivalProficient = character.Skills.Survival.IsProficient;
    export.SkillsContent.AcrobaticsExpertise = character.Skills.Acrobatics.IsExpertise(character.Proficiency);
    export.SkillsContent.AnimalHandlingExpertise = character.Skills.AnimalHandling.IsExpertise(character.Proficiency);
    export.SkillsContent.ArcanaExpertise = character.Skills.Arcana.IsExpertise(character.Proficiency);
    export.SkillsContent.AthleticsExpertise = character.Skills.Athletics.IsExpertise(character.Proficiency);
    export.SkillsContent.DeceptionExpertise = character.Skills.Deception.IsExpertise(character.Proficiency);
    export.SkillsContent.HistoryExpertise = character.Skills.History.IsExpertise(character.Proficiency);
    export.SkillsContent.InsightExpertise = character.Skills.Insight.IsExpertise(character.Proficiency);
    export.SkillsContent.IntimidationExpertise = character.Skills.Intimidation.IsExpertise(character.Proficiency);
    export.SkillsContent.InvestigationExpertise = character.Skills.Investigation.IsExpertise(character.Proficiency);
    export.SkillsContent.MedicineExpertise = character.Skills.Medicine.IsExpertise(character.Proficiency);
    export.SkillsContent.NatureExpertise = character.Skills.Nature.IsExpertise(character.Proficiency);
    export.SkillsContent.PerceptionExpertise = character.Skills.Perception.IsExpertise(character.Proficiency);
    export.SkillsContent.PerformanceExpertise = character.Skills.Performance.IsExpertise(character.Proficiency);
    export.SkillsContent.PersuasionExpertise = character.Skills.Persuasion.IsExpertise(character.Proficiency);
    export.SkillsContent.ReligionExpertise = character.Skills.Religion.IsExpertise(character.Proficiency);
    export.SkillsContent.SleightOfHandExpertise = character.Skills.SleightOfHand.IsExpertise(character.Proficiency);
    export.SkillsContent.StealthExpertise = character.Skills.Stealth.IsExpertise(character.Proficiency);
    export.SkillsContent.SurvivalExpertise = character.Skills.Survival.IsExpertise(character.Proficiency);
    export.SkillsContent.PerceptionPassive = $"{statisticValues.GetValue(names.PerceptionPassive) + character.Skills.Perception.FinalBonus}";
    CharacterSheetExportContent.ArmorClassExportContent armorClassContent = export.ArmorClassContent;
    num2 = character.ArmorClass;
    string str9 = num2.ToString();
    armorClassContent.ArmorClass = str9;
    export.ArmorClassContent.StealthDisadvantage = elements.HasElement(InternalGrants.StealthDisadvantage);
    if (inventory.EquippedArmor != null)
    {
      export.ArmorClassContent.EquippedArmor = inventory.EquippedArmor.ToString();
      export.ArmorClassContent.ConditionalArmorClass = statisticValues.GetGroup("ac:misc").GetSummery();
    }
    else if (statisticValues.ContainsGroup("ac:calculation"))
    {
      StatisticValuesGroup group2 = statisticValues.GetGroup("ac:calculation");
      export.ArmorClassContent.EquippedArmor = group2.GetSummery();
      export.ArmorClassContent.ConditionalArmorClass = statisticValues.GetGroup("ac:misc").GetSummery();
    }
    else
      export.ArmorClassContent.ConditionalArmorClass = statisticValues.GetGroup("ac").GetSummery();
    if (inventory.EquippedSecondary != null && inventory.IsEquippedShield())
      export.ArmorClassContent.EquippedShield = inventory.EquippedSecondary.ToString();
    if (!string.IsNullOrWhiteSpace(export.ArmorClassContent.ConditionalArmorClass))
      export.ArmorClassContent.ConditionalArmorClass += Environment.NewLine;
    export.ArmorClassContent.ConditionalArmorClass += character.ConditionalArmorClassField.ToString().TrimEnd();
    CharacterSheetExportContent.HitPointsExportContent hitPointsContent = export.HitPointsContent;
    num2 = character.MaxHp;
    string str10 = num2.ToString();
    hitPointsContent.Maximum = str10;
    export.HitPointsContent.Current = "";
    export.HitPointsContent.Temporary = "";
    export.HitPointsContent.DeathSavingThrowFailure1 = false;
    export.HitPointsContent.DeathSavingThrowFailure2 = false;
    export.HitPointsContent.DeathSavingThrowFailure3 = false;
    export.HitPointsContent.DeathSavingThrowSuccess1 = false;
    export.HitPointsContent.DeathSavingThrowSuccess2 = false;
    export.HitPointsContent.DeathSavingThrowSuccess3 = false;
    export.HitPointsContent.HitDice = string.Join("/", source1.Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Value}{x.Key}")));
    CharacterSheetExportContent.ConditionsExportContent conditionsContent1 = export.ConditionsContent;
    num2 = statisticValues.GetValue(names.Speed);
    string str11 = num2.ToString();
    conditionsContent1.WalkingSpeed = str11;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent2 = export.ConditionsContent;
    num2 = statisticValues.GetValue(names.SpeedFly);
    string str12 = num2.ToString();
    conditionsContent2.FlySpeed = str12;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent3 = export.ConditionsContent;
    num2 = statisticValues.GetValue(names.SpeedClimb);
    string str13 = num2.ToString();
    conditionsContent3.ClimbSpeed = str13;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent4 = export.ConditionsContent;
    num2 = statisticValues.GetValue(names.SpeedSwim);
    string str14 = num2.ToString();
    conditionsContent4.SwimSpeed = str14;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent5 = export.ConditionsContent;
    num2 = statisticValues.GetValue(names.SpeedBurrow);
    string str15 = num2.ToString();
    conditionsContent5.BurrowSpeed = str15;
    IEnumerable<ElementBase> source2 = elements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Vision"))).Distinct<ElementBase>();
    export.ConditionsContent.Vision = string.Join(", ", source2.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)));
    export.ConditionsContent.Exhaustion = "";
    List<ElementBase> list1 = elements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Condition"))).OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)).Distinct<ElementBase>().ToList<ElementBase>();
    List<ElementBase> list2 = list1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Supports.Contains("Resistance"))).ToList<ElementBase>();
    List<ElementBase> list3 = list1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Supports.Contains("Vulnerability"))).ToList<ElementBase>();
    List<ElementBase> list4 = list1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Supports.Contains("Immunity"))).ToList<ElementBase>();
    StringBuilder stringBuilder1 = new StringBuilder();
    if (list2.Any<ElementBase>())
      stringBuilder1.AppendLine("Resistances. " + string.Join(", ", list2.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Resistance", "").Replace("(", "").Replace(")", "").Trim()))));
    if (list3.Any<ElementBase>())
      stringBuilder1.AppendLine("Vulnerabilities. " + string.Join(", ", list3.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Vulnerability", "").Replace("(", "").Replace(")", "").Trim()))));
    if (list4.Any<ElementBase>())
      stringBuilder1.AppendLine("Immunities. " + string.Join(", ", list4.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Immunity", "").Replace("(", "").Replace(")", "").Trim()))));
    export.ConditionsContent.Resistances = stringBuilder1.ToString();
    CharacterSheetExportContent.EquipmentExportContent equipmentContent1 = export.EquipmentContent;
    long num3 = inventory.Coins.Copper;
    string str16 = num3.ToString();
    equipmentContent1.Copper = str16;
    CharacterSheetExportContent.EquipmentExportContent equipmentContent2 = export.EquipmentContent;
    num3 = inventory.Coins.Silver;
    string str17 = num3.ToString();
    equipmentContent2.Silver = str17;
    CharacterSheetExportContent.EquipmentExportContent equipmentContent3 = export.EquipmentContent;
    num3 = inventory.Coins.Electrum;
    string str18 = num3.ToString();
    equipmentContent3.Electrum = str18;
    CharacterSheetExportContent.EquipmentExportContent equipmentContent4 = export.EquipmentContent;
    num3 = inventory.Coins.Gold;
    string str19 = num3.ToString();
    equipmentContent4.Gold = str19;
    CharacterSheetExportContent.EquipmentExportContent equipmentContent5 = export.EquipmentContent;
    num3 = inventory.Coins.Platinum;
    string str20 = num3.ToString();
    equipmentContent5.Platinum = str20;
    export.EquipmentContent.Weight = inventory.EquipmentWeight.ToString();
    foreach (RefactoredEquipmentItem refactoredEquipmentItem in (Collection<RefactoredEquipmentItem>) inventory.Items)
    {
      string str21 = !string.IsNullOrWhiteSpace(refactoredEquipmentItem.AlternativeName) ? refactoredEquipmentItem.AlternativeName : refactoredEquipmentItem.DisplayName;
      List<Tuple<string, string>> equipment = export.EquipmentContent.Equipment;
      num2 = refactoredEquipmentItem.Amount;
      Tuple<string, string> tuple = new Tuple<string, string>(num2.ToString(), str21);
      equipment.Add(tuple);
    }
    export.Initiative = character.Initiative.ToValueString();
    export.InitiativeAdvantage = elements.HasElement(InternalGrants.InitiativeAdvantage);
    foreach (AttackSectionItem attackSectionItem in (Collection<AttackSectionItem>) character.AttacksSection.Items)
    {
      if (attackSectionItem.IsDisplayed)
        export.AttacksContent.Add(new CharacterSheetExportContent.AttackExportContent()
        {
          Name = attackSectionItem.Name.Content,
          Range = attackSectionItem.Range.Content,
          Bonus = attackSectionItem.Attack.Content,
          Damage = attackSectionItem.Damage.Content,
          Description = attackSectionItem.Description.Content,
          Underline = attackSectionItem.EquipmentItem?.GetUnderline(),
          AttackBreakdown = attackSectionItem.DisplayCalculatedAttack,
          DamageBreakdown = attackSectionItem.DisplayCalculatedDamage,
          AsCard = attackSectionItem.IsDisplayedAsCard
        });
    }
    export.AttackAndSpellcastingField = character.AttacksSection.AttacksAndSpellcasting;
    ElementsOrganizer organizer = new ElementsOrganizer((IEnumerable<ElementBase>) elements);
    List<Language> list5 = organizer.GetLanguages(false).ToList<Language>();
    List<ElementBase> list6 = manager.GetProficiencyList((IEnumerable<ElementBase>) organizer.GetArmorProficiencies(false)).ToList<ElementBase>();
    List<ElementBase> list7 = manager.GetProficiencyList((IEnumerable<ElementBase>) organizer.GetWeaponProficiencies(false)).ToList<ElementBase>();
    List<ElementBase> list8 = manager.GetProficiencyList((IEnumerable<ElementBase>) organizer.GetToolProficiencies(false)).ToList<ElementBase>();
    export.Languages = list5.Count <= 0 ? "–" : string.Join(", ", list5.Select<Language, string>((Func<Language, string>) (x => x.Name)));
    if (list6.Count > 0)
    {
      IEnumerable<string> values = list6.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Armor Proficiency", "").Replace("(", "").Replace(")", "").Trim()));
      export.ArmorProficiencies = string.Join(", ", values);
    }
    else
      export.ArmorProficiencies = "–";
    if (list7.Count > 0)
    {
      IEnumerable<string> values = list7.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Weapon Proficiency", "").Replace("(", "").Replace(")", "").Trim()));
      export.WeaponProficiencies = string.Join(", ", values);
    }
    else
      export.WeaponProficiencies = "–";
    if (list8.Count > 0)
    {
      IEnumerable<string> values = list8.Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Name.Replace("Tool Proficiency", "").Replace("(", "").Replace(")", "").Trim()));
      export.ToolProficiencies = string.Join(", ", values);
    }
    else
      export.ToolProficiencies = "–";
    ContentField contentField = new ContentField();
    List<ElementBase> children = new List<ElementBase>();
    foreach (ElementBase excludingRacialTrait in organizer.GetSortedFeaturesExcludingRacialTraits(children))
    {
      ElementBase element = excludingRacialTrait;
      int num4 = character.Level;
      ClassProgressionManager progressionManager = manager.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.GetElements().Contains(element)));
      if (progressionManager != null)
        num4 = progressionManager.ProgressionLevel;
      if (element.SheetDescription.DisplayOnSheet)
      {
        string input = "";
        string usage = element.SheetDescription.HasUsage ? element.SheetDescription.Usage : "";
        string action = element.SheetDescription.HasAction ? element.SheetDescription.Action : "";
        foreach (ElementSheetDescriptions.SheetDescription sheetDescription in (IEnumerable<ElementSheetDescriptions.SheetDescription>) element.SheetDescription.OrderBy<ElementSheetDescriptions.SheetDescription, int>((Func<ElementSheetDescriptions.SheetDescription, int>) (x => x.Level)))
        {
          if (sheetDescription.Level <= num4)
          {
            input = sheetDescription.Description;
            if (sheetDescription.HasUsage)
              usage = sheetDescription.Usage;
            if (sheetDescription.HasAction)
              action = sheetDescription.Action;
          }
        }
        string content = manager.StatisticsCalculator.ReplaceInline(input);
        string str22 = manager.StatisticsCalculator.ReplaceInline(usage);
        string str23 = manager.StatisticsCalculator.ReplaceInline(action);
        bool flag = false;
        if (contentField.Lines.Any<ContentLine>())
          flag = contentField.Lines.Last<ContentLine>().Indent;
        string str24 = "";
        if (!string.IsNullOrWhiteSpace(str23))
          str24 = $"({str23}{(!string.IsNullOrWhiteSpace(str22) ? "—" + str22 : "")})";
        else if (!string.IsNullOrWhiteSpace(str22))
          str24 = $"({str22})";
        string str25 = element.GetAlternateName();
        if (ApplicationManager.Current.Settings.Settings.SheetFormattingActionSuffixBold)
          str25 = string.IsNullOrWhiteSpace(str24) ? str25 : $"{str25} {str24}";
        else
          content = string.IsNullOrWhiteSpace(str24) ? content : $"{str24} {content}";
        contentField.Lines.Add(new ContentLine(str25.Trim(), content, flag || !children.Contains(element), children.Contains(element)));
      }
    }
    if (configuration.IncludeFormatting)
    {
      StringBuilder stringBuilder2 = new StringBuilder();
      foreach (ContentLine line in contentField.Lines)
      {
        if ((!line.NewLineBefore || !line.Indent) && line.NewLineBefore && !string.IsNullOrWhiteSpace(stringBuilder2.ToString()))
          stringBuilder2.Append("<p>&nbsp;</p>");
        string str26 = line.Content.Replace(Environment.NewLine, "<br>&nbsp;  &nbsp;");
        stringBuilder2.Append($"<p>{(line.Indent ? "&nbsp;    &nbsp;" : "")}<strong><em>{line.Name}.</em></strong> {str26}</p>");
      }
      export.Features = stringBuilder2.ToString().Trim();
    }
    else
    {
      StringBuilder stringBuilder3 = new StringBuilder();
      foreach (ContentLine line in contentField.Lines)
      {
        if (line.NewLineBefore)
          stringBuilder3.AppendLine();
        stringBuilder3.AppendLine($"{(line.Indent ? "    " : "")}{line.Name}. {line.Content}");
      }
      export.Features = stringBuilder3.ToString().Trim();
    }
    this.GenerateTemporaryRacialTraitsExportContent(configuration, organizer, character, manager, export);
    export.BackgroundContent.Name = character.Background;
    ElementBase elementBase2 = elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Background Variant")));
    if (elementBase2 != null)
      export.BackgroundContent.Name = elementBase2.GetAlternateName();
    export.BackgroundContent.PersonalityTrait = character.FillableBackgroundCharacteristics.Traits.Content;
    export.BackgroundContent.Ideal = character.FillableBackgroundCharacteristics.Ideals.Content;
    export.BackgroundContent.Bond = character.FillableBackgroundCharacteristics.Bonds.Content;
    export.BackgroundContent.Flaw = character.FillableBackgroundCharacteristics.Flaws.Content;
    export.BackgroundContent.Trinket = character.Trinket.Content;
    export.BackgroundContent.Story = character.BackgroundStory.Content;
    export.BackgroundContent.FeatureName = character.BackgroundFeatureName.ToString();
    export.BackgroundContent.FeatureDescription = character.BackgroundFeatureDescription.ToString();
    export.AppearanceContent.Portrait = character.PortraitFilename;
    export.AppearanceContent.Gender = character.Gender;
    export.AppearanceContent.Age = character.AgeField.Content;
    export.AppearanceContent.Height = character.HeightField.Content;
    export.AppearanceContent.Weight = character.WeightField.Content;
    export.AppearanceContent.Eyes = character.Eyes;
    export.AppearanceContent.Skin = character.Skin;
    export.AppearanceContent.Hair = character.Hair;
    export.AlliesAndOrganizations = character.Allies;
    export.OrganizationName = character.OrganisationName;
    export.OrganizationSymbol = character.OrganisationSymbol;
    export.AdditionalFeaturesAndTraits = character.AdditionalFeatures;
    export.EquipmentContent.AdditionalTreasure = character.Inventory.Treasure;
    export.Treasure = character.Inventory.Treasure;
    return export;
  }

  private void GenerateTemporaryRacialTraitsExportContent(
    CharacterSheetConfiguration configuration,
    ElementsOrganizer organizer,
    Character character,
    CharacterManager manager,
    CharacterSheetExportContent export)
  {
    ContentField contentField = new ContentField();
    List<ElementBase> children = new List<ElementBase>();
    foreach (ElementBase sortedRacialTrait in organizer.GetSortedRacialTraits(children))
    {
      ElementBase element = sortedRacialTrait;
      int num = character.Level;
      ClassProgressionManager progressionManager = manager.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.GetElements().Contains(element)));
      if (progressionManager != null)
        num = progressionManager.ProgressionLevel;
      if (element.SheetDescription.DisplayOnSheet)
      {
        string input = "";
        string usage = element.SheetDescription.HasUsage ? element.SheetDescription.Usage : "";
        string action = element.SheetDescription.HasAction ? element.SheetDescription.Action : "";
        foreach (ElementSheetDescriptions.SheetDescription sheetDescription in (IEnumerable<ElementSheetDescriptions.SheetDescription>) element.SheetDescription.OrderBy<ElementSheetDescriptions.SheetDescription, int>((Func<ElementSheetDescriptions.SheetDescription, int>) (x => x.Level)))
        {
          if (sheetDescription.Level <= num)
          {
            input = sheetDescription.Description;
            if (sheetDescription.HasUsage)
              usage = sheetDescription.Usage;
            if (sheetDescription.HasAction)
              action = sheetDescription.Action;
          }
        }
        string content = manager.StatisticsCalculator.ReplaceInline(input);
        string str1 = manager.StatisticsCalculator.ReplaceInline(usage);
        string str2 = manager.StatisticsCalculator.ReplaceInline(action);
        bool flag = false;
        if (contentField.Lines.Any<ContentLine>())
          flag = contentField.Lines.Last<ContentLine>().Indent;
        string str3 = "";
        if (!string.IsNullOrWhiteSpace(str2))
          str3 = $"({str2}{(!string.IsNullOrWhiteSpace(str1) ? "—" + str1 : "")})";
        else if (!string.IsNullOrWhiteSpace(str1))
          str3 = $"({str1})";
        string str4 = element.GetAlternateName();
        if (ApplicationManager.Current.Settings.Settings.SheetFormattingActionSuffixBold)
          str4 = string.IsNullOrWhiteSpace(str3) ? str4 : $"{str4} {str3}";
        else
          content = string.IsNullOrWhiteSpace(str3) ? content : $"{str3} {content}";
        contentField.Lines.Add(new ContentLine(str4.Trim(), content, flag || !children.Contains(element), children.Contains(element)));
      }
    }
    if (configuration.IncludeFormatting)
    {
      StringBuilder stringBuilder = new StringBuilder();
      foreach (ContentLine line in contentField.Lines)
      {
        if ((!line.NewLineBefore || !line.Indent) && line.NewLineBefore && !string.IsNullOrWhiteSpace(stringBuilder.ToString()))
          stringBuilder.Append("<p>&nbsp;</p>");
        string str = line.Content.Replace(Environment.NewLine, "<br>&nbsp;  &nbsp;");
        stringBuilder.Append($"<p>{(line.Indent ? "&nbsp;    &nbsp;" : "")}<strong><em>{line.Name}.</em></strong> {str}</p>");
      }
      stringBuilder.Append("<p>&nbsp;</p>");
      export.TemporaryRacialTraits = stringBuilder.ToString();
    }
    else
    {
      StringBuilder stringBuilder = new StringBuilder();
      foreach (ContentLine line in contentField.Lines)
      {
        if (line.NewLineBefore)
          stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{(line.Indent ? "    " : "")}{line.Name}. {line.Content}");
      }
      stringBuilder.AppendLine();
      export.TemporaryRacialTraits = stringBuilder.ToString();
    }
  }

  private void GenerateCompanionSheetExportContent(
    CharacterManager manager,
    CharacterSheetConfiguration configuration,
    CharacterSheetExportContent export)
  {
    Companion companion = manager.Character.Companion;
    StatisticValuesGroupCollection statisticValues = manager.StatisticsCalculator.StatisticValues;
    export.CompanionName = companion.CompanionName.Content;
    export.CompanionKind = companion.Element.Name;
    export.CompanionBuild = companion.DisplayBuild;
    export.CompanionChallenge = companion.Element.Challenge;
    export.CompanionAppearance = "";
    export.CompanionSkills = companion.Element.Skills;
    export.CompanionInitiative = companion.Statistics.Initiative.ToValueString();
    export.CompanionProficiency = companion.Statistics.Proficiency.ToString();
    export.CompanionPortrait = companion.Portrait.Content;
    export.CompanionSpeedString = $"{companion.Statistics.Speed} ft.";
    if (companion.Statistics.SpeedFly > 0)
      export.CompanionSpeedString += $", fly {companion.Statistics.SpeedFly} ft.";
    if (companion.Statistics.SpeedClimb > 0)
      export.CompanionSpeedString += $", climb {companion.Statistics.SpeedClimb} ft.";
    if (companion.Statistics.SpeedSwim > 0)
      export.CompanionSpeedString += $", swim {companion.Statistics.SpeedSwim} ft.";
    if (companion.Statistics.SpeedBurrow > 0)
      export.CompanionSpeedString += $", burrow {companion.Statistics.SpeedBurrow} ft.";
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent1 = export.CompanionAbilitiesContent;
    int num = companion.Abilities.Strength.FinalScore;
    string str1 = num.ToString();
    abilitiesContent1.Strength = str1;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent2 = export.CompanionAbilitiesContent;
    num = companion.Abilities.Dexterity.FinalScore;
    string str2 = num.ToString();
    abilitiesContent2.Dexterity = str2;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent3 = export.CompanionAbilitiesContent;
    num = companion.Abilities.Constitution.FinalScore;
    string str3 = num.ToString();
    abilitiesContent3.Constitution = str3;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent4 = export.CompanionAbilitiesContent;
    num = companion.Abilities.Intelligence.FinalScore;
    string str4 = num.ToString();
    abilitiesContent4.Intelligence = str4;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent5 = export.CompanionAbilitiesContent;
    num = companion.Abilities.Wisdom.FinalScore;
    string str5 = num.ToString();
    abilitiesContent5.Wisdom = str5;
    CharacterSheetExportContent.AbilitiesExportContent abilitiesContent6 = export.CompanionAbilitiesContent;
    num = companion.Abilities.Charisma.FinalScore;
    string str6 = num.ToString();
    abilitiesContent6.Charisma = str6;
    export.CompanionAbilitiesContent.StrengthModifier = companion.Abilities.Strength.ModifierString;
    export.CompanionAbilitiesContent.DexterityModifier = companion.Abilities.Dexterity.ModifierString;
    export.CompanionAbilitiesContent.ConstitutionModifier = companion.Abilities.Constitution.ModifierString;
    export.CompanionAbilitiesContent.IntelligenceModifier = companion.Abilities.Intelligence.ModifierString;
    export.CompanionAbilitiesContent.WisdomModifier = companion.Abilities.Wisdom.ModifierString;
    export.CompanionAbilitiesContent.CharismaModifier = companion.Abilities.Charisma.ModifierString;
    export.CompanionAbilitiesContent.StrengthSave = companion.SavingThrows.Strength.FinalBonus.ToValueString();
    export.CompanionAbilitiesContent.DexteritySave = companion.SavingThrows.Dexterity.FinalBonus.ToValueString();
    export.CompanionAbilitiesContent.ConstitutionSave = companion.SavingThrows.Constitution.FinalBonus.ToValueString();
    export.CompanionAbilitiesContent.IntelligenceSave = companion.SavingThrows.Intelligence.FinalBonus.ToValueString();
    export.CompanionAbilitiesContent.WisdomSave = companion.SavingThrows.Wisdom.FinalBonus.ToValueString();
    export.CompanionAbilitiesContent.CharismaSave = companion.SavingThrows.Charisma.FinalBonus.ToValueString();
    export.CompanionAbilitiesContent.StrengthSaveProficient = companion.SavingThrows.Strength.IsProficient;
    export.CompanionAbilitiesContent.DexteritySaveProficient = companion.SavingThrows.Dexterity.IsProficient;
    export.CompanionAbilitiesContent.ConstitutionSaveProficient = companion.SavingThrows.Constitution.IsProficient;
    export.CompanionAbilitiesContent.IntelligenceSaveProficient = companion.SavingThrows.Intelligence.IsProficient;
    export.CompanionAbilitiesContent.WisdomSaveProficient = companion.SavingThrows.Wisdom.IsProficient;
    export.CompanionAbilitiesContent.CharismaSaveProficient = companion.SavingThrows.Charisma.IsProficient;
    export.CompanionSkillsContent.Acrobatics = companion.Skills.Acrobatics.FinalBonus.ToValueString();
    export.CompanionSkillsContent.AnimalHandling = companion.Skills.AnimalHandling.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Arcana = companion.Skills.Arcana.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Athletics = companion.Skills.Athletics.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Deception = companion.Skills.Deception.FinalBonus.ToValueString();
    export.CompanionSkillsContent.History = companion.Skills.History.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Insight = companion.Skills.Insight.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Intimidation = companion.Skills.Intimidation.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Investigation = companion.Skills.Investigation.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Medicine = companion.Skills.Medicine.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Nature = companion.Skills.Nature.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Perception = companion.Skills.Perception.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Performance = companion.Skills.Performance.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Persuasion = companion.Skills.Persuasion.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Religion = companion.Skills.Religion.FinalBonus.ToValueString();
    export.CompanionSkillsContent.SleightOfHand = companion.Skills.SleightOfHand.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Stealth = companion.Skills.Stealth.FinalBonus.ToValueString();
    export.CompanionSkillsContent.Survival = companion.Skills.Survival.FinalBonus.ToValueString();
    export.CompanionSkillsContent.AcrobaticsProficient = companion.Skills.Acrobatics.IsProficient;
    export.CompanionSkillsContent.AnimalHandlingProficient = companion.Skills.AnimalHandling.IsProficient;
    export.CompanionSkillsContent.ArcanaProficient = companion.Skills.Arcana.IsProficient;
    export.CompanionSkillsContent.AthleticsProficient = companion.Skills.Athletics.IsProficient;
    export.CompanionSkillsContent.DeceptionProficient = companion.Skills.Deception.IsProficient;
    export.CompanionSkillsContent.HistoryProficient = companion.Skills.History.IsProficient;
    export.CompanionSkillsContent.InsightProficient = companion.Skills.Insight.IsProficient;
    export.CompanionSkillsContent.IntimidationProficient = companion.Skills.Intimidation.IsProficient;
    export.CompanionSkillsContent.InvestigationProficient = companion.Skills.Investigation.IsProficient;
    export.CompanionSkillsContent.MedicineProficient = companion.Skills.Medicine.IsProficient;
    export.CompanionSkillsContent.NatureProficient = companion.Skills.Nature.IsProficient;
    export.CompanionSkillsContent.PerceptionProficient = companion.Skills.Perception.IsProficient;
    export.CompanionSkillsContent.PerformanceProficient = companion.Skills.Performance.IsProficient;
    export.CompanionSkillsContent.PersuasionProficient = companion.Skills.Persuasion.IsProficient;
    export.CompanionSkillsContent.ReligionProficient = companion.Skills.Religion.IsProficient;
    export.CompanionSkillsContent.SleightOfHandProficient = companion.Skills.SleightOfHand.IsProficient;
    export.CompanionSkillsContent.StealthProficient = companion.Skills.Stealth.IsProficient;
    export.CompanionSkillsContent.SurvivalProficient = companion.Skills.Survival.IsProficient;
    export.CompanionSkillsContent.AcrobaticsExpertise = companion.Skills.Acrobatics.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.AnimalHandlingExpertise = companion.Skills.AnimalHandling.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.ArcanaExpertise = companion.Skills.Arcana.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.AthleticsExpertise = companion.Skills.Athletics.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.DeceptionExpertise = companion.Skills.Deception.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.HistoryExpertise = companion.Skills.History.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.InsightExpertise = companion.Skills.Insight.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.IntimidationExpertise = companion.Skills.Intimidation.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.InvestigationExpertise = companion.Skills.Investigation.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.MedicineExpertise = companion.Skills.Medicine.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.NatureExpertise = companion.Skills.Nature.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.PerceptionExpertise = companion.Skills.Perception.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.PerformanceExpertise = companion.Skills.Performance.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.PersuasionExpertise = companion.Skills.Persuasion.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.ReligionExpertise = companion.Skills.Religion.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.SleightOfHandExpertise = companion.Skills.SleightOfHand.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.StealthExpertise = companion.Skills.Stealth.IsExpertise(companion.Statistics.Proficiency);
    export.CompanionSkillsContent.SurvivalExpertise = companion.Skills.Survival.IsExpertise(companion.Statistics.Proficiency);
    CharacterSheetExportContent.SkillsExportContent companionSkillsContent = export.CompanionSkillsContent;
    num = companion.Skills.Perception.FinalPassiveBonus;
    string str7 = num.ToString();
    companionSkillsContent.PerceptionPassive = str7;
    CharacterSheetExportContent.HitPointsExportContent hitPointsContent = export.CompanionHitPointsContent;
    num = companion.Statistics.MaxHp;
    string str8 = num.ToString();
    hitPointsContent.Maximum = str8;
    CharacterSheetExportContent.ArmorClassExportContent armorClassContent = export.CompanionArmorClassContent;
    num = companion.Statistics.ArmorClass;
    string str9 = num.ToString();
    armorClassContent.ArmorClass = str9;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent1 = export.CompanionConditionsContent;
    num = companion.Statistics.Speed;
    string str10 = num.ToString();
    conditionsContent1.WalkingSpeed = str10;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent2 = export.CompanionConditionsContent;
    num = companion.Statistics.SpeedFly;
    string str11 = num.ToString();
    conditionsContent2.FlySpeed = str11;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent3 = export.CompanionConditionsContent;
    num = companion.Statistics.SpeedClimb;
    string str12 = num.ToString();
    conditionsContent3.ClimbSpeed = str12;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent4 = export.CompanionConditionsContent;
    num = companion.Statistics.SpeedSwim;
    string str13 = num.ToString();
    conditionsContent4.SwimSpeed = str13;
    CharacterSheetExportContent.ConditionsExportContent conditionsContent5 = export.CompanionConditionsContent;
    num = companion.Statistics.SpeedBurrow;
    string str14 = num.ToString();
    conditionsContent5.BurrowSpeed = str14;
    export.CompanionFeatures = this.GetCompanionFeaturesContent(configuration, companion, statisticValues, manager.StatisticsCalculator.InlineValues);
    export.CompanionStats = this.GetCompanionStatsContent(configuration, companion, statisticValues, manager.StatisticsCalculator.InlineValues);
    export.CompanionOwner = companion.Element.Aquisition.GetParentHeader().Name ?? manager.Character.Name;
  }

  private CharacterSheetSpellcastingPageExportContent GenerateSpellcastingExportContent(
    CharacterManager manager,
    SpellcastingInformation information,
    List<string> addedSpells)
  {
    CharacterSheetSpellcastingPageExportContent spellcastingExportContent = new CharacterSheetSpellcastingPageExportContent();
    Character character = manager.Character;
    StatisticValuesGroupCollection statisticValues = manager.StatisticsCalculator.StatisticValues;
    CharacterStatus status = manager.Status;
    SpellcasterSelectionControlViewModel sectionViewModel = SpellcastingSectionHandler.Current.GetSpellcasterSectionViewModel(information.UniqueIdentifier);
    IOrderedEnumerable<SelectionElement> spells = SpellcastingSectionHandler.Current.GetSpells(information);
    spellcastingExportContent.SpellcastingClass = information.Name;
    try
    {
      ClassProgressionManager progressionManager = manager.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (c => c.SpellcastingInformations.Select<SpellcastingInformation, string>((Func<SpellcastingInformation, string>) (x => x.UniqueIdentifier)).Contains<string>(information.UniqueIdentifier)));
      ElementBase elementBase = progressionManager.HasArchetype() ? progressionManager.GetElements().FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Archetype"))) : (ElementBase) null;
      spellcastingExportContent.SpellcastingArchetype = elementBase?.Name ?? "";
    }
    catch (Exception ex)
    {
    }
    spellcastingExportContent.Ability = information.AbilityName;
    spellcastingExportContent.AttackBonus = sectionViewModel.InformationHeader.SpellAttackModifier.ToValueString();
    CharacterSheetSpellcastingPageExportContent pageExportContent1 = spellcastingExportContent;
    int num1 = sectionViewModel.InformationHeader.SpellSaveDc;
    string str1 = num1.ToString();
    pageExportContent1.Save = str1;
    CharacterSheetSpellcastingPageExportContent pageExportContent2 = spellcastingExportContent;
    string str2;
    if (!information.Prepare)
    {
      str2 = "N/A";
    }
    else
    {
      num1 = statisticValues.GetValue(information.GetPrepareAmountStatisticName());
      str2 = num1.ToString();
    }
    pageExportContent2.PrepareCount = str2;
    spellcastingExportContent.Notes = "";
    spellcastingExportContent.IsMulticlassSpellcaster = status.HasMulticlassSpellSlots;
    spellcastingExportContent.Spells1.AvailableSlots = sectionViewModel.InformationHeader.Slot1;
    spellcastingExportContent.Spells2.AvailableSlots = sectionViewModel.InformationHeader.Slot2;
    spellcastingExportContent.Spells3.AvailableSlots = sectionViewModel.InformationHeader.Slot3;
    spellcastingExportContent.Spells4.AvailableSlots = sectionViewModel.InformationHeader.Slot4;
    spellcastingExportContent.Spells5.AvailableSlots = sectionViewModel.InformationHeader.Slot5;
    spellcastingExportContent.Spells6.AvailableSlots = sectionViewModel.InformationHeader.Slot6;
    spellcastingExportContent.Spells7.AvailableSlots = sectionViewModel.InformationHeader.Slot7;
    spellcastingExportContent.Spells8.AvailableSlots = sectionViewModel.InformationHeader.Slot8;
    spellcastingExportContent.Spells9.AvailableSlots = sectionViewModel.InformationHeader.Slot9;
    spellcastingExportContent.Cantrips.Level = 0;
    spellcastingExportContent.Spells1.Level = 1;
    spellcastingExportContent.Spells2.Level = 2;
    spellcastingExportContent.Spells3.Level = 3;
    spellcastingExportContent.Spells4.Level = 4;
    spellcastingExportContent.Spells5.Level = 5;
    spellcastingExportContent.Spells6.Level = 6;
    spellcastingExportContent.Spells7.Level = 7;
    spellcastingExportContent.Spells8.Level = 8;
    spellcastingExportContent.Spells9.Level = 9;
    if (status.HasMulticlassSpellSlots)
    {
      int slot1 = character.MulticlassSpellSlots.Slot1;
      int slot2 = character.MulticlassSpellSlots.Slot2;
      int slot3 = character.MulticlassSpellSlots.Slot3;
      int slot4 = character.MulticlassSpellSlots.Slot4;
      int slot5 = character.MulticlassSpellSlots.Slot5;
      int slot6 = character.MulticlassSpellSlots.Slot6;
      int slot7 = character.MulticlassSpellSlots.Slot7;
      int slot8 = character.MulticlassSpellSlots.Slot8;
      int slot9 = character.MulticlassSpellSlots.Slot9;
      spellcastingExportContent.Spells1.AvailableSlots = slot1;
      spellcastingExportContent.Spells2.AvailableSlots = slot2;
      spellcastingExportContent.Spells3.AvailableSlots = slot3;
      spellcastingExportContent.Spells4.AvailableSlots = slot4;
      spellcastingExportContent.Spells5.AvailableSlots = slot5;
      spellcastingExportContent.Spells6.AvailableSlots = slot6;
      spellcastingExportContent.Spells7.AvailableSlots = slot7;
      spellcastingExportContent.Spells8.AvailableSlots = slot8;
      spellcastingExportContent.Spells9.AvailableSlots = slot9;
    }
    bool nonPreparedSpells = ApplicationManager.Current.Settings.Settings.SheetIncludeNonPreparedSpells;
    foreach (SelectionElement selectionElement in (IEnumerable<SelectionElement>) spells)
    {
      Spell spell = selectionElement.Element.AsElement<Spell>();
      int num2;
      if (!spell.Aquisition.WasGranted)
      {
        num2 = 0;
      }
      else
      {
        ElementSetters.Setter setter = spell.Aquisition.GrantRule.Setters.GetSetter("prepared");
        num2 = setter != null ? (setter.ValueAsBool() ? 1 : 0) : 0;
      }
      bool flag = num2 != 0;
      CharacterSheetSpellcastingPageExportContent.SpellExportContent spellExportContent1 = new CharacterSheetSpellcastingPageExportContent.SpellExportContent();
      spellExportContent1.IsPrepared = selectionElement.IsChosen;
      spellExportContent1.AlwaysPrepared = flag;
      spellExportContent1.Name = spell.Name;
      spellExportContent1.CastingTime = spell.CastingTime;
      spellExportContent1.Description = ElementDescriptionGenerator.GeneratePlainDescription(spell.Description);
      spellExportContent1.Range = spell.Range;
      num1 = spell.Level;
      spellExportContent1.Level = num1.ToString();
      spellExportContent1.Duration = spell.Duration;
      spellExportContent1.Components = spell.GetComponentsString();
      spellExportContent1.Concentration = spell.IsConcentration;
      spellExportContent1.Ritual = spell.IsRitual;
      spellExportContent1.School = spell.MagicSchool;
      spellExportContent1.Subtitle = spell.Underline;
      CharacterSheetSpellcastingPageExportContent.SpellExportContent spellExportContent2 = spellExportContent1;
      if (spell.Level <= 0 || !information.Prepare || spellExportContent2.IsPrepared || nonPreparedSpells)
      {
        addedSpells.Add(spell.Name);
        num1 = spell.Level;
        switch (num1)
        {
          case 0:
            spellcastingExportContent.Cantrips.Spells.Add(spellExportContent2);
            continue;
          case 1:
            spellcastingExportContent.Spells1.Spells.Add(spellExportContent2);
            continue;
          case 2:
            spellcastingExportContent.Spells2.Spells.Add(spellExportContent2);
            continue;
          case 3:
            spellcastingExportContent.Spells3.Spells.Add(spellExportContent2);
            continue;
          case 4:
            spellcastingExportContent.Spells4.Spells.Add(spellExportContent2);
            continue;
          case 5:
            spellcastingExportContent.Spells5.Spells.Add(spellExportContent2);
            continue;
          case 6:
            spellcastingExportContent.Spells6.Spells.Add(spellExportContent2);
            continue;
          case 7:
            spellcastingExportContent.Spells7.Spells.Add(spellExportContent2);
            continue;
          case 8:
            spellcastingExportContent.Spells8.Spells.Add(spellExportContent2);
            continue;
          case 9:
            spellcastingExportContent.Spells9.Spells.Add(spellExportContent2);
            continue;
          default:
            continue;
        }
      }
    }
    return spellcastingExportContent;
  }

  private CharacterSheetSpellcastingPageExportContent GenerateOtherSpellcastingExportContent(
    CharacterManager manager,
    List<string> includedSpells)
  {
    CharacterSheetSpellcastingPageExportContent spellcastingExportContent = new CharacterSheetSpellcastingPageExportContent();
    List<ElementBase> list1 = manager.GetElements().ToList<ElementBase>();
    List<ElementBase> list2 = list1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Spell") && !includedSpells.Contains(x.Name))).ToList<ElementBase>();
    if (!list2.Any<ElementBase>())
      return (CharacterSheetSpellcastingPageExportContent) null;
    spellcastingExportContent.SpellcastingClass = "Other Spells";
    spellcastingExportContent.Cantrips.Level = 0;
    spellcastingExportContent.Spells1.Level = 1;
    spellcastingExportContent.Spells2.Level = 2;
    spellcastingExportContent.Spells3.Level = 3;
    spellcastingExportContent.Spells4.Level = 4;
    spellcastingExportContent.Spells5.Level = 5;
    spellcastingExportContent.Spells6.Level = 6;
    spellcastingExportContent.Spells7.Level = 7;
    spellcastingExportContent.Spells8.Level = 8;
    spellcastingExportContent.Spells9.Level = 9;
    foreach (Spell spell in list2.Cast<Spell>())
    {
      CharacterSheetSpellcastingPageExportContent.SpellExportContent spellExportContent = new CharacterSheetSpellcastingPageExportContent.SpellExportContent()
      {
        IsPrepared = false,
        AlwaysPrepared = false,
        Name = spell.Name,
        CastingTime = spell.CastingTime,
        Description = ElementDescriptionGenerator.GeneratePlainDescription(spell.Description),
        Range = spell.Range,
        Level = spell.Level.ToString(),
        Duration = spell.Duration,
        Components = spell.GetComponentsString(),
        Concentration = spell.IsConcentration,
        Ritual = spell.IsRitual,
        School = spell.MagicSchool,
        Subtitle = spell.Underline
      };
      try
      {
        string str = "";
        ElementHeader parent = spell.Aquisition.GetParentHeader();
        if (parent != null)
        {
          str = parent.Name;
          if (parent.Name.StartsWith("Additional Spell,") || parent.Name.StartsWith("Additional ") && parent.Name.Contains("Spell,"))
            str = parent.Name.Replace(spell.Name, "").Trim().Trim(',').Trim();
          if (parent.Type.Equals("Racial Trait"))
          {
            ElementHeader parentHeader = list1.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id == parent.Id))?.Aquisition.GetParentHeader();
            if (parentHeader != null)
              str = parentHeader.Name;
          }
        }
        if (!string.IsNullOrWhiteSpace(str))
          spellExportContent.Name = $"{spell.Name} ({str})";
      }
      catch (Exception ex)
      {
        Logger.Exception(ex, nameof (GenerateOtherSpellcastingExportContent));
        AnalyticsErrorHelper.Exception(ex, method: nameof (GenerateOtherSpellcastingExportContent), line: 2917);
      }
      switch (spell.Level)
      {
        case 0:
          spellcastingExportContent.Cantrips.Spells.Add(spellExportContent);
          continue;
        case 1:
          spellcastingExportContent.Spells1.Spells.Add(spellExportContent);
          continue;
        case 2:
          spellcastingExportContent.Spells2.Spells.Add(spellExportContent);
          continue;
        case 3:
          spellcastingExportContent.Spells3.Spells.Add(spellExportContent);
          continue;
        case 4:
          spellcastingExportContent.Spells4.Spells.Add(spellExportContent);
          continue;
        case 5:
          spellcastingExportContent.Spells5.Spells.Add(spellExportContent);
          continue;
        case 6:
          spellcastingExportContent.Spells6.Spells.Add(spellExportContent);
          continue;
        case 7:
          spellcastingExportContent.Spells7.Spells.Add(spellExportContent);
          continue;
        case 8:
          spellcastingExportContent.Spells8.Spells.Add(spellExportContent);
          continue;
        case 9:
          spellcastingExportContent.Spells9.Spells.Add(spellExportContent);
          continue;
        default:
          continue;
      }
    }
    return spellcastingExportContent;
  }

  private string GetCompanionFeaturesContent(
    CharacterSheetConfiguration configuration,
    Companion companion,
    StatisticValuesGroupCollection statistics,
    Dictionary<string, string> inlineStatistics)
  {
    List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Companion Trait") || x.Type.Equals("Companion Action") || x.Type.Equals("Companion Reaction"))).ToList<ElementBase>();
    List<ElementBase> elementBaseList = new List<ElementBase>();
    foreach (string trait1 in companion.Element.Traits)
    {
      string trait = trait1;
      ElementBase elementBase = list.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(trait)));
      if (elementBase != null)
        elementBaseList.Add(elementBase);
    }
    foreach (string action1 in companion.Element.Actions)
    {
      string action = action1;
      ElementBase elementBase = list.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(action)));
      if (elementBase != null)
        elementBaseList.Add(elementBase);
    }
    foreach (string reaction1 in companion.Element.Reactions)
    {
      string reaction = reaction1;
      ElementBase elementBase = list.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(reaction)));
      if (elementBase != null)
        elementBaseList.Add(elementBase);
    }
    ContentField contentField = new ContentField();
    foreach (ElementBase elementBase in elementBaseList)
    {
      if (elementBase.SheetDescription.DisplayOnSheet)
      {
        string str1 = elementBase.SheetDescription[0]?.Description ?? "";
        foreach (Match match in Regex.Matches(str1, "\\$\\((.*?)\\)"))
        {
          string oldValue = match.Value;
          string str2 = match.Value.Substring(2, match.Value.Length - 3);
          string newValue = $"==INVALID REPLACEMENT: {oldValue} VALUE==";
          if (statistics.ContainsGroup(str2))
            newValue = statistics.GetValue(str2).ToString();
          else if (inlineStatistics.ContainsKey(str2))
            newValue = inlineStatistics[str2];
          else
            Logger.Warning($"UNKNOWN REPLACE STRING: {oldValue} in {elementBase}");
          str1 = str1.Replace(oldValue, newValue);
        }
        string name = elementBase.GetAlternateName();
        if (elementBase.SheetDescription.HasUsage)
          name = $"{name} ({elementBase.SheetDescription.Usage})";
        contentField.Lines.Add(new ContentLine(name, str1, contentField.Lines.Any<ContentLine>()));
      }
    }
    StringBuilder stringBuilder = new StringBuilder();
    if (configuration.IncludeFormatting)
    {
      foreach (ContentLine line in contentField.Lines)
      {
        if (line.NewLineBefore && !string.IsNullOrWhiteSpace(stringBuilder.ToString()))
          stringBuilder.Append("<p>&nbsp;</p>");
        string str = line.Content.Replace(Environment.NewLine, "<br>&nbsp;  &nbsp;");
        stringBuilder.Append($"<p>{(line.Indent ? "&nbsp;    &nbsp;" : "")}<strong><em>{line.Name}.</em></strong> {str}</p>");
      }
    }
    else
    {
      foreach (ContentLine line in contentField.Lines)
      {
        if (line.NewLineBefore)
          stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{(line.Indent ? "    " : "")}{line.Name}. {line.Content}");
      }
    }
    return stringBuilder.ToString().Trim();
  }

  private string GetCompanionStatsContent(
    CharacterSheetConfiguration configuration,
    Companion companion,
    StatisticValuesGroupCollection statistics,
    Dictionary<string, string> inlineStatistics)
  {
    ContentField contentField = new ContentField();
    string str1 = string.Join(", ", companion.SavingThrows.GetCollection().Where<SavingThrowItem>((Func<SavingThrowItem, bool>) (x => x.IsProficient)).Select<SavingThrowItem, string>((Func<SavingThrowItem, string>) (x => $"{x.Name} {x.FinalBonus.ToValueString()}")));
    if (!string.IsNullOrWhiteSpace(str1))
    {
      string content = str1.Replace(" Saving Throw", "");
      contentField.Lines.Add(new ContentLine("Saving Throws", content, contentField.Lines.Any<ContentLine>()));
    }
    string content1 = string.Join(", ", companion.Skills.GetCollection().Where<SkillItem>((Func<SkillItem, bool>) (x => x.IsProficient)).Select<SkillItem, string>((Func<SkillItem, string>) (x => $"{x.Name} {x.FinalBonus.ToValueString()}")));
    if (!string.IsNullOrWhiteSpace(content1))
      contentField.Lines.Add(new ContentLine("Skills", content1, contentField.Lines.Any<ContentLine>()));
    if (companion.Element.HasDamageVulnerabilities)
      contentField.Lines.Add(new ContentLine("Damage Vulnerabilities", companion.Element.DamageVulnerabilities, contentField.Lines.Any<ContentLine>()));
    if (companion.Element.HasDamageResistances)
      contentField.Lines.Add(new ContentLine("Damage Resistances", companion.Element.DamageResistances, contentField.Lines.Any<ContentLine>()));
    if (companion.Element.HasDamageImmunities)
      contentField.Lines.Add(new ContentLine("Damage Immunities", companion.Element.DamageImmunities, contentField.Lines.Any<ContentLine>()));
    if (companion.Element.HasConditionVulnerabilities)
      contentField.Lines.Add(new ContentLine("Condition Vulnerabilities", companion.Element.ConditionVulnerabilities, contentField.Lines.Any<ContentLine>()));
    if (companion.Element.HasConditionResistances)
      contentField.Lines.Add(new ContentLine("Condition Resistances", companion.Element.ConditionResistances, contentField.Lines.Any<ContentLine>()));
    if (companion.Element.HasConditionImmunities)
      contentField.Lines.Add(new ContentLine("Condition Immunities", companion.Element.ConditionImmunities, contentField.Lines.Any<ContentLine>()));
    if (companion.Element.HasSenses)
      contentField.Lines.Add(new ContentLine("Senses", companion.Element.Senses, contentField.Lines.Any<ContentLine>()));
    contentField.Lines.Add(new ContentLine("Languages", companion.Element.Languages, contentField.Lines.Any<ContentLine>()));
    StringBuilder stringBuilder = new StringBuilder();
    if (configuration.IncludeFormatting)
    {
      foreach (ContentLine line in contentField.Lines)
      {
        if (line.NewLineBefore && !string.IsNullOrWhiteSpace(stringBuilder.ToString()))
          stringBuilder.Append("<p>&nbsp;</p>");
        string str2 = line.Content.Replace(Environment.NewLine, "<br>&nbsp;  &nbsp;");
        stringBuilder.Append($"<p>{(line.Indent ? "&nbsp;    &nbsp;" : "")}<strong><em>{line.Name}.</em></strong> {str2}</p>");
      }
    }
    else
    {
      foreach (ContentLine line in contentField.Lines)
      {
        if (line.NewLineBefore)
          stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{(line.Indent ? "    " : "")}{line.Name}. {line.Content}");
      }
    }
    return stringBuilder.ToString().Trim();
  }
}
