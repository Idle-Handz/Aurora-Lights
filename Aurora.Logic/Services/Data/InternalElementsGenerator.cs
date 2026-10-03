// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.Data.InternalElementsGenerator
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#nullable disable
namespace Builder.Presentation.Services.Data;

public class InternalElementsGenerator
{
  public bool IncludeSpellsNotOnSpelllist { get; set; }

  private string GenerateInternalId(ElementBase element, string splitSection, string format)
  {
    string str = ((IEnumerable<string>) Regex.Split(element.Id, splitSection)).LastOrDefault<string>() ?? "";
    return string.Format(format, (object) str).ToUpperInvariant();
  }

  /// <summary>
  /// Teaches the alias table what each generated proxy used to be called. A save refers to the
  /// proxy, not the definition underneath it, so an alias on the definition only reaches the save
  /// if the proxy id derived from the old name forwards too. The id is derived exactly as
  /// <see cref="GenerateInternalId"/> derives the current one.
  /// </summary>
  private void ForwardGeneratedProxyIds(ElementBase element, string splitSection, string format) =>
    ElementIdAliases.ForwardGeneratedIds(element.Id, savedId => savedId.Contains(splitSection)
      ? string.Format(format, (object) (((IEnumerable<string>) Regex.Split(savedId, splitSection)).LastOrDefault<string>() ?? "")).ToUpperInvariant()
      : null);

  public List<ElementBase> GenerateInternalFeats(IEnumerable<ElementBase> content)
  {
    List<ElementBase> internalFeats = new List<ElementBase>();
    if (content == null)
      return internalFeats;
    List<Feat> list1 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Feat"))).Cast<Feat>().ToList<Feat>();
    List<Source> list2 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Source"))).Cast<Source>().ToList<Source>();
    foreach (Feat feat in list1)
    {
      Feat element = feat;
      if (!element.Source.Equals("Core") && !element.Source.Equals("Internal"))
      {
        if (!element.Id.Contains("_FEAT_"))
        {
          Logger.Warning($"{element.Name} doesn't contain _FEAT_ in the id ({element.Id})");
        }
        else
        {
          string upperInvariant = (list2.FirstOrDefault<Source>((Func<Source, bool>) (x => x.Name.Equals(element.Source)))?.Abbreviation ?? "").ToUpperInvariant();
          string internalId = this.GenerateInternalId((ElementBase) element, "_FEAT_", $"ID_{upperInvariant}_INTERNAL_ITEM_FEAT_PROXY_{{0}}");
          this.ForwardGeneratedProxyIds((ElementBase) element, "_FEAT_", $"ID_{upperInvariant}_INTERNAL_ITEM_FEAT_PROXY_{{0}}");
          Item obj1 = new Item();
          obj1.ElementHeader = new ElementHeader($"Additional {element.Type}, {element.Name}", "Item", element.Source, internalId);
          Item obj2 = obj1;
          obj2.Category = "Additional " + element.Type;
          obj2.Description = element.Description;
          obj2.Slot = "proxy";
          obj2.ElementSetters.Add(new ElementSetters.Setter("inventory-hidden", "true"));
          obj2.HideFromInventory = true;
          StringBuilder stringBuilder = new StringBuilder();
          stringBuilder.Append("<p><em>You can equip this item to “enable” it. It remains hidden from the inventory on your character sheet.</em></p>");
          stringBuilder.Append("<div class=\"reference\">");
          stringBuilder.Append($"<div element=\"{element.Id}\" />");
          stringBuilder.Append("</div>");
          obj2.Description = stringBuilder.ToString();
          obj2.Keywords.AddRange((IEnumerable<string>) element.Keywords);
          obj2.Rules.Add((RuleBase) new GrantRule(obj2.ElementHeader)
          {
            Attributes = {
              Type = element.Type,
              Name = element.Id
            }
          });
          obj2.IncludeInCompendium = false;
          internalFeats.Add((ElementBase) obj2);
        }
      }
    }
    return internalFeats;
  }

  public List<ElementBase> GenerateInternalLanguages(IEnumerable<ElementBase> content)
  {
    List<ElementBase> internalLanguages = new List<ElementBase>();
    if (content == null)
      return internalLanguages;
    List<Language> list1 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Language"))).Cast<Language>().ToList<Language>();
    List<Source> list2 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Source"))).Cast<Source>().ToList<Source>();
    foreach (Language language in list1)
    {
      Language element = language;
      if (!element.Source.Equals("Core") && !element.Source.Equals("Internal"))
      {
        string splitSection = "_LANGUAGE_";
        if (!element.Id.Contains(splitSection))
        {
          Logger.Warning($"{element.Name} doesn't contain {splitSection} in the id ({element.Id})");
        }
        else
        {
          string upperInvariant = (list2.FirstOrDefault<Source>((Func<Source, bool>) (x => x.Name.Equals(element.Source)))?.Abbreviation ?? "").ToUpperInvariant();
          string internalId = this.GenerateInternalId((ElementBase) element, splitSection, $"ID_{upperInvariant}_INTERNAL_ITEM_LANGUAGE_PROXY{splitSection}{{0}}");
          this.ForwardGeneratedProxyIds((ElementBase) element, splitSection, $"ID_{upperInvariant}_INTERNAL_ITEM_LANGUAGE_PROXY{splitSection}{{0}}");
          Item obj1 = new Item();
          obj1.ElementHeader = new ElementHeader($"Additional {element.Type}, {element.Name}", "Item", element.Source, internalId);
          Item obj2 = obj1;
          obj2.Category = "Additional " + element.Type;
          obj2.Description = element.Description;
          obj2.Slot = "proxy";
          obj2.ElementSetters.Add(new ElementSetters.Setter("inventory-hidden", "true"));
          obj2.HideFromInventory = true;
          StringBuilder stringBuilder = new StringBuilder();
          stringBuilder.Append("<p><em>You can equip this item to “enable” it. It remains hidden from the inventory on your character sheet.</em></p>");
          stringBuilder.Append("<div class=\"reference\">");
          stringBuilder.Append($"<div element=\"{element.Id}\" />");
          stringBuilder.Append("</div>");
          obj2.Description = stringBuilder.ToString();
          obj2.Keywords.AddRange((IEnumerable<string>) element.Keywords);
          obj2.Rules.Add((RuleBase) new GrantRule(obj2.ElementHeader)
          {
            Attributes = {
              Type = element.Type,
              Name = element.Id
            }
          });
          obj2.IncludeInCompendium = false;
          internalLanguages.Add((ElementBase) obj2);
        }
      }
    }
    return internalLanguages;
  }

  public List<ElementBase> GenerateInternalProficiency(IEnumerable<ElementBase> content)
  {
    List<ElementBase> internalProficiency = new List<ElementBase>();
    if (content == null)
      return internalProficiency;
    List<Proficiency> list1 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Proficiency"))).Cast<Proficiency>().ToList<Proficiency>();
    List<Source> list2 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Source"))).Cast<Source>().ToList<Source>();
    foreach (Proficiency proficiency in list1)
    {
      Proficiency element = proficiency;
      if (!element.Source.Equals("Core") && !element.Source.Equals("Internal"))
      {
        string splitSection = "_PROFICIENCY_";
        if (!element.Id.Contains(splitSection))
        {
          Logger.Warning($"{element.Name} doesn't contain {splitSection} in the id ({element.Id})");
        }
        else
        {
          string upperInvariant = (list2.FirstOrDefault<Source>((Func<Source, bool>) (x => x.Name.Equals(element.Source)))?.Abbreviation ?? "").ToUpperInvariant();
          string internalId = this.GenerateInternalId((ElementBase) element, splitSection, $"ID_{upperInvariant}_INTERNAL_ITEM_PROFICIENCY_PROXY{splitSection}{{0}}");
          this.ForwardGeneratedProxyIds((ElementBase) element, splitSection, $"ID_{upperInvariant}_INTERNAL_ITEM_PROFICIENCY_PROXY{splitSection}{{0}}");
          string str1 = $"Skill Proficiency ({element.Name})";
          string str2 = element.Name;
          if (element.HasSupports && element.Supports.Contains("Skill"))
            str2 = $"Skill Proficiency ({element.Name})";
          Item obj1 = new Item();
          obj1.ElementHeader = new ElementHeader($"Additional {element.Type}, {str2}", "Item", element.Source, internalId);
          Item obj2 = obj1;
          obj2.Category = "Additional " + element.Type;
          obj2.Slot = "proxy";
          obj2.ElementSetters.Add(new ElementSetters.Setter("inventory-hidden", "true"));
          obj2.HideFromInventory = true;
          StringBuilder stringBuilder = new StringBuilder();
          stringBuilder.Append("<p><em>You can equip this item to “enable” it. It remains hidden from the inventory on your character sheet.</em></p>");
          stringBuilder.Append("<div class=\"reference\">");
          stringBuilder.Append($"<div element=\"{element.Id}\" />");
          stringBuilder.Append("</div>");
          obj2.Description = stringBuilder.ToString();
          obj2.ElementSetters.Add(new ElementSetters.Setter("inventory-hidden", "true"));
          obj2.Keywords.AddRange((IEnumerable<string>) element.Keywords);
          obj2.Rules.Add((RuleBase) new GrantRule(obj2.ElementHeader)
          {
            Attributes = {
              Type = element.Type,
              Name = element.Id
            }
          });
          obj2.IncludeInCompendium = false;
          internalProficiency.Add((ElementBase) obj2);
        }
      }
    }
    return internalProficiency;
  }

  public List<ElementBase> GenerateInternalAsi(IEnumerable<ElementBase> content)
  {
    List<ElementBase> internalAsi = new List<ElementBase>();
    if (content == null)
      return internalAsi;
    List<ElementBase> list1 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Ability Score Improvement") && x.Id.Equals("ID_INTERNAL_ASI_STRENGTH") || x.Id.Equals("ID_INTERNAL_ASI_DEXTERITY") || x.Id.Equals("ID_INTERNAL_ASI_CONSTITUTION") || x.Id.Equals("ID_INTERNAL_ASI_INTELLIGENCE") || x.Id.Equals("ID_INTERNAL_ASI_WISDOM") || x.Id.Equals("ID_INTERNAL_ASI_CHARISMA"))).ToList<ElementBase>();
    List<Source> list2 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Source"))).Cast<Source>().ToList<Source>();
    foreach (ElementBase elementBase in list1)
    {
      ElementBase element = elementBase;
      if (!element.Source.Equals("Core") && !element.Source.Equals("Internal"))
      {
        string splitSection = "_ASI_";
        if (!element.Id.Contains(splitSection))
        {
          Logger.Warning($"{element.Name} doesn't contain {splitSection} in the id ({element.Id})");
        }
        else
        {
          string upperInvariant = (list2.FirstOrDefault<Source>((Func<Source, bool>) (x => x.Name.Equals(element.Source)))?.Abbreviation ?? "").ToUpperInvariant();
          string internalId = this.GenerateInternalId(element, splitSection, $"ID_{upperInvariant}_INTERNAL_ITEM_PROXY{splitSection}{{0}}");
          this.ForwardGeneratedProxyIds(element, splitSection, $"ID_{upperInvariant}_INTERNAL_ITEM_PROXY{splitSection}{{0}}");
          Item obj1 = new Item();
          obj1.ElementHeader = new ElementHeader($"Additional {element.Type}, {element.Name}", "Item", element.Source, internalId);
          Item obj2 = obj1;
          obj2.Category = "Additional " + element.Type;
          obj2.Slot = "proxy";
          obj2.ElementSetters.Add(new ElementSetters.Setter("inventory-hidden", "true"));
          obj2.HideFromInventory = true;
          StringBuilder stringBuilder = new StringBuilder();
          stringBuilder.Append("<p><em>You can equip this item to “enable” it. It remains hidden from the inventory on your character sheet.</em></p>");
          stringBuilder.Append("<div class=\"reference\">");
          stringBuilder.Append($"<div element=\"{element.Id}\" />");
          stringBuilder.Append("</div>");
          obj2.Description = stringBuilder.ToString();
          obj2.ElementSetters.Add(new ElementSetters.Setter("inventory-hidden", "true"));
          obj2.Keywords.AddRange((IEnumerable<string>) element.Keywords);
          obj2.Rules.Add((RuleBase) new GrantRule(obj2.ElementHeader)
          {
            Attributes = {
              Type = element.Type,
              Name = element.Id
            }
          });
          obj2.IncludeInCompendium = false;
          internalAsi.Add((ElementBase) obj2);
        }
      }
    }
    return internalAsi;
  }

  /// <summary>
  /// The spellcasting list names the spell proxies are generated across, with the unnamed list
  /// first. Deriving these without building any proxy is what lets the proxies themselves be
  /// built on demand; see <see cref="SpellProxyCatalog"/>.
  /// </summary>
  public List<string> GetSpellcastingListNames(IEnumerable<ElementBase> content)
  {
    List<string> names = content == null
      ? new List<string>()
      : content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.HasSpellcastingInformation && !x.SpellcastingInformation.IsExtension && !x.SpellcastingInformation.ElementHeader.Id.Equals("ID_WOTC_UA20170313_CLASS_FEATURE_MYSTIC_PSIONICS"))).Select<ElementBase, SpellcastingInformation>((Func<ElementBase, SpellcastingInformation>) (x => x.SpellcastingInformation)).ToList<SpellcastingInformation>().Select<SpellcastingInformation, string>((Func<SpellcastingInformation, string>) (x => x.Name)).Distinct<string>().ToList<string>();
    names.Insert(0, "");
    return names;
  }

  /// <summary>
  /// One proxy item per spell per spellcasting list. That cross product is 79,032 items for the
  /// shipped catalog, so <paramref name="includeList"/> narrows it to the lists actually wanted;
  /// passing null keeps the whole product.
  /// </summary>
  public List<ElementBase> GenerateInternalSpells(IEnumerable<ElementBase> content, Func<string, bool> includeList = null)
  {
    List<ElementBase> internalSpells = new List<ElementBase>();
    if (content == null)
      return internalSpells;
    List<Spell> list1 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Spell"))).Cast<Spell>().ToList<Spell>();
    List<Source> list2 = content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Source"))).Cast<Source>().ToList<Source>();
    List<string> list3 = this.GetSpellcastingListNames(content);
    if (includeList != null)
      list3 = list3.Where<string>(includeList).ToList<string>();
    if (list3.Count == 0)
      return internalSpells;
    foreach (Spell spell in list1)
    {
      Spell element = spell;
      if (!element.Source.Equals("Core") && !element.Source.Equals("Internal"))
      {
        string splitSection = "_SPELL_";
        if (!element.Id.Contains(splitSection))
        {
          Logger.Warning($"{element.Name} doesn't contain {splitSection} in the id ({element.Id})");
        }
        else
        {
          Source source = list2.FirstOrDefault<Source>((Func<Source, bool>) (x => x.Name.Equals(element.Source)));
          string upperInvariant = (source?.Abbreviation ?? "").ToUpperInvariant();
          if (string.IsNullOrWhiteSpace(upperInvariant) && source != null)
            upperInvariant = source.Name[0].ToString();
          foreach (string str in list3)
          {
            string internalId = this.GenerateInternalId((ElementBase) element, splitSection, $"ID_{upperInvariant}_INTERNAL_ITEM_{str.Replace(" ", "_")}_SPELL_PROXY{splitSection}{{0}}");
            this.ForwardGeneratedProxyIds((ElementBase) element, splitSection, $"ID_{upperInvariant}_INTERNAL_ITEM_{str.Replace(" ", "_")}_SPELL_PROXY{splitSection}{{0}}");
            string name = $"Additional {element.Type}, {element.Name}";
            if (!string.IsNullOrWhiteSpace(str))
              name = $"Additional {str} {element.Type}, {element.Name}";
            Item obj1 = new Item();
            obj1.ElementHeader = new ElementHeader(name, "Item", element.Source, internalId);
            Item obj2 = obj1;
            obj2.Category = string.IsNullOrWhiteSpace(str) ? "Additional " + element.Type : $"Additional {str} {element.Type}";
            obj2.Slot = "proxy";
            obj2.ElementSetters.Add(new ElementSetters.Setter("inventory-hidden", "true"));
            obj2.HideFromInventory = true;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("<p><em>You can equip this item to “enable” it. It remains hidden from the inventory on your character sheet.</em></p>");
            stringBuilder.Append("<div class=\"reference\">");
            stringBuilder.Append($"<div element=\"{element.Id}\" />");
            stringBuilder.Append("</div>");
            obj2.Description = stringBuilder.ToString();
            obj2.ElementSetters.Add(new ElementSetters.Setter("inventory-hidden", "true"));
            obj2.Keywords.AddRange((IEnumerable<string>) element.Keywords);
            obj2.Keywords.Add(str);
            foreach (string support in element.Supports)
              obj2.Keywords.Add(support);
            obj2.SheetDescription.AlternateName = obj2.Name.Replace("Additional ", "");
            GrantRule grantRule = new GrantRule(obj2.ElementHeader);
            grantRule.Attributes.Type = element.Type;
            grantRule.Attributes.Name = element.Id;
            if (!string.IsNullOrWhiteSpace(str))
              grantRule.Setters.Add(new ElementSetters.Setter("spellcasting", str));
            obj2.Rules.Add((RuleBase) grantRule);
            obj2.IncludeInCompendium = false;
            internalSpells.Add((ElementBase) obj2);
          }
        }
      }
    }
    return internalSpells;
  }

  public List<ElementBase> GenerateInternalIgnore(IEnumerable<ElementBase> content)
  {
    List<ElementBase> internalIgnore = new List<ElementBase>();
    if (content == null)
      return internalIgnore;
    content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Racial Trait") && x.Type.Equals("Class Feature") && x.Type.Equals("Archetype Feature") && x.Type.Equals("Background Feature") && x.Type.Equals("Grants")));
    string str = "_IGNORE";
    foreach (ElementBase elementBase1 in content.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Racial Trait"))))
    {
      if (!elementBase1.Source.Equals("Core") && !elementBase1.Source.Equals("Internal"))
      {
        ElementBase elementBase2 = new ElementBase()
        {
          ElementHeader = new ElementHeader("Ignore " + elementBase1.Name, "Ignore", "Internal", elementBase1.Id + str),
          IncludeInCompendium = false
        };
        elementBase1.Requirements = !string.IsNullOrWhiteSpace(elementBase1.Requirements) ? $"({elementBase1.Requirements})&&!{elementBase2.Id}" : "!" + elementBase2.Id;
        internalIgnore.Add(elementBase2);
      }
    }
    return internalIgnore;
  }
}
