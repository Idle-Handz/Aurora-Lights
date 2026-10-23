// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.CharacterSheetEx
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Aurora.Documents.ExportContent.Notes;
using Aurora.Documents.Resources.Aurora;
using Aurora.Documents.Sheets;
using Aurora.Documents.Writers;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Presentation.Extensions;
using Builder.Presentation.Models.CharacterSheet.Content;
using Builder.Presentation.Models.CharacterSheet.Pages;
using Builder.Presentation.Models.CharacterSheet.Pages.Content;
using Builder.Presentation.Models.Sheet;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Telemetry;
using Builder.Presentation.Utilities;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Shell.Items;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

#nullable disable
namespace Builder.Presentation.Models.CharacterSheet;

public class CharacterSheetEx
{
  private const string CheckBoxTrue = "Yes";
  private const float BaseFontSize = 7f;

  public CharacterSheetConfiguration Configuration { get; }

  public CharacterSheetExportContent ExportContent { get; set; }

  public Aurora.Documents.ExportContent.Equipment.EquipmentExportContent EquipmentSheetExportContent { get; set; }

  public NotesExportContent NotesExportContent { get; set; }

  public List<CharacterSheetSpellcastingPageExportContent> SpellcastingPageExportContentCollection { get; }

  public List<Spell> Spells { get; }

  public List<RefactoredEquipmentItem> Items { get; }

  public List<ElementBase> Features { get; }

  public List<string> PartialFlatteningNames { get; } = new List<string>();

  public CharacterSheetEx(CharacterSheetConfiguration configuration = null)
  {
    this.Configuration = configuration ?? new CharacterSheetConfiguration();
    this.ExportContent = new CharacterSheetExportContent();
    this.SpellcastingPageExportContentCollection = new List<CharacterSheetSpellcastingPageExportContent>();
    this.Spells = new List<Spell>();
    this.Items = new List<RefactoredEquipmentItem>();
    this.Features = new List<ElementBase>();
  }

  public FileInfo Save(string destinationPath)
  {
    FileInfo fileInfo = this.InitializeCharacterSheet();
    using (MemoryStream staticOutput = new MemoryStream())
    using (PdfReader reader = new PdfReader(fileInfo.FullName))
    {
      using (Stream os = this.Configuration.IsFormFillable ? new FileStream(destinationPath, FileMode.Create) : staticOutput)
      {
        using (PdfStamper stamper = new PdfStamper(reader, (Stream) os))
        {
          this.WriteExportContent(stamper);
          List<string> source = new List<string>();
          if (this.Configuration.IncludeEquipmentPage)
            new EquipmentPageWriter(this.Configuration, stamper).Write(this.EquipmentSheetExportContent);
          if (this.Configuration.IncludeNotesPage)
            new NotesPageWriter(this.Configuration, stamper).Write(this.NotesExportContent);
          if (this.Configuration.UseLegacyDetailsPage)
            this.StampLegacyDetailsContent(stamper);
          if (this.Configuration.UseLegacyBackgroundPage)
            this.StampLegacyBackgroundContent(stamper);
          if (this.Configuration.IncludeCompanionPage)
            this.WriteCompanionExportContent(stamper);
          if (this.Configuration.UseLegacySpellcastingPage)
          {
            for (int index = 0; index < this.SpellcastingPageExportContentCollection.Count; ++index)
            {
              CharacterSheetSpellcastingPageExportContent pageExportContent = this.SpellcastingPageExportContentCollection[index];
              SpellcastingSheetContent content = new SpellcastingSheetContent();
              content.SpellcastingClass = pageExportContent.SpellcastingClass;
              content.SpellcastingAbility = pageExportContent.Ability;
              content.SpellcastingAttackModifier = pageExportContent.AttackBonus;
              content.SpellcastingSave = pageExportContent.Save;
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Cantrips.Spells)
                content.Cantrips.Collection.Add(new SpellcastingSpellContent(spell.Name));
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells1.Spells)
                content.Spells1.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              content.Spells1.SlotsCount = pageExportContent.Spells1.AvailableSlots;
              content.Spells2.SlotsCount = pageExportContent.Spells2.AvailableSlots;
              content.Spells3.SlotsCount = pageExportContent.Spells3.AvailableSlots;
              content.Spells4.SlotsCount = pageExportContent.Spells4.AvailableSlots;
              content.Spells5.SlotsCount = pageExportContent.Spells5.AvailableSlots;
              content.Spells6.SlotsCount = pageExportContent.Spells6.AvailableSlots;
              content.Spells7.SlotsCount = pageExportContent.Spells7.AvailableSlots;
              content.Spells8.SlotsCount = pageExportContent.Spells8.AvailableSlots;
              content.Spells9.SlotsCount = pageExportContent.Spells9.AvailableSlots;
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells2.Spells)
                content.Spells2.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells3.Spells)
                content.Spells3.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells4.Spells)
                content.Spells4.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells5.Spells)
                content.Spells5.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells6.Spells)
                content.Spells6.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells7.Spells)
                content.Spells7.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells8.Spells)
                content.Spells8.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in pageExportContent.Spells9.Spells)
                content.Spells9.Collection.Add(new SpellcastingSpellContent(spell.GetDisplayName(), spell.IsPrepared));
              this.StampSpellcastingContent(stamper, content, index + 1);
            }
          }
          if (this.Configuration.IncludeFormatting)
          {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"<p><strong><em>Armor Proficiencies.</em></strong> {this.ExportContent.ArmorProficiencies}</p><p>&nbsp;</p>");
            stringBuilder.Append($"<p><strong><em>Weapon Proficiencies.</em></strong> {this.ExportContent.WeaponProficiencies}</p></p><p>&nbsp;</p>");
            stringBuilder.Append($"<p><strong><em>Tool Proficiencies.</em></strong> {this.ExportContent.ToolProficiencies}</p></p><p>&nbsp;</p>");
            stringBuilder.Append($"<p><strong><em>Languages.</em></strong> {this.ExportContent.Languages}</p>");
            this.ReplaceField(stamper, "details_proficiencies_languages", stringBuilder.ToString(), 8f);
            this.ReplaceField(stamper, "details_features", this.ExportContent.Features, 8f);
            this.ReplaceField(stamper, "details_additional_notes", this.ExportContent.TemporaryRacialTraits, 8f);
            this.ReplaceField(stamper, "background_feature", this.ExportContent.BackgroundContent.FeatureDescription, dynamic: false);
            this.ReplaceField(stamper, "background_traits", (this.ExportContent.BackgroundContent.PersonalityTrait ?? "").Replace(Environment.NewLine, "<br>"), dynamic: false);
            this.ReplaceField(stamper, "background_ideals", this.ExportContent.BackgroundContent.Ideal, dynamic: false);
            this.ReplaceField(stamper, "background_bonds", this.ExportContent.BackgroundContent.Bond, dynamic: false);
            this.ReplaceField(stamper, "background_flaws", this.ExportContent.BackgroundContent.Flaw, dynamic: false);
            this.ReplaceField(stamper, "background_trinket", this.ExportContent.BackgroundContent.Trinket, dynamic: false);
          }
          stamper.FormFlattening = !this.Configuration.IsFormFillable;
          if (this.Configuration.IsFormFillable && this.PartialFlatteningNames.Any<string>())
          {
            foreach (string partialFlatteningName in this.PartialFlatteningNames)
              stamper.PartialFormFlattening(partialFlatteningName);
            stamper.FormFlattening = true;
          }
          if (this.Configuration.IsFormFillable && source.Any<string>())
          {
            foreach (string name in source)
              stamper.PartialFormFlattening(name);
            stamper.FormFlattening = true;
          }
        }
      }
      if (!this.Configuration.IsFormFillable)
      {
        // iText cannot flatten image buttons added in the same stamping pass.
        // Reopen the completed document so those fields are flattened as well.
        using (PdfReader completed = new PdfReader(staticOutput.ToArray()))
        using (FileStream output = new FileStream(destinationPath, FileMode.Create))
        using (PdfStamper finalStamper = new PdfStamper(completed, output))
          finalStamper.FormFlattening = true;
      }
    }
    fileInfo.Delete();
    return new FileInfo(destinationPath);
  }

  private FileInfo InitializeCharacterSheet()
  {
    string tempFileName = Path.GetTempFileName();
    using (FileStream fileStream = new FileStream(tempFileName, FileMode.Create))
    {
      List<PdfReader> readers = new List<PdfReader>();
      if (this.Configuration.IncludeCharacterPage)
      {
        if (this.Configuration.UseLegacyDetailsPage)
          readers.Add(CharacterSheetResources.GetLegacyDetailsPage().CreateReader());
        else
          readers.Add(CharacterSheetResources.GetDetailsPage().CreateReader());
      }
      if (this.Configuration.IncludeBackgroundPage)
      {
        if (this.Configuration.UseLegacyBackgroundPage)
          readers.Add(CharacterSheetResources.GetLegacyBackgroundPage().CreateReader());
        else
          readers.Add(CharacterSheetResources.GetBackgroundPage().CreateReader());
      }
      AuroraDocumentResources documentResources = new AuroraDocumentResources();
      if (this.Configuration.IncludeEquipmentPage)
      {
        PdfReader pdfReader = new PdfReader(documentResources.GetEquipmentPage());
        readers.Add(pdfReader);
      }
      if (this.Configuration.IncludeNotesPage)
      {
        PdfReader pdfReader = new PdfReader(documentResources.GetNotesPage());
        readers.Add(pdfReader);
      }
      if (this.Configuration.IncludeCompanionPage)
        readers.Add(CharacterSheetResources.GetCompanionPage().CreateReader());
      if (this.Configuration.IncludeSpellcastingPage)
      {
        if (this.Configuration.UseLegacySpellcastingPage)
        {
          CharacterSheetResourcePage spellcastingPage = CharacterSheetResources.GetLegacySpellcastingPage();
          for (int index = 1; index <= this.SpellcastingPageExportContentCollection.Count; ++index)
          {
            PdfReader reader = spellcastingPage.CreateReader();
            foreach (KeyValuePair<string, AcroFields.Item> field in (IEnumerable<KeyValuePair<string, AcroFields.Item>>) reader.AcroFields.Fields)
              reader.AcroFields.RenameField(field.Key, $"{field.Key}:{index}");
            readers.Add(reader);
          }
        }
        else
        {
          SpellcastingPageGenerator spellcastingPageGenerator = new SpellcastingPageGenerator();
          foreach (CharacterSheetSpellcastingPageExportContent pageExportContent in this.SpellcastingPageExportContentCollection)
            spellcastingPageGenerator.Add(pageExportContent);
          readers.Add(spellcastingPageGenerator.AsReader());
          this.PartialFlatteningNames.AddRange((IEnumerable<string>) spellcastingPageGenerator.PartialFlatteningNames);
        }
      }
      CardPageGenerator cardPageGenerator1 = new CardPageGenerator();
      cardPageGenerator1.Flatten = !this.Configuration.IsEditable;
      CardPageGenerator cardPageGenerator2 = cardPageGenerator1;
      bool flag = false;
      if (this.Configuration.IncludeSpellcards)
      {
        ElementBaseCollection elements = CharacterManager.Current.GetElements();
        foreach (Spell spell in this.Spells)
        {
          SpellCardContent content = new SpellCardContent(spell.Name, spell.Underline);
          content.CastingTime = spell.CastingTime;
          content.Range = spell.Range;
          content.Duration = spell.Duration;
          content.Components = spell.GetComponentsString();
          content.Description = this.GeneratePlainDescription(spell.Description);
          content.DescriptionHtml = spell.Description;
          ElementHeader parent = spell.Aquisition.GetParentHeader();
          if (parent == null)
          {
            content.LeftFooter = $"Prepared ({spell.Aquisition.PrepareParent})";
          }
          else
          {
            content.LeftFooter = parent.Name;
            try
            {
              if (parent.Type.Equals("Racial Trait"))
              {
                ElementHeader parentHeader = elements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id == parent.Id))?.Aquisition.GetParentHeader();
                if (parentHeader != null)
                  content.LeftFooter = $"{parent.Name} ({parentHeader.Name})";
              }
              if (!parent.Name.StartsWith("Additional Spell,"))
              {
                if (parent.Name.StartsWith("Additional "))
                {
                  if (!parent.Name.Contains("Spell,"))
                    goto label_47;
                }
                else
                  goto label_47;
              }
              content.LeftFooter = parent.Name.Replace(spell.Name, "").Trim().Trim(',').Trim();
            }
            catch (Exception ex)
            {
              Logger.Exception(ex, nameof (InitializeCharacterSheet));
              Dictionary<string, string> properties = AnalyticsErrorHelper.CreateProperties("id", parent.Id);
              properties.Add("comment", "trying to set parent name on spell card");
              Dictionary<string, string> additionalProperties = properties;
              AnalyticsErrorHelper.Exception(ex, additionalProperties, method: nameof (InitializeCharacterSheet), line: 450);
            }
label_47:
            foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>) CharacterManager.Current.ClassProgressionManagers)
            {
              if (progressionManager.GetElements().Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals(parent.Type))).Select<ElementBase, string>((Func<ElementBase, string>) (x => x.Id)).Contains<string>(parent.Id) && content.LeftFooter != progressionManager.ClassElement.Name)
              {
                SpellCardContent spellCardContent = content;
                spellCardContent.LeftFooter = $"{spellCardContent.LeftFooter} ({progressionManager.ClassElement.Name})";
                break;
              }
            }
          }
          content.RightFooter = spell.Source.Replace("Unearthed Arcana: ", "UA: ").Replace("Adventurers League: ", "AL: ");
          cardPageGenerator2.AddSpellCard(content);
          flag = true;
        }
      }
      if (this.Configuration.IncludeAttackCards)
      {
        if (this.Configuration.StartNewAttackCardsPage & flag)
          cardPageGenerator2.StartNewPage();
        foreach (CharacterSheetExportContent.AttackExportContent attackExportContent in this.ExportContent.AttacksContent)
        {
          if (attackExportContent.AsCard)
          {
            AttackCardContent content = new AttackCardContent(attackExportContent.Name, attackExportContent.Underline);
            content.Title = attackExportContent.Name;
            content.Range = attackExportContent.Range;
            content.Attack = attackExportContent.Bonus;
            content.Damage = attackExportContent.Damage;
            content.Description = attackExportContent.Description;
            cardPageGenerator2.AddAttackCard(content);
            flag = true;
          }
        }
      }
      if (this.Configuration.IncludeItemcards)
      {
        if (this.Configuration.StartNewItemCardsPage & flag)
          cardPageGenerator2.StartNewPage();
        foreach (RefactoredEquipmentItem refactoredEquipmentItem in this.Items)
        {
          cardPageGenerator2.AddGenericCard(new Builder.Presentation.Models.CharacterSheet.Pages.Content.GenericCardContent("", "")
          {
            Title = string.IsNullOrWhiteSpace(refactoredEquipmentItem.AlternativeName) ? refactoredEquipmentItem.DisplayName : refactoredEquipmentItem.AlternativeName,
            Subtitle = refactoredEquipmentItem.Item.Category,
            DescriptionHtml = refactoredEquipmentItem.IsAdorned ? refactoredEquipmentItem.AdornerItem.Description : refactoredEquipmentItem.Item.Description,
            LeftFooter = refactoredEquipmentItem.Item.Weight,
            RightFooter = refactoredEquipmentItem.IsAdorned ? refactoredEquipmentItem.AdornerItem.Source : refactoredEquipmentItem.Item.Source
          });
          flag = true;
        }
      }
      if (this.Configuration.IncludeFeatureCards)
      {
        if (this.Configuration.StartNewFeatureCardsPage & flag)
          cardPageGenerator2.StartNewPage();
        foreach (ElementBase feature in this.Features)
        {
          if (feature.SheetDescription.DisplayOnSheet)
          {
            Builder.Presentation.Models.CharacterSheet.Pages.Content.GenericCardContent content = new Builder.Presentation.Models.CharacterSheet.Pages.Content.GenericCardContent(feature.Name, feature.Type, this.GeneratePlainDescription(feature.Description));
            content.DescriptionHtml = feature.Description;
            ElementHeader parentHeader = feature.Aquisition.GetParentHeader();
            content.LeftFooter = parentHeader?.Name ?? "";
            content.RightFooter = feature.Source;
            cardPageGenerator2.AddGenericCard(content);
            flag = true;
          }
        }
      }
      if (flag)
        readers.Add(cardPageGenerator2.AsReader());
      this.ConcatenateReaders((IEnumerable<PdfReader>) readers, (Stream) fileStream);
    }
    return new FileInfo(tempFileName);
  }

  private void ConcatenateReaders(IEnumerable<PdfReader> readers, Stream stream)
  {
    PdfConcatenate pdfConcatenate = new PdfConcatenate(stream);
    pdfConcatenate.Writer.SetMergeFields();
    pdfConcatenate.Open();
    foreach (PdfReader reader in readers)
      pdfConcatenate.Writer.AddDocument(reader);
    pdfConcatenate.Close();
  }

  private void WriteExportContent(PdfStamper stamper)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "details_character_name",
        this.ExportContent.CharacterName
      },
      {
        "details_player",
        this.ExportContent.PlayerName
      },
      {
        "details_race",
        this.ExportContent.Race
      },
      {
        "details_background",
        this.ExportContent.BackgroundContent.Name
      },
      {
        "details_build",
        this.ExportContent.GetClassBuild()
      },
      {
        "details_alignment",
        this.ExportContent.Alignment
      },
      {
        "details_deity",
        this.ExportContent.Deity
      },
      {
        "details_xp",
        this.ExportContent.Experience
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_str_modifier" : "details_str_score",
        this.ExportContent.AbilitiesContent.Strength
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_dex_modifier" : "details_dex_score",
        this.ExportContent.AbilitiesContent.Dexterity
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_con_modifier" : "details_con_score",
        this.ExportContent.AbilitiesContent.Constitution
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_int_modifier" : "details_int_score",
        this.ExportContent.AbilitiesContent.Intelligence
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_wis_modifier" : "details_wis_score",
        this.ExportContent.AbilitiesContent.Wisdom
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_cha_modifier" : "details_cha_score",
        this.ExportContent.AbilitiesContent.Charisma
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_str_score" : "details_str_modifier",
        this.ExportContent.AbilitiesContent.StrengthModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_dex_score" : "details_dex_modifier",
        this.ExportContent.AbilitiesContent.DexterityModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_con_score" : "details_con_modifier",
        this.ExportContent.AbilitiesContent.ConstitutionModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_int_score" : "details_int_modifier",
        this.ExportContent.AbilitiesContent.IntelligenceModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_wis_score" : "details_wis_modifier",
        this.ExportContent.AbilitiesContent.WisdomModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_cha_score" : "details_cha_modifier",
        this.ExportContent.AbilitiesContent.CharismaModifier
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "details_acrobatics_total",
        this.ExportContent.SkillsContent.Acrobatics
      },
      {
        "details_animalhandling_total",
        this.ExportContent.SkillsContent.AnimalHandling
      },
      {
        "details_arcana_total",
        this.ExportContent.SkillsContent.Arcana
      },
      {
        "details_athletics_total",
        this.ExportContent.SkillsContent.Athletics
      },
      {
        "details_deception_total",
        this.ExportContent.SkillsContent.Deception
      },
      {
        "details_history_total",
        this.ExportContent.SkillsContent.History
      },
      {
        "details_insight_total",
        this.ExportContent.SkillsContent.Insight
      },
      {
        "details_intimidation_total",
        this.ExportContent.SkillsContent.Intimidation
      },
      {
        "details_investigation_total",
        this.ExportContent.SkillsContent.Investigation
      },
      {
        "details_perception_total",
        this.ExportContent.SkillsContent.Perception
      },
      {
        "details_nature_total",
        this.ExportContent.SkillsContent.Nature
      },
      {
        "details_performance_total",
        this.ExportContent.SkillsContent.Performance
      },
      {
        "details_medicine_total",
        this.ExportContent.SkillsContent.Medicine
      },
      {
        "details_religion_total",
        this.ExportContent.SkillsContent.Religion
      },
      {
        "details_stealth_total",
        this.ExportContent.SkillsContent.Stealth
      },
      {
        "details_persuasion_total",
        this.ExportContent.SkillsContent.Persuasion
      },
      {
        "details_sleightofhand_total",
        this.ExportContent.SkillsContent.SleightOfHand
      },
      {
        "details_survival_total",
        this.ExportContent.SkillsContent.Survival
      },
      {
        "details_acrobatics_proficiency",
        this.ExportContent.SkillsContent.AcrobaticsProficient ? "Yes" : "No"
      },
      {
        "details_animalhandling_proficiency",
        this.ExportContent.SkillsContent.AnimalHandlingProficient ? "Yes" : "No"
      },
      {
        "details_arcana_proficiency",
        this.ExportContent.SkillsContent.ArcanaProficient ? "Yes" : "No"
      },
      {
        "details_athletics_proficiency",
        this.ExportContent.SkillsContent.AthleticsProficient ? "Yes" : "No"
      },
      {
        "details_deception_proficiency",
        this.ExportContent.SkillsContent.DeceptionProficient ? "Yes" : "No"
      },
      {
        "details_history_proficiency",
        this.ExportContent.SkillsContent.HistoryProficient ? "Yes" : "No"
      },
      {
        "details_insight_proficiency",
        this.ExportContent.SkillsContent.InsightProficient ? "Yes" : "No"
      },
      {
        "details_intimidation_proficiency",
        this.ExportContent.SkillsContent.IntimidationProficient ? "Yes" : "No"
      },
      {
        "details_investigation_proficiency",
        this.ExportContent.SkillsContent.InvestigationProficient ? "Yes" : "No"
      },
      {
        "details_medicine_proficiency",
        this.ExportContent.SkillsContent.MedicineProficient ? "Yes" : "No"
      },
      {
        "details_nature_proficiency",
        this.ExportContent.SkillsContent.NatureProficient ? "Yes" : "No"
      },
      {
        "details_perception_proficiency",
        this.ExportContent.SkillsContent.PerceptionProficient ? "Yes" : "No"
      },
      {
        "details_performance_proficiency",
        this.ExportContent.SkillsContent.PerformanceProficient ? "Yes" : "No"
      },
      {
        "details_persuasion_proficiency",
        this.ExportContent.SkillsContent.PersuasionProficient ? "Yes" : "No"
      },
      {
        "details_religion_proficiency",
        this.ExportContent.SkillsContent.ReligionProficient ? "Yes" : "No"
      },
      {
        "details_sleightofhand_proficiency",
        this.ExportContent.SkillsContent.SleightOfHandProficient ? "Yes" : "No"
      },
      {
        "details_stealth_proficiency",
        this.ExportContent.SkillsContent.StealthProficient ? "Yes" : "No"
      },
      {
        "details_survival_proficiency",
        this.ExportContent.SkillsContent.SurvivalProficient ? "Yes" : "No"
      },
      {
        "details_acrobatics_expertise",
        this.ExportContent.SkillsContent.AcrobaticsExpertise ? "Yes" : "No"
      },
      {
        "details_animalhandling_expertise",
        this.ExportContent.SkillsContent.AnimalHandlingExpertise ? "Yes" : "No"
      },
      {
        "details_arcana_expertise",
        this.ExportContent.SkillsContent.ArcanaExpertise ? "Yes" : "No"
      },
      {
        "details_athletics_expertise",
        this.ExportContent.SkillsContent.AthleticsExpertise ? "Yes" : "No"
      },
      {
        "details_deception_expertise",
        this.ExportContent.SkillsContent.DeceptionExpertise ? "Yes" : "No"
      },
      {
        "details_history_expertise",
        this.ExportContent.SkillsContent.HistoryExpertise ? "Yes" : "No"
      },
      {
        "details_insight_expertise",
        this.ExportContent.SkillsContent.InsightExpertise ? "Yes" : "No"
      },
      {
        "details_intimidation_expertise",
        this.ExportContent.SkillsContent.IntimidationExpertise ? "Yes" : "No"
      },
      {
        "details_investigation_expertise",
        this.ExportContent.SkillsContent.InvestigationExpertise ? "Yes" : "No"
      },
      {
        "details_medicine_expertise",
        this.ExportContent.SkillsContent.MedicineExpertise ? "Yes" : "No"
      },
      {
        "details_nature_expertise",
        this.ExportContent.SkillsContent.NatureExpertise ? "Yes" : "No"
      },
      {
        "details_perception_expertise",
        this.ExportContent.SkillsContent.PerceptionExpertise ? "Yes" : "No"
      },
      {
        "details_performance_expertise",
        this.ExportContent.SkillsContent.PerformanceExpertise ? "Yes" : "No"
      },
      {
        "details_persuasion_expertise",
        this.ExportContent.SkillsContent.PersuasionExpertise ? "Yes" : "No"
      },
      {
        "details_religion_expertise",
        this.ExportContent.SkillsContent.ReligionExpertise ? "Yes" : "No"
      },
      {
        "details_sleightofhand_expertise",
        this.ExportContent.SkillsContent.SleightOfHandExpertise ? "Yes" : "No"
      },
      {
        "details_stealth_expertise",
        this.ExportContent.SkillsContent.StealthExpertise ? "Yes" : "No"
      },
      {
        "details_survival_expertise",
        this.ExportContent.SkillsContent.SurvivalExpertise ? "Yes" : "No"
      },
      {
        "details_passive_perception_total",
        this.ExportContent.SkillsContent.PerceptionPassive
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "details_str_save_proficiency",
        this.ExportContent.AbilitiesContent.StrengthSaveProficient ? "Yes" : "No"
      },
      {
        "details_dex_save_proficiency",
        this.ExportContent.AbilitiesContent.DexteritySaveProficient ? "Yes" : "No"
      },
      {
        "details_con_save_proficiency",
        this.ExportContent.AbilitiesContent.ConstitutionSaveProficient ? "Yes" : "No"
      },
      {
        "details_int_save_proficiency",
        this.ExportContent.AbilitiesContent.IntelligenceSaveProficient ? "Yes" : "No"
      },
      {
        "details_wis_save_proficiency",
        this.ExportContent.AbilitiesContent.WisdomSaveProficient ? "Yes" : "No"
      },
      {
        "details_cha_save_proficiency",
        this.ExportContent.AbilitiesContent.CharismaSaveProficient ? "Yes" : "No"
      },
      {
        "details_str_save_total",
        this.ExportContent.AbilitiesContent.StrengthSave
      },
      {
        "details_dex_save_total",
        this.ExportContent.AbilitiesContent.DexteritySave
      },
      {
        "details_con_save_total",
        this.ExportContent.AbilitiesContent.ConstitutionSave
      },
      {
        "details_int_save_total",
        this.ExportContent.AbilitiesContent.IntelligenceSave
      },
      {
        "details_wis_save_total",
        this.ExportContent.AbilitiesContent.WisdomSave
      },
      {
        "details_cha_save_total",
        this.ExportContent.AbilitiesContent.CharismaSave
      },
      {
        "details_saving_throws",
        this.ExportContent.AbilitiesContent.ConditionalSave
      },
      {
        "details_inspiration",
        this.ExportContent.Inspiration ? "Yes" : ""
      },
      {
        "details_proficiency_bonus",
        this.ExportContent.ProficiencyBonus
      },
      {
        "details_initiative",
        this.ExportContent.Initiative
      },
      {
        "details_initiative_advantage",
        this.ExportContent.InitiativeAdvantage ? "Yes" : "No"
      },
      {
        "details_hp_max",
        this.ExportContent.HitPointsContent.Maximum
      },
      {
        "details_hp_current",
        this.ExportContent.HitPointsContent.Current
      },
      {
        "details_hp_temp",
        this.ExportContent.HitPointsContent.Temporary
      },
      {
        "details_hd",
        this.ExportContent.HitPointsContent.HitDice
      },
      {
        "details_death_save_success_1",
        this.ExportContent.HitPointsContent.DeathSavingThrowSuccess1 ? "Yes" : "No"
      },
      {
        "details_death_save_success_2",
        this.ExportContent.HitPointsContent.DeathSavingThrowSuccess2 ? "Yes" : "No"
      },
      {
        "details_death_save_success_3",
        this.ExportContent.HitPointsContent.DeathSavingThrowSuccess3 ? "Yes" : "No"
      },
      {
        "details_death_save_fail_1",
        this.ExportContent.HitPointsContent.DeathSavingThrowFailure1 ? "Yes" : "No"
      },
      {
        "details_death_save_fail_2",
        this.ExportContent.HitPointsContent.DeathSavingThrowFailure2 ? "Yes" : "No"
      },
      {
        "details_death_save_fail_3",
        this.ExportContent.HitPointsContent.DeathSavingThrowFailure3 ? "Yes" : "No"
      },
      {
        "details_speed_walking",
        this.ExportContent.ConditionsContent.WalkingSpeed + "ft."
      },
      {
        "details_speed_fly",
        !string.IsNullOrWhiteSpace(this.ExportContent.ConditionsContent.FlySpeed) ? this.ExportContent.ConditionsContent.FlySpeed + "ft." : ""
      },
      {
        "details_speed_climb",
        !string.IsNullOrWhiteSpace(this.ExportContent.ConditionsContent.ClimbSpeed) ? this.ExportContent.ConditionsContent.ClimbSpeed + "ft." : ""
      },
      {
        "details_speed_swim",
        !string.IsNullOrWhiteSpace(this.ExportContent.ConditionsContent.SwimSpeed) ? this.ExportContent.ConditionsContent.SwimSpeed + "ft." : ""
      },
      {
        "details_vision",
        this.ExportContent.ConditionsContent.Vision
      },
      {
        "details_resistances",
        this.ExportContent.ConditionsContent.Resistances
      },
      {
        "details_coinage_cp",
        this.ExportContent.EquipmentContent.Copper
      },
      {
        "details_coinage_sp",
        this.ExportContent.EquipmentContent.Silver
      },
      {
        "details_coinage_ep",
        this.ExportContent.EquipmentContent.Electrum
      },
      {
        "details_coinage_gp",
        this.ExportContent.EquipmentContent.Gold
      },
      {
        "details_coinage_pp",
        this.ExportContent.EquipmentContent.Platinum
      },
      {
        "details_equipment_weight",
        this.ExportContent.EquipmentContent.Weight + " lb."
      },
      {
        "details_attack_description",
        this.ExportContent.AttackAndSpellcastingField
      },
      {
        "details_encounter_box",
        this.ExportContent.AttacksCount == "1" ? this.ExportContent.AttacksCount + " Attack / Attack Action" : this.ExportContent.AttacksCount + " Attacks / Attack Action"
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    stamper.SetFontSize(6f, "details_encounter_box");
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "details_armor_class",
        this.ExportContent.ArmorClassContent.ArmorClass
      },
      {
        "details_equipped_armor",
        this.ExportContent.ArmorClassContent.EquippedArmor
      },
      {
        "details_equipped_shield",
        this.ExportContent.ArmorClassContent.EquippedShield
      },
      {
        "details_armor_stealth_disadvantage",
        this.ExportContent.ArmorClassContent.StealthDisadvantage ? "Ja" : ""
      },
      {
        "details_armor_conditional",
        this.ExportContent.ArmorClassContent.ConditionalArmorClass
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    int num = 1;
    foreach (CharacterSheetExportContent.AttackExportContent attackExportContent in this.ExportContent.AttacksContent)
    {
      if (num <= 4)
      {
        stamper.AcroFields.SetField($"details_attack{num}_weapon", attackExportContent.Name);
        stamper.AcroFields.SetField($"details_attack{num}_range", attackExportContent.Range);
        stamper.AcroFields.SetField($"details_attack{num}_attack", attackExportContent.Bonus);
        stamper.AcroFields.SetField($"details_attack{num}_damage", attackExportContent.Damage);
        stamper.AcroFields.SetField($"details_attack{num}_misc", attackExportContent.Misc);
        stamper.AcroFields.SetField($"details_attack{num}_description", attackExportContent.Description);
        ++num;
      }
      else
        break;
    }
    for (int index = 0; index < Math.Min(this.ExportContent.EquipmentContent.Equipment.Count, 15); ++index)
    {
      stamper.AcroFields.SetField($"details_equipment_amount_1.{index}", this.ExportContent.EquipmentContent.Equipment[index].Item1);
      stamper.AcroFields.SetField($"details_equipment_name_1.{index}", this.ExportContent.EquipmentContent.Equipment[index].Item2);
    }
    if (!this.Configuration.IncludeFormatting)
    {
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.AppendLine("Armor Proficiencies. " + this.ExportContent.ArmorProficiencies);
      stringBuilder.AppendLine("Weapon Proficiencies. " + this.ExportContent.WeaponProficiencies);
      stringBuilder.AppendLine("Tool Proficiencies. " + this.ExportContent.ToolProficiencies);
      stringBuilder.AppendLine("Languages. " + this.ExportContent.Languages);
      foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
      {
        {
          "details_proficiencies_languages",
          stringBuilder.ToString()
        },
        {
          "details_features",
          this.ExportContent.Features
        },
        {
          "details_additional_notes",
          this.ExportContent.TemporaryRacialTraits
        }
      })
        stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    }
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "background_character_name",
        this.ExportContent.CharacterName
      },
      {
        "background_story",
        this.ExportContent.BackgroundContent.Story
      },
      {
        "background_allies",
        this.ExportContent.AlliesAndOrganizations
      },
      {
        "background_organization_name",
        this.ExportContent.OrganizationName
      },
      {
        "background_gender",
        this.ExportContent.AppearanceContent.Gender
      },
      {
        "background_age",
        this.ExportContent.AppearanceContent.Age
      },
      {
        "background_eyes",
        this.ExportContent.AppearanceContent.Eyes
      },
      {
        "background_hair",
        this.ExportContent.AppearanceContent.Hair
      },
      {
        "background_height",
        this.ExportContent.AppearanceContent.Height
      },
      {
        "background_skin",
        this.ExportContent.AppearanceContent.Skin
      },
      {
        "background_weight",
        this.ExportContent.AppearanceContent.Weight
      },
      {
        "background_traits",
        this.ExportContent.BackgroundContent.PersonalityTrait
      },
      {
        "background_ideals",
        this.ExportContent.BackgroundContent.Ideal
      },
      {
        "background_bonds",
        this.ExportContent.BackgroundContent.Bond
      },
      {
        "background_flaws",
        this.ExportContent.BackgroundContent.Flaw
      },
      {
        "background_feature_name",
        this.ExportContent.BackgroundContent.FeatureName
      },
      {
        "background_feature",
        this.ExportContent.BackgroundContent.FeatureDescription
      },
      {
        "background_trinket",
        this.ExportContent.BackgroundContent.Trinket
      },
      {
        "background_additional_features",
        this.ExportContent.AdditionalFeaturesAndTraits
      },
      {
        "background_additional_treasure",
        this.ExportContent.EquipmentContent.AdditionalTreasure
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    stamper.SetFontSize(0.0f, "background_feature");
    this.WriteImage(stamper, "background_portrait_image", this.ExportContent.AppearanceContent.Portrait);
    this.WriteImage(stamper, "background_organization_image", this.ExportContent.OrganizationSymbol);
  }

  private void WriteCompanionExportContent(PdfStamper stamper)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "companion_name",
        this.ExportContent.CompanionName
      },
      {
        "companion_kind",
        this.ExportContent.CompanionKind
      },
      {
        "companion_build",
        this.ExportContent.CompanionBuild
      },
      {
        "companion_skills",
        this.ExportContent.CompanionSkills
      },
      {
        "companion_appearance",
        this.ExportContent.CompanionSkills
      },
      {
        "companion_challenge",
        this.ExportContent.CompanionChallenge
      },
      {
        "companion_owner",
        this.ExportContent.CompanionOwner
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_str_modifier" : "companion_str_score",
        this.ExportContent.CompanionAbilitiesContent.Strength
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_dex_modifier" : "companion_dex_score",
        this.ExportContent.CompanionAbilitiesContent.Dexterity
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_con_modifier" : "companion_con_score",
        this.ExportContent.CompanionAbilitiesContent.Constitution
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_int_modifier" : "companion_int_score",
        this.ExportContent.CompanionAbilitiesContent.Intelligence
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_wis_modifier" : "companion_wis_score",
        this.ExportContent.CompanionAbilitiesContent.Wisdom
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_cha_modifier" : "companion_cha_score",
        this.ExportContent.CompanionAbilitiesContent.Charisma
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_str_score" : "companion_str_modifier",
        this.ExportContent.CompanionAbilitiesContent.StrengthModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_dex_score" : "companion_dex_modifier",
        this.ExportContent.CompanionAbilitiesContent.DexterityModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_con_score" : "companion_con_modifier",
        this.ExportContent.CompanionAbilitiesContent.ConstitutionModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_int_score" : "companion_int_modifier",
        this.ExportContent.CompanionAbilitiesContent.IntelligenceModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_wis_score" : "companion_wis_modifier",
        this.ExportContent.CompanionAbilitiesContent.WisdomModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "companion_cha_score" : "companion_cha_modifier",
        this.ExportContent.CompanionAbilitiesContent.CharismaModifier
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "companion_acrobatics_total",
        this.ExportContent.CompanionSkillsContent.Acrobatics
      },
      {
        "companion_animalhandling_total",
        this.ExportContent.CompanionSkillsContent.AnimalHandling
      },
      {
        "companion_arcana_total",
        this.ExportContent.CompanionSkillsContent.Arcana
      },
      {
        "companion_athletics_total",
        this.ExportContent.CompanionSkillsContent.Athletics
      },
      {
        "companion_deception_total",
        this.ExportContent.CompanionSkillsContent.Deception
      },
      {
        "companion_history_total",
        this.ExportContent.CompanionSkillsContent.History
      },
      {
        "companion_insight_total",
        this.ExportContent.CompanionSkillsContent.Insight
      },
      {
        "companion_intimidation_total",
        this.ExportContent.CompanionSkillsContent.Intimidation
      },
      {
        "companion_investigation_total",
        this.ExportContent.CompanionSkillsContent.Investigation
      },
      {
        "companion_perception_total",
        this.ExportContent.CompanionSkillsContent.Perception
      },
      {
        "companion_nature_total",
        this.ExportContent.CompanionSkillsContent.Nature
      },
      {
        "companion_performance_total",
        this.ExportContent.CompanionSkillsContent.Performance
      },
      {
        "companion_medicine_total",
        this.ExportContent.CompanionSkillsContent.Medicine
      },
      {
        "companion_religion_total",
        this.ExportContent.CompanionSkillsContent.Religion
      },
      {
        "companion_stealth_total",
        this.ExportContent.CompanionSkillsContent.Stealth
      },
      {
        "companion_persuasion_total",
        this.ExportContent.CompanionSkillsContent.Persuasion
      },
      {
        "companion_sleightofhand_total",
        this.ExportContent.CompanionSkillsContent.SleightOfHand
      },
      {
        "companion_survival_total",
        this.ExportContent.CompanionSkillsContent.Survival
      },
      {
        "companion_acrobatics_proficiency",
        this.ExportContent.CompanionSkillsContent.AcrobaticsProficient ? "Yes" : "No"
      },
      {
        "companion_animalhandling_proficiency",
        this.ExportContent.CompanionSkillsContent.AnimalHandlingProficient ? "Yes" : "No"
      },
      {
        "companion_arcana_proficiency",
        this.ExportContent.CompanionSkillsContent.ArcanaProficient ? "Yes" : "No"
      },
      {
        "companion_athletics_proficiency",
        this.ExportContent.CompanionSkillsContent.AthleticsProficient ? "Yes" : "No"
      },
      {
        "companion_deception_proficiency",
        this.ExportContent.CompanionSkillsContent.DeceptionProficient ? "Yes" : "No"
      },
      {
        "companion_history_proficiency",
        this.ExportContent.CompanionSkillsContent.HistoryProficient ? "Yes" : "No"
      },
      {
        "companion_insight_proficiency",
        this.ExportContent.CompanionSkillsContent.InsightProficient ? "Yes" : "No"
      },
      {
        "companion_intimidation_proficiency",
        this.ExportContent.CompanionSkillsContent.IntimidationProficient ? "Yes" : "No"
      },
      {
        "companion_investigation_proficiency",
        this.ExportContent.CompanionSkillsContent.InvestigationProficient ? "Yes" : "No"
      },
      {
        "companion_medicine_proficiency",
        this.ExportContent.CompanionSkillsContent.MedicineProficient ? "Yes" : "No"
      },
      {
        "companion_nature_proficiency",
        this.ExportContent.CompanionSkillsContent.NatureProficient ? "Yes" : "No"
      },
      {
        "companion_perception_proficiency",
        this.ExportContent.CompanionSkillsContent.PerceptionProficient ? "Yes" : "No"
      },
      {
        "companion_performance_proficiency",
        this.ExportContent.CompanionSkillsContent.PerformanceProficient ? "Yes" : "No"
      },
      {
        "companion_persuasion_proficiency",
        this.ExportContent.CompanionSkillsContent.PersuasionProficient ? "Yes" : "No"
      },
      {
        "companion_religion_proficiency",
        this.ExportContent.CompanionSkillsContent.ReligionProficient ? "Yes" : "No"
      },
      {
        "companion_sleightofhand_proficiency",
        this.ExportContent.CompanionSkillsContent.SleightOfHandProficient ? "Yes" : "No"
      },
      {
        "companion_stealth_proficiency",
        this.ExportContent.CompanionSkillsContent.StealthProficient ? "Yes" : "No"
      },
      {
        "companion_survival_proficiency",
        this.ExportContent.CompanionSkillsContent.SurvivalProficient ? "Yes" : "No"
      },
      {
        "companion_acrobatics_expertise",
        this.ExportContent.CompanionSkillsContent.AcrobaticsExpertise ? "Yes" : "No"
      },
      {
        "companion_animalhandling_expertise",
        this.ExportContent.CompanionSkillsContent.AnimalHandlingExpertise ? "Yes" : "No"
      },
      {
        "companion_arcana_expertise",
        this.ExportContent.CompanionSkillsContent.ArcanaExpertise ? "Yes" : "No"
      },
      {
        "companion_athletics_expertise",
        this.ExportContent.CompanionSkillsContent.AthleticsExpertise ? "Yes" : "No"
      },
      {
        "companion_deception_expertise",
        this.ExportContent.CompanionSkillsContent.DeceptionExpertise ? "Yes" : "No"
      },
      {
        "companion_history_expertise",
        this.ExportContent.CompanionSkillsContent.HistoryExpertise ? "Yes" : "No"
      },
      {
        "companion_insight_expertise",
        this.ExportContent.CompanionSkillsContent.InsightExpertise ? "Yes" : "No"
      },
      {
        "companion_intimidation_expertise",
        this.ExportContent.CompanionSkillsContent.IntimidationExpertise ? "Yes" : "No"
      },
      {
        "companion_investigation_expertise",
        this.ExportContent.CompanionSkillsContent.InvestigationExpertise ? "Yes" : "No"
      },
      {
        "companion_medicine_expertise",
        this.ExportContent.CompanionSkillsContent.MedicineExpertise ? "Yes" : "No"
      },
      {
        "companion_nature_expertise",
        this.ExportContent.CompanionSkillsContent.NatureExpertise ? "Yes" : "No"
      },
      {
        "companion_perception_expertise",
        this.ExportContent.CompanionSkillsContent.PerceptionExpertise ? "Yes" : "No"
      },
      {
        "companion_performance_expertise",
        this.ExportContent.CompanionSkillsContent.PerformanceExpertise ? "Yes" : "No"
      },
      {
        "companion_persuasion_expertise",
        this.ExportContent.CompanionSkillsContent.PersuasionExpertise ? "Yes" : "No"
      },
      {
        "companion_religion_expertise",
        this.ExportContent.CompanionSkillsContent.ReligionExpertise ? "Yes" : "No"
      },
      {
        "companion_sleightofhand_expertise",
        this.ExportContent.CompanionSkillsContent.SleightOfHandExpertise ? "Yes" : "No"
      },
      {
        "companion_stealth_expertise",
        this.ExportContent.CompanionSkillsContent.StealthExpertise ? "Yes" : "No"
      },
      {
        "companion_survival_expertise",
        this.ExportContent.CompanionSkillsContent.SurvivalExpertise ? "Yes" : "No"
      },
      {
        "companion_passive_perception_total",
        this.ExportContent.CompanionSkillsContent.PerceptionPassive
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "companion_proficiency",
        this.ExportContent.CompanionProficiency
      },
      {
        "companion_str_save_total",
        this.ExportContent.CompanionAbilitiesContent.StrengthSave
      },
      {
        "companion_dex_save_total",
        this.ExportContent.CompanionAbilitiesContent.DexteritySave
      },
      {
        "companion_con_save_total",
        this.ExportContent.CompanionAbilitiesContent.ConstitutionSave
      },
      {
        "companion_int_save_total",
        this.ExportContent.CompanionAbilitiesContent.IntelligenceSave
      },
      {
        "companion_wis_save_total",
        this.ExportContent.CompanionAbilitiesContent.WisdomSave
      },
      {
        "companion_cha_save_total",
        this.ExportContent.CompanionAbilitiesContent.CharismaSave
      },
      {
        "companion_saving_throws",
        this.ExportContent.CompanionAbilitiesContent.ConditionalSave
      },
      {
        "companion_str_save_proficiency",
        this.ExportContent.CompanionAbilitiesContent.StrengthSaveProficient ? "Yes" : "No"
      },
      {
        "companion_dex_save_proficiency",
        this.ExportContent.CompanionAbilitiesContent.DexteritySaveProficient ? "Yes" : "No"
      },
      {
        "companion_con_save_proficiency",
        this.ExportContent.CompanionAbilitiesContent.ConstitutionSaveProficient ? "Yes" : "No"
      },
      {
        "companion_int_save_proficiency",
        this.ExportContent.CompanionAbilitiesContent.IntelligenceSaveProficient ? "Yes" : "No"
      },
      {
        "companion_wis_save_proficiency",
        this.ExportContent.CompanionAbilitiesContent.WisdomSaveProficient ? "Yes" : "No"
      },
      {
        "companion_cha_save_proficiency",
        this.ExportContent.CompanionAbilitiesContent.CharismaSaveProficient ? "Yes" : "No"
      },
      {
        "companion_hp_max",
        this.ExportContent.CompanionHitPointsContent.Maximum
      },
      {
        "companion_speed",
        this.ExportContent.CompanionSpeedString
      },
      {
        "companion_speed_walking",
        this.ExportContent.CompanionConditionsContent.WalkingSpeed + "ft."
      },
      {
        "companion_speed_fly",
        !string.IsNullOrWhiteSpace(this.ExportContent.CompanionConditionsContent.FlySpeed) ? this.ExportContent.CompanionConditionsContent.FlySpeed + "ft." : ""
      },
      {
        "companion_speed_climb",
        !string.IsNullOrWhiteSpace(this.ExportContent.CompanionConditionsContent.ClimbSpeed) ? this.ExportContent.CompanionConditionsContent.ClimbSpeed + "ft." : ""
      },
      {
        "companion_speed_swim",
        !string.IsNullOrWhiteSpace(this.ExportContent.CompanionConditionsContent.SwimSpeed) ? this.ExportContent.CompanionConditionsContent.SwimSpeed + "ft." : ""
      },
      {
        "companion_speed_burrow",
        !string.IsNullOrWhiteSpace(this.ExportContent.CompanionConditionsContent.BurrowSpeed) ? this.ExportContent.CompanionConditionsContent.BurrowSpeed + "ft." : ""
      },
      {
        "companion_vision",
        this.ExportContent.CompanionConditionsContent.Vision
      },
      {
        "companion_resistances",
        this.ExportContent.CompanionConditionsContent.Resistances
      },
      {
        "companion_stats",
        this.ExportContent.CompanionConditionsContent.Resistances
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "companion_armor_class",
        this.ExportContent.CompanionArmorClassContent.ArmorClass
      },
      {
        "companion_initiative",
        this.ExportContent.CompanionInitiative
      },
      {
        "companion_armor_conditional",
        this.ExportContent.CompanionArmorClassContent.ConditionalArmorClass
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    this.WriteImage(stamper, "companion_portrait_image", this.ExportContent.CompanionPortrait);
    if (this.Configuration.IncludeFormatting)
    {
      this.ReplaceField(stamper, "companion_features", this.ExportContent.CompanionFeatures);
      this.ReplaceField(stamper, "companion_stats", this.ExportContent.CompanionStats);
    }
    else
    {
      stamper.AcroFields.SetField("companion_features", this.ExportContent.CompanionFeatures);
      stamper.AcroFields.SetField("companion_stats", this.ExportContent.CompanionStats);
    }
  }

  private void WriteImage(PdfStamper stamper, string fieldName, string imagePath)
  {
    try
    {
      IList<AcroFields.FieldPosition> fieldPositions = stamper.AcroFields.GetFieldPositions(fieldName);
      AcroFields.FieldPosition fieldPosition = fieldPositions != null ? fieldPositions.FirstOrDefault<AcroFields.FieldPosition>() : (AcroFields.FieldPosition) null;
      if (fieldPosition == null || !File.Exists(imagePath))
        return;
      PushbuttonField pushbuttonField1 = new PushbuttonField(stamper.Writer, fieldPosition.position, fieldName + ":replaced");
      pushbuttonField1.Layout = 2;
      pushbuttonField1.Image = Image.GetInstance(imagePath);
      pushbuttonField1.ProportionalIcon = true;
      pushbuttonField1.Options = 1;
      PushbuttonField pushbuttonField2 = pushbuttonField1;
      stamper.AddAnnotation((PdfAnnotation) pushbuttonField2.Field, fieldPosition.page);
      stamper.AcroFields.RemoveField(fieldName);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (WriteImage));
    }
  }

  private void ReplaceField(
    PdfStamper stamper,
    string name,
    string htmlContent,
    float fontsize = 7f,
    bool dynamic = true)
  {
    if (!stamper.AcroFields.Fields.ContainsKey(name))
      return;
    htmlContent ??= "";
    name.Contains("features");
    Rectangle position = stamper.AcroFields.GetFieldPositions(name)[0].position;
    int page = stamper.AcroFields.GetFieldPositions(name)[0].page;
    stamper.AcroFields.RemoveField(name);
    iTextSharp.text.Font regular = Builder.Presentation.Models.CharacterSheet.PDF.FontsHelper.GetRegular();
    float num = ColumnText.FitText(regular, $"{htmlContent}{Environment.NewLine}<p></p>", position, fontsize, 1);
    if ((double) num < (double) fontsize)
    {
      string str = "";
      if (htmlContent.Length > 1000)
        str = $"{str}<p>&nbsp;</p><p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 2500)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 3200)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 4000)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 4800)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 5600)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 5800)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 6000)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 6200)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      if (htmlContent.Length > 7000)
        str = $"{str}<p>&nbsp;</p>{Environment.NewLine}";
      num = ColumnText.FitText(regular, htmlContent + str + Environment.NewLine, position, fontsize, 1);
      if (htmlContent.Length > 6000)
        num = Math.Min(num, 5f);
    }
    ColumnText column = new ColumnText(stamper.GetOverContent(page));
    column.SetSimpleColumn(position);
    ElementDescriptionGenerator.FillSheetColumn(column, htmlContent, num, dynamic);
    column.Go();
  }

  [Obsolete]
  public MainSheetContent MainSheetContent { get; set; }

  [Obsolete]
  public DetailsSheetContent DetailsSheetContent { get; set; }

  [Obsolete]
  public List<SpellcastingSheetContent> SpellcastingSheetContentItems { get; set; }

  [Obsolete]
  private void StampMainContentRevamped(PdfStamper stamper, MainSheetContent content)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "details_character_name",
        content.CharacterName
      },
      {
        "details_player",
        content.PlayerName
      },
      {
        "details_race",
        content.Race
      },
      {
        "details_background",
        content.Background
      },
      {
        "details_build",
        content.ClassLevel
      },
      {
        "details_alignment",
        content.Alignment
      },
      {
        "details_xp",
        content.Experience
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_str_modifier" : "details_str_score",
        content.Strength
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_dex_modifier" : "details_dex_score",
        content.Dexterity
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_con_modifier" : "details_con_score",
        content.Constitution
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_int_modifier" : "details_int_score",
        content.Intelligence
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_wis_modifier" : "details_wis_score",
        content.Wisdom
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_cha_modifier" : "details_cha_score",
        content.Charisma
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_str_score" : "details_str_modifier",
        content.StrengthModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_dex_score" : "details_dex_modifier",
        content.DexterityModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_con_score" : "details_con_modifier",
        content.ConstitutionModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_int_score" : "details_int_modifier",
        content.IntelligenceModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_wis_score" : "details_wis_modifier",
        content.WisdomModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "details_cha_score" : "details_cha_modifier",
        content.CharismaModifier
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "details_acrobatics_total",
        content.Acrobatics
      },
      {
        "details_animalhandling_total",
        content.AnimalHandling
      },
      {
        "details_arcana_total",
        content.Arcana
      },
      {
        "details_athletics_total",
        content.Athletics
      },
      {
        "details_deception_total",
        content.Deception
      },
      {
        "details_history_total",
        content.History
      },
      {
        "details_insight_total",
        content.Insight
      },
      {
        "details_intimidation_total",
        content.Intimidation
      },
      {
        "details_investigation_total",
        content.Investigation
      },
      {
        "details_perception_total",
        content.Perception
      },
      {
        "details_nature_total",
        content.Nature
      },
      {
        "details_performance_total",
        content.Performance
      },
      {
        "details_medicine_total",
        content.Medicine
      },
      {
        "details_religion_total",
        content.Religion
      },
      {
        "details_stealth_total",
        content.Stealth
      },
      {
        "details_persuasion_total",
        content.Persuasion
      },
      {
        "details_sleightofhand_total",
        content.SleightOfHand
      },
      {
        "details_survival_total",
        content.Survival
      },
      {
        "details_acrobatics_proficiency",
        content.AcrobaticsProficient ? "Yes" : "No"
      },
      {
        "details_animalhandling_proficiency",
        content.AnimalHandlingProficient ? "Yes" : "No"
      },
      {
        "details_arcana_proficiency",
        content.ArcanaProficient ? "Yes" : "No"
      },
      {
        "details_athletics_proficiency",
        content.AthleticsProficient ? "Yes" : "No"
      },
      {
        "details_deception_proficiency",
        content.DeceptionProficient ? "Yes" : "No"
      },
      {
        "details_history_proficiency",
        content.HistoryProficient ? "Yes" : "No"
      },
      {
        "details_insight_proficiency",
        content.InsightProficient ? "Yes" : "No"
      },
      {
        "details_intimidation_proficiency",
        content.IntimidationProficient ? "Yes" : "No"
      },
      {
        "details_investigation_proficiency",
        content.InvestigationProficient ? "Yes" : "No"
      },
      {
        "details_medicine_proficiency",
        content.MedicineProficient ? "Yes" : "No"
      },
      {
        "details_nature_proficiency",
        content.NatureProficient ? "Yes" : "No"
      },
      {
        "details_perception_proficiency",
        content.PerceptionProficient ? "Yes" : "No"
      },
      {
        "details_performance_proficiency",
        content.PerformanceProficient ? "Yes" : "No"
      },
      {
        "details_persuasion_proficiency",
        content.PersuasionProficient ? "Yes" : "No"
      },
      {
        "details_religion_proficiency",
        content.ReligionProficient ? "Yes" : "No"
      },
      {
        "details_sleightofhand_proficiency",
        content.SleightOfHandProficient ? "Yes" : "No"
      },
      {
        "details_stealth_proficiency",
        content.StealthProficient ? "Yes" : "No"
      },
      {
        "details_survival_proficiency",
        content.SurvivalProficient ? "Yes" : "No"
      },
      {
        "details_passive_perception_total",
        content.PassiveWisdomPerception
      },
      {
        "details_inspiration",
        content.Inspiration ? "x" : ""
      },
      {
        "details_proficiency_bonus",
        content.ProficiencyBonus
      },
      {
        "details_str_save_total",
        content.StrengthSavingThrow
      },
      {
        "details_dex_save_total",
        content.DexteritySavingThrow
      },
      {
        "details_con_save_total",
        content.ConstitutionSavingThrow
      },
      {
        "details_int_save_total",
        content.IntelligenceSavingThrow
      },
      {
        "details_wis_save_total",
        content.WisdomSavingThrow
      },
      {
        "details_cha_save_total",
        content.CharismaSavingThrow
      },
      {
        "details_saving_throws",
        this.ExportContent.AbilitiesContent.ConditionalSave
      },
      {
        "details_str_save_proficiency",
        content.StrengthSavingThrowProficient ? "Yes" : "No"
      },
      {
        "details_dex_save_proficiency",
        content.DexteritySavingThrowProficient ? "Yes" : "No"
      },
      {
        "details_con_save_proficiency",
        content.ConstitutionSavingThrowProficient ? "Yes" : "No"
      },
      {
        "details_int_save_proficiency",
        content.IntelligenceSavingThrowProficient ? "Yes" : "No"
      },
      {
        "details_wis_save_proficiency",
        content.WisdomSavingThrowProficient ? "Yes" : "No"
      },
      {
        "details_cha_save_proficiency",
        content.CharismaSavingThrowProficient ? "Yes" : "No"
      },
      {
        "details_armor_class",
        content.ArmorClass
      },
      {
        "details_initiative",
        content.Initiative
      },
      {
        "details_initiative_advantage",
        content.InitiativeAdvantage ? "Yes" : "No"
      },
      {
        "details_speed_walking",
        content.Speed + "ft."
      },
      {
        "details_hp_max",
        content.MaximumHitPoints
      },
      {
        "details_hp_current",
        content.CurrentHitPoints
      },
      {
        "details_hp_temp",
        content.TemporaryHitPoints
      },
      {
        "details_hd",
        content.TotalHitDice
      },
      {
        "details_death_save_success_1",
        content.DeathSavingThrowSuccess1 ? "Yes" : "No"
      },
      {
        "details_death_save_success_2",
        content.DeathSavingThrowSuccess2 ? "Yes" : "No"
      },
      {
        "details_death_save_success_3",
        content.DeathSavingThrowSuccess3 ? "Yes" : "No"
      },
      {
        "details_death_save_fail_1",
        content.DeathSavingThrowFailure1 ? "Yes" : "No"
      },
      {
        "details_death_save_fail_2",
        content.DeathSavingThrowFailure2 ? "Yes" : "No"
      },
      {
        "details_death_save_fail_3",
        content.DeathSavingThrowFailure3 ? "Yes" : "No"
      },
      {
        "details_vision",
        this.ExportContent.ConditionsContent.Vision
      },
      {
        "details_attack1_weapon",
        content.Name1
      },
      {
        "details_attack1_attack",
        content.AttackBonus1
      },
      {
        "details_attack1_damage",
        content.DamageType1
      },
      {
        "details_attack2_weapon",
        content.Name2
      },
      {
        "details_attack2_attack",
        content.AttackBonus2
      },
      {
        "details_attack2_damage",
        content.DamageType2
      },
      {
        "details_attack3_weapon",
        content.Name3
      },
      {
        "details_attack3_attack",
        content.AttackBonus3
      },
      {
        "details_attack3_damage",
        content.DamageType3
      },
      {
        "details_attack_description",
        content.AttackAndSpellcastingField
      },
      {
        "details_coinage_cp",
        content.Copper
      },
      {
        "details_coinage_sp",
        content.Silver
      },
      {
        "details_coinage_ep",
        content.Electrum
      },
      {
        "details_coinage_gp",
        content.Gold
      },
      {
        "details_coinage_pp",
        content.Platinum
      },
      {
        "details_equipment_weight",
        this.ExportContent.EquipmentContent.Weight
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "details_equipped_armor",
        content.EquippedArmor
      },
      {
        "details_equipped_shield",
        content.EquippedShield
      },
      {
        "details_armor_conditional",
        content.ConditionalArmorClass
      },
      {
        "details_armor_stealth_disadvantage",
        content.StealthDisadvantage ? "Yes" : "No"
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    int num = 1;
    foreach (CharacterSheetExportContent.AttackExportContent attackExportContent in this.ExportContent.AttacksContent)
    {
      if (num <= 4)
      {
        stamper.AcroFields.SetField($"details_attack{num}_weapon", attackExportContent.Name);
        stamper.AcroFields.SetField($"details_attack{num}_range", attackExportContent.Range);
        stamper.AcroFields.SetField($"details_attack{num}_attack", attackExportContent.Bonus);
        stamper.AcroFields.SetField($"details_attack{num}_damage", attackExportContent.Damage);
        stamper.AcroFields.SetField($"details_attack{num}_misc", attackExportContent.Misc);
        stamper.AcroFields.SetField($"details_attack{num}_description", attackExportContent.Description);
        ++num;
      }
      else
        break;
    }
    for (int index = 0; index < Math.Min(this.ExportContent.EquipmentContent.Equipment.Count, 15); ++index)
    {
      stamper.AcroFields.SetField($"details_equipment_amount_1.{index}", this.ExportContent.EquipmentContent.Equipment[index].Item1);
      stamper.AcroFields.SetField("details_equipment_name_1." + index.ToString(), this.ExportContent.EquipmentContent.Equipment[index].Item2);
      stamper.SetFontSize(7f, $"details_equipment_amount_1.{index}", $"details_equipment_name_1.{index}");
    }
    if (this.Configuration.IncludeFormatting)
      return;
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "details_proficiencies_languages",
        content.ProficienciesAndLanguages
      },
      {
        "details_features",
        content.FeaturesAndTraitsField
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    if (!this.Configuration.IncludeFeatureCards)
      return;
    this.GenerateFeatureCards(stamper, content);
  }

  [Obsolete]
  private void StampDetailsContentRevamped(
    PdfStamper stamper,
    MainSheetContent mainContent,
    DetailsSheetContent content)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "background_character_name",
        content.CharacterName
      },
      {
        "background_story",
        content.CharacterBackstory
      },
      {
        "background_allies",
        content.AlliesAndOrganizations
      },
      {
        "background_organization_name",
        content.OrganizationName
      },
      {
        "background_age",
        content.Age
      },
      {
        "background_eyes",
        content.Eyes
      },
      {
        "background_hair",
        content.Hair
      },
      {
        "background_height",
        content.Height
      },
      {
        "background_skin",
        content.Skin
      },
      {
        "background_weight",
        content.Weight
      },
      {
        "background_traits",
        mainContent.PersonalityTraits
      },
      {
        "background_ideals",
        mainContent.Ideals
      },
      {
        "background_bonds",
        mainContent.Bonds
      },
      {
        "background_flaws",
        mainContent.Flaws
      },
      {
        "background_feature_name",
        content.BackgroundFeatureName
      },
      {
        "background_feature",
        content.BackgroundFeature
      },
      {
        "background_trinket",
        content.Trinket
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value, false);
    try
    {
      AcroFields.FieldPosition fieldPosition = stamper.AcroFields.GetFieldPositions("background_portrait_image").FirstOrDefault<AcroFields.FieldPosition>();
      if (fieldPosition != null)
      {
        PushbuttonField pushbuttonField1 = new PushbuttonField(stamper.Writer, fieldPosition.position, "background_portrait_image-replaced");
        pushbuttonField1.Layout = 2;
        pushbuttonField1.Image = Image.GetInstance(content.CharacterAppearance);
        pushbuttonField1.ProportionalIcon = true;
        pushbuttonField1.Options = 1;
        PushbuttonField pushbuttonField2 = pushbuttonField1;
        stamper.AddAnnotation((PdfAnnotation) pushbuttonField2.Field, fieldPosition.page);
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (StampDetailsContentRevamped));
    }
    try
    {
      AcroFields.FieldPosition fieldPosition = stamper.AcroFields.GetFieldPositions("background_organization_image").FirstOrDefault<AcroFields.FieldPosition>();
      if (fieldPosition == null || string.IsNullOrWhiteSpace(content.OrganizationSymbol))
        return;
      PushbuttonField pushbuttonField3 = new PushbuttonField(stamper.Writer, fieldPosition.position, "background_organization_image-replaced");
      pushbuttonField3.Layout = 2;
      pushbuttonField3.Image = Image.GetInstance(content.OrganizationSymbol);
      pushbuttonField3.ProportionalIcon = true;
      pushbuttonField3.Options = 1;
      PushbuttonField pushbuttonField4 = pushbuttonField3;
      stamper.AddAnnotation((PdfAnnotation) pushbuttonField4.Field, fieldPosition.page);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (StampDetailsContentRevamped));
    }
  }

  [Obsolete]
  private void StampSpellcastingContentRevamped(
    PdfStamper stamper,
    SpellcastingSheetContent content,
    int page)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "spellcasting_class",
        content.SpellcastingClass
      },
      {
        "spellcasting_ability",
        content.SpellcastingAbility
      },
      {
        "spellcasting_save",
        content.SpellcastingSave
      },
      {
        "spellcasting_attack_bonus",
        content.SpellcastingAttackModifier
      },
      {
        "spellcasting_prepare_count",
        content.SpellcastingPrepareCount
      },
      {
        "spellcasting_notes",
        content.SpellcastingNotes
      }
    })
      stamper.AcroFields.SetField($"{keyValuePair.Key}:{page}", keyValuePair.Value);
    AcroFields acroFields1 = stamper.AcroFields;
    string name1 = $"spellcasting_slot1_count:{page}";
    int slotsCount1 = content.Spells1.SlotsCount;
    string str1 = slotsCount1.ToString();
    acroFields1.SetField(name1, str1);
    AcroFields acroFields2 = stamper.AcroFields;
    string name2 = $"spellcasting_slot2_count:{page}";
    slotsCount1 = content.Spells2.SlotsCount;
    string str2 = slotsCount1.ToString();
    acroFields2.SetField(name2, str2);
    stamper.AcroFields.SetField($"spellcasting_slot3_count:{page}", content.Spells3.SlotsCount.ToString());
    AcroFields acroFields3 = stamper.AcroFields;
    string name3 = $"spellcasting_slot4_count:{page}";
    int slotsCount2 = content.Spells4.SlotsCount;
    string str3 = slotsCount2.ToString();
    acroFields3.SetField(name3, str3);
    AcroFields acroFields4 = stamper.AcroFields;
    string name4 = $"spellcasting_slot5_count:{page}";
    slotsCount2 = content.Spells5.SlotsCount;
    string str4 = slotsCount2.ToString();
    acroFields4.SetField(name4, str4);
    AcroFields acroFields5 = stamper.AcroFields;
    string name5 = $"spellcasting_slot6_count:{page}";
    slotsCount2 = content.Spells6.SlotsCount;
    string str5 = slotsCount2.ToString();
    acroFields5.SetField(name5, str5);
    AcroFields acroFields6 = stamper.AcroFields;
    string name6 = $"spellcasting_slot7_count:{page}";
    slotsCount2 = content.Spells7.SlotsCount;
    string str6 = slotsCount2.ToString();
    acroFields6.SetField(name6, str6);
    AcroFields acroFields7 = stamper.AcroFields;
    string name7 = $"spellcasting_slot8_count:{page}";
    slotsCount2 = content.Spells8.SlotsCount;
    string str7 = slotsCount2.ToString();
    acroFields7.SetField(name7, str7);
    AcroFields acroFields8 = stamper.AcroFields;
    string name8 = $"spellcasting_slot9_count:{page}";
    slotsCount2 = content.Spells9.SlotsCount;
    string str8 = slotsCount2.ToString();
    acroFields8.SetField(name8, str8);
    int num1 = 0;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Cantrips.Collection)
    {
      stamper.AcroFields.SetField($"spell_name_1.{num1}:{page}", spellcastingSpellContent.Name);
      ++num1;
    }
    int num2 = num1 + 1;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells1.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num2}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num2}:{page}", spellcastingSpellContent.Name);
      ++num2;
    }
    int num3 = num2 + 1;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells2.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num3}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num3}:{page}", spellcastingSpellContent.Name);
      ++num3;
    }
    int num4 = num3 + 1;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells3.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num4}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num4}:{page}", spellcastingSpellContent.Name);
      ++num4;
    }
    int num5 = num4 + 1;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells4.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num5}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num5}:{page}", spellcastingSpellContent.Name);
      ++num5;
    }
    int num6 = num5 + 1;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells5.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num6}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num6}:{page}", spellcastingSpellContent.Name);
      ++num6;
    }
    int num7 = num6 + 1;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells6.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num7}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num7}:{page}", spellcastingSpellContent.Name);
      ++num7;
    }
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells7.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num7}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num7}:{page}", spellcastingSpellContent.Name);
      ++num7;
    }
    int num8 = num7 + 1;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells8.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num8}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num8}:{page}", spellcastingSpellContent.Name);
      ++num8;
    }
    int num9 = num8 + 1;
    foreach (SpellcastingSpellContent spellcastingSpellContent in (Collection<SpellcastingSpellContent>) content.Spells9.Collection)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num9}:{page}", spellcastingSpellContent.IsPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num9}:{page}", spellcastingSpellContent.Name);
      ++num9;
    }
  }

  private void GenerateSpellcardsRevamped(
    PdfStamper stamper,
    IEnumerable<Spell> spells,
    int startingPage = 1)
  {
    Logger.Info("stamping revamped spellcards");
    int num1 = startingPage;
    int num2 = 0;
    PageContentWriter pageContentWriter = new PageContentWriter(stamper);
    foreach (Spell spell in spells)
    {
      string str = $"_{num1}:{num2}";
      foreach (IPageContentItem pageContentItem in new List<IPageContentItem>()
      {
        (IPageContentItem) new LineContent("card_title" + str, spell.Name),
        (IPageContentItem) new LineContent("card_subtitle" + str, spell.Underline),
        (IPageContentItem) new LineContent("card_time" + str, spell.CastingTime),
        (IPageContentItem) new LineContent("card_range" + str, spell.Range),
        (IPageContentItem) new LineContent("card_duration" + str, spell.Duration),
        (IPageContentItem) new LineContent("card_components" + str, spell.GetComponentsString()),
        (IPageContentItem) new LineContent("card_description" + str, this.GeneratePlainDescription(spell.Description)),
        (IPageContentItem) new LineContent("card_footer_right" + str, spell.Source)
      })
        pageContentWriter.Write<IPageContentItem>(pageContentItem);
      ++num2;
      if (num2 == 9)
      {
        num2 = 0;
        ++num1;
      }
    }
  }

  private void GenerateItemCards(
    PdfStamper stamper,
    IEnumerable<RefactoredEquipmentItem> items,
    int currentPage = 1)
  {
    Logger.Info("stamping revamped itemcards");
    int num = 0;
    PageContentWriter pageContentWriter = new PageContentWriter(stamper);
    foreach (RefactoredEquipmentItem refactoredEquipmentItem in items)
    {
      string str = $"_{currentPage}:{num}";
      string content = "";
      if (refactoredEquipmentItem.IsAdorned || refactoredEquipmentItem.Item.Type.Equals("Magic Item"))
      {
        StringBuilder stringBuilder = new StringBuilder();
        Builder.Data.Elements.Item obj = refactoredEquipmentItem.IsAdorned ? refactoredEquipmentItem.AdornerItem : refactoredEquipmentItem.Item;
        if (!string.IsNullOrWhiteSpace(obj.ItemType))
        {
          string additionAttribute = obj.GetSetterAdditionAttribute("type");
          stringBuilder.Append(additionAttribute != null ? $"{obj.ItemType} ({additionAttribute}), " : obj.ItemType + ", ");
        }
        else
          stringBuilder.Append("Magic item, ");
        if (!string.IsNullOrWhiteSpace(obj.Rarity))
          stringBuilder.Append(obj.Rarity.ToLower() + " ");
        if (obj.RequiresAttunement)
        {
          string additionAttribute = obj.GetSetterAdditionAttribute("attunement");
          stringBuilder.Append(additionAttribute != null ? $"(requires attunement {additionAttribute})" : "(requires attunement)");
        }
        content = stringBuilder.ToString();
      }
      else if (!refactoredEquipmentItem.IsAdorned && refactoredEquipmentItem.Item.Type.Equals("Weapon"))
      {
        WeaponElement weaponElement = refactoredEquipmentItem.Item.AsElement<WeaponElement>();
        foreach (ElementBase elementBase in DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Weapon Category"))).OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)).ToList<ElementBase>())
        {
          if (weaponElement.Supports.Contains(elementBase.Id))
            content = elementBase.Name + " Weapon";
        }
      }
      else
        content = refactoredEquipmentItem.Item.Category;
      foreach (IPageContentItem pageContentItem in new List<IPageContentItem>()
      {
        (IPageContentItem) new LineContent("card_title" + str, string.IsNullOrWhiteSpace(refactoredEquipmentItem.AlternativeName) ? refactoredEquipmentItem.DisplayName : refactoredEquipmentItem.AlternativeName),
        (IPageContentItem) new LineContent("card_subtitle" + str, content),
        (IPageContentItem) new LineContent("card_description" + str, this.GetHtmlString(refactoredEquipmentItem)),
        (IPageContentItem) new LineContent("card_footer_left" + str, refactoredEquipmentItem.IsEquipped ? "Equipped" : ""),
        (IPageContentItem) new LineContent("card_footer_right" + str, refactoredEquipmentItem.Item.Source)
      })
        pageContentWriter.Write<IPageContentItem>(pageContentItem);
      ++num;
      if (num == 9)
      {
        num = 0;
        ++currentPage;
      }
    }
  }

  private void GenerateFeatureCards(PdfStamper stamper, MainSheetContent content, int currentPage = 1)
  {
    int num = 0;
    PageContentWriter pageContentWriter = new PageContentWriter(stamper);
    foreach (ContentLine line in content.FeaturesFieldContent.Lines)
    {
      if (!(line.Content == ""))
      {
        string str = $"_{currentPage}:{num}";
        foreach (IPageContentItem pageContentItem in new List<IPageContentItem>()
        {
          (IPageContentItem) new LineContent("card_title" + str, line.Name),
          (IPageContentItem) new LineContent("card_subtitle" + str, "feature"),
          (IPageContentItem) new LineContent("card_description" + str, line.Content),
          (IPageContentItem) new LineContent("card_footer_left" + str, ""),
          (IPageContentItem) new LineContent("card_footer_right" + str, "")
        })
          pageContentWriter.Write<IPageContentItem>(pageContentItem);
        ++num;
        if (num == 9)
        {
          num = 0;
          ++currentPage;
        }
      }
    }
  }

  private void GenerateCardsRevamped(PdfStamper stamper)
  {
    int num1 = 1;
    int num2 = 0;
    PageContentWriter pageContentWriter = new PageContentWriter(stamper);
    if (this.Configuration.IncludeSpellcards)
    {
      foreach (Spell spell in this.Spells)
      {
        string str = $"_{num1}:{num2}";
        foreach (IPageContentItem pageContentItem in new List<IPageContentItem>()
        {
          (IPageContentItem) new LineContent("card_title" + str, spell.Name),
          (IPageContentItem) new LineContent("card_subtitle" + str, spell.Underline),
          (IPageContentItem) new LineContent("card_time" + str, spell.CastingTime),
          (IPageContentItem) new LineContent("card_range" + str, spell.Range),
          (IPageContentItem) new LineContent("card_duration" + str, spell.Duration),
          (IPageContentItem) new LineContent("card_components" + str, spell.GetComponentsString()),
          (IPageContentItem) new LineContent("card_description" + str, this.GeneratePlainDescription(spell.Description)),
          (IPageContentItem) new LineContent("card_footer_right" + str, spell.Source)
        })
          pageContentWriter.Write<IPageContentItem>(pageContentItem);
        ++num2;
        if (num2 == 9)
        {
          num2 = 0;
          ++num1;
        }
      }
    }
    if (!this.Configuration.IncludeItemcards)
      return;
    foreach (RefactoredEquipmentItem refactoredEquipmentItem in this.Items)
    {
      string str = $"_{num1}:{num2}";
      string content = "";
      if (refactoredEquipmentItem.IsAdorned || refactoredEquipmentItem.Item.Type.Equals("Magic Item"))
      {
        StringBuilder stringBuilder = new StringBuilder();
        Builder.Data.Elements.Item obj = refactoredEquipmentItem.IsAdorned ? refactoredEquipmentItem.AdornerItem : refactoredEquipmentItem.Item;
        if (!string.IsNullOrWhiteSpace(obj.ItemType))
        {
          string additionAttribute = obj.GetSetterAdditionAttribute("type");
          stringBuilder.Append(additionAttribute != null ? $"{obj.ItemType} ({additionAttribute}), " : obj.ItemType + ", ");
        }
        else
          stringBuilder.Append("Magic item, ");
        if (!string.IsNullOrWhiteSpace(obj.Rarity))
          stringBuilder.Append(obj.Rarity.ToLower() + " ");
        if (obj.RequiresAttunement)
        {
          string additionAttribute = obj.GetSetterAdditionAttribute("attunement");
          stringBuilder.Append(additionAttribute != null ? $"(requires attunement {additionAttribute})" : "(requires attunement)");
        }
        content = stringBuilder.ToString();
      }
      else if (!refactoredEquipmentItem.IsAdorned && refactoredEquipmentItem.Item.Type.Equals("Weapon"))
      {
        WeaponElement weaponElement = refactoredEquipmentItem.Item.AsElement<WeaponElement>();
        foreach (ElementBase elementBase in DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Weapon Category"))).OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)).ToList<ElementBase>())
        {
          if (weaponElement.Supports.Contains(elementBase.Id))
            content = elementBase.Name + " Weapon";
        }
      }
      else
        content = refactoredEquipmentItem.Item.Category;
      foreach (IPageContentItem pageContentItem in new List<IPageContentItem>()
      {
        (IPageContentItem) new LineContent("card_title" + str, string.IsNullOrWhiteSpace(refactoredEquipmentItem.AlternativeName) ? refactoredEquipmentItem.DisplayName : refactoredEquipmentItem.AlternativeName),
        (IPageContentItem) new LineContent("card_subtitle" + str, content),
        (IPageContentItem) new LineContent("card_description" + str, this.GetHtmlString(refactoredEquipmentItem)),
        (IPageContentItem) new LineContent("card_footer_left" + str, refactoredEquipmentItem.IsEquipped ? "Equipped" : ""),
        (IPageContentItem) new LineContent("card_footer_right" + str, refactoredEquipmentItem.Item.Source)
      })
        pageContentWriter.Write<IPageContentItem>(pageContentItem);
      ++num2;
      if (num2 == 9)
      {
        num2 = 0;
        ++num1;
      }
    }
  }

  private string GeneratePlainDescription(string description)
  {
    StringBuilder stringBuilder1 = new StringBuilder();
    foreach (IElement to in HTMLWorker.ParseToList((TextReader) new StringReader(description), (StyleSheet) null))
    {
      StringBuilder stringBuilder2 = new StringBuilder();
      foreach (Chunk chunk in (IEnumerable<Chunk>) to.Chunks)
      {
        if (to is List)
        {
          Chunk symbol = (to as List).Symbol;
          stringBuilder2.AppendLine($"{symbol} {chunk.Content}");
        }
        else
          stringBuilder2.Append(chunk.Content);
      }
      stringBuilder1.AppendLine(stringBuilder2.ToString());
      stringBuilder1.AppendLine();
    }
    return stringBuilder1.ToString();
  }

  private string GetHtmlString(RefactoredEquipmentItem _item)
  {
    List<IElement> toList = HTMLWorker.ParseToList((TextReader) new StringReader(_item.IsAdorned ? _item.AdornerItem.Description : _item.Item.Description), (StyleSheet) null);
    if (!_item.IsAdorned && (_item.Item.Type.Equals("Weapon") || _item.Item.Type.Equals("Armor")))
      toList = HTMLWorker.ParseToList((TextReader) new StringReader(ElementCardHelper.GenerateHeaderForCard((ElementBase) _item.Item)), (StyleSheet) null);
    StringBuilder stringBuilder1 = new StringBuilder();
    foreach (IElement element in toList)
    {
      StringBuilder stringBuilder2 = new StringBuilder();
      foreach (Chunk chunk in (IEnumerable<Chunk>) element.Chunks)
      {
        if (element.GetType() == typeof (List))
        {
          stringBuilder2.Append((object) (element as List).Symbol);
          stringBuilder2.AppendLine(chunk.Content);
        }
        else
          stringBuilder2.Append(chunk.Content);
      }
      stringBuilder1.AppendLine(stringBuilder2.ToString());
      stringBuilder1.AppendLine();
    }
    if (!string.IsNullOrWhiteSpace(_item.Notes))
    {
      stringBuilder1.AppendLine("ADDITIONAL NOTES");
      stringBuilder1.AppendLine(_item.Notes);
    }
    return stringBuilder1.ToString();
  }

  [Obsolete("used in legacy spellcasting page")]
  private void StampSpellcastingContent(
    PdfStamper stamper,
    SpellcastingSheetContent content,
    int page)
  {
    Dictionary<string, string> dictionary1 = new Dictionary<string, string>();
    dictionary1.Add("Spellcasting Class 2", content.SpellcastingClass);
    dictionary1.Add("SpellcastingAbility 2", content.SpellcastingAbility);
    dictionary1.Add("SpellSaveDC  2", content.SpellcastingSave);
    dictionary1.Add("SpellAtkBonus 2", content.SpellcastingAttackModifier);
    Dictionary<string, string> dictionary2 = new Dictionary<string, string>()
    {
      {
        "Spells 1014",
        content.Cantrips.GetSpell(0, true).Name
      },
      {
        "Spells 1016",
        content.Cantrips.GetSpell(1, true).Name
      },
      {
        "Spells 1017",
        content.Cantrips.GetSpell(2, true).Name
      },
      {
        "Spells 1018",
        content.Cantrips.GetSpell(3, true).Name
      },
      {
        "Spells 1019",
        content.Cantrips.GetSpell(4, true).Name
      },
      {
        "Spells 1020",
        content.Cantrips.GetSpell(5, true).Name
      },
      {
        "Spells 1021",
        content.Cantrips.GetSpell(6, true).Name
      },
      {
        "Spells 1022",
        content.Cantrips.GetSpell(7, true).Name
      }
    };
    Dictionary<string, string> dictionary3 = new Dictionary<string, string>()
    {
      {
        "SlotsTotal 19",
        content.Spells1.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 19",
        content.Spells1.ExpendedSlotsCount > 0 ? content.Spells1.ExpendedSlotsCount.ToString() : ""
      },
      {
        "SlotsTotal 20",
        content.Spells2.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 20",
        content.Spells2.ExpendedSlotsCount > 0 ? content.Spells2.ExpendedSlotsCount.ToString() : ""
      },
      {
        "SlotsTotal 21",
        content.Spells3.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 21",
        content.Spells3.ExpendedSlotsCount > 0 ? content.Spells3.ExpendedSlotsCount.ToString() : ""
      },
      {
        "SlotsTotal 22",
        content.Spells4.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 22",
        content.Spells4.ExpendedSlotsCount > 0 ? content.Spells4.ExpendedSlotsCount.ToString() : ""
      },
      {
        "SlotsTotal 23",
        content.Spells5.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 23",
        content.Spells5.ExpendedSlotsCount > 0 ? content.Spells5.ExpendedSlotsCount.ToString() : ""
      },
      {
        "SlotsTotal 24",
        content.Spells6.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 24",
        content.Spells6.ExpendedSlotsCount > 0 ? content.Spells6.ExpendedSlotsCount.ToString() : ""
      },
      {
        "SlotsTotal 25",
        content.Spells7.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 25",
        content.Spells7.ExpendedSlotsCount > 0 ? content.Spells7.ExpendedSlotsCount.ToString() : ""
      },
      {
        "SlotsTotal 26",
        content.Spells8.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 26",
        content.Spells8.ExpendedSlotsCount > 0 ? content.Spells8.ExpendedSlotsCount.ToString() : ""
      },
      {
        "SlotsTotal 27",
        content.Spells9.SlotsCount.ToString()
      },
      {
        "SlotsRemaining 27",
        content.Spells9.ExpendedSlotsCount > 0 ? content.Spells9.ExpendedSlotsCount.ToString() : ""
      }
    };
    foreach (KeyValuePair<string, string> keyValuePair in dictionary1)
      stamper.AcroFields.SetField($"{keyValuePair.Key}:{page}", keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in dictionary2)
      stamper.AcroFields.SetField($"{keyValuePair.Key}:{page}", keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in dictionary3)
      stamper.AcroFields.SetField($"{keyValuePair.Key}:{page}", keyValuePair.Value);
    Dictionary<string, string> pairs1 = new Dictionary<string, string>()
    {
      {
        "Spells 1015",
        "Check Box 251"
      },
      {
        "Spells 1023",
        "Check Box 309"
      },
      {
        "Spells 1024",
        "Check Box 3010"
      },
      {
        "Spells 1025",
        "Check Box 3011"
      },
      {
        "Spells 1026",
        "Check Box 3012"
      },
      {
        "Spells 1027",
        "Check Box 3013"
      },
      {
        "Spells 1028",
        "Check Box 3014"
      },
      {
        "Spells 1029",
        "Check Box 3015"
      },
      {
        "Spells 1030",
        "Check Box 3016"
      },
      {
        "Spells 1031",
        "Check Box 3017"
      },
      {
        "Spells 1032",
        "Check Box 3018"
      },
      {
        "Spells 1033",
        "Check Box 3019"
      }
    };
    Dictionary<string, string> pairs2 = new Dictionary<string, string>()
    {
      {
        "Spells 1046",
        "Check Box 313"
      },
      {
        "Spells 1034",
        "Check Box 310"
      },
      {
        "Spells 1035",
        "Check Box 3020"
      },
      {
        "Spells 1036",
        "Check Box 3021"
      },
      {
        "Spells 1037",
        "Check Box 3022"
      },
      {
        "Spells 1038",
        "Check Box 3023"
      },
      {
        "Spells 1039",
        "Check Box 3024"
      },
      {
        "Spells 1040",
        "Check Box 3025"
      },
      {
        "Spells 1041",
        "Check Box 3026"
      },
      {
        "Spells 1042",
        "Check Box 3027"
      },
      {
        "Spells 1043",
        "Check Box 3028"
      },
      {
        "Spells 1044",
        "Check Box 3029"
      },
      {
        "Spells 1045",
        "Check Box 3030"
      }
    };
    Dictionary<string, string> pairs3 = new Dictionary<string, string>()
    {
      {
        "Spells 1048",
        "Check Box 315"
      },
      {
        "Spells 1047",
        "Check Box 314"
      },
      {
        "Spells 1049",
        "Check Box 3031"
      },
      {
        "Spells 1050",
        "Check Box 3032"
      },
      {
        "Spells 1051",
        "Check Box 3033"
      },
      {
        "Spells 1052",
        "Check Box 3034"
      },
      {
        "Spells 1053",
        "Check Box 3035"
      },
      {
        "Spells 1054",
        "Check Box 3036"
      },
      {
        "Spells 1055",
        "Check Box 3037"
      },
      {
        "Spells 1056",
        "Check Box 3038"
      },
      {
        "Spells 1057",
        "Check Box 3039"
      },
      {
        "Spells 1058",
        "Check Box 3040"
      },
      {
        "Spells 1059",
        "Check Box 3041"
      }
    };
    Dictionary<string, string> pairs4 = new Dictionary<string, string>()
    {
      {
        "Spells 1061",
        "Check Box 317"
      },
      {
        "Spells 1060",
        "Check Box 316"
      },
      {
        "Spells 1062",
        "Check Box 3042"
      },
      {
        "Spells 1063",
        "Check Box 3043"
      },
      {
        "Spells 1064",
        "Check Box 3044"
      },
      {
        "Spells 1065",
        "Check Box 3045"
      },
      {
        "Spells 1066",
        "Check Box 3046"
      },
      {
        "Spells 1067",
        "Check Box 3047"
      },
      {
        "Spells 1068",
        "Check Box 3048"
      },
      {
        "Spells 1069",
        "Check Box 3049"
      },
      {
        "Spells 1070",
        "Check Box 3050"
      },
      {
        "Spells 1071",
        "Check Box 3051"
      },
      {
        "Spells 1072",
        "Check Box 3052"
      }
    };
    Dictionary<string, string> pairs5 = new Dictionary<string, string>()
    {
      {
        "Spells 1074",
        "Check Box 319"
      },
      {
        "Spells 1073",
        "Check Box 318"
      },
      {
        "Spells 1075",
        "Check Box 3053"
      },
      {
        "Spells 1076",
        "Check Box 3054"
      },
      {
        "Spells 1077",
        "Check Box 3055"
      },
      {
        "Spells 1078",
        "Check Box 3056"
      },
      {
        "Spells 1079",
        "Check Box 3057"
      },
      {
        "Spells 1080",
        "Check Box 3058"
      },
      {
        "Spells 1081",
        "Check Box 3059"
      }
    };
    Dictionary<string, string> pairs6 = new Dictionary<string, string>()
    {
      {
        "Spells 1083",
        "Check Box 321"
      },
      {
        "Spells 1082",
        "Check Box 320"
      },
      {
        "Spells 1084",
        "Check Box 3060"
      },
      {
        "Spells 1085",
        "Check Box 3061"
      },
      {
        "Spells 1086",
        "Check Box 3062"
      },
      {
        "Spells 1087",
        "Check Box 3063"
      },
      {
        "Spells 1088",
        "Check Box 3064"
      },
      {
        "Spells 1089",
        "Check Box 3065"
      },
      {
        "Spells 1090",
        "Check Box 3066"
      }
    };
    Dictionary<string, string> pairs7 = new Dictionary<string, string>()
    {
      {
        "Spells 1092",
        "Check Box 323"
      },
      {
        "Spells 1091",
        "Check Box 322"
      },
      {
        "Spells 1093",
        "Check Box 3067"
      },
      {
        "Spells 1094",
        "Check Box 3068"
      },
      {
        "Spells 1095",
        "Check Box 3069"
      },
      {
        "Spells 1096",
        "Check Box 3070"
      },
      {
        "Spells 1097",
        "Check Box 3071"
      },
      {
        "Spells 1098",
        "Check Box 3072"
      },
      {
        "Spells 1099",
        "Check Box 3073"
      }
    };
    Dictionary<string, string> pairs8 = new Dictionary<string, string>()
    {
      {
        "Spells 10101",
        "Check Box 325"
      },
      {
        "Spells 10100",
        "Check Box 324"
      },
      {
        "Spells 10102",
        "Check Box 3074"
      },
      {
        "Spells 10103",
        "Check Box 3075"
      },
      {
        "Spells 10104",
        "Check Box 3076"
      },
      {
        "Spells 10105",
        "Check Box 3077"
      },
      {
        "Spells 10106",
        "Check Box 3078"
      }
    };
    Dictionary<string, string> pairs9 = new Dictionary<string, string>()
    {
      {
        "Spells 10108",
        "Check Box 327"
      },
      {
        "Spells 10107",
        "Check Box 326"
      },
      {
        "Spells 10109",
        "Check Box 3079"
      },
      {
        "Spells 101010",
        "Check Box 3080"
      },
      {
        "Spells 101011",
        "Check Box 3081"
      },
      {
        "Spells 101012",
        "Check Box 3082"
      },
      {
        "Spells 101013",
        "Check Box 3083"
      }
    };
    this.StampSpellList(stamper, pairs1, content.Spells1, page);
    this.StampSpellList(stamper, pairs2, content.Spells2, page);
    this.StampSpellList(stamper, pairs3, content.Spells3, page);
    this.StampSpellList(stamper, pairs4, content.Spells4, page);
    this.StampSpellList(stamper, pairs5, content.Spells5, page);
    this.StampSpellList(stamper, pairs6, content.Spells6, page);
    this.StampSpellList(stamper, pairs7, content.Spells7, page);
    this.StampSpellList(stamper, pairs8, content.Spells8, page);
    this.StampSpellList(stamper, pairs9, content.Spells9, page);
  }

  [Obsolete("used in legacy spellcasting page")]
  private void StampSpellList(
    PdfStamper stamper,
    Dictionary<string, string> pairs,
    SpellcastingSpellsContent content,
    int page)
  {
    int index = 0;
    foreach (KeyValuePair<string, string> pair in pairs)
    {
      stamper.AcroFields.SetField($"{pair.Key}:{page}", content.GetSpell(index, true).Name);
      stamper.AcroFields.SetField($"{pair.Value}:{page}", content.GetSpell(index, true).IsPrepared ? "Yes" : "No");
      ++index;
    }
  }

  private void StampLegacyDetailsContent(PdfStamper stamper)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "CharacterName",
        this.ExportContent.CharacterName
      },
      {
        "PlayerName",
        this.ExportContent.PlayerName
      },
      {
        "Race ",
        this.ExportContent.Race
      },
      {
        "Background",
        this.ExportContent.BackgroundContent.Name
      },
      {
        "ClassLevel",
        this.ExportContent.Level
      },
      {
        "Alignment",
        this.ExportContent.Alignment
      },
      {
        "XP",
        this.ExportContent.Experience
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        this.Configuration.IsAttributeDisplayFlipped ? "STRmod" : "STR",
        this.ExportContent.AbilitiesContent.Strength
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "DEXmod " : "DEX",
        this.ExportContent.AbilitiesContent.Dexterity
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "CONmod" : "CON",
        this.ExportContent.AbilitiesContent.Constitution
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "INTmod" : "INT",
        this.ExportContent.AbilitiesContent.Intelligence
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "WISmod" : "WIS",
        this.ExportContent.AbilitiesContent.Wisdom
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "CHamod" : "CHA",
        this.ExportContent.AbilitiesContent.Charisma
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "STR" : "STRmod",
        this.ExportContent.AbilitiesContent.StrengthModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "DEX" : "DEXmod ",
        this.ExportContent.AbilitiesContent.DexterityModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "CON" : "CONmod",
        this.ExportContent.AbilitiesContent.ConstitutionModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "INT" : "INTmod",
        this.ExportContent.AbilitiesContent.IntelligenceModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "WIS" : "WISmod",
        this.ExportContent.AbilitiesContent.WisdomModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "CHA" : "CHamod",
        this.ExportContent.AbilitiesContent.CharismaModifier
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "Acrobatics",
        this.ExportContent.SkillsContent.Acrobatics
      },
      {
        "Animal",
        this.ExportContent.SkillsContent.AnimalHandling
      },
      {
        "Athletics",
        this.ExportContent.SkillsContent.Athletics
      },
      {
        "Deception ",
        this.ExportContent.SkillsContent.Deception
      },
      {
        "History ",
        this.ExportContent.SkillsContent.History
      },
      {
        "Insight",
        this.ExportContent.SkillsContent.Insight
      },
      {
        "Intimidation",
        this.ExportContent.SkillsContent.Intimidation
      },
      {
        "Investigation ",
        this.ExportContent.SkillsContent.Investigation
      },
      {
        "Arcana",
        this.ExportContent.SkillsContent.Arcana
      },
      {
        "Perception ",
        this.ExportContent.SkillsContent.Perception
      },
      {
        "Nature",
        this.ExportContent.SkillsContent.Nature
      },
      {
        "Performance",
        this.ExportContent.SkillsContent.Performance
      },
      {
        "Medicine",
        this.ExportContent.SkillsContent.Medicine
      },
      {
        "Religion",
        this.ExportContent.SkillsContent.Religion
      },
      {
        "Stealth ",
        this.ExportContent.SkillsContent.Stealth
      },
      {
        "Persuasion",
        this.ExportContent.SkillsContent.Persuasion
      },
      {
        "SleightofHand",
        this.ExportContent.SkillsContent.SleightOfHand
      },
      {
        "Survival",
        this.ExportContent.SkillsContent.Survival
      },
      {
        "Check Box 23",
        this.ExportContent.SkillsContent.AcrobaticsProficient ? "Yes" : "No"
      },
      {
        "Check Box 24",
        this.ExportContent.SkillsContent.AnimalHandlingProficient ? "Yes" : "No"
      },
      {
        "Check Box 25",
        this.ExportContent.SkillsContent.ArcanaProficient ? "Yes" : "No"
      },
      {
        "Check Box 26",
        this.ExportContent.SkillsContent.AthleticsProficient ? "Yes" : "No"
      },
      {
        "Check Box 27",
        this.ExportContent.SkillsContent.DeceptionProficient ? "Yes" : "No"
      },
      {
        "Check Box 28",
        this.ExportContent.SkillsContent.HistoryProficient ? "Yes" : "No"
      },
      {
        "Check Box 29",
        this.ExportContent.SkillsContent.InsightProficient ? "Yes" : "No"
      },
      {
        "Check Box 30",
        this.ExportContent.SkillsContent.IntimidationProficient ? "Yes" : "No"
      },
      {
        "Check Box 31",
        this.ExportContent.SkillsContent.InvestigationProficient ? "Yes" : "No"
      },
      {
        "Check Box 32",
        this.ExportContent.SkillsContent.MedicineProficient ? "Yes" : "No"
      },
      {
        "Check Box 33",
        this.ExportContent.SkillsContent.NatureProficient ? "Yes" : "No"
      },
      {
        "Check Box 34",
        this.ExportContent.SkillsContent.PerceptionProficient ? "Yes" : "No"
      },
      {
        "Check Box 35",
        this.ExportContent.SkillsContent.PerformanceProficient ? "Yes" : "No"
      },
      {
        "Check Box 36",
        this.ExportContent.SkillsContent.PersuasionProficient ? "Yes" : "No"
      },
      {
        "Check Box 37",
        this.ExportContent.SkillsContent.ReligionProficient ? "Yes" : "No"
      },
      {
        "Check Box 38",
        this.ExportContent.SkillsContent.SleightOfHandProficient ? "Yes" : "No"
      },
      {
        "Check Box 39",
        this.ExportContent.SkillsContent.StealthProficient ? "Yes" : "No"
      },
      {
        "Check Box 40",
        this.ExportContent.SkillsContent.SurvivalProficient ? "Yes" : "No"
      },
      {
        "Passive",
        this.ExportContent.SkillsContent.PerceptionPassive
      },
      {
        "Inspiration",
        this.ExportContent.Inspiration ? "x" : ""
      },
      {
        "ProfBonus",
        this.ExportContent.ProficiencyBonus
      },
      {
        "ST Strength",
        this.ExportContent.AbilitiesContent.StrengthSave
      },
      {
        "ST Dexterity",
        this.ExportContent.AbilitiesContent.DexteritySave
      },
      {
        "ST Constitution",
        this.ExportContent.AbilitiesContent.ConstitutionSave
      },
      {
        "ST Intelligence",
        this.ExportContent.AbilitiesContent.IntelligenceSave
      },
      {
        "ST Wisdom",
        this.ExportContent.AbilitiesContent.WisdomSave
      },
      {
        "ST Charisma",
        this.ExportContent.AbilitiesContent.CharismaSave
      },
      {
        "Check Box 11",
        this.ExportContent.AbilitiesContent.StrengthSaveProficient ? "Yes" : "No"
      },
      {
        "Check Box 18",
        this.ExportContent.AbilitiesContent.DexteritySaveProficient ? "Yes" : "No"
      },
      {
        "Check Box 19",
        this.ExportContent.AbilitiesContent.ConstitutionSaveProficient ? "Yes" : "No"
      },
      {
        "Check Box 20",
        this.ExportContent.AbilitiesContent.IntelligenceSaveProficient ? "Yes" : "No"
      },
      {
        "Check Box 21",
        this.ExportContent.AbilitiesContent.WisdomSaveProficient ? "Yes" : "No"
      },
      {
        "Check Box 22",
        this.ExportContent.AbilitiesContent.CharismaSaveProficient ? "Yes" : "No"
      },
      {
        "AC",
        this.ExportContent.ArmorClassContent.ArmorClass
      },
      {
        "Initiative",
        this.ExportContent.Initiative
      },
      {
        "Speed",
        this.ExportContent.ConditionsContent.WalkingSpeed + "ft."
      },
      {
        "HPMax",
        this.ExportContent.HitPointsContent.Maximum
      },
      {
        "HPCurrent",
        this.ExportContent.HitPointsContent.Current
      },
      {
        "HPTemp",
        this.ExportContent.HitPointsContent.Temporary
      },
      {
        "HD",
        ""
      },
      {
        "HDTotal",
        this.ExportContent.HitPointsContent.HitDice
      },
      {
        "Check Box 12",
        this.ExportContent.HitPointsContent.DeathSavingThrowSuccess1 ? "Yes" : "No"
      },
      {
        "Check Box 13",
        this.ExportContent.HitPointsContent.DeathSavingThrowSuccess2 ? "Yes" : "No"
      },
      {
        "Check Box 14",
        this.ExportContent.HitPointsContent.DeathSavingThrowSuccess3 ? "Yes" : "No"
      },
      {
        "Check Box 15",
        this.ExportContent.HitPointsContent.DeathSavingThrowFailure1 ? "Yes" : "No"
      },
      {
        "Check Box 16",
        this.ExportContent.HitPointsContent.DeathSavingThrowFailure2 ? "Yes" : "No"
      },
      {
        "Check Box 17",
        this.ExportContent.HitPointsContent.DeathSavingThrowFailure3 ? "Yes" : "No"
      },
      {
        "Wpn Name",
        this.ExportContent.AttacksContent[0]?.Name
      },
      {
        "Wpn1 AtkBonus",
        this.ExportContent.AttacksContent[0]?.Bonus
      },
      {
        "Wpn1 Damage",
        this.ExportContent.AttacksContent[0]?.Damage
      },
      {
        "Wpn Name 2",
        this.ExportContent.AttacksContent[0]?.Name
      },
      {
        "Wpn2 AtkBonus ",
        this.ExportContent.AttacksContent[0]?.Bonus
      },
      {
        "Wpn2 Damage ",
        this.ExportContent.AttacksContent[0]?.Damage
      },
      {
        "Wpn Name 3",
        this.ExportContent.AttacksContent[0]?.Name
      },
      {
        "Wpn3 AtkBonus  ",
        this.ExportContent.AttacksContent[0]?.Bonus
      },
      {
        "Wpn3 Damage ",
        this.ExportContent.AttacksContent[0]?.Damage
      },
      {
        "AttacksSpellcasting",
        this.ExportContent.AttackAndSpellcastingField
      },
      {
        "CP",
        this.ExportContent.EquipmentContent.Copper
      },
      {
        "SP",
        this.ExportContent.EquipmentContent.Silver
      },
      {
        "EP",
        this.ExportContent.EquipmentContent.Electrum
      },
      {
        "GP",
        this.ExportContent.EquipmentContent.Gold
      },
      {
        "PP",
        this.ExportContent.EquipmentContent.Platinum
      },
      {
        "Equipment",
        string.Join(Environment.NewLine, this.ExportContent.EquipmentContent.Equipment.Select<Tuple<string, string>, string>((Func<Tuple<string, string>, string>) (x => x.Item2)))
      },
      {
        "PersonalityTraits ",
        this.ExportContent.BackgroundContent.PersonalityTrait
      },
      {
        "Ideals",
        this.ExportContent.BackgroundContent.Ideal
      },
      {
        "Bonds",
        this.ExportContent.BackgroundContent.Bond
      },
      {
        "Flaws",
        this.ExportContent.BackgroundContent.Flaw
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    if (this.Configuration.IncludeFormatting)
      return;
    StringBuilder stringBuilder = new StringBuilder();
    stringBuilder.AppendLine("Armor Proficiencies. " + this.ExportContent.ArmorProficiencies);
    stringBuilder.AppendLine("Weapon Proficiencies. " + this.ExportContent.WeaponProficiencies);
    stringBuilder.AppendLine("Tool Proficiencies. " + this.ExportContent.ToolProficiencies);
    stringBuilder.AppendLine("Languages. " + this.ExportContent.Languages);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "ProficienciesLang",
        stringBuilder.ToString()
      },
      {
        "Features and Traits",
        this.ExportContent.Features
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
  }

  private void StampLegacyBackgroundContent(PdfStamper stamper)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "CharacterName 2",
        this.ExportContent.CharacterName
      },
      {
        "Age",
        this.ExportContent.AppearanceContent.Age
      },
      {
        "Height",
        this.ExportContent.AppearanceContent.Height
      },
      {
        "Weight",
        this.ExportContent.AppearanceContent.Weight
      },
      {
        "Eyes",
        this.ExportContent.AppearanceContent.Eyes
      },
      {
        "Skin",
        this.ExportContent.AppearanceContent.Skin
      },
      {
        "Hair",
        this.ExportContent.AppearanceContent.Hair
      },
      {
        "Backstory",
        this.ExportContent.BackgroundContent.Story
      },
      {
        "Allies",
        this.ExportContent.AlliesAndOrganizations
      },
      {
        "FactionName",
        this.ExportContent.OrganizationName
      },
      {
        "Feat+Traits",
        this.ExportContent.AdditionalFeaturesAndTraits
      },
      {
        "Treasure",
        this.ExportContent.Treasure
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    this.WriteImage(stamper, "CHARACTER IMAGE", this.ExportContent.AppearanceContent.Portrait);
    this.WriteImage(stamper, "Faction Symbol Image", this.ExportContent.OrganizationSymbol);
  }

  private void StampSpellcastingExportRevamped2(
    PdfStamper stamper,
    CharacterSheetSpellcastingPageExportContent export,
    int page)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "spellcasting_class",
        export.SpellcastingClass
      },
      {
        "spellcasting_ability",
        export.Ability
      },
      {
        "spellcasting_save",
        export.Save
      },
      {
        "spellcasting_attack_bonus",
        export.AttackBonus
      },
      {
        "spellcasting_prepare_count",
        export.PrepareCount
      },
      {
        "spellcasting_notes",
        export.Notes
      },
      {
        "spellcasting_multiclass",
        export.IsMulticlassSpellcaster ? "Yes" : "No"
      }
    })
      stamper.AcroFields.SetField($"{keyValuePair.Key}:{page}", keyValuePair.Value);
    AcroFields acroFields1 = stamper.AcroFields;
    string name1 = $"spellcasting_slot1_count:{page}";
    int availableSlots = export.Spells1.AvailableSlots;
    string str1 = availableSlots.ToString();
    acroFields1.SetField(name1, str1);
    AcroFields acroFields2 = stamper.AcroFields;
    string name2 = $"spellcasting_slot2_count:{page}";
    availableSlots = export.Spells2.AvailableSlots;
    string str2 = availableSlots.ToString();
    acroFields2.SetField(name2, str2);
    AcroFields acroFields3 = stamper.AcroFields;
    string name3 = $"spellcasting_slot3_count:{page}";
    availableSlots = export.Spells3.AvailableSlots;
    string str3 = availableSlots.ToString();
    acroFields3.SetField(name3, str3);
    AcroFields acroFields4 = stamper.AcroFields;
    string name4 = $"spellcasting_slot4_count:{page}";
    availableSlots = export.Spells4.AvailableSlots;
    string str4 = availableSlots.ToString();
    acroFields4.SetField(name4, str4);
    AcroFields acroFields5 = stamper.AcroFields;
    string name5 = $"spellcasting_slot5_count:{page}";
    availableSlots = export.Spells5.AvailableSlots;
    string str5 = availableSlots.ToString();
    acroFields5.SetField(name5, str5);
    AcroFields acroFields6 = stamper.AcroFields;
    string name6 = $"spellcasting_slot6_count:{page}";
    availableSlots = export.Spells6.AvailableSlots;
    string str6 = availableSlots.ToString();
    acroFields6.SetField(name6, str6);
    AcroFields acroFields7 = stamper.AcroFields;
    string name7 = $"spellcasting_slot7_count:{page}";
    availableSlots = export.Spells7.AvailableSlots;
    string str7 = availableSlots.ToString();
    acroFields7.SetField(name7, str7);
    AcroFields acroFields8 = stamper.AcroFields;
    string name8 = $"spellcasting_slot8_count:{page}";
    availableSlots = export.Spells8.AvailableSlots;
    string str8 = availableSlots.ToString();
    acroFields8.SetField(name8, str8);
    AcroFields acroFields9 = stamper.AcroFields;
    string name9 = $"spellcasting_slot9_count:{page}";
    availableSlots = export.Spells9.AvailableSlots;
    string str9 = availableSlots.ToString();
    acroFields9.SetField(name9, str9);
    int num1 = 0;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Cantrips.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num1}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num1}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num1}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num1}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num1}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num1}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num1}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num1}:{page}", spell.School);
      ++num1;
    }
    int num2 = num1 + 1;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells1.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num2}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num2}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num2}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num2}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num2}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num2}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num2}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num2}:{page}", spell.School);
      ++num2;
    }
    int num3 = num2 + 1;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells2.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num3}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num3}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num3}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num3}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num3}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num3}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num3}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num3}:{page}", spell.School);
      ++num3;
    }
    int num4 = num3 + 1;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells3.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num4}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num4}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num4}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num4}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num4}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num4}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num4}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num4}:{page}", spell.School);
      ++num4;
    }
    int num5 = num4 + 1;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells4.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num5}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num5}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num5}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num5}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num5}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num5}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num5}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num5}:{page}", spell.School);
      ++num5;
    }
    int num6 = num5 + 1;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells5.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num6}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num6}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num6}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num6}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num6}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num6}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num6}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num6}:{page}", spell.School);
      ++num6;
    }
    int num7 = num6 + 1;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells6.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num7}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num7}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num7}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num7}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num7}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num7}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num7}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num7}:{page}", spell.School);
      ++num7;
    }
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells7.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num7}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num7}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num7}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num7}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num7}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num7}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num7}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num7}:{page}", spell.School);
      ++num7;
    }
    int num8 = num7 + 1;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells8.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num8}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num8}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num8}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num8}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num8}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num8}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num8}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num8}:{page}", spell.School);
      ++num8;
    }
    int num9 = num8 + 1;
    foreach (CharacterSheetSpellcastingPageExportContent.SpellExportContent spell in export.Spells9.Spells)
    {
      stamper.AcroFields.SetField($"spell_prepared_1.{num9}:{page}", spell.IsPrepared || spell.AlwaysPrepared ? "Yes" : "No");
      stamper.AcroFields.SetField($"spell_name_1.{num9}:{page}", spell.Name);
      stamper.AcroFields.SetField($"spell_description_1.{num9}:{page}", spell.Description);
      stamper.AcroFields.SetField($"spell_casting_time_1.{num9}:{page}", spell.CastingTime);
      stamper.AcroFields.SetField($"spell_casting_range_1.{num9}:{page}", spell.Range);
      stamper.AcroFields.SetField($"spell_casting_duration_1.{num9}:{page}", spell.Duration);
      stamper.AcroFields.SetField($"spell_casting_components_1.{num9}:{page}", spell.Components);
      stamper.AcroFields.SetField($"spell_casting_school_1.{num9}:{page}", spell.School);
      ++num9;
    }
  }

  [Obsolete]
  private void StampMainContent(PdfStamper stamper, MainSheetContent content)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "CharacterName",
        content.CharacterName
      },
      {
        "PlayerName",
        content.PlayerName
      },
      {
        "Race ",
        content.Race
      },
      {
        "Background",
        content.Background
      },
      {
        "ClassLevel",
        content.ClassLevel
      },
      {
        "Alignment",
        content.Alignment
      },
      {
        "XP",
        content.Experience
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        this.Configuration.IsAttributeDisplayFlipped ? "STRmod" : "STR",
        content.Strength
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "DEXmod " : "DEX",
        content.Dexterity
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "CONmod" : "CON",
        content.Constitution
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "INTmod" : "INT",
        content.Intelligence
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "WISmod" : "WIS",
        content.Wisdom
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "CHamod" : "CHA",
        content.Charisma
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "STR" : "STRmod",
        content.StrengthModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "DEX" : "DEXmod ",
        content.DexterityModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "CON" : "CONmod",
        content.ConstitutionModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "INT" : "INTmod",
        content.IntelligenceModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "WIS" : "WISmod",
        content.WisdomModifier
      },
      {
        this.Configuration.IsAttributeDisplayFlipped ? "CHA" : "CHamod",
        content.CharismaModifier
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "Acrobatics",
        content.Acrobatics
      },
      {
        "Animal",
        content.AnimalHandling
      },
      {
        "Athletics",
        content.Athletics
      },
      {
        "Deception ",
        content.Deception
      },
      {
        "History ",
        content.History
      },
      {
        "Insight",
        content.Insight
      },
      {
        "Intimidation",
        content.Intimidation
      },
      {
        "Investigation ",
        content.Investigation
      },
      {
        "Arcana",
        content.Arcana
      },
      {
        "Perception ",
        content.Perception
      },
      {
        "Nature",
        content.Nature
      },
      {
        "Performance",
        content.Performance
      },
      {
        "Medicine",
        content.Medicine
      },
      {
        "Religion",
        content.Religion
      },
      {
        "Stealth ",
        content.Stealth
      },
      {
        "Persuasion",
        content.Persuasion
      },
      {
        "SleightofHand",
        content.SleightOfHand
      },
      {
        "Survival",
        content.Survival
      },
      {
        "Check Box 23",
        content.AcrobaticsProficient ? "Yes" : "No"
      },
      {
        "Check Box 24",
        content.AnimalHandlingProficient ? "Yes" : "No"
      },
      {
        "Check Box 25",
        content.ArcanaProficient ? "Yes" : "No"
      },
      {
        "Check Box 26",
        content.AthleticsProficient ? "Yes" : "No"
      },
      {
        "Check Box 27",
        content.DeceptionProficient ? "Yes" : "No"
      },
      {
        "Check Box 28",
        content.HistoryProficient ? "Yes" : "No"
      },
      {
        "Check Box 29",
        content.InsightProficient ? "Yes" : "No"
      },
      {
        "Check Box 30",
        content.IntimidationProficient ? "Yes" : "No"
      },
      {
        "Check Box 31",
        content.InvestigationProficient ? "Yes" : "No"
      },
      {
        "Check Box 32",
        content.MedicineProficient ? "Yes" : "No"
      },
      {
        "Check Box 33",
        content.NatureProficient ? "Yes" : "No"
      },
      {
        "Check Box 34",
        content.PerceptionProficient ? "Yes" : "No"
      },
      {
        "Check Box 35",
        content.PerformanceProficient ? "Yes" : "No"
      },
      {
        "Check Box 36",
        content.PersuasionProficient ? "Yes" : "No"
      },
      {
        "Check Box 37",
        content.ReligionProficient ? "Yes" : "No"
      },
      {
        "Check Box 38",
        content.SleightOfHandProficient ? "Yes" : "No"
      },
      {
        "Check Box 39",
        content.StealthProficient ? "Yes" : "No"
      },
      {
        "Check Box 40",
        content.SurvivalProficient ? "Yes" : "No"
      },
      {
        "Passive",
        content.PassiveWisdomPerception
      },
      {
        "Inspiration",
        content.Inspiration ? "x" : ""
      },
      {
        "ProfBonus",
        content.ProficiencyBonus
      },
      {
        "ST Strength",
        content.StrengthSavingThrow
      },
      {
        "ST Dexterity",
        content.DexteritySavingThrow
      },
      {
        "ST Constitution",
        content.ConstitutionSavingThrow
      },
      {
        "ST Intelligence",
        content.IntelligenceSavingThrow
      },
      {
        "ST Wisdom",
        content.WisdomSavingThrow
      },
      {
        "ST Charisma",
        content.CharismaSavingThrow
      },
      {
        "Check Box 11",
        content.StrengthSavingThrowProficient ? "Yes" : "No"
      },
      {
        "Check Box 18",
        content.DexteritySavingThrowProficient ? "Yes" : "No"
      },
      {
        "Check Box 19",
        content.ConstitutionSavingThrowProficient ? "Yes" : "No"
      },
      {
        "Check Box 20",
        content.IntelligenceSavingThrowProficient ? "Yes" : "No"
      },
      {
        "Check Box 21",
        content.WisdomSavingThrowProficient ? "Yes" : "No"
      },
      {
        "Check Box 22",
        content.CharismaSavingThrowProficient ? "Yes" : "No"
      },
      {
        "AC",
        content.ArmorClass
      },
      {
        "Initiative",
        content.Initiative
      },
      {
        "Speed",
        content.Speed + "ft."
      },
      {
        "HPMax",
        content.MaximumHitPoints
      },
      {
        "HPCurrent",
        content.CurrentHitPoints
      },
      {
        "HPTemp",
        content.TemporaryHitPoints
      },
      {
        "HD",
        content.HitDice
      },
      {
        "HDTotal",
        content.TotalHitDice
      },
      {
        "Check Box 12",
        content.DeathSavingThrowSuccess1 ? "Yes" : "No"
      },
      {
        "Check Box 13",
        content.DeathSavingThrowSuccess2 ? "Yes" : "No"
      },
      {
        "Check Box 14",
        content.DeathSavingThrowSuccess3 ? "Yes" : "No"
      },
      {
        "Check Box 15",
        content.DeathSavingThrowFailure1 ? "Yes" : "No"
      },
      {
        "Check Box 16",
        content.DeathSavingThrowFailure2 ? "Yes" : "No"
      },
      {
        "Check Box 17",
        content.DeathSavingThrowFailure3 ? "Yes" : "No"
      },
      {
        "Wpn Name",
        content.Name1
      },
      {
        "Wpn1 AtkBonus",
        content.AttackBonus1
      },
      {
        "Wpn1 Damage",
        content.DamageType1
      },
      {
        "Wpn Name 2",
        content.Name2
      },
      {
        "Wpn2 AtkBonus ",
        content.AttackBonus2
      },
      {
        "Wpn2 Damage ",
        content.DamageType2
      },
      {
        "Wpn Name 3",
        content.Name3
      },
      {
        "Wpn3 AtkBonus  ",
        content.AttackBonus3
      },
      {
        "Wpn3 Damage ",
        content.DamageType3
      },
      {
        "AttacksSpellcasting",
        content.AttackAndSpellcastingField
      },
      {
        "CP",
        content.Copper
      },
      {
        "SP",
        content.Silver
      },
      {
        "EP",
        content.Electrum
      },
      {
        "GP",
        content.Gold
      },
      {
        "PP",
        content.Platinum
      },
      {
        "Equipment",
        content.Equipment
      },
      {
        "PersonalityTraits ",
        content.PersonalityTraits
      },
      {
        "Ideals",
        content.Ideals
      },
      {
        "Bonds",
        content.Bonds
      },
      {
        "Flaws",
        content.Flaws
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    if (this.Configuration.IncludeFormatting)
      return;
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "ProficienciesLang",
        content.ProficienciesAndLanguages
      },
      {
        "Features and Traits",
        content.FeaturesAndTraitsField
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value);
    this.GenerateFeatureCards(stamper, content);
  }

  [Obsolete]
  private void StampDetailsContent(
    PdfStamper stamper,
    MainSheetContent mainContent,
    DetailsSheetContent content)
  {
    foreach (KeyValuePair<string, string> keyValuePair in new Dictionary<string, string>()
    {
      {
        "background_character_name",
        content.CharacterName
      },
      {
        "background_story",
        content.CharacterBackstory
      },
      {
        "background_allies",
        content.AlliesAndOrganizations
      },
      {
        "background_organization_name",
        content.OrganizationName
      },
      {
        "background_age",
        content.Age
      },
      {
        "background_eyes",
        content.Eyes
      },
      {
        "background_hair",
        content.Hair
      },
      {
        "background_height",
        content.Height
      },
      {
        "background_skin",
        content.Skin
      },
      {
        "background_weight",
        content.Weight
      },
      {
        "background_traits",
        mainContent.PersonalityTraits
      },
      {
        "background_ideals",
        mainContent.Ideals
      },
      {
        "background_bonds",
        mainContent.Bonds
      },
      {
        "background_flaws",
        mainContent.Flaws
      },
      {
        "background_feature_name",
        content.BackgroundFeatureName
      },
      {
        "background_feature",
        content.BackgroundFeature
      },
      {
        "background_trinket",
        content.Trinket
      }
    })
      stamper.AcroFields.SetField(keyValuePair.Key, keyValuePair.Value, false);
    try
    {
      AcroFields.FieldPosition fieldPosition = stamper.AcroFields.GetFieldPositions("background_portrait_image").FirstOrDefault<AcroFields.FieldPosition>();
      if (fieldPosition != null)
      {
        PushbuttonField pushbuttonField1 = new PushbuttonField(stamper.Writer, fieldPosition.position, "background_portrait_image-replaced");
        pushbuttonField1.Layout = 2;
        pushbuttonField1.Image = Image.GetInstance(content.CharacterAppearance);
        pushbuttonField1.ProportionalIcon = true;
        pushbuttonField1.Options = 1;
        PushbuttonField pushbuttonField2 = pushbuttonField1;
        stamper.AddAnnotation((PdfAnnotation) pushbuttonField2.Field, fieldPosition.page);
      }
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (StampDetailsContent));
    }
    try
    {
      AcroFields.FieldPosition fieldPosition = stamper.AcroFields.GetFieldPositions("background_organization_image").FirstOrDefault<AcroFields.FieldPosition>();
      if (fieldPosition == null || string.IsNullOrWhiteSpace(content.OrganizationSymbol))
        return;
      PushbuttonField pushbuttonField3 = new PushbuttonField(stamper.Writer, fieldPosition.position, "background_organization_image-replaced");
      pushbuttonField3.Layout = 2;
      pushbuttonField3.Image = Image.GetInstance(content.OrganizationSymbol);
      pushbuttonField3.ProportionalIcon = true;
      pushbuttonField3.Options = 1;
      PushbuttonField pushbuttonField4 = pushbuttonField3;
      stamper.AddAnnotation((PdfAnnotation) pushbuttonField4.Field, fieldPosition.page);
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (StampDetailsContent));
    }
  }

  [Obsolete]
  private void CardTest()
  {
    CharacterSheetResourcePage sheetResourcePage1 = new CharacterSheetResourcePage("Builder.Presentation.Resources.Sheets.aurora_spelldemo_flat.pdf");
    CharacterSheetResourcePage sheetResourcePage2 = new CharacterSheetResourcePage("Builder.Presentation.Resources.Sheets.Partial.partial_100h.pdf");
    string str = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "File2.pdf");
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "File3.pdf");
    MemoryStream os1 = new MemoryStream();
    using (Document document = new Document())
    {
      using (PdfWriter instance = PdfWriter.GetInstance(document, (Stream) os1))
      {
        using (PdfReader reader1 = new PdfReader(sheetResourcePage1.GetResourceStream()))
        {
          PdfReader reader2 = sheetResourcePage2.CreateReader();
          document.SetPageSize(reader1.GetPageSize(1));
          document.Open();
          document.NewPage();
          PdfImportedPage importedPage1 = instance.GetImportedPage(reader1, 1);
          instance.DirectContentUnder.AddTemplate((PdfTemplate) importedPage1, 0.0f, 100f);
          PdfImportedPage importedPage2 = instance.GetImportedPage(reader2, 1);
          instance.DirectContentUnder.AddTemplate((PdfTemplate) importedPage2, 0.0f, 150f);
          instance.GetImportedPage(reader1, 1);
          FillableContentGenerator contentGenerator = new FillableContentGenerator(instance);
          int llx1 = 43;
          float lly = document.PageSize.Height - 160f;
          for (int index = 0; index < 5; ++index)
          {
            int llx2 = llx1;
            int num1 = index + 1;
            int num2 = 72;
            int num3 = 10;
            Rectangle area = new Rectangle((float) llx2, lly, (float) (llx2 + num2), lly - (float) num3);
            contentGenerator.AddText(area, $"spell_name_{num1}", "Fireball " + num1.ToString());
            area.Left += area.Width;
            area.Left += 2f;
            contentGenerator.AddText(area, $"spell_description_{num1}", "Fireball Description" + num1.ToString());
            contentGenerator.SetBoldItalic();
            int llx3 = llx2 + (num2 + 2);
            int num4 = 100;
            instance.AddAnnotation((PdfAnnotation) contentGenerator.CreateText(new Rectangle((float) llx3, lly, (float) (llx3 + num4), lly - (float) num3), $"spell_description2_{num1}", "Fireball More." + num1.ToString()).GetTextField());
            contentGenerator.SetItalic();
            int llx4 = llx3 + (num4 + 2);
            int num5 = 100;
            instance.AddAnnotation((PdfAnnotation) contentGenerator.CreateText(new Rectangle((float) llx4, lly, (float) (llx4 + num5), lly - (float) num3), $"spell_description3_{num1}", "Fireball Again" + num1.ToString()).GetTextField());
            lly -= (float) (num3 + 2);
          }
          iTextSharp.text.Font font1 = new iTextSharp.text.Font(BaseFont.CreateFont("Helvetica", "Cp1252", false));
          iTextSharp.text.Font font2 = new iTextSharp.text.Font(BaseFont.CreateFont("Helvetica-Oblique", "Cp1252", false));
          iTextSharp.text.Font font3 = new iTextSharp.text.Font(BaseFont.CreateFont("Helvetica-Bold", "Cp1252", false));
          iTextSharp.text.Font font4 = new iTextSharp.text.Font(BaseFont.CreateFont("Helvetica-BoldOblique", "Cp1252", false));
          font1.Size = 7f;
          font2.Size = 7f;
          font3.Size = 7f;
          font4.Size = 7f;
          Paragraph paragraph = new Paragraph();
          paragraph.Add((IElement) new Chunk("Fire. ", font4));
          paragraph.Add((IElement) new Chunk("This is a test using an ", font1));
          paragraph.Add((IElement) new Chunk("italic", font2));
          paragraph.Add((IElement) new Chunk(" font.\r\n", font1));
          foreach (Chunk chunk in (IEnumerable<Chunk>) paragraph.Chunks)
            chunk.setLineHeight(7f);
          ColumnText columnText = new ColumnText(instance.DirectContent);
          columnText.SetSimpleColumn(new Rectangle((float) llx1, 10f, 200f, 200f));
          columnText.AddText((Phrase) paragraph);
          columnText.AddText((Phrase) paragraph);
          columnText.AddText((Phrase) paragraph);
          columnText.Go();
          document.Close();
        }
      }
    }
    File.WriteAllBytes(str, os1.ToArray());
    MemoryStream os2 = new MemoryStream();
    PdfReader reader = new PdfReader(str);
    PdfStamper pdfStamper = new PdfStamper(reader, (Stream) os2);
    pdfStamper.PartialFormFlattening("spell_name_1");
    pdfStamper.PartialFormFlattening("spell_description_1");
    pdfStamper.PartialFormFlattening("spell_description2_1");
    pdfStamper.PartialFormFlattening("spell_description3_1");
    pdfStamper.FormFlattening = true;
    pdfStamper.Close();
    reader.Close();
    File.WriteAllBytes(str, os2.ToArray());
    ExternalLauncherContext.Current?.OpenPath(str);
  }
}
