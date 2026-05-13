using Aurora.Documents.Sheets;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Documents;
using Builder.Presentation.Extensions;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Models;
using Builder.Presentation.Models.CharacterSheet;
using Builder.Presentation.Models.CharacterSheet.Content;
using Builder.Presentation.Models.Helpers;
using Builder.Presentation.Models.Sheet;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.UserControls.Spellcasting;
using Builder.Presentation.Utilities;
using Builder.Presentation.ViewModels.Shell.Items;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace Aurora.App.Services;

/// <summary>
/// MAUI implementation of ISelectionRuleExpanderHandler.
/// In WPF, a WPF user-control expander is created for each SelectionRule; that control
/// registers itself with this handler and triggers CharacterManager.RegisterElement when a
/// selection is made.  In MAUI we have no WPF controls, so we do both jobs here:
///   • HasExpander — reports true whenever CharacterManager already tracks the rule,
///     meaning the owning element was processed and the rule is ready to receive a value.
///   • SetRegisteredElement — looks up the element by ID and registers it directly.
/// This lets CharacterFile.Load() run its full element-registration loop without WPF.
/// </summary>
internal sealed class MauiSelectionRuleExpanderHandler : ISelectionRuleExpanderHandler
{
    // Keyed by "uniqueIdentifier:number" so GetRegisteredElement can answer later queries.
    private readonly Dictionary<string, object> _registered = new(StringComparer.Ordinal);

    public void RegisterSupport(ISupportExpanders support) { }

    /// <summary>
    /// Always returns true — in MAUI there are no WPF expander controls to wait for,
    /// so AwaitExpanderCreationAsync returns immediately without polling delays.
    /// </summary>
    public bool HasExpander(string uniqueIdentifier) => true;

    public bool HasExpander(string uniqueIdentifier, int number) => true;

    /// <summary>Clears the registry entry for a slot without registering anything new.</summary>
    public void ClearRegisteredElement(SelectRule selectionRule, int number = 1)
        => _registered.Remove($"{selectionRule.UniqueIdentifier}:{number}");

    /// <summary>
    /// Directly registers the element (identified by <paramref name="id"/>) that was
    /// selected for the given selection rule.  The element's Acquisition is configured so
    /// that CharacterManager routes it to the correct ProgressionManager.
    /// </summary>
    public void SetRegisteredElement(SelectRule selectionRule, string id, int number = 1)
    {
        if (selectionRule == null || string.IsNullOrEmpty(id))
            return;

        var key = $"{selectionRule.UniqueIdentifier}:{number}";

        // List-type rules (Bond, Ideal, Flaw, Personality Trait, etc.) use inline <item>
        // elements rather than element IDs. Register the list item directly and add it to
        // the parent element's SelectionRuleListItems so CharacterManager.SetCharacterDetails
        // can pick it up and populate FillableBackgroundCharacteristics.
        if (selectionRule.Attributes.IsList)
        {
            var listItem = selectionRule.Attributes.ListItems?
                .FirstOrDefault(li => li.ID.ToString() == id);
            if (listItem == null)
            {
                Logger.Warning($"[MauiExpander] list item {id} not found in rule '{selectionRule.Attributes.Name}'");
                return;
            }

            _registered[key] = listItem;

            // Add to parent element's dictionary so SetCharacterDetails picks it up.
            string itemKey = $"{selectionRule.Attributes.Name}:{number}";
            try
            {
                var parentEl = CharacterManager.Current.GetElements()
                    .FirstOrDefault(e => e.Id == selectionRule.ElementHeader.Id);
                if (parentEl != null)
                {
                    parentEl.SelectionRuleListItems.Remove(itemKey);
                    parentEl.SelectionRuleListItems.Add(itemKey, listItem);
                }
            }
            catch { }
            return;
        }

        var element = DataManager.Current.ElementsCollection.GetElement(id);
        if (element == null)
        {
            Logger.Warning($"[MauiExpander] element not found in collection: {id}");
            return;
        }

        // If this slot already has a selection (e.g. user is changing a feat mid-session),
        // unregister the previous element first so it doesn't persist alongside the new one.
        if (_registered.TryGetValue(key, out var previous) && previous is Builder.Data.ElementBase prev)
        {
            try { CharacterManager.Current.UnregisterElement(prev); }
            catch { }
        }

        // Tell the element it was acquired via this selection rule so that
        // CharacterManager.RegisterElement routes it to the correct ProgressionManager
        // (needed for Class Feature / Archetype dispatch).
        element.Aquisition.WasSelected = true;
        element.Aquisition.SelectRule  = selectionRule;

        CharacterManager.Current.RegisterElement(element);

        _registered[key] = element;
    }

    public object GetRegisteredElement(SelectRule selectionRule, int number = 1)
    {
        _registered.TryGetValue($"{selectionRule.UniqueIdentifier}:{number}", out var element);
        return element!;
    }

    /// <summary>
    /// Always 0: no WPF expander controls exist, so CharacterManager.New()'s cleanup
    /// loop exits on its first iteration.
    /// </summary>
    public int GetExpandersCount() => 0;

    public void FocusExpander(SelectRule rule, int number = 1) { }

    public void RetrainSpellExpander(SelectRule rule, int number, int retrainLevel) { }

    public void RemoveAllExpanders() => _registered.Clear();

    public bool RequiresSelection(SelectRule rule, int number = 1) => false;

    public int GetRetrainLevel(SelectRule rule, int number) => 0;
}

/// <summary>
/// MAUI implementation of ISpellcastingSectionHandler that stores prepared spell IDs
/// so CharacterSnapshot can reflect them when building the spell list.
/// </summary>
internal sealed class MauiSpellcastingSectionHandler : ISpellcastingSectionHandler
{
    // Key: spellcasting class name (e.g. "Cleric"), Value: set of prepared element IDs loaded from XML.
    private readonly Dictionary<string, HashSet<string>> _preparedIds
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Clears prepared state. Call before loading a new character.</summary>
    public void ResetPreparedState() => _preparedIds.Clear();

    /// <summary>Returns the prepared spell element IDs for the given spellcasting class.</summary>
    public IReadOnlyCollection<string> GetPreparedIds(string spellcastingName) =>
        _preparedIds.TryGetValue(spellcastingName, out var ids) ? ids : Array.Empty<string>();

    public SpellcasterSelectionControlViewModel? GetSpellcasterSectionViewModel(string uniqueIdentifier) => null;

    public bool SetPrepareSpell(SpellcastingInformation information, string elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return false;
        if (!_preparedIds.TryGetValue(information.Name, out var ids))
            _preparedIds[information.Name] = ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ids.Add(elementId);
        return true;
    }

    public void UnsetPrepareSpell(string spellcastingName, string elementId)
    {
        if (_preparedIds.TryGetValue(spellcastingName, out var ids))
            ids.Remove(elementId);
    }
}

// No-op IMessageDialogService — logs to debug output instead of showing WPF dialogs.
#if false
internal sealed partial class MauiMessageDialogServiceStub : IMessageDialogService
{
    public void Show(string message, string? caption = null)
    {
        string title = GetCaption(caption);
        Debug.WriteLine($"[Dialog] {title}: {message}");

#if WINDOWS
        ShowWindowsMessageBox(message, title, MessageBoxStyle.Ok, MessageBoxIcon.Info);
#else
        _ = ShowAlertAsync(title, message);
#endif
    }
    public void ShowException(Exception ex, string? message = null, string? caption = null)
    {
        string title = GetCaption(caption ?? ex.GetType().Name);
        string body = BuildExceptionMessage(ex, message);
        Debug.WriteLine($"[Dialog:Exception] {title}: {body}");

#if WINDOWS
        ShowWindowsMessageBox(body, title, MessageBoxStyle.Ok, MessageBoxIcon.Error);
#else
        _ = ShowAlertAsync(title, body);
#endif
    }
    public bool Confirm(string message, string? caption = null)
    {
        string title = GetCaption(caption ?? "Confirm");
        Debug.WriteLine($"[Dialog:Confirm] {title}: {message}");

#if WINDOWS
        return ShowWindowsMessageBox(message, title, MessageBoxStyle.YesNo, MessageBoxIcon.Question) == MessageBoxResult.Yes;
#else
        Page? page = GetDialogPage();
        if (page == null)
            return false;

        if (MainThread.IsMainThread)
        {
            Debug.WriteLine("[Dialog:Confirm] synchronous confirm requested on UI thread without native sync dialog support; returning false.");
            _ = ShowAlertAsync(title, message);
            return false;
        }

        return MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlert(title, message, "Yes", "No"))
            .GetAwaiter()
            .GetResult();
#endif
        Debug.WriteLine($"[Dialog:Confirm] {caption}: {message} → auto-returning false");
    }
}
#endif

/// <summary>
/// MAUI implementation of the shared launcher contract.
/// Uses MAUI Essentials where possible and returns false when the platform
/// cannot open the requested target.
/// </summary>
internal sealed class MauiExternalLauncher : IExternalLauncher
{
    public bool OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (File.Exists(path))
            {
                _ = Launcher.Default.OpenAsync(
                    new OpenFileRequest(Path.GetFileName(path), new ReadOnlyFile(path)));
                return true;
            }

            Uri? uri = TryCreateUri(path);
            if (uri == null)
                return false;

            _ = Launcher.Default.OpenAsync(uri);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Launcher:Path] {path}\n{ex}");
            return false;
        }
    }

    public bool OpenUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        try
        {
            _ = Launcher.Default.OpenAsync(new Uri(uri, UriKind.Absolute));
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Launcher:Uri] {uri}\n{ex}");
            return false;
        }
    }

    private static Uri? TryCreateUri(string pathOrUri)
    {
        if (Uri.TryCreate(pathOrUri, UriKind.Absolute, out var uri))
            return uri;

        if (Path.IsPathRooted(pathOrUri))
            return new Uri(Path.GetFullPath(pathOrUri));

        return null;
    }
}

/// <summary>
/// MAUI implementation of ICharacterSheetGenerator.
/// Builds CharacterSheetEx directly from CharacterManager.Current without any WPF/ApplicationManager
/// dependencies, then calls CharacterSheetEx.Save() (pure iTextSharp) to write the PDF.
/// </summary>
internal sealed class MauiCharacterSheetGenerator : ICharacterSheetGenerator
{
    public FileInfo GenerateNewSheet(string outputPath, bool isPreview)
    {
#if ANDROID
        throw new PlatformNotSupportedException("PDF export is not available on Android.");
#else
        var cm          = CharacterManager.Current;
        var character   = cm.Character;
        var elements    = cm.GetElements();
        var stats       = cm.StatisticsCalculator.StatisticValues;
        var spellInfos  = cm.GetSpellcastingInformations()
                            .Where(x => !x.IsExtension).ToList();

        var sheet = new CharacterSheetEx();
        sheet.Configuration.IncludeBackgroundPage    = Preferences.Default.Get(UserPreferencesService.KeyBackgroundPage, defaultValue: true);
        sheet.Configuration.IncludeEquipmentPage     = Preferences.Default.Get(UserPreferencesService.KeyEquipmentPage,  defaultValue: true);
        sheet.Configuration.IncludeSpellcastingPage    = spellInfos.Any();
#pragma warning disable CS0618 // IsEditable backs form-fillable behavior in the shared generator
        sheet.Configuration.IsEditable                 = Preferences.Default.Get(UserPreferencesService.KeyEditableSheet, defaultValue: false);
#pragma warning restore CS0618
        sheet.Configuration.IncludeFormatting         = Preferences.Default.Get(UserPreferencesService.KeyIncludeFormatting, defaultValue: true);
        sheet.Configuration.IsAttributeDisplayFlipped = Preferences.Default.Get(UserPreferencesService.KeyFlippedAbilities, defaultValue: false);
        sheet.Configuration.UseLegacySpellcastingPage = Preferences.Default.Get(UserPreferencesService.KeyLegacySpellcastingPage, defaultValue: false);

        // Card pages — read directly from MAUI Preferences (same keys as UserPreferencesService).
        sheet.Configuration.IncludeSpellcards   = Preferences.Default.Get(UserPreferencesService.KeySpellCards,   defaultValue: false);
        sheet.Configuration.IncludeItemcards    = Preferences.Default.Get(UserPreferencesService.KeyItemCards,    defaultValue: false);
        sheet.Configuration.IncludeAttackCards  = Preferences.Default.Get(UserPreferencesService.KeyAttackCards,  defaultValue: false);
        sheet.Configuration.IncludeFeatureCards = Preferences.Default.Get(UserPreferencesService.KeyFeatureCards, defaultValue: false);
        sheet.Configuration.StartNewSpellCardsPage   = Preferences.Default.Get(UserPreferencesService.KeyStartSpellCardsOnNewPage, defaultValue: false);
        sheet.Configuration.StartNewItemCardsPage    = Preferences.Default.Get(UserPreferencesService.KeyStartItemCardsOnNewPage, defaultValue: false);
        sheet.Configuration.StartNewAttackCardsPage  = Preferences.Default.Get(UserPreferencesService.KeyStartAttackCardsOnNewPage, defaultValue: false);
        sheet.Configuration.StartNewFeatureCardsPage = Preferences.Default.Get(UserPreferencesService.KeyStartFeatureCardsOnNewPage, defaultValue: false);

        sheet.ExportContent = BuildExportContent(cm, character, elements, stats, sheet.Configuration);

        // Equipment page — ExportContentGenerator has no WPF dependencies.
        sheet.EquipmentSheetExportContent =
            new ExportContentGenerator(cm, sheet.Configuration).GetEquipmentContent();

        // Spellcasting pages
        var addedSpells = new List<string>();
        foreach (var info in spellInfos)
            sheet.SpellcastingPageExportContentCollection.Add(
                BuildSpellcastingPage(cm, stats, info, addedSpells));

        // Populate card collections if the corresponding pages are enabled.
        if (sheet.Configuration.IncludeSpellcards)
        {
            var spellCollection = elements
                .Where(e => e.Type.Equals("Spell"))
                .Cast<Spell>()
                .Concat(cm.GetPreparedSpells())
                .OrderBy(x => x.Level)
                .ThenBy(x => x.Name)
                .Distinct();
            sheet.Spells.AddRange(spellCollection);
        }

        if (sheet.Configuration.IncludeItemcards)
        {
            foreach (var item in character.Inventory.Items)
            {
                if (item.ShowCard)
                    sheet.Items.Add(item);
            }
        }

        if (sheet.Configuration.IncludeFeatureCards)
        {
            var organizer = new ElementsOrganizer(elements);
            foreach (var feature in organizer.GetSortedFeatures(new List<ElementBase>()))
            {
                if (feature.SheetDescription.DisplayOnSheet)
                    sheet.Features.Add(feature);
            }
        }

        return sheet.Save(outputPath);
#endif
    }

    // ── Main sheet content ───────────────────────────────────────────────────

    private static CharacterSheetExportContent BuildExportContent(
        CharacterManager cm,
        Character character,
        ElementBaseCollection elements,
        Builder.Presentation.Services.Calculator.StatisticValuesGroupCollection stats,
        CharacterSheetConfiguration config)
    {
        var export   = new CharacterSheetExportContent();
        var names    = cm.StatisticsCalculator.Names;
        var inv      = character.Inventory;
        var status   = cm.Status;

        // ── Identity ──
        export.PlayerName      = character.PlayerName;
        export.CharacterName   = character.Name;
        export.Race            = character.Race;
        var raceVariant = elements.FirstOrDefault(x => x.Type == "Race Variant");
        if (raceVariant != null) export.Race = raceVariant.GetAlternateName();
        export.Alignment       = character.Alignment;
        export.Deity           = character.Deity;
        export.Experience      = character.Experience.ToString();
        export.ProficiencyBonus= character.Proficiency.ToValueString();
        export.Level           = character.Level.ToString();

        // Extra attacks count
        var extraAtk = stats.GetGroup("extra attack:count", false);
        export.AttacksCount = (extraAtk != null ? extraAtk.Sum() + 1 : 1).ToString();

        // ── Class / multiclass ──
        var hitDiceByType = new Dictionary<string, int>();
        foreach (var pm in cm.ClassProgressionManagers)
        {
            if (pm.IsMainClass)
            {
                export.MainClass.Level    = pm.ProgressionLevel.ToString();
                export.MainClass.Name     = pm.ClassElement.GetAlternateName();
                export.MainClass.Archetype= pm.Elements.FirstOrDefault(x => x.Type == "Archetype")?.GetAlternateName();
                export.MainClass.HitDie   = pm.HD;
            }
            else
            {
                var mc = new CharacterSheetExportContent.ClassExportContent
                {
                    Level     = pm.ProgressionLevel.ToString(),
                    Name      = pm.ClassElement.GetAlternateName(),
                    Archetype = pm.Elements.FirstOrDefault(x => x.Type == "Archetype")?.GetAlternateName(),
                    HitDie    = pm.HD,
                };
                export.Multiclass.Add(mc);
            }
            if (hitDiceByType.ContainsKey(pm.HD))
                hitDiceByType[pm.HD] += pm.ProgressionLevel;
            else
                hitDiceByType[pm.HD] = pm.ProgressionLevel;
        }
        export.IsMulticlass = status.HasMulticlass;

        // ── Abilities ──
        export.AbilitiesContent.Strength             = character.Abilities.Strength.FinalScore.ToString();
        export.AbilitiesContent.Dexterity            = character.Abilities.Dexterity.FinalScore.ToString();
        export.AbilitiesContent.Constitution         = character.Abilities.Constitution.FinalScore.ToString();
        export.AbilitiesContent.Intelligence         = character.Abilities.Intelligence.FinalScore.ToString();
        export.AbilitiesContent.Wisdom               = character.Abilities.Wisdom.FinalScore.ToString();
        export.AbilitiesContent.Charisma             = character.Abilities.Charisma.FinalScore.ToString();
        export.AbilitiesContent.StrengthModifier     = character.Abilities.Strength.ModifierString;
        export.AbilitiesContent.DexterityModifier    = character.Abilities.Dexterity.ModifierString;
        export.AbilitiesContent.ConstitutionModifier = character.Abilities.Constitution.ModifierString;
        export.AbilitiesContent.IntelligenceModifier = character.Abilities.Intelligence.ModifierString;
        export.AbilitiesContent.WisdomModifier       = character.Abilities.Wisdom.ModifierString;
        export.AbilitiesContent.CharismaModifier     = character.Abilities.Charisma.ModifierString;

        // ── Saving throws ──
        export.AbilitiesContent.StrengthSave             = character.SavingThrows.Strength.FinalBonus.ToValueString();
        export.AbilitiesContent.DexteritySave            = character.SavingThrows.Dexterity.FinalBonus.ToValueString();
        export.AbilitiesContent.ConstitutionSave         = character.SavingThrows.Constitution.FinalBonus.ToValueString();
        export.AbilitiesContent.IntelligenceSave         = character.SavingThrows.Intelligence.FinalBonus.ToValueString();
        export.AbilitiesContent.WisdomSave               = character.SavingThrows.Wisdom.FinalBonus.ToValueString();
        export.AbilitiesContent.CharismaSave             = character.SavingThrows.Charisma.FinalBonus.ToValueString();
        export.AbilitiesContent.StrengthSaveProficient   = character.SavingThrows.Strength.IsProficient;
        export.AbilitiesContent.DexteritySaveProficient  = character.SavingThrows.Dexterity.IsProficient;
        export.AbilitiesContent.ConstitutionSaveProficient= character.SavingThrows.Constitution.IsProficient;
        export.AbilitiesContent.IntelligenceSaveProficient= character.SavingThrows.Intelligence.IsProficient;
        export.AbilitiesContent.WisdomSaveProficient     = character.SavingThrows.Wisdom.IsProficient;
        export.AbilitiesContent.CharismaSaveProficient   = character.SavingThrows.Charisma.IsProficient;

        // ── Skills ──
        export.SkillsContent.Acrobatics      = character.Skills.Acrobatics.FinalBonus.ToValueString();
        export.SkillsContent.AnimalHandling  = character.Skills.AnimalHandling.FinalBonus.ToValueString();
        export.SkillsContent.Arcana          = character.Skills.Arcana.FinalBonus.ToValueString();
        export.SkillsContent.Athletics       = character.Skills.Athletics.FinalBonus.ToValueString();
        export.SkillsContent.Deception       = character.Skills.Deception.FinalBonus.ToValueString();
        export.SkillsContent.History         = character.Skills.History.FinalBonus.ToValueString();
        export.SkillsContent.Insight         = character.Skills.Insight.FinalBonus.ToValueString();
        export.SkillsContent.Intimidation    = character.Skills.Intimidation.FinalBonus.ToValueString();
        export.SkillsContent.Investigation   = character.Skills.Investigation.FinalBonus.ToValueString();
        export.SkillsContent.Medicine        = character.Skills.Medicine.FinalBonus.ToValueString();
        export.SkillsContent.Nature          = character.Skills.Nature.FinalBonus.ToValueString();
        export.SkillsContent.Perception      = character.Skills.Perception.FinalBonus.ToValueString();
        export.SkillsContent.Performance     = character.Skills.Performance.FinalBonus.ToValueString();
        export.SkillsContent.Persuasion      = character.Skills.Persuasion.FinalBonus.ToValueString();
        export.SkillsContent.Religion        = character.Skills.Religion.FinalBonus.ToValueString();
        export.SkillsContent.SleightOfHand   = character.Skills.SleightOfHand.FinalBonus.ToValueString();
        export.SkillsContent.Stealth         = character.Skills.Stealth.FinalBonus.ToValueString();
        export.SkillsContent.Survival        = character.Skills.Survival.FinalBonus.ToValueString();
        export.SkillsContent.AcrobaticsProficient     = character.Skills.Acrobatics.IsProficient;
        export.SkillsContent.AnimalHandlingProficient = character.Skills.AnimalHandling.IsProficient;
        export.SkillsContent.ArcanaProficient         = character.Skills.Arcana.IsProficient;
        export.SkillsContent.AthleticsProficient      = character.Skills.Athletics.IsProficient;
        export.SkillsContent.DeceptionProficient      = character.Skills.Deception.IsProficient;
        export.SkillsContent.HistoryProficient        = character.Skills.History.IsProficient;
        export.SkillsContent.InsightProficient        = character.Skills.Insight.IsProficient;
        export.SkillsContent.IntimidationProficient   = character.Skills.Intimidation.IsProficient;
        export.SkillsContent.InvestigationProficient  = character.Skills.Investigation.IsProficient;
        export.SkillsContent.MedicineProficient       = character.Skills.Medicine.IsProficient;
        export.SkillsContent.NatureProficient         = character.Skills.Nature.IsProficient;
        export.SkillsContent.PerceptionProficient     = character.Skills.Perception.IsProficient;
        export.SkillsContent.PerformanceProficient    = character.Skills.Performance.IsProficient;
        export.SkillsContent.PersuasionProficient     = character.Skills.Persuasion.IsProficient;
        export.SkillsContent.ReligionProficient       = character.Skills.Religion.IsProficient;
        export.SkillsContent.SleightOfHandProficient  = character.Skills.SleightOfHand.IsProficient;
        export.SkillsContent.StealthProficient        = character.Skills.Stealth.IsProficient;
        export.SkillsContent.SurvivalProficient       = character.Skills.Survival.IsProficient;
        export.SkillsContent.AcrobaticsExpertise      = character.Skills.Acrobatics.IsExpertise(character.Proficiency);
        export.SkillsContent.AnimalHandlingExpertise  = character.Skills.AnimalHandling.IsExpertise(character.Proficiency);
        export.SkillsContent.ArcanaExpertise          = character.Skills.Arcana.IsExpertise(character.Proficiency);
        export.SkillsContent.AthleticsExpertise       = character.Skills.Athletics.IsExpertise(character.Proficiency);
        export.SkillsContent.DeceptionExpertise       = character.Skills.Deception.IsExpertise(character.Proficiency);
        export.SkillsContent.HistoryExpertise         = character.Skills.History.IsExpertise(character.Proficiency);
        export.SkillsContent.InsightExpertise         = character.Skills.Insight.IsExpertise(character.Proficiency);
        export.SkillsContent.IntimidationExpertise    = character.Skills.Intimidation.IsExpertise(character.Proficiency);
        export.SkillsContent.InvestigationExpertise   = character.Skills.Investigation.IsExpertise(character.Proficiency);
        export.SkillsContent.MedicineExpertise        = character.Skills.Medicine.IsExpertise(character.Proficiency);
        export.SkillsContent.NatureExpertise          = character.Skills.Nature.IsExpertise(character.Proficiency);
        export.SkillsContent.PerceptionExpertise      = character.Skills.Perception.IsExpertise(character.Proficiency);
        export.SkillsContent.PerformanceExpertise     = character.Skills.Performance.IsExpertise(character.Proficiency);
        export.SkillsContent.PersuasionExpertise      = character.Skills.Persuasion.IsExpertise(character.Proficiency);
        export.SkillsContent.ReligionExpertise        = character.Skills.Religion.IsExpertise(character.Proficiency);
        export.SkillsContent.SleightOfHandExpertise   = character.Skills.SleightOfHand.IsExpertise(character.Proficiency);
        export.SkillsContent.StealthExpertise         = character.Skills.Stealth.IsExpertise(character.Proficiency);
        export.SkillsContent.SurvivalExpertise        = character.Skills.Survival.IsExpertise(character.Proficiency);
        export.SkillsContent.PerceptionPassive =
            $"{stats.GetValue(names.PerceptionPassive) + character.Skills.Perception.FinalBonus}";

        // ── Armor Class ──
        export.ArmorClassContent.ArmorClass = character.ArmorClass.ToString();
        if (inv.EquippedArmor != null)
        {
            export.ArmorClassContent.EquippedArmor         = inv.EquippedArmor.ToString();
            export.ArmorClassContent.ConditionalArmorClass = stats.GetGroup("ac:misc")?.GetSummery() ?? "";
        }
        else if (stats.ContainsGroup("ac:calculation"))
        {
            export.ArmorClassContent.EquippedArmor         = stats.GetGroup("ac:calculation").GetSummery();
            export.ArmorClassContent.ConditionalArmorClass = stats.GetGroup("ac:misc")?.GetSummery() ?? "";
        }
        else
        {
            export.ArmorClassContent.ConditionalArmorClass = stats.GetGroup("ac")?.GetSummery() ?? "";
        }
        if (inv.EquippedSecondary != null && inv.IsEquippedShield())
            export.ArmorClassContent.EquippedShield = inv.EquippedSecondary.ToString();
        if (!string.IsNullOrWhiteSpace(export.ArmorClassContent.ConditionalArmorClass))
            export.ArmorClassContent.ConditionalArmorClass += Environment.NewLine;
        export.ArmorClassContent.ConditionalArmorClass +=
            character.ConditionalArmorClassField?.ToString()?.TrimEnd() ?? "";

        // ── Hit Points ──
        export.HitPointsContent.Maximum = character.MaxHp.ToString();
        export.HitPointsContent.Current = "";
        export.HitPointsContent.Temporary = "";
        export.HitPointsContent.HitDice =
            string.Join("/", hitDiceByType.Select(kv => $"{kv.Value}{kv.Key}"));

        // ── Conditions / speeds ──
        export.ConditionsContent.WalkingSpeed = stats.GetValue(names.Speed).ToString();
        export.ConditionsContent.FlySpeed     = stats.GetValue(names.SpeedFly).ToString();
        export.ConditionsContent.ClimbSpeed   = stats.GetValue(names.SpeedClimb).ToString();
        export.ConditionsContent.SwimSpeed    = stats.GetValue(names.SpeedSwim).ToString();
        export.ConditionsContent.BurrowSpeed  = stats.GetValue(names.SpeedBurrow).ToString();
        var visions = elements.Where(x => x.Type == "Vision").Distinct();
        export.ConditionsContent.Vision = string.Join(", ", visions.Select(x => x.Name));

        var conditions  = elements.Where(x => x.Type == "Condition").OrderBy(x => x.Name).Distinct().ToList();
        var resistances = conditions.Where(x => x.Supports?.Contains("Resistance") == true).ToList();
        var vulns       = conditions.Where(x => x.Supports?.Contains("Vulnerability") == true).ToList();
        var immunes     = conditions.Where(x => x.Supports?.Contains("Immunity") == true).ToList();
        var sb = new StringBuilder();
        static string CleanCondition(string name) =>
            name.Replace("Resistance","").Replace("Vulnerability","").Replace("Immunity","")
                .Replace("(","").Replace(")","").Trim();
        if (resistances.Any())
            sb.AppendLine("Resistances. " + string.Join(", ", resistances.Select(x => CleanCondition(x.Name ?? ""))));
        if (vulns.Any())
            sb.AppendLine("Vulnerabilities. " + string.Join(", ", vulns.Select(x => CleanCondition(x.Name ?? ""))));
        if (immunes.Any())
            sb.AppendLine("Immunities. " + string.Join(", ", immunes.Select(x => CleanCondition(x.Name ?? ""))));
        export.ConditionsContent.Resistances = sb.ToString();

        // ── Equipment ──
        export.EquipmentContent.Copper   = inv.Coins.Copper.ToString();
        export.EquipmentContent.Silver   = inv.Coins.Silver.ToString();
        export.EquipmentContent.Electrum = inv.Coins.Electrum.ToString();
        export.EquipmentContent.Gold     = inv.Coins.Gold.ToString();
        export.EquipmentContent.Platinum = inv.Coins.Platinum.ToString();
        export.EquipmentContent.Weight   = inv.EquipmentWeight.ToString();
        foreach (var item in inv.Items)
        {
            string name = !string.IsNullOrWhiteSpace(item.AlternativeName)
                ? item.AlternativeName : item.DisplayName ?? item.Name ?? "";
            export.EquipmentContent.Equipment.Add(Tuple.Create(item.Amount.ToString(), name));
        }
        export.EquipmentContent.AdditionalTreasure = inv.Treasure;

        // ── Initiative & attacks ──
        export.Initiative = character.Initiative.ToValueString();
        foreach (var atk in character.AttacksSection.Items)
        {
            if (!atk.IsDisplayed) continue;
            export.AttacksContent.Add(new CharacterSheetExportContent.AttackExportContent
            {
                Name              = atk.Name.Content,
                Range             = atk.Range.Content,
                Bonus             = atk.Attack.Content,
                Damage            = atk.Damage.Content,
                Description       = atk.Description.Content,
                Underline         = atk.EquipmentItem?.GetUnderline(),
                AttackBreakdown   = atk.DisplayCalculatedAttack,
                DamageBreakdown   = atk.DisplayCalculatedDamage,
                AsCard            = atk.IsDisplayedAsCard,
            });
        }
        export.AttackAndSpellcastingField = character.AttacksSection.AttacksAndSpellcasting;

        // ── Proficiencies & languages ──
        var organizer = new ElementsOrganizer((IEnumerable<ElementBase>)elements);
        var langs     = organizer.GetLanguages(false).ToList();
        var armorProfs  = cm.GetProficiencyList((IEnumerable<ElementBase>)organizer.GetArmorProficiencies(false)).ToList();
        var weapProfs   = cm.GetProficiencyList((IEnumerable<ElementBase>)organizer.GetWeaponProficiencies(false)).ToList();
        var toolProfs   = cm.GetProficiencyList((IEnumerable<ElementBase>)organizer.GetToolProficiencies(false)).ToList();

        export.Languages         = langs.Any() ? string.Join(", ", langs.Select(x => x.Name)) : "—";
        export.ArmorProficiencies = armorProfs.Any()
            ? string.Join(", ", armorProfs.Select(x => x.Name.Replace("Armor Proficiency","").Replace("(","").Replace(")","").Trim()))
            : "—";
        export.WeaponProficiencies = weapProfs.Any()
            ? string.Join(", ", weapProfs.Select(x => x.Name.Replace("Weapon Proficiency","").Replace("(","").Replace(")","").Trim()))
            : "—";
        export.ToolProficiencies = toolProfs.Any()
            ? string.Join(", ", toolProfs.Select(x => x.Name.Replace("Tool Proficiency","").Replace("(","").Replace(")","").Trim()))
            : "—";

        // ── Features ──
        var featuresField = new ContentField();
        var featureChildren = new List<ElementBase>();
        foreach (var feature in organizer.GetSortedFeaturesExcludingRacialTraits(featureChildren))
        {
            if (!feature.SheetDescription.DisplayOnSheet) continue;

            int charLevel = character.Level;
            var pm = cm.ClassProgressionManagers
                .FirstOrDefault(p => p.GetElements().Contains(feature));
            if (pm != null) charLevel = pm.ProgressionLevel;

            string descText = "";
            string usage = feature.SheetDescription.HasUsage   ? feature.SheetDescription.Usage   : "";
            string action= feature.SheetDescription.HasAction  ? feature.SheetDescription.Action  : "";
            foreach (var sd in feature.SheetDescription.OrderBy(x => x.Level))
            {
                if (sd.Level > charLevel) continue;
                descText = sd.Description;
                if (sd.HasUsage)  usage  = sd.Usage;
                if (sd.HasAction) action = sd.Action;
            }
            string content = cm.StatisticsCalculator.ReplaceInline(descText);
            string usageStr = cm.StatisticsCalculator.ReplaceInline(usage);
            string actionStr= cm.StatisticsCalculator.ReplaceInline(action);

            string suffix = "";
            if (!string.IsNullOrWhiteSpace(actionStr))
                suffix = $"({actionStr}{(!string.IsNullOrWhiteSpace(usageStr) ? "·" + usageStr : "")})";
            else if (!string.IsNullOrWhiteSpace(usageStr))
                suffix = $"({usageStr})";

            // Default: action info prepended to content (SheetFormattingActionSuffixBold = false)
            if (!string.IsNullOrWhiteSpace(suffix))
                content = $"{suffix} {content}";

            bool prevIndent = featuresField.Lines.Any() ? featuresField.Lines.Last().Indent : false;
            string displayName = feature.GetAlternateName();
            featuresField.Lines.Add(new ContentLine(
                displayName.Trim(), content,
                prevIndent || !featureChildren.Contains(feature),
                featureChildren.Contains(feature)));
        }

        if (config.IncludeFormatting)
        {
            var fsb = new StringBuilder();
            foreach (var line in featuresField.Lines)
            {
                if ((!line.NewLineBefore || !line.Indent) && line.NewLineBefore && fsb.Length > 0)
                    fsb.Append("<p>&nbsp;</p>");
                string lineContent = line.Content.Replace(Environment.NewLine, "<br>&nbsp;  &nbsp;");
                fsb.Append($"<p>{(line.Indent ? "&nbsp;    &nbsp;" : "")}<strong><em>{line.Name}.</em></strong> {lineContent}</p>");
            }
            export.Features = fsb.ToString().Trim();
        }
        else
        {
            var fsb = new StringBuilder();
            foreach (var line in featuresField.Lines)
            {
                if (line.NewLineBefore) fsb.AppendLine();
                fsb.AppendLine($"{(line.Indent ? "    " : "")}{line.Name}. {line.Content}");
            }
            export.Features = fsb.ToString().Trim();
        }

        // ── Background ──
        export.BackgroundContent.Name             = character.Background ?? "";
        var bgVariant = elements.FirstOrDefault(x => x.Type == "Background Variant");
        if (bgVariant != null) export.BackgroundContent.Name = bgVariant.GetAlternateName();
        export.BackgroundContent.PersonalityTrait = character.FillableBackgroundCharacteristics.Traits.Content ?? "";
        export.BackgroundContent.Ideal            = character.FillableBackgroundCharacteristics.Ideals.Content ?? "";
        export.BackgroundContent.Bond             = character.FillableBackgroundCharacteristics.Bonds.Content ?? "";
        export.BackgroundContent.Flaw             = character.FillableBackgroundCharacteristics.Flaws.Content ?? "";
        export.BackgroundContent.Trinket          = character.Trinket?.Content ?? "";
        export.BackgroundContent.Story            = character.BackgroundStory?.Content ?? "";
        export.BackgroundContent.FeatureName      = character.BackgroundFeatureName?.ToString() ?? "";
        export.BackgroundContent.FeatureDescription = character.BackgroundFeatureDescription?.ToString() ?? "";

        // ── Appearance ──
        export.AppearanceContent.Portrait = character.PortraitFilename;
        export.AppearanceContent.Gender   = character.Gender;
        export.AppearanceContent.Age      = character.Age;
        export.AppearanceContent.Height   = character.Height;
        export.AppearanceContent.Weight   = character.Weight;
        export.AppearanceContent.Eyes     = character.Eyes;
        export.AppearanceContent.Skin     = character.Skin;
        export.AppearanceContent.Hair     = character.Hair;

        // ── Affiliations ──
        export.AlliesAndOrganizations      = character.Allies ?? "";
        export.OrganizationName            = character.OrganisationName ?? "";
        export.AdditionalFeaturesAndTraits = character.AdditionalFeatures ?? "";
        export.Treasure                    = inv.Treasure ?? "";

        // ── Additional notes (racial traits displayed separately on sheet) ──
        var racialTraitsSb = new StringBuilder();
        var racialTraitChildren = new List<ElementBase>();
        foreach (var feature in organizer.GetSortedRacialTraits(racialTraitChildren))
        {
            if (!feature.SheetDescription.DisplayOnSheet) continue;
            int charLevel = character.Level;
            string descText = "";
            foreach (var sd in feature.SheetDescription.OrderBy(x => x.Level))
            {
                if (sd.Level > charLevel) continue;
                descText = sd.Description;
            }
            string content = cm.StatisticsCalculator.ReplaceInline(descText);
            string displayName = feature.GetAlternateName();
            if (config.IncludeFormatting)
            {
                if (racialTraitsSb.Length > 0) racialTraitsSb.Append("<p>&nbsp;</p>");
                racialTraitsSb.Append($"<p><strong><em>{displayName}.</em></strong> {content}</p>");
            }
            else
            {
                racialTraitsSb.AppendLine($"{displayName}. {content}");
            }
        }
        export.TemporaryRacialTraits = racialTraitsSb.ToString().Trim();

        return export;
    }

    // ── Spellcasting page ────────────────────────────────────────────────────

    private static CharacterSheetSpellcastingPageExportContent BuildSpellcastingPage(
        CharacterManager cm,
        Builder.Presentation.Services.Calculator.StatisticValuesGroupCollection stats,
        SpellcastingInformation info,
        List<string> addedSpells)
    {
        var page = new CharacterSheetSpellcastingPageExportContent();
        page.SpellcastingClass = info.Name;

        // Archetype name
        try
        {
            var pm = cm.ClassProgressionManagers
                .FirstOrDefault(c => c.SpellcastingInformations
                    .Select(x => x.UniqueIdentifier)
                    .Contains(info.UniqueIdentifier));
            page.SpellcastingArchetype =
                (pm?.HasArchetype() == true
                    ? pm.GetElements().FirstOrDefault(x => x.Type == "Archetype")?.Name
                    : null) ?? "";
        }
        catch { }

        // Stats
        page.Ability      = info.AbilityName;
        try { page.AttackBonus = stats.GetValue(info.GetSpellcasterSpellAttackStatisticName()).ToValueString(); } catch { }
        try { page.Save        = stats.GetValue(info.GetSpellcasterSpellSaveStatisticName()).ToString(); } catch { }
        page.PrepareCount = info.Prepare
            ? stats.GetValue(info.GetPrepareAmountStatisticName()).ToString()
            : "N/A";

        page.IsMulticlassSpellcaster = cm.Status.HasMulticlassSpellSlots;

        // Spell slots
        page.Cantrips.Level = 0;
        for (int lvl = 1; lvl <= 9; lvl++)
        {
            var levelProp = GetSpellsProperty(page, lvl);
            levelProp.Level = lvl;
            try { levelProp.AvailableSlots = stats.GetValue(info.GetSlotStatisticName(lvl)); } catch { }
        }
        if (cm.Status.HasMulticlassSpellSlots)
        {
            var mss = cm.Character.MulticlassSpellSlots;
            page.Spells1.AvailableSlots = mss.Slot1;
            page.Spells2.AvailableSlots = mss.Slot2;
            page.Spells3.AvailableSlots = mss.Slot3;
            page.Spells4.AvailableSlots = mss.Slot4;
            page.Spells5.AvailableSlots = mss.Slot5;
            page.Spells6.AvailableSlots = mss.Slot6;
            page.Spells7.AvailableSlots = mss.Slot7;
            page.Spells8.AvailableSlots = mss.Slot8;
            page.Spells9.AvailableSlots = mss.Slot9;
        }

        // Collect prepared/always-prepared IDs
        var preparedIds = new HashSet<string>(
            SpellcastingSectionContext.Current?.GetPreparedIds(info.Name) ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var alwaysPreparedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in cm.GetElements().Where(e => e.Type == "Spell"))
        {
            try
            {
                dynamic d = e;
                if ((bool)d.Aquisition.WasGranted && (bool)d.Aquisition.GrantRule.IsAlwaysPrepared())
                    alwaysPreparedIds.Add((string)d.Id);
            }
            catch { }
        }

        // Spell list: registered elements of type Spell
        var registeredSpells = cm.GetElements()
            .Where(e => e.Type == "Spell")
            .Select(e => (Element: e, Level: GetSpellLevel(e)))
            .Where(t => t.Level >= 0)
            .OrderBy(t => t.Level).ThenBy(t => t.Element.Name)
            .ToList();

        bool includeNonPreparedSpells = Preferences.Default.Get(
            UserPreferencesService.KeyIncludeNonPreparedSpells,
            defaultValue: false);

        foreach (var (spell, level) in registeredSpells)
        {
            bool alwaysPrepared = alwaysPreparedIds.Contains(spell.Id ?? "");
            bool isPrepared     = alwaysPrepared || preparedIds.Contains(spell.Id ?? "");

            // For prepared casters, only include cantrips + prepared spells.
            if (level > 0 && info.Prepare && !isPrepared && !includeNonPreparedSpells) continue;

            string description = "";
            string castingTime = "", range = "", duration = "", components = "", subtitle = "";
            try { dynamic d = spell; castingTime = (string)(d.CastingTime ?? ""); } catch { }
            try { dynamic d = spell; range       = (string)(d.Range       ?? ""); } catch { }
            try { dynamic d = spell; duration    = (string)(d.Duration    ?? ""); } catch { }
            try { dynamic d = spell; components  = (string)(d.GetComponentsString()); } catch { }
            try { dynamic d = spell; subtitle    = (string)(d.Underline   ?? ""); } catch { }
            try { dynamic d = spell; description = ElementDescriptionGenerator.GeneratePlainDescription(d.Description); } catch { }

            bool isRitual = false, isConc = false;
            string school = "";
            try { dynamic d = spell; isRitual = (bool)d.IsRitual; } catch { }
            try { dynamic d = spell; isConc   = (bool)d.IsConcentration; } catch { }
            try { dynamic d = spell; school   = (string)(d.MagicSchool ?? ""); } catch { }

            var entry = new CharacterSheetSpellcastingPageExportContent.SpellExportContent
            {
                IsPrepared    = isPrepared,
                AlwaysPrepared= alwaysPrepared,
                Name          = spell.Name ?? "",
                CastingTime   = castingTime,
                Range         = range,
                Duration      = duration,
                Components    = components,
                Subtitle      = subtitle,
                Description   = description,
                Level         = level.ToString(),
                School        = school,
                Ritual        = isRitual,
                Concentration = isConc,
            };

            addedSpells.Add(spell.Name ?? "");
            GetSpellsProperty(page, level).Spells.Add(entry);
        }

        return page;
    }

    private static CharacterSheetSpellcastingPageExportContent.SpellcastingLevelExportContent
        GetSpellsProperty(CharacterSheetSpellcastingPageExportContent page, int level) => level switch
    {
        0 => page.Cantrips,
        1 => page.Spells1,
        2 => page.Spells2,
        3 => page.Spells3,
        4 => page.Spells4,
        5 => page.Spells5,
        6 => page.Spells6,
        7 => page.Spells7,
        8 => page.Spells8,
        9 => page.Spells9,
        _ => page.Cantrips,
    };

    private static int GetSpellLevel(ElementBase e)
    {
        try { dynamic d = e; return (int)d.Level; } catch { return -1; }
    }
}
