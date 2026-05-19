// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.CharacterManager
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Rules;
using Builder.Data.Strings;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Events.Global;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Views;
using Builder.Presentation.Extensions;
using Builder.Presentation.Models;
using Builder.Presentation.Models.Collections;

using Builder.Presentation.Services;
using Builder.Presentation.Services.Calculator;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Sources;
using Builder.Presentation.Interfaces;
using Builder.Presentation.UserControls.Spellcasting;
using Builder.Presentation.Utilities;
using Builder.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

#nullable disable
namespace Builder.Presentation;

public sealed class CharacterManager
{
  private readonly IEventAggregator _eventAggregator;
  private readonly ProgressionManager _progressionManager;
  private static Random _rnd = new Random();
#pragma warning disable CS8632
  private ElementBaseCollection? _elementsCache;
#pragma warning restore CS8632
  private bool _elementsCacheDirty = true;
  private bool _suppressSetCharacterDetails = false;

  private CharacterManager()
  {
    this._eventAggregator = ApplicationContext.Current.EventAggregator;
    this._progressionManager = new ProgressionManager();
    this._progressionManager.SelectionRuleCreated += new EventHandler<SelectRule>(this._progressionManager_SelectionRuleCreated);
    this._progressionManager.SelectionRuleRemoved += new EventHandler<SelectRule>(this._progressionManager_SelectionRuleRemoved);
    this._progressionManager.SpellcastingInformationCreated += new EventHandler<SpellcastingInformation>(this._progressionManager_SpellcastingSectionCreated);
    this._progressionManager.SpellcastingInformationRemoved += new EventHandler<SpellcastingInformation>(this._progressionManager_SpellcastingSectionRemoved);
    this.StatisticsCalculator = new StatisticsHandler2(this);
    this.Character.PropertyChanged += new PropertyChangedEventHandler(this.Character_PropertyChanged);
  }

  private void _progressionManager_SelectionRuleCreated(object sender, SelectRule e)
  {
    this._eventAggregator.Send<CharacterManagerSelectionRuleCreated>(new CharacterManagerSelectionRuleCreated(e));
  }

  private void _progressionManager_SelectionRuleRemoved(object sender, SelectRule e)
  {
    this._eventAggregator.Send<CharacterManagerSelectionRuleDeleted>(new CharacterManagerSelectionRuleDeleted(e));
  }

  private void _progressionManager_SpellcastingSectionCreated(
    object sender,
    SpellcastingInformation e)
  {
    this._eventAggregator.Send<SpellcastingInformationCreatedEvent>(new SpellcastingInformationCreatedEvent(e));
    if (sender is ProgressionManager && !(sender is ClassProgressionManager) && e.IsExtension)
    {
      foreach (SpellcastingInformation spellcastingInformation in this.GetSpellcastingInformations())
      {
        if (!spellcastingInformation.IsExtension && (spellcastingInformation.Name.Equals(e.Name, StringComparison.OrdinalIgnoreCase) || e.AssignToAllSpellcastingClasses))
          spellcastingInformation.MergeExtended(e.ExtendedSupportedSpellsExpressions);
      }
    }
    this.Status.HasSpellcasting = this.GetSpellcastingInformations().Any<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => !x.IsExtension));
  }

  private void _progressionManager_SpellcastingSectionRemoved(
    object sender,
    SpellcastingInformation e)
  {
    this._eventAggregator.Send<SpellcastingInformationRemovedEvent>(new SpellcastingInformationRemovedEvent(e));
    if (sender is ProgressionManager && !(sender is ClassProgressionManager) && e.IsExtension)
    {
      foreach (SpellcastingInformation spellcastingInformation in this.GetSpellcastingInformations())
      {
        if (!spellcastingInformation.IsExtension && (spellcastingInformation.Name.Equals(e.Name, StringComparison.OrdinalIgnoreCase) || e.AssignToAllSpellcastingClasses))
          spellcastingInformation.Unmerge(e.ExtendedSupportedSpellsExpressions);
      }
    }
    this.Status.HasSpellcasting = this.GetSpellcastingInformations().Any<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => !x.IsExtension));
  }

  private void Character_PropertyChanged(object sender, PropertyChangedEventArgs e)
  {
    switch (e.PropertyName)
    {
      case "Experience":
        if (this.Status.IsLoaded)
        {
          LevelElement upcomingLevelElement = this.GetUpcomingLevelElement();
          if (upcomingLevelElement == null || this.Character.Experience < upcomingLevelElement.RequiredExperience)
            return;
          this._eventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent("You have earned enough experience to advance to the next level."));
          break;
        }
        break;
    }
    this.Status.HasChanges = true;
  }

  public static CharacterManager Current { get; } = new CharacterManager();

  public StatisticsHandler2 StatisticsCalculator { get; private set; }

  public Builder.Presentation.Models.Character Character { get; } = new Builder.Presentation.Models.Character();

  public CharacterStatus Status { get; } = new CharacterStatus();

  public CharacterFile File { get; set; }

  public ObservableCollection<ClassProgressionManager> ClassProgressionManagers { get; } = new ObservableCollection<ClassProgressionManager>();

  public SourcesManager SourcesManager { get; } = new SourcesManager();

  public ElementBase RegisterElement(ElementBase element)
  {
    if (element == null)
      throw new ArgumentNullException(nameof (element));
    Logger.Info("Registering Element: " + element?.ToString());
    switch (element.Type)
    {
      case "Archetype":
      case "Archetype Feature":
      case "Class Feature":
        ProgressionManager progressionManager1 = (ProgressionManager) null;
        if (element.Aquisition.WasSelected)
          progressionManager1 = (ProgressionManager) this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.SelectionRules.Contains(element.Aquisition.SelectRule)));
        else if (element.Aquisition.WasGranted)
          progressionManager1 = (ProgressionManager) this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.GetElements().Select<ElementBase, string>((Func<ElementBase, string>) (e => e.Id)).Contains<string>(element.Aquisition.GrantRule.ElementHeader.Id)));
        else if (Debugger.IsAttached)
          Debugger.Break();
        if (progressionManager1 == null)
          progressionManager1 = this._progressionManager;
        if (!element.AllowMultipleElements && Debugger.IsAttached)
          Debugger.Break();
        if (!element.AllowMultipleElements && progressionManager1.Elements.Any<ElementBase>((Func<ElementBase, bool>) (e => e.Type == element.Type)))
        {
          Logger.Warning("{0} not allowed for multiple elements", (object) element);
          this.UnregisterElement(progressionManager1.Elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (e => e.Type == element.Type)));
        }
        progressionManager1.Elements.Add(element);
        progressionManager1.Process(element);
        progressionManager1.ProcessExistingElements();
        break;
      case "Class":
        ClassProgressionManager progressionManager2 = this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.IsMainClass));
        if (progressionManager2 == null)
        {
          progressionManager2 = new ClassProgressionManager(true, false, 1, element);
          progressionManager2.LevelElements.AddRange(this._progressionManager.Elements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level"))));
          progressionManager2.ProgressionLevel = this.Character.Level;
          progressionManager2.SelectionRuleCreated += new EventHandler<SelectRule>(this._progressionManager_SelectionRuleCreated);
          progressionManager2.SelectionRuleRemoved += new EventHandler<SelectRule>(this._progressionManager_SelectionRuleRemoved);
          progressionManager2.SpellcastingInformationCreated += new EventHandler<SpellcastingInformation>(this._progressionManager_SpellcastingSectionCreated);
          progressionManager2.SpellcastingInformationRemoved += new EventHandler<SpellcastingInformation>(this._progressionManager_SpellcastingSectionRemoved);
          this.ClassProgressionManagers.Add(progressionManager2);
        }
        else
        {
          progressionManager2.RemoveClass();
          progressionManager2.SetClass(element);
        }
        if (!element.AllowMultipleElements && progressionManager2.Elements.Any<ElementBase>((Func<ElementBase, bool>) (e => e.Type == element.Type)))
          this.UnregisterElement(progressionManager2.Elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (e => e.Type == element.Type)));
        progressionManager2.Elements.Add(element);
        progressionManager2.Process(element);
        progressionManager2.ProcessExistingElements();
        this.Status.HasMainClass = true;
        break;
      case "Level":
        LevelElement levelElement = (LevelElement) element;
        if (levelElement.Level > this.Character.Level)
        {
          this.Character.Level = levelElement.Level;
          if (this.Character.Experience < levelElement.RequiredExperience)
            this.Character.Experience = levelElement.RequiredExperience;
        }
        this._progressionManager.ProgressionLevel = this.Character.Level;
        this._progressionManager.Elements.Add(element);
        this._progressionManager.Process(element);
        this._progressionManager.ProcessExistingElements();
        break;
      case "Multiclass":
        string id = element.Aquisition.SelectRule.UniqueIdentifier;
        ClassProgressionManager multiclassProgressionManager = this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.SelectRule.UniqueIdentifier.Equals(id)));
        if (multiclassProgressionManager == null)
        {
          multiclassProgressionManager = new ClassProgressionManager(false, true, element.Aquisition.SelectRule.Attributes.RequiredLevel, element);
          multiclassProgressionManager.SelectionRuleCreated += new EventHandler<SelectRule>(this._progressionManager_SelectionRuleCreated);
          multiclassProgressionManager.SelectionRuleRemoved += new EventHandler<SelectRule>(this._progressionManager_SelectionRuleRemoved);
          multiclassProgressionManager.ProgressionLevel = 1;
          multiclassProgressionManager.SpellcastingInformationCreated += new EventHandler<SpellcastingInformation>(this._progressionManager_SpellcastingSectionCreated);
          multiclassProgressionManager.SpellcastingInformationRemoved += new EventHandler<SpellcastingInformation>(this._progressionManager_SpellcastingSectionRemoved);
          multiclassProgressionManager.LevelElements.Add(this._progressionManager.Elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level") && x.ElementSetters.GetSetter("Level").ValueAsInteger() == multiclassProgressionManager.StartingLevel)));
          this.ClassProgressionManagers.Add(multiclassProgressionManager);
        }
        else
          multiclassProgressionManager.SetClass(element);
        if (element.ContainsSelectRules)
        {
          foreach (SelectRule selectRule in element.GetSelectRules())
          {
            if (multiclassProgressionManager.SelectionRules.Contains(selectRule) && Debugger.IsAttached)
              Debugger.Break();
          }
        }
        multiclassProgressionManager.Elements.Add(element);
        multiclassProgressionManager.Process(element);
        multiclassProgressionManager.ProcessExistingElements();
        this.Status.HasMulticlass = true;
        break;
      case "Option":
        this._progressionManager.Elements.Add(element);
        this._progressionManager.Process(element);
        this._progressionManager.ProcessExistingElements();
        if (!element.AsElement<Option>().IsInternal)
          break;
        break;
      default:
        int num = element.AllowMultipleElements ? 1 : 0;
        if (!element.AllowMultipleElements && this._progressionManager.Elements.Any<ElementBase>((Func<ElementBase, bool>) (e => e.Type == element.Type)))
        {
          Logger.Warning("{0} not allowed for multiple elements", (object) element);
          this.UnregisterElement(this._progressionManager.Elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (e => e.Type == element.Type)));
        }
        this._progressionManager.Elements.Add(element);
        this._progressionManager.Process(element);
        this._progressionManager.ProcessExistingElements();
        break;
    }
    this._progressionManager.ProcessExistingElements();
    foreach (ProgressionManager progressionManager3 in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      progressionManager3.ProcessExistingElements();
    _elementsCacheDirty = true;
    if (!_suppressSetCharacterDetails)
      this.SetCharacterDetails();
    this._eventAggregator.Send<CharacterManagerElementRegistered>(new CharacterManagerElementRegistered(element));
    this._eventAggregator.Send<CharacterManagerElementsUpdated>(new CharacterManagerElementsUpdated(element, CharacterManagerUpdateReason.ElementRegistered));
    if (element.Type == "Race" || element.Type == "Sub Race" || element.Type == "Class" || element.Type == "Multiclass" || element.Type == "Archetype" || element.Type == "Level")
    {
      this._eventAggregator.Send<CharacterBuildChangedEvent>(new CharacterBuildChangedEvent(this.Character));
      if (element.Type == "Race" || element.Type == "Sub Race")
        this.SetPortrait(element);
    }
    if (this.Status.IsLoaded && ApplicationContext.Current.Settings.GenerateSheetOnCharacterChangedRegistered)
      this.GenerateCharacterSheet();
    return element;
  }

  public ElementBase UnregisterElement(ElementBase element)
  {
    if (element == null)
      throw new ArgumentNullException(nameof (element));
    Logger.Info("Unregistering Element: " + element?.ToString());
    switch (element.Type)
    {
      case "Archetype":
      case "Archetype Feature":
      case "Class Feature":
        ProgressionManager progressionManager1 = (ProgressionManager) null;
        foreach (ClassProgressionManager progressionManager2 in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
        {
          if (progressionManager2.Elements.Contains(element))
          {
            progressionManager1 = (ProgressionManager) progressionManager2;
            break;
          }
        }
        if (progressionManager1 == null)
        {
          Logger.Warning($"trying to unregister {element} from the normal progression manager");
          progressionManager1 = this._progressionManager;
        }
        progressionManager1.Clean(element);
        progressionManager1.Elements.Remove(element);
        break;
      case "Class":
        ClassProgressionManager progressionManager3 = this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.ClassElement == element));
        if (progressionManager3 != null)
        {
          progressionManager3.Clean(element);
          progressionManager3.Elements.Remove(element);
          break;
        }
        this._progressionManager.Clean(element);
        break;
      case "Level":
        LevelElement levelElement = (LevelElement) element;
        if (levelElement.Level == this.Character.Level)
        {
          this.Character.Level = levelElement.Level - 1;
          this.Character.Experience = levelElement.RequiredExperience - 50;
        }
        this._progressionManager.ProgressionLevel = this.Character.Level;
        this._progressionManager.Clean(element);
        this._progressionManager.Elements.Remove(element);
        break;
      case "Multiclass":
        ClassProgressionManager progressionManager4 = this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.ClassElement == element));
        if (progressionManager4 != null)
        {
          progressionManager4.Clean(element);
          progressionManager4.Elements.Remove(element);
          if (!progressionManager4.IsObsolete)
          {
            progressionManager4.RemoveClass();
            if (progressionManager4.Elements.Any<ElementBase>() || !progressionManager4.SelectionRules.Any<SelectRule>())
              break;
            break;
          }
          break;
        }
        this._progressionManager.Clean(element);
        break;
      case "Option":
        this._progressionManager.Clean(element);
        this._progressionManager.Elements.Remove(element);
        if (!element.AsElement<Option>().IsInternal)
          break;
        break;
      default:
        this._progressionManager.Clean(element);
        this._progressionManager.Elements.Remove(element);
        break;
    }
    this._progressionManager.ProcessExistingElements();
    foreach (ProgressionManager progressionManager5 in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      progressionManager5.ProcessExistingElements();
    _elementsCacheDirty = true;
    if (!_suppressSetCharacterDetails)
      this.SetCharacterDetails();
    this._eventAggregator.Send<CharacterManagerElementUnregistered>(new CharacterManagerElementUnregistered(element));
    this._eventAggregator.Send<CharacterManagerElementsUpdated>(new CharacterManagerElementsUpdated(element, CharacterManagerUpdateReason.ElementUnregistered));
    if (element.Type == "Race" || element.Type == "Sub Race" || element.Type == "Class" || element.Type == "Multiclass" || element.Type == "Archetype" || element.Type == "Level")
      this._eventAggregator.Send<CharacterBuildChangedEvent>(new CharacterBuildChangedEvent(this.Character));
    if (this.Status.IsLoaded && ApplicationContext.Current.Settings.GenerateSheetOnCharacterChangedRegistered)
      this.GenerateCharacterSheet();
    return element;
  }

  public async Task<Builder.Presentation.Models.Character> New(bool initializeFirstLevel)
  {
    Logger.Info("creating a new character");
    Stopwatch sw = Stopwatch.StartNew();
    Logger.Info("unregister all remaining elements");
    _suppressSetCharacterDetails = true;
    try
    {
      foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      {
        while (progressionManager.Elements.Any<ElementBase>())
          this.UnregisterElement(progressionManager.Elements.Last<ElementBase>());
      }
      this.ClassProgressionManagers.Clear();
      while (this._progressionManager.Elements.Any<ElementBase>())
        this.UnregisterElement(this._progressionManager.Elements.Last<ElementBase>());
    }
    finally
    {
      _suppressSetCharacterDetails = false;
    }
    _elementsCacheDirty = true;
    this._progressionManager.ProgressionLevel = 0;
    while (true)
    {
      int expandersCount = SelectionRuleExpanderContext.Current.GetExpandersCount();
      if (expandersCount != 0)
      {
        await Task.Delay(10);
        Logger.Debug("post=cleanup | expanders remaining: {0}", (object) expandersCount);
      }
      else
        break;
    }
    SelectionRuleExpanderContext.Current.RemoveAllExpanders();
    if (ApplicationContext.Current.Settings.ApplyDefaultSourceRestrictionsOnNewCharacter)
      this.SourcesManager.LoadDefaults();
    this.Character.ResetEntryFields();
    this.Status.HasCompanion = false;
    if (initializeFirstLevel)
    {
      this.LevelUp();
      this.Character.Name = "New Character";
      this.Character.PlayerName = ApplicationContext.Current.Settings.PlayerName;
      this.Character.PortraitFilename = Path.Combine(DataManager.Current.UserDocumentsPortraitsDirectory, "default-portrait.png");
    }
    this.Status.IsUserPortrait = !initializeFirstLevel;
    this.Status.IsLoaded = true;
    this.Status.IsNew = initializeFirstLevel;
    this.Status.HasChanges = false;
    this.Status.CanLevelUp = true;
    this.Status.CanLevelDown = false;
    this.Status.HasMainClass = false;
    this.Status.HasMulticlass = false;
    this.Status.HasSpellcasting = false;
    this.Status.HasMulticlassSpellSlots = false;
    Logger.Info("finished initializing the new character ({0}ms)", (object) sw.ElapsedMilliseconds);
    Builder.Presentation.Models.Character character = this.Character;
    sw = (Stopwatch) null;
    return character;
  }

  public ElementBase LevelUp()
  {
    List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (e => e.Type == "Level")).ToList<ElementBase>();
    if (!this._progressionManager.Elements.Any<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level"))))
      return this.RegisterElement(list.First<ElementBase>());
    ElementBase elementBase1 = this._progressionManager.Elements.Last<ElementBase>((Func<ElementBase, bool>) (e => e.Type == "Level"));
    int num = list.IndexOf(elementBase1);
    if (num + 1 > list.Count)
      return (ElementBase) null;
    if (elementBase1 == list.Last<ElementBase>())
    {
      this.Status.CanLevelUp = false;
      MessageDialogContext.Current?.Show($"You have reached the maximum level available ({elementBase1.Name})");
      return (ElementBase) null;
    }
    ElementBase elementBase2 = this.RegisterElement(list[num + 1]);
    if (elementBase2 != list.Last<ElementBase>())
      return elementBase2;
    this.Status.CanLevelUp = false;
    return elementBase2;
  }

  public void LevelDown()
  {
    if (!this.Status.CanLevelDown)
    {
      Logger.Warning("LevelDown !Status.CanLevelDown");
    }
    else
    {
      ElementBase lastLevel = this._progressionManager.Elements.Last<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level")));
      ClassProgressionManager sender = this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.LevelElements.Contains(lastLevel)));
      if (sender != null)
      {
        if (sender.LevelElements.Remove(lastLevel))
          --sender.ProgressionLevel;
        if (sender.ProgressionLevel == 0)
          sender.IsObsolete = true;
        if (sender.IsObsolete)
        {
          while (sender.Elements.Any<ElementBase>())
            this.UnregisterElement(sender.Elements.Last<ElementBase>());
          if (sender.SelectionRules.Any<SelectRule>())
          {
            foreach (SelectRule selectionRule in (Collection<SelectRule>) sender.SelectionRules)
              this._progressionManager_SelectionRuleRemoved((object) sender, selectionRule);
          }
        }
      }
      this.UnregisterElement(lastLevel);
      if (sender != null && sender.IsObsolete)
      {
        while (sender.Elements.Any<ElementBase>())
        {
          Logger.Warning("classProgressionManager.Elements.Any() after unregistering level element");
          this.UnregisterElement(sender.Elements.Last<ElementBase>());
        }
        if (sender.SelectionRules.Any<SelectRule>())
        {
          Logger.Warning("classProgressionManager.SelectionRules.Any() after unregistering level element");
          foreach (SelectRule selectionRule in (Collection<SelectRule>) sender.SelectionRules)
            this._progressionManager_SelectionRuleRemoved((object) sender, selectionRule);
        }
        sender.SelectionRuleCreated -= new EventHandler<SelectRule>(this._progressionManager_SelectionRuleCreated);
        sender.SelectionRuleRemoved -= new EventHandler<SelectRule>(this._progressionManager_SelectionRuleRemoved);
        this.ClassProgressionManagers.Remove(sender);
        this.Status.HasMulticlass = this.ClassProgressionManagers.Any<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.IsMulticlass));
      }
      lastLevel = this._progressionManager.Elements.Last<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level")));
      if (lastLevel.AsElement<LevelElement>().Level == 1)
        this.Status.CanLevelDown = false;
      this.Status.CanLevelUp = lastLevel.AsElement<LevelElement>().Level != 20;
    }
  }

  public void LevelUpMain()
  {
    if (!this.Status.CanLevelUp)
    {
      Logger.Warning("LevelUpMain !Status.CanLevelUp");
      MessageDialogContext.Current?.Show($"You have reached the maximum level available ({this.Character.Level})");
    }
    else
    {
      if (this.Status.HasMainClass)
      {
        ClassProgressionManager progressionManager = this.ClassProgressionManagers.Single<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.IsMainClass));
        if (this.Character.Level == 20)
        {
          if (!Debugger.IsAttached)
            return;
          Debugger.Break();
          return;
        }
        ++progressionManager.ProgressionLevel;
        ElementBase elementBase = this.LevelUp();
        if (elementBase != null)
          progressionManager.LevelElements.Add(elementBase);
      }
      else
        this.LevelUp();
      this.Status.CanLevelDown = true;
    }
  }

  public void LevelUpMulti(Multiclass element)
  {
    if (!this.Status.CanLevelUp)
    {
      Logger.Warning("LevelUpMulti !Status.CanLevelUp");
    }
    else
    {
      try
      {
        if (this.Status.HasMulticlass)
        {
          ClassProgressionManager progressionManager = this.ClassProgressionManagers.Single<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.IsMulticlass && x.ClassElement.Id.Equals(element.Id)));
          if (this.Character.Level == 20 && Debugger.IsAttached)
            Debugger.Break();
          ++progressionManager.ProgressionLevel;
          ElementBase elementBase = this.LevelUp();
          if (elementBase == null)
            return;
          progressionManager.LevelElements.Add(elementBase);
          this.Status.CanLevelDown = true;
        }
        else
        {
          if (!Debugger.IsAttached)
            return;
          Debugger.Break();
        }
      }
      catch (Exception ex)
      {
        Logger.Exception(ex, nameof (LevelUpMulti));
        MessageDialogContext.Current?.ShowException(ex);
      }
    }
  }

  public void NewMulticlass()
  {
    if (!this.Status.CanLevelUp)
    {
      Logger.Warning("NewMulticlass !Status.CanLevelUp");
    }
    else
    {
      try
      {
        if (this.Status.HasMainClass)
        {
          LevelElement level = this.LevelUp() as LevelElement;
          if (level == null)
            return;
          ElementBase elementBase = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Grants"))).Single<ElementBase>((Func<ElementBase, bool>) (x => x.Id == $"ID_INTERNAL_MULTICLASS_LEVEL_{level.Level}"));
          level.RuleElements.Add(elementBase);
          this.ReprocessCharacter();
          this.Status.CanLevelDown = true;
        }
        else
        {
          if (!Debugger.IsAttached)
            return;
          Debugger.Break();
        }
      }
      catch (Exception ex)
      {
        Logger.Exception(ex, nameof (NewMulticlass));
        MessageDialogContext.Current?.ShowException(ex);
      }
    }
  }

  public void ReprocessCharacter(bool generateSheet = false)
  {
    this._progressionManager.ProcessExistingElements();
    foreach (ProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      progressionManager.ProcessExistingElements();
    this._eventAggregator.Send<ReprocessCharacterEvent>(new ReprocessCharacterEvent());
    this.SetCharacterDetails();
    if (!generateSheet || !this.Status.IsLoaded || !ApplicationContext.Current.Settings.GenerateSheetOnCharacterChangedRegistered)
      return;
    this.GenerateCharacterSheet();
  }

  public FileInfo GenerateCharacterSheetPreview()
  {
    int num = this.Status.HasChanges ? 1 : 0;
    this.ReprocessCharacter();
    if (num == 0)
      this.Status.HasChanges = false;
    FileInfo newSheet = CharacterSheetGeneratorContext.Current?.GenerateNewSheet(Path.GetTempFileName(), true)!;
    this._eventAggregator.Send<CharacterSheetPreviewEvent>(new CharacterSheetPreviewEvent(newSheet));
    return newSheet;
  }

  public FileInfo GenerateCharacterSheet()
  {
    try
    {
      return this.GenerateCharacterSheetPreview();
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (GenerateCharacterSheet));
      this._eventAggregator.Send<MainWindowStatusUpdateEvent>(new MainWindowStatusUpdateEvent($"Unable to generate a preview of your character sheet. ({ex.Message})"));
      if (Debugger.IsAttached)
        MessageDialogContext.Current?.ShowException(ex);
    }
    return (FileInfo) null;
  }

  [Obsolete]
  public void SetCharacterDetailsAfterAbilitiesChange()
  {
    if (!this.Status.IsLoaded)
      return;
    this._progressionManager.ProcessExistingElements();
    this.SetCharacterDetails();
    if (!this.Status.IsLoaded || !ApplicationContext.Current.Settings.GenerateSheetOnCharacterChangedRegistered)
      return;
    if (Debugger.IsAttached)
      return;
    try
    {
      this.GenerateCharacterSheet();
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (SetCharacterDetailsAfterAbilitiesChange));
    }
  }

  public LevelElement GetCurrentLevelElement()
  {
    if (!this.Elements.Any<ElementBase>())
      return (LevelElement) null;
    ElementBase element = this.Elements.Last<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level")));
    return element == null ? (LevelElement) null : element.AsElement<LevelElement>();
  }

  public LevelElement GetUpcomingLevelElement()
  {
    LevelElement currentLevelElement = this.GetCurrentLevelElement();
    if (currentLevelElement == null)
      return (LevelElement) null;
    if (currentLevelElement.Level == 20)
      return (LevelElement) null;
    List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Level"))).ToList<ElementBase>();
    return list[list.IndexOf((ElementBase) currentLevelElement) + 1].AsElement<LevelElement>();
  }

  private void SetCharacterDetails()
  {
    int progressionLevel = this._progressionManager.ProgressionLevel;
    if (this.StatisticsCalculator == null)
      this.StatisticsCalculator = new StatisticsHandler2(this);
    StatisticValuesGroupCollection seed = this.StatisticsCalculator.CreateSeed(progressionLevel, this);
    StatisticValuesGroupCollection values = this.StatisticsCalculator.CalculateValues(this.GetElements(), seed);
    AuroraStatisticStrings statisticStrings = new AuroraStatisticStrings();
    this.Character.Proficiency = values.GetValue(statisticStrings.Proficiency);
    AbilitiesCollection abilities = this.Character.Abilities;
    abilities.Strength.AdditionalScore = values.GetValue(statisticStrings.Strength);
    abilities.Dexterity.AdditionalScore = values.GetValue(statisticStrings.Dexterity);
    abilities.Constitution.AdditionalScore = values.GetValue(statisticStrings.Constitution);
    abilities.Intelligence.AdditionalScore = values.GetValue(statisticStrings.Intelligence);
    abilities.Wisdom.AdditionalScore = values.GetValue(statisticStrings.Wisdom);
    abilities.Charisma.AdditionalScore = values.GetValue(statisticStrings.Charisma);
    abilities.Strength.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.Strength).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    abilities.Dexterity.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.Dexterity).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    abilities.Constitution.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.Constitution).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    abilities.Intelligence.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.Intelligence).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    abilities.Wisdom.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.Wisdom).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    abilities.Charisma.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.Charisma).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    if (abilities.Strength.UseOverrideScore())
      abilities.Strength.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.StrengthSet).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    if (abilities.Dexterity.UseOverrideScore())
      abilities.Dexterity.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.DexteritySet).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    if (abilities.Constitution.UseOverrideScore())
      abilities.Constitution.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.ConstitutionSet).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    if (abilities.Intelligence.UseOverrideScore())
      abilities.Intelligence.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.IntelligenceSet).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    if (abilities.Wisdom.UseOverrideScore())
      abilities.Wisdom.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.WisdomSet).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    if (abilities.Charisma.UseOverrideScore())
      abilities.Charisma.AdditionalSummery = string.Join(", ", values.GetGroup(statisticStrings.CharismaSet).GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})")));
    this.Character.SavingThrows.Strength.ProficiencyBonus = values.GetValue(statisticStrings.StrengthSaveProficiency);
    this.Character.SavingThrows.Dexterity.ProficiencyBonus = values.GetValue(statisticStrings.DexteritySaveProficiency);
    this.Character.SavingThrows.Constitution.ProficiencyBonus = values.GetValue(statisticStrings.ConstitutionSaveProficiency);
    this.Character.SavingThrows.Intelligence.ProficiencyBonus = values.GetValue(statisticStrings.IntelligenceSaveProficiency);
    this.Character.SavingThrows.Wisdom.ProficiencyBonus = values.GetValue(statisticStrings.WisdomSaveProficiency);
    this.Character.SavingThrows.Charisma.ProficiencyBonus = values.GetValue(statisticStrings.CharismaSaveProficiency);
    this.Character.SavingThrows.Strength.MiscBonus = values.GetValue(statisticStrings.StrengthSaveMisc);
    this.Character.SavingThrows.Dexterity.MiscBonus = values.GetValue(statisticStrings.DexteritySaveMisc);
    this.Character.SavingThrows.Constitution.MiscBonus = values.GetValue(statisticStrings.ConstitutionSaveMisc);
    this.Character.SavingThrows.Intelligence.MiscBonus = values.GetValue(statisticStrings.IntelligenceSaveMisc);
    this.Character.SavingThrows.Wisdom.MiscBonus = values.GetValue(statisticStrings.WisdomSaveMisc);
    this.Character.SavingThrows.Charisma.MiscBonus = values.GetValue(statisticStrings.CharismaSaveMisc);
    this.Character.Skills.Acrobatics.ProficiencyBonus = values.GetValue(statisticStrings.AcrobaticsProficiency);
    this.Character.Skills.AnimalHandling.ProficiencyBonus = values.GetValue(statisticStrings.AnimalHandlingProficiency);
    this.Character.Skills.Arcana.ProficiencyBonus = values.GetValue(statisticStrings.ArcanaProficiency);
    this.Character.Skills.Athletics.ProficiencyBonus = values.GetValue(statisticStrings.AthleticsProficiency);
    this.Character.Skills.Deception.ProficiencyBonus = values.GetValue(statisticStrings.DeceptionProficiency);
    this.Character.Skills.History.ProficiencyBonus = values.GetValue(statisticStrings.HistoryProficiency);
    this.Character.Skills.Insight.ProficiencyBonus = values.GetValue(statisticStrings.InsightProficiency);
    this.Character.Skills.Intimidation.ProficiencyBonus = values.GetValue(statisticStrings.IntimidationProficiency);
    this.Character.Skills.Investigation.ProficiencyBonus = values.GetValue(statisticStrings.InvestigationProficiency);
    this.Character.Skills.Medicine.ProficiencyBonus = values.GetValue(statisticStrings.MedicineProficiency);
    this.Character.Skills.Nature.ProficiencyBonus = values.GetValue(statisticStrings.NatureProficiency);
    this.Character.Skills.Perception.ProficiencyBonus = values.GetValue(statisticStrings.PerceptionProficiency);
    this.Character.Skills.Performance.ProficiencyBonus = values.GetValue(statisticStrings.PerformanceProficiency);
    this.Character.Skills.Persuasion.ProficiencyBonus = values.GetValue(statisticStrings.PersuasionProficiency);
    this.Character.Skills.Religion.ProficiencyBonus = values.GetValue(statisticStrings.ReligionProficiency);
    this.Character.Skills.SleightOfHand.ProficiencyBonus = values.GetValue(statisticStrings.SleightOfHandProficiency);
    this.Character.Skills.Stealth.ProficiencyBonus = values.GetValue(statisticStrings.StealthProficiency);
    this.Character.Skills.Survival.ProficiencyBonus = values.GetValue(statisticStrings.SurvivalProficiency);
    this.Character.Skills.Acrobatics.MiscBonus = values.GetValue(statisticStrings.AcrobaticsMisc);
    this.Character.Skills.AnimalHandling.MiscBonus = values.GetValue(statisticStrings.AnimalHandlingMisc);
    this.Character.Skills.Arcana.MiscBonus = values.GetValue(statisticStrings.ArcanaMisc);
    this.Character.Skills.Athletics.MiscBonus = values.GetValue(statisticStrings.AthleticsMisc);
    this.Character.Skills.Deception.MiscBonus = values.GetValue(statisticStrings.DeceptionMisc);
    this.Character.Skills.History.MiscBonus = values.GetValue(statisticStrings.HistoryMisc);
    this.Character.Skills.Insight.MiscBonus = values.GetValue(statisticStrings.InsightMisc);
    this.Character.Skills.Intimidation.MiscBonus = values.GetValue(statisticStrings.IntimidationMisc);
    this.Character.Skills.Investigation.MiscBonus = values.GetValue(statisticStrings.InvestigationMisc);
    this.Character.Skills.Medicine.MiscBonus = values.GetValue(statisticStrings.MedicineMisc);
    this.Character.Skills.Nature.MiscBonus = values.GetValue(statisticStrings.NatureMisc);
    this.Character.Skills.Perception.MiscBonus = values.GetValue(statisticStrings.PerceptionMisc);
    this.Character.Skills.Performance.MiscBonus = values.GetValue(statisticStrings.PerformanceMisc);
    this.Character.Skills.Persuasion.MiscBonus = values.GetValue(statisticStrings.PersuasionMisc);
    this.Character.Skills.Religion.MiscBonus = values.GetValue(statisticStrings.ReligionMisc);
    this.Character.Skills.SleightOfHand.MiscBonus = values.GetValue(statisticStrings.SleightOfHandMisc);
    this.Character.Skills.Stealth.MiscBonus = values.GetValue(statisticStrings.StealthMisc);
    this.Character.Skills.Survival.MiscBonus = values.GetValue(statisticStrings.SurvivalMisc);
    this.Character.ArmorClass = values.GetValue(statisticStrings.ArmorClass);
    this.Character.Initiative = values.GetValue(statisticStrings.Initiative);
    this.Character.Speed = values.GetValue(statisticStrings.Speed);
    this.Character.MaxHp = values.GetValue(statisticStrings.HitPointsStarting) + values.GetValue(statisticStrings.HitPoints);
    this.Character.Inventory.MaxAttunedItemCount = values.GetValue(statisticStrings.AttunementMax);
    if (this.Status.IsLoaded)
    {
      foreach (StatisticValuesGroup statisticValuesGroup in values.Where<StatisticValuesGroup>((Func<StatisticValuesGroup, bool>) (x => x.GroupName.StartsWith("equipment:"))).ToList<StatisticValuesGroup>())
      {
        string id = statisticValuesGroup.GroupName.Replace("equipment:", "");
        int amount = statisticValuesGroup.Sum();
        string name = "";
        foreach (KeyValuePair<string, int> keyValuePair in statisticValuesGroup.GetValues())
          name = keyValuePair.Key;
        this.Character.Inventory.AddFromStatistics(new ElementHeader(name, "", "", ""), id, amount);
      }
    }
    ElementsOrganizer elementsOrganizer = this.SetCharacterBuild();
    this.Status.HasDragonmark = elementsOrganizer.GetTypes("Grants").Any<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(InternalGrants.Dragonmark)));
    this.Character.Dragonmark = !this.Status.HasDragonmark ? "" : elementsOrganizer.GetTypes<Dragonmark>("Dragonmark").FirstOrDefault<Dragonmark>().Name;
    this.Status.CanMulticlass = elementsOrganizer.GetTypes("Grants").Any<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(InternalGrants.MulticlassPrerequisite)));
    ElementBase elementBase1 = elementsOrganizer.GetTypes("Background").FirstOrDefault<ElementBase>();
    this.Character.Background = elementBase1 != null ? elementBase1.Name : "";
    if (elementBase1 != null)
    {
      StringBuilder stringBuilder = new StringBuilder();
      foreach (XmlNode node in (XmlNode) elementBase1.ElementNode["description"])
      {
        if (node.Name == "p")
        {
          if (!node.InnerXml.Contains("<span class=\"feature\">"))
          {
            if (!node.InnerXml.Contains("<span class=\"emphasis\">"))
            {
              if (!node.InnerText.Contains("Skill Proficiencies:"))
              {
                stringBuilder.AppendLine(node.GetInnerText());
                stringBuilder.AppendLine();
              }
              else
                break;
            }
            else
              break;
          }
          else
            break;
        }
        else
          break;
      }
      ElementBase elementBase2 = elementsOrganizer.GetTypes("Background Variant").FirstOrDefault<ElementBase>();
      if (elementBase2 != null)
      {
        foreach (XmlNode node in (XmlNode) elementBase2.ElementNode["description"])
        {
          if (node.Name == "p")
          {
            stringBuilder.AppendLine(node.GetInnerText());
            stringBuilder.AppendLine();
          }
          else
            break;
        }
        this.Character.Background = elementBase2.GetAlternateName();
      }
      this.Character.BackgroundStory.OriginalContent = stringBuilder.ToString().Trim();
      int num = elementsOrganizer.GetTypes("Background Feature").Count<ElementBase>();
      ElementBase elementBase3 = elementsOrganizer.GetTypes("Background Feature").FirstOrDefault<ElementBase>();
      if (num > 1)
      {
        ElementBase elementBase4 = elementsOrganizer.GetTypes("Background Feature").FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Supports.Contains("Background Feature") && x.Supports.Contains("Primary")));
        if (elementBase4 != null)
        {
          elementBase3 = elementBase4;
        }
        else
        {
          ElementBase elementBase5 = elementsOrganizer.GetTypes("Background Feature").FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Supports.Contains("Background Feature")));
          if (elementBase5 != null)
            elementBase3 = elementBase5;
        }
      }
      if (elementBase3 != null)
      {
        this.Character.BackgroundFeatureName.OriginalContent = elementBase3.GetAlternateName();
        this.Character.BackgroundFeatureDescription.OriginalContent = elementBase3.SheetDescription.Any<ElementSheetDescriptions.SheetDescription>() ? elementBase3.SheetDescription[0].Description : ElementDescriptionGenerator.GeneratePlainDescription(elementBase3.Description).Trim();
      }
    }
    else
    {
      this.Character.BackgroundStory.OriginalContent = string.Empty;
      this.Character.BackgroundFeatureName.OriginalContent = string.Empty;
      this.Character.BackgroundFeatureDescription.OriginalContent = string.Empty;
    }
    try
    {
      IEnumerable<SelectRule> selectRules = this._progressionManager.SelectionRules.Where<SelectRule>((Func<SelectRule, bool>) (x => x.ElementHeader.Type == "Background" && x.Attributes.IsList));
      string str1 = "";
      string str2 = "";
      string str3 = "";
      string str4 = "";
      bool flag = false;
      foreach (SelectRule selectRule in selectRules)
      {
        SelectRule selectionRule = selectRule;
        foreach (KeyValuePair<string, SelectionRuleListItem> selectionRuleListItem in elementsOrganizer.Elements.First<ElementBase>((Func<ElementBase, bool>) (x => x.Id == selectionRule.ElementHeader.Id)).SelectionRuleListItems)
        {
          if (!flag)
          {
            switch (selectionRuleListItem.Key)
            {
              case "Personality Trait:1":
                str1 += selectionRuleListItem.Value.Text;
                continue;
              case "Personality Trait:2":
                str1 = str1 + Environment.NewLine + selectionRuleListItem.Value.Text;
                continue;
              case "Ideal:1":
                str2 += selectionRuleListItem.Value.Text;
                continue;
              case "Bond:1":
                str3 += selectionRuleListItem.Value.Text;
                continue;
              case "Flaw:1":
                str4 += selectionRuleListItem.Value.Text;
                continue;
              default:
                continue;
            }
          }
        }
        flag = true;
      }
      this.Character.FillableBackgroundCharacteristics.Traits.OriginalContent = str1.Trim();
      this.Character.FillableBackgroundCharacteristics.Ideals.OriginalContent = str2;
      this.Character.FillableBackgroundCharacteristics.Bonds.OriginalContent = str3;
      this.Character.FillableBackgroundCharacteristics.Flaws.OriginalContent = str4;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (SetCharacterDetails));
    }
    ElementBase elementBase6 = elementsOrganizer.GetTypes<ElementBase>("Alignment").FirstOrDefault<ElementBase>();
    this.Character.Alignment = elementBase6 != null ? elementBase6.Name : "";
    if (this.Status.HasSpellcasting)
    {
      List<ElementBase> list = elementsOrganizer.GetTypes<ElementBase>("Grants").ToList<ElementBase>();
      this.Status.HasMulticlassSpellSlots = list.Count<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(InternalGrants.MulticlassSpellcastingSlotsFull))) + list.Count<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(InternalGrants.MulticlassSpellcastingSlotsHalf) || x.Id.Equals(InternalGrants.MulticlassSpellcastingSlotsHalfUp))) + list.Count<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(InternalGrants.MulticlassSpellcastingSlotsThird) || x.Id.Equals(InternalGrants.MulticlassSpellcastingSlotsThirdUp))) + list.Count<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(InternalGrants.MulticlassSpellcastingSlotsFourth) || x.Id.Equals(InternalGrants.MulticlassSpellcastingSlotsFourthUp))) + list.Count<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(InternalGrants.MulticlassSpellcastingSlotsFifth))) >= 2;
      if (this.Status.HasMulticlassSpellSlots)
      {
        int level = values.GetGroup("multiclass:spellcasting:level").Sum();
        this.Character.MulticlassSpellcasterLevel = level;
        StatisticValuesGroupCollection valuesAtLevel = new StatisticsHandler2(this).CalculateValuesAtLevel(level, new ElementBaseCollection(elementsOrganizer.Elements));
        this.Character.MulticlassSpellSlots.Slot1 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:1").Sum();
        this.Character.MulticlassSpellSlots.Slot2 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:2").Sum();
        this.Character.MulticlassSpellSlots.Slot3 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:3").Sum();
        this.Character.MulticlassSpellSlots.Slot4 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:4").Sum();
        this.Character.MulticlassSpellSlots.Slot5 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:5").Sum();
        this.Character.MulticlassSpellSlots.Slot6 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:6").Sum();
        this.Character.MulticlassSpellSlots.Slot7 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:7").Sum();
        this.Character.MulticlassSpellSlots.Slot8 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:8").Sum();
        this.Character.MulticlassSpellSlots.Slot9 = valuesAtLevel.GetGroup("multiclass:spellcasting:slot:9").Sum();
      }
    }
    else
      this.Status.HasMulticlassSpellSlots = false;
    if (this.Status.HasCompanion)
      this.Character.Companion.Statistics.Update(values);
    this.Character.Inventory.CalculateWeight();
    CharacterManager.Current.Status.HasChanges = true;
  }

  private ElementsOrganizer SetCharacterBuild()
  {
    ElementsOrganizer elementsOrganizer = new ElementsOrganizer((IEnumerable<ElementBase>) this.GetElements());
    Race race = elementsOrganizer.GetTypes<Race>("Race").FirstOrDefault<Race>();
    ElementBase elementBase = elementsOrganizer.GetTypes<ElementBase>("Sub Race").FirstOrDefault<ElementBase>();
    this.Character.Race = race != null ? race.GetAlternateName() : "";
    if (elementBase != null)
      this.Character.Race = elementBase.GetAlternateName();
    this.Character.HeightField.OriginalContent = race?.BaseHeight ?? "";
    this.Character.WeightField.OriginalContent = race?.BaseWeight ?? "";
    Class mainclass = elementsOrganizer.GetTypes<Class>("Class").FirstOrDefault<Class>();
    List<Multiclass> list = elementsOrganizer.GetTypes<Multiclass>("Multiclass").ToList<Multiclass>();
    this.Character.Class = mainclass != null ? mainclass.GetAlternateName() : "";
    if (list.Any<Multiclass>())
    {
      this.Character.Class += $" ({this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.ClassElement == mainclass))?.ProgressionLevel})";
      foreach (Multiclass multiclass1 in list)
      {
        Multiclass multiclass = multiclass1;
        Builder.Presentation.Models.Character character = this.Character;
        character.Class = $"{character.Class} / {multiclass.GetAlternateName()}";
        this.Character.Class += $" ({this.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.ClassElement == multiclass))?.ProgressionLevel})";
      }
    }
    int num = this.Status.HasMulticlass ? 1 : 0;
    Deity deity = elementsOrganizer.GetTypes<Deity>("Deity").FirstOrDefault<Deity>();
    this.Character.Deity = deity != null ? deity.GetAlternateName() : "";
    return elementsOrganizer;
  }

  public ObservableCollection<ElementBase> Elements
  {
    get
    {
      ElementBaseCollection elements = new ElementBaseCollection((IEnumerable<ElementBase>) this._progressionManager.Elements);
      foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
        elements.AddRange((IEnumerable<ElementBase>) progressionManager.Elements);
      return (ObservableCollection<ElementBase>) elements;
    }
  }

  public ObservableCollection<SelectRule> SelectionRules
  {
    get
    {
      ObservableCollection<SelectRule> selectionRules = new ObservableCollection<SelectRule>((IEnumerable<SelectRule>) this._progressionManager.SelectionRules);
      foreach (ProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      {
        foreach (SelectRule selectionRule in (Collection<SelectRule>) progressionManager.SelectionRules)
          selectionRules.Add(selectionRule);
      }
      return selectionRules;
    }
  }

  public ElementBaseCollection GetElements()
  {
    if (!_elementsCacheDirty && _elementsCache != null)
      return _elementsCache;
    ElementBaseCollection elements = this._progressionManager.GetElements();
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      elements.AddRange((IEnumerable<ElementBase>) progressionManager.GetElements());
    _elementsCache = elements;
    _elementsCacheDirty = false;
    return elements;
  }

  [Obsolete]
  public IEnumerable<StatisticRule> GetStatisticRules()
  {
    return this.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (e => e.ContainsStatisticRules)).SelectMany<ElementBase, StatisticRule>((Func<ElementBase, IEnumerable<StatisticRule>>) (e => e.GetStatisticRules()));
  }

  public IEnumerable<ElementBase> GetProficiencyList(IEnumerable<ElementBase> collection)
  {
    ElementBaseCollection proficiencyList = new ElementBaseCollection();
    foreach (ElementBase element in collection)
    {
      bool flag = false;
      foreach (ElementBase elementBase in (Collection<ElementBase>) proficiencyList)
      {
        if (elementBase.ContainsRuleElement(element))
          flag = true;
      }
      if (!flag)
        proficiencyList.Add(element);
    }
    return (IEnumerable<ElementBase>) proficiencyList;
  }

  public IEnumerable<StatisticRule> GetStatisticRules2()
  {
    List<StatisticRule> statisticRules2 = new List<StatisticRule>();
    statisticRules2.AddRange(this._progressionManager.GetStatisticRules());
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      statisticRules2.AddRange(progressionManager.GetStatisticRules());
    return (IEnumerable<StatisticRule>) statisticRules2;
  }

  public IEnumerable<StatisticRule> GetStatisticRulesAtLevel(int level)
  {
    List<StatisticRule> statisticRulesAtLevel = new List<StatisticRule>();
    statisticRulesAtLevel.AddRange(this._progressionManager.GetStatisticRulesAtLevel(level));
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      statisticRulesAtLevel.AddRange(progressionManager.GetStatisticRulesAtLevel(level));
    return (IEnumerable<StatisticRule>) statisticRulesAtLevel;
  }

  public IEnumerable<StatisticRule> GetInlineStatisticRules2()
  {
    List<StatisticRule> inlineStatisticRules2 = new List<StatisticRule>();
    inlineStatisticRules2.AddRange(this._progressionManager.GetInlineStatisticRules());
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      inlineStatisticRules2.AddRange(progressionManager.GetInlineStatisticRules());
    return (IEnumerable<StatisticRule>) inlineStatisticRules2;
  }

  [Obsolete]
  public bool AllowMulticlass()
  {
    return this.GetElements().Any<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(InternalOptions.AllowMulticlassing)));
  }

  public IEnumerable<SpellcastingInformation> GetSpellcastingInformations()
  {
    List<SpellcastingInformation> spellcastingInformations = new List<SpellcastingInformation>();
    spellcastingInformations.AddRange((IEnumerable<SpellcastingInformation>) this._progressionManager.SpellcastingInformations);
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
      spellcastingInformations.AddRange((IEnumerable<SpellcastingInformation>) progressionManager.SpellcastingInformations);
    return (IEnumerable<SpellcastingInformation>) spellcastingInformations;
  }

  public IEnumerable<Spell> GetPreparedSpells()
  {
    List<Spell> preparedSpells = new List<Spell>();
    ISpellcastingSectionHandler current = SpellcastingSectionContext.Current;
    foreach (SpellcastingInformation spellcastingInformation in this.GetSpellcastingInformations().Where<SpellcastingInformation>((Func<SpellcastingInformation, bool>) (x => !x.IsExtension)))
    {
      foreach (SelectionElement selectionElement in (IEnumerable<SelectionElement>) (current?.GetSpellcasterSectionViewModel(spellcastingInformation.UniqueIdentifier)?.KnownSpells ?? Enumerable.Empty<SelectionElement>()).Where<SelectionElement>((Func<SelectionElement, bool>) (x => x.IsChosen)).OrderBy<SelectionElement, int>((Func<SelectionElement, int>) (x => x.Element.AsElement<Spell>().Level)).ThenBy<SelectionElement, string>((Func<SelectionElement, string>) (x => x.Element.Name)))
      {
        Spell spell = selectionElement.Element.AsElement<Spell>();
        spell.Aquisition.PrepareParent = spellcastingInformation.Name;
        preparedSpells.Add(spell);
      }
    }
    return (IEnumerable<Spell>) preparedSpells;
  }

  private void SetPortrait(ElementBase element)
  {
    if (this.Status.IsUserPortrait)
      return;
    try
    {
      IEnumerable<string> source1 = ((IEnumerable<string>) Directory.GetFiles(DataManager.Current.UserDocumentsPortraitsDirectory)).Select<string, string>((Func<string, string>) (x => x.ToLower()));
      List<string> stringList = new List<string>();
      Func<string, bool> predicate = (Func<string, bool>) (x => x.Contains(element.Name.Replace("-", " ").ToLower()));
      List<string> list = source1.Where<string>(predicate).ToList<string>();
      if (!list.Any<string>())
        return;
      string lower = this.Character.Gender.ToLower();
      List<string> source2 = new List<string>();
      if (lower != null)
      {
        int num = lower == "male" ? 1 : 0;
      }
      stringList.AddRange(source2.Any<string>() ? (IEnumerable<string>) source2 : (IEnumerable<string>) list);
      int index = CharacterManager._rnd.Next(stringList.Count);
      this.Character.PortraitFilename = stringList[index];
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (SetPortrait));
    }
  }

  [Obsolete("not yet implemented")]
  // TODO: implement random name generation (name lists by race)
  public string GenerateCharacterName() => string.Empty;

  public ProgressionManager GetProgressManager(SelectRule selectionRule)
  {
    if (this._progressionManager.SelectionRules.Contains(selectionRule))
      return this._progressionManager;
    foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) this.ClassProgressionManagers)
    {
      if (progressionManager.SelectionRules.Contains(selectionRule))
        return (ProgressionManager) progressionManager;
    }
    return (ProgressionManager) null;
  }

  public bool ContainsOption(string id)
  {
    return this._progressionManager.GetElements().Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id)).Contains<string>(id);
  }

  public bool ContainsAverageHitPointsOption()
  {
    return this.ContainsOption(InternalOptions.AllowAverageHitPoints);
  }

  public bool ContainsMulticlassOption() => this.ContainsOption(InternalOptions.AllowMulticlassing);

  public bool ContainsFeatsOption() => this.ContainsOption(InternalOptions.AllowFeats);
}
