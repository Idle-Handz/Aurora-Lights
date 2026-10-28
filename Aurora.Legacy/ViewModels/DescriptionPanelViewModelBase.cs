// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.DescriptionPanelViewModelBase
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Events.Character;
using Builder.Presentation.Models.Collections;
using Builder.Presentation.Models.Sources;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Telemetry;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Input;
using System.Xml;

#nullable disable
namespace Builder.Presentation.ViewModels;

public class DescriptionPanelViewModelBase : 
  ViewModelBase,
  ISubscriber<ElementDescriptionDisplayRequestEvent>,
  ISubscriber<HtmlDisplayRequestEvent>,
  ISubscriber<ResourceDocumentDisplayRequestEvent>,
  ISubscriber<SettingsChangedEvent>,
  ISubscriber<CharacterLoadingCompletedEvent>
{
  private string _styleSheet;
  private bool _isDarkStyle;
  private bool _includeSource;
  private string _description;
  private ElementBase _currentElement;
  private bool _isSpeechEnabled;
  private bool _isSpeechActive;
  private string _selectedText = "";

  public DescriptionPanelViewModelBase()
  {
    this.SupportedTypes = new List<string>();
    this.IncludeSource = true;
    this.IsDarkStyle = ApplicationContext.Current.Settings.Theme.Contains("Dark");
    this._styleSheet = DataManager.Current.GetResourceWebDocument(this._isDarkStyle ? "stylesheet-dark.css" : "stylesheet.css");
    if (this.IsInDesignMode)
    {
      this._description = $"<body><h2>Design Time DWARF</h2>{DataManager.Current.GetResourceWebDocument("description-panel-design-data.html")}</body>";
    }
    else
    {
      this.Description = "";
      this.SubscribeWithEventAggregator();
      SpeechService.Default.SpeechStarted += new EventHandler(this._speechService_SpeechStarted);
      SpeechService.Default.SpeechStopped += new EventHandler(this._speechService_SpeechStopped);
    }
  }

  public List<string> SupportedTypes { get; protected set; }

  public bool IsDarkStyle
  {
    get => this._isDarkStyle;
    set
    {
      this.SetProperty<bool>(ref this._isDarkStyle, value, nameof (IsDarkStyle));
      this.StyleSheet = DataManager.Current.GetResourceWebDocument(this._isDarkStyle ? "stylesheet-dark.css" : "stylesheet.css");
    }
  }

  public bool IncludeSource
  {
    get => this._includeSource;
    set => this.SetProperty<bool>(ref this._includeSource, value, nameof (IncludeSource));
  }

  public string StyleSheet
  {
    get => this._styleSheet;
    set => this.SetProperty<string>(ref this._styleSheet, value, nameof (StyleSheet));
  }

  public string Description
  {
    get => this._description;
    set => this.SetProperty<string>(ref this._description, value, nameof (Description));
  }

  public ElementBase CurrentElement
  {
    get => this._currentElement;
    protected set
    {
      this.SetProperty<ElementBase>(ref this._currentElement, value, nameof (CurrentElement));
    }
  }

  protected virtual void HandleDisplayRequest(ElementDescriptionDisplayRequestEvent args)
  {
    this.CurrentElement = args.Element;
    if (this.CurrentElement == null || this.SupportedTypes.Count > 0 && !this.SupportedTypes.Contains(args.Element.Type))
      return;
    if (this.CurrentElement.HasGeneratedDescription && !args.IgnoreGeneratedDescription)
    {
      this.Description = this.CurrentElement.GeneratedDescription;
    }
    else
    {
      if (this.CurrentElement.Description.Contains("<br>"))
      {
        Logger.Warning($"the description of '{this.CurrentElement.Name}' contains <br> tags, please use <br/> since it's loaded into a xml document");
        this.CurrentElement.Description = this.CurrentElement.Description.Replace("<br>", "<br/>");
      }
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.AppendLine($"<h2>{this.CurrentElement.Name.ToUpper()}</h2>");
      stringBuilder.AppendLine(this.GenerateHeader(this._currentElement));
      this.AppendDescription(stringBuilder, this.CurrentElement);
      this.AppendBeforeSource(stringBuilder, this.CurrentElement);
      if (string.IsNullOrWhiteSpace(this.CurrentElement.SourceUrl))
      {
        this.CurrentElement.SourceUrl = this.GenerateSourceUrl(this._currentElement);
        if (string.IsNullOrWhiteSpace(this.CurrentElement.SourceUrl))
        {
          string str = WebUtility.UrlEncode(this.CurrentElement.Name);
          this.CurrentElement.SourceUrl = $"https://www.google.com/search?q={WebUtility.UrlEncode(this.CurrentElement.Source)}+{str}";
        }
      }
      if (this.IncludeSource)
        stringBuilder.Append($"<h6>SOURCE</h6><p><i><a href=\"{this.CurrentElement.SourceUrl}\">{this.CurrentElement.Source}</a></i></p>");
      this.AppendAfterSource(stringBuilder, this.CurrentElement);
      this.CurrentElement.GeneratedDescription = this.Description = $"<body>{stringBuilder}</body>";
      if (!args.ContainsStylesheet)
        return;
      this.StyleSheet = args.Stylesheet;
    }
  }

  protected virtual void AppendBeforeSource(
    StringBuilder descriptionBuilder,
    ElementBase currentElement)
  {
  }

  protected virtual void AppendAfterSource(
    StringBuilder descriptionBuilder,
    ElementBase currentElement)
  {
  }

  protected virtual void HandleHtmlDisplayRequest(HtmlDisplayRequestEvent args)
  {
    if (args.ContainsStylesheet)
      this.StyleSheet = args.Stylesheet;
    this.Description = args.Html;
    this.CurrentElement = (ElementBase) null;
  }

  public static string GenerateHeaderForCard(ElementBase element)
  {
    StringBuilder stringBuilder = new StringBuilder();
    switch (element.Type)
    {
      case "Alignment":
        stringBuilder.AppendLine("<p>");
        stringBuilder.AppendLine($"<b>Abbreviation:</b> {element.ElementSetters.GetSetter("Abbreviation").Value}<br/>");
        stringBuilder.AppendLine("</p>");
        break;
      case "Armor":
        ArmorElement armorElement = element.AsElement<ArmorElement>();
        stringBuilder.AppendLine("<p>");
        stringBuilder.AppendLine(armorElement.ElementSetters.ContainsSetter("armorClass") ? "<br/><b><i>Armor Class. </i></b>" + armorElement.ElementSetters.GetSetter("armorClass").Value : "");
        stringBuilder.AppendLine(armorElement.ElementSetters.ContainsSetter("strength") ? "<br/><b><i>Strength. </i></b>" + armorElement.ElementSetters.GetSetter("strength").Value : "<br/><b><i>Strength. </i></b>—");
        stringBuilder.AppendLine(armorElement.ElementSetters.ContainsSetter("stealth") ? "<br/><b><i>Stealth. </i></b>" + armorElement.ElementSetters.GetSetter("stealth").Value : "<br/><b><i>Stealth. </i></b>—");
        stringBuilder.AppendLine("</p>");
        break;
      case "Companion":
        CompanionElement companionElement = element.AsElement<CompanionElement>();
        stringBuilder.Append($"<p><em>{companionElement.CreatureType}</em></p>");
        break;
      case "Magic Item":
        Item obj = element.AsElement<Item>();
        stringBuilder.Append("<p class=\"underline\"> ");
        if (!string.IsNullOrWhiteSpace(obj.ItemType))
        {
          string additionAttribute = obj.GetSetterAdditionAttribute("type");
          if (additionAttribute != null)
            stringBuilder.Append($"{obj.ItemType} ({additionAttribute}), ");
          else
            stringBuilder.Append(obj.ItemType + ", ");
        }
        else
          stringBuilder.Append("Magic item, ");
        if (!string.IsNullOrWhiteSpace(obj.Rarity))
          stringBuilder.Append(obj.Rarity.ToLower() + " ");
        if (obj.RequiresAttunement)
        {
          string additionAttribute = obj.GetSetterAdditionAttribute("attunement");
          if (additionAttribute != null)
            stringBuilder.Append($"(requires attunement {additionAttribute})");
          else
            stringBuilder.Append("(requires attunement)");
        }
        stringBuilder.Append("</p>");
        break;
      case "Spell":
        Spell spell = element.AsElement<Spell>();
        stringBuilder.Append($"<p class=\"underline\">{spell.GetShortDescription()}</p>");
        stringBuilder.Append("<p>");
        stringBuilder.Append($"<b>Casting Time:</b> {spell.CastingTime}<br/>");
        stringBuilder.Append($"<b>Range:</b> {spell.Range}<br/>");
        stringBuilder.Append($"<b>Components:</b> {spell.GetComponentsString()}<br/>");
        stringBuilder.Append($"<b>Duration:</b> {spell.Duration}<br/>");
        stringBuilder.Append("</p>");
        break;
      case "Weapon":
        WeaponElement weaponElement = element.AsElement<WeaponElement>();
        foreach (ElementBase elementBase in DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Weapon Category"))).OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)).ToList<ElementBase>())
        {
          if (weaponElement.Supports.Contains(elementBase.Id))
            stringBuilder.AppendLine($"<p class=\"underline\">{elementBase.Name} Weapon</p>");
        }
        List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Weapon Property"))).ToList<ElementBase>();
        stringBuilder.Append("<p>");
        List<string> source = new List<string>();
        foreach (string support1 in weaponElement.Supports)
        {
          string support = support1;
          ElementBase elementBase = list.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(support)));
          if (elementBase != null)
          {
            string str = source.Any<string>() ? elementBase.Name.ToLower() : elementBase.Name;
            switch (elementBase.Id)
            {
              case "ID_INTERNAL_WEAPON_PROPERTY_THROWN":
              case "ID_INTERNAL_WEAPON_PROPERTY_AMMUNITION":
                source.Add($"{str} ({weaponElement.Range})");
                continue;
              case "ID_INTERNAL_WEAPON_PROPERTY_VERSATILE":
                source.Add($"{str} ({weaponElement.Versatile})");
                continue;
              case "ID_INTERNAL_WEAPON_PROPERTY_SPECIAL":
                if (weaponElement.Supports.Count<string>((Func<string, bool>) (x => x.Contains("WEAPON_PROPERTY_SPECIAL"))) == 1)
                {
                  source.Add($"{str} ({weaponElement.Versatile})");
                  continue;
                }
                continue;
              default:
                source.Add(str);
                continue;
            }
          }
        }
        stringBuilder.AppendLine(source.Any<string>() ? "<b><i>Properties. </i></b>" + string.Join(", ", (IEnumerable<string>) source.OrderBy<string, string>((Func<string, string>) (x => x))) : "<b><i>Properties. </i></b>—");
        stringBuilder.AppendLine($"<br/><b><i>Damage. </i></b>{weaponElement.Damage} {weaponElement.DamageType}");
        stringBuilder.Append("</p>");
        stringBuilder.AppendLine($"Proficiency with a {weaponElement.Name.ToLower()} allows you to add your proficiency bonus to the attack roll for any attack you make with it.");
        break;
    }
    return stringBuilder.ToString();
  }

  protected virtual string GenerateHeader(ElementBase element)
  {
    StringBuilder stringBuilder = new StringBuilder();
    switch (element.Type)
    {
      case "Alignment":
        stringBuilder.AppendLine("<p>");
        stringBuilder.AppendLine($"<b>Abbreviation:</b> {element.ElementSetters.GetSetter("Abbreviation").Value}<br/>");
        stringBuilder.AppendLine("</p>");
        break;
      case "Armor":
        ArmorElement armorElement = element.AsElement<ArmorElement>();
        stringBuilder.AppendLine("<p>");
        stringBuilder.AppendLine(armorElement.ElementSetters.ContainsSetter("armorClass") ? "<br/><b><i>Armor Class. </i></b>" + armorElement.ElementSetters.GetSetter("armorClass").Value : "");
        stringBuilder.AppendLine(armorElement.ElementSetters.ContainsSetter("strength") ? "<br/><b><i>Strength. </i></b>" + armorElement.ElementSetters.GetSetter("strength").Value : "<br/><b><i>Strength. </i></b>—");
        stringBuilder.AppendLine(armorElement.ElementSetters.ContainsSetter("stealth") ? "<br/><b><i>Stealth. </i></b>" + armorElement.ElementSetters.GetSetter("stealth").Value : "<br/><b><i>Stealth. </i></b>—");
        stringBuilder.AppendLine("</p>");
        break;
      case "Companion":
        Stopwatch stopwatch = Stopwatch.StartNew();
        CompanionElement companionElement = element.AsElement<CompanionElement>();
        stringBuilder.Append($"<p><em>{companionElement.Size} {companionElement.CreatureType.ToLower()}, {companionElement.Alignment.ToLower()}</em></p>");
        stringBuilder.Append($"<p><strong>Armor Class</strong> {companionElement.ArmorClass}<br />");
        stringBuilder.Append($"<strong>Hit Points</strong> {companionElement.HitPoints}<br />");
        stringBuilder.Append($"<strong>Speed</strong> {companionElement.Speed}</p>");
        stringBuilder.Append("<table class=\"abilities\">");
        stringBuilder.Append("<tr><td><strong>STR</strong></td><td><strong>DEX</strong></td><td><strong>CON</strong></td><td><strong>INT</strong></td><td><strong>WIS</strong></td><td><strong>CHA</strong></td></tr>");
        AbilitiesCollection abilitiesCollection = new AbilitiesCollection();
        abilitiesCollection.DisablePointsCalculation = true;
        abilitiesCollection.Strength.BaseScore = companionElement.Strength;
        abilitiesCollection.Dexterity.BaseScore = companionElement.Dexterity;
        abilitiesCollection.Constitution.BaseScore = companionElement.Constitution;
        abilitiesCollection.Intelligence.BaseScore = companionElement.Intelligence;
        abilitiesCollection.Wisdom.BaseScore = companionElement.Wisdom;
        abilitiesCollection.Charisma.BaseScore = companionElement.Charisma;
        stringBuilder.Append($"<tr><td>{abilitiesCollection.Strength.AbilityAndModifierString}</td><td>{abilitiesCollection.Dexterity.AbilityAndModifierString}</td><td>{abilitiesCollection.Constitution.AbilityAndModifierString}</td><td>{abilitiesCollection.Intelligence.AbilityAndModifierString}</td><td>{abilitiesCollection.Wisdom.AbilityAndModifierString}</td><td>{abilitiesCollection.Charisma.AbilityAndModifierString}</td></tr>");
        stringBuilder.Append("</table>");
        stringBuilder.Append("<p>");
        if (companionElement.HasSavingThrows)
          stringBuilder.Append($"<strong>Saving Throws</strong> {companionElement.SavingThrows}<br />");
        if (companionElement.HasSkills)
          stringBuilder.Append($"<strong>Skills</strong> {companionElement.Skills}<br />");
        if (companionElement.HasDamageVulnerabilities)
          stringBuilder.Append($"<strong>Damage Vulnerabilities</strong> {companionElement.DamageVulnerabilities}<br />");
        if (companionElement.HasDamageResistances)
          stringBuilder.Append($"<strong>Damage Resistances</strong> {companionElement.DamageResistances}<br />");
        if (companionElement.HasDamageImmunities)
          stringBuilder.Append($"<strong>Damage Immunities</strong> {companionElement.DamageImmunities}<br />");
        if (companionElement.HasConditionResistances)
          stringBuilder.Append($"<strong>Condition Resistances</strong> {companionElement.ConditionResistances}<br />");
        if (companionElement.HasSenses)
          stringBuilder.Append($"<strong>Senses</strong> {companionElement.Senses}<br />");
        stringBuilder.Append(companionElement.HasLanguages ? $"<strong>Languages</strong> {companionElement.Languages}<br />" : "<strong>Languages</strong>—<br />");
        stringBuilder.Append("<strong>Challenge</strong> " + companionElement.Challenge);
        if (companionElement.HasExperience)
          stringBuilder.Append($" ({companionElement.Experience})");
        stringBuilder.Append("</p>");
        if (companionElement.Traits.Any<string>())
        {
          IEnumerable<ElementBase> source = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Companion Trait")));
          foreach (string trait in companionElement.Traits)
          {
            string item = trait;
            ElementBase elementBase = source.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(item)));
            if (elementBase != null)
              stringBuilder.Append($"<p><strong><em>{elementBase.Name}.</em></strong> {elementBase.Description.Replace("<p>", "")}");
          }
        }
        if (companionElement.Actions.Any<string>())
        {
          stringBuilder.Append("<p style=\"fontsize:14\"><strong>ACTIONS</strong></p>");
          IEnumerable<ElementBase> source = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Companion Action")));
          foreach (string action in companionElement.Actions)
          {
            string companionAction = action;
            ElementBase elementBase = source.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(companionAction)));
            if (elementBase != null)
              stringBuilder.Append($"<p><strong><em>{elementBase.Name}.</em></strong> {elementBase.Description.Replace("<p>", "")}");
          }
        }
        if (companionElement.Reactions.Any<string>())
        {
          stringBuilder.Append("<p style=\"fontsize:14\"><strong>REACTIONS</strong></p>");
          IEnumerable<ElementBase> source = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Companion Reaction")));
          foreach (string reaction in companionElement.Reactions)
          {
            string companionReaction = reaction;
            ElementBase elementBase = source.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(companionReaction)));
            if (elementBase != null)
              stringBuilder.Append($"<p><strong><em>{elementBase.Name}.</em></strong> {elementBase.Description.Replace("<p>", "")}");
          }
        }
        stopwatch.Stop();
        Logger.Warning($"it took {stopwatch.ElapsedMilliseconds}ms to generate the description header of {companionElement}");
        break;
      case "Item":
        Item obj1 = element.AsElement<Item>();
        if (obj1.ElementSetters.ContainsSetter("valuable") || obj1.Category.Equals("Treasure"))
        {
          stringBuilder.Append("<p class=\"underline\">");
          stringBuilder.Append($"{obj1.Category}, {obj1.DisplayPrice}");
          stringBuilder.Append("</p>");
          break;
        }
        break;
      case "Magic Item":
        Item obj2 = element.AsElement<Item>();
        stringBuilder.Append("<p class=\"underline\"> ");
        if (!string.IsNullOrWhiteSpace(obj2.ItemType))
        {
          string additionAttribute = obj2.GetSetterAdditionAttribute("type");
          if (additionAttribute != null)
            stringBuilder.Append($"{obj2.ItemType} ({additionAttribute})");
          else
            stringBuilder.Append(obj2.ItemType ?? "");
        }
        else
          stringBuilder.Append("Magic item");
        if (!string.IsNullOrWhiteSpace(obj2.Rarity))
        {
          stringBuilder.Append(", ");
          stringBuilder.Append(obj2.Rarity.ToLower() + " ");
        }
        else
          stringBuilder.Append(" ");
        if (obj2.RequiresAttunement)
        {
          string additionAttribute = obj2.GetSetterAdditionAttribute("attunement");
          if (additionAttribute != null)
            stringBuilder.Append($"(requires attunement {additionAttribute})");
          else
            stringBuilder.Append("(requires attunement)");
        }
        stringBuilder.Append("</p>");
        break;
      case "Spell":
        Spell spell = element.AsElement<Spell>();
        stringBuilder.Append($"<p class=\"underline\">{spell.GetShortDescription()}</p>");
        stringBuilder.Append("<p>");
        stringBuilder.Append($"<b>Casting Time:</b> {spell.CastingTime}<br/>");
        stringBuilder.Append($"<b>Range:</b> {spell.Range}<br/>");
        stringBuilder.Append($"<b>Components:</b> {spell.GetComponentsString()}<br/>");
        stringBuilder.Append($"<b>Duration:</b> {spell.Duration}<br/>");
        stringBuilder.Append("</p>");
        break;
      case "Weapon":
        WeaponElement weaponElement = element.AsElement<WeaponElement>();
        foreach (ElementBase elementBase in DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Weapon Category"))).OrderBy<ElementBase, string>((Func<ElementBase, string>) (x => x.Name)).ToList<ElementBase>())
        {
          if (weaponElement.Supports.Contains(elementBase.Id))
            stringBuilder.AppendLine($"<p class=\"underline\">{elementBase.Name} Weapon</p>");
        }
        List<ElementBase> list = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Weapon Property"))).ToList<ElementBase>();
        stringBuilder.Append("<p>");
        List<string> source1 = new List<string>();
        foreach (string support1 in weaponElement.Supports)
        {
          string support = support1;
          ElementBase elementBase = list.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(support)));
          if (elementBase != null)
          {
            string str = source1.Any<string>() ? elementBase.Name.ToLower() : elementBase.Name;
            switch (elementBase.Id)
            {
              case "ID_INTERNAL_WEAPON_PROPERTY_THROWN":
              case "ID_INTERNAL_WEAPON_PROPERTY_AMMUNITION":
                source1.Add($"{str} ({weaponElement.Range})");
                continue;
              case "ID_INTERNAL_WEAPON_PROPERTY_VERSATILE":
                source1.Add($"{str} ({weaponElement.Versatile})");
                continue;
              case "ID_INTERNAL_WEAPON_PROPERTY_SPECIAL":
                if (weaponElement.Supports.Count<string>((Func<string, bool>) (x => x.Contains("WEAPON_PROPERTY_SPECIAL"))) == 1)
                {
                  source1.Add($"{str} ({weaponElement.Versatile})");
                  continue;
                }
                continue;
              default:
                source1.Add(str);
                continue;
            }
          }
        }
        stringBuilder.AppendLine(source1.Any<string>() ? "<b><i>Properties. </i></b>" + string.Join(", ", (IEnumerable<string>) source1.OrderBy<string, string>((Func<string, string>) (x => x))) : "<b><i>Properties. </i></b>—");
        stringBuilder.AppendLine($"<br/><b><i>Damage. </i></b>{weaponElement.Damage} {weaponElement.DamageType}");
        stringBuilder.Append("</p>");
        stringBuilder.AppendLine($"Proficiency with a {weaponElement.Name.ToLower()} allows you to add your proficiency bonus to the attack roll for any attack you make with it.");
        break;
    }
    return stringBuilder.ToString();
  }

  protected virtual void AppendDescription(StringBuilder documentBuilder, ElementBase element)
  {
    if (element.Description.Contains("<div element=") || element.Description.Contains("<p element="))
    {
      XmlDocument xmlDocument = new XmlDocument();
      xmlDocument.LoadXml($"<div>{element.Description}</div>");
      if (xmlDocument.DocumentElement != null)
        this.ReplaceInjectedElementDescription((XmlNode) xmlDocument.DocumentElement, element);
      documentBuilder.AppendLine(xmlDocument.InnerXml);
    }
    else
      documentBuilder.AppendLine(element.Description);
  }

  private void ReplaceInjectedElementDescription(
    XmlNode parentNode,
    ElementBase parentElement,
    bool recursive = false)
  {
    if (parentNode == null)
      return;
    foreach (XmlNode childNode in parentNode.ChildNodes)
    {
      if (childNode.ParentNode != null && childNode.ParentNode.Name == "div" && childNode.ParentNode.ContainsAttribute("class"))
      {
        int num = childNode.ParentNode.GetAttributeValue("class") == "reference" ? 1 : 0;
      }
      if (childNode.ContainsAttribute("element"))
        childNode.GetAttributeValue("element").Contains("ID_WOTC_PHB_CLASS_FEATURE_MONK_EMPTY_BODY");
      if (childNode.Name == "div")
        this.ReplaceInjectedElementDescription(childNode, parentElement, true);
      if (childNode.Name == "div" && childNode.ContainsAttribute("element"))
      {
        string injectedElementId = childNode.GetAttributeValue("element");
        ElementBase element = DataManager.Current.ElementsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id == injectedElementId));
        if (element == null)
        {
          Logger.Warning($"the injected description div element '{parentElement.Name}' with the id '{injectedElementId}' is not found in the description of {parentElement}.");
          MessageDialogService.Show($"the injected description div element '{parentElement.Name}' with the id '{injectedElementId}' is not found in the description of {parentElement}.");
          continue;
        }
        childNode.InnerXml = $"<h5 class=\"h5-enhance\">{element.Name.ToUpperInvariant().Replace(" & ", " &amp; ")}</h5>";
        string str = ElementDescriptionHelper.GenerateDescriptionBase(element).Replace("class=\"reference\"", "class=\"\"");
        childNode.InnerXml += str;
      }
      if (childNode.Name == "p" && childNode.ContainsAttribute("element"))
      {
        string injectedElementId = childNode.GetAttributeValue("element");
        ElementBase elementBase = DataManager.Current.ElementsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id == injectedElementId));
        if (elementBase == null)
        {
          Logger.Warning($"the injected description p element '{parentElement.Name}' with the id '{injectedElementId}' is not found in the description of {parentElement}.");
          MessageDialogService.Show($"the injected description p element '{parentElement.Name}' with the id '{injectedElementId}' is not found in the description of {parentElement}.");
        }
        else
        {
          string str = $"<p class=\"indent\"><strong><em>{elementBase.Name}. </em></strong> ";
          try
          {
            if (elementBase.Description.StartsWith("<p>"))
              str += elementBase.Description.Substring(3, elementBase.Description.Length - 3);
            else if (elementBase.Description.StartsWith("<p "))
            {
              if (Debugger.IsAttached)
                Debugger.Break();
            }
            else
              str += "</p>";
          }
          catch (Exception ex)
          {
            Logger.Exception(ex, nameof (ReplaceInjectedElementDescription));
            Dictionary<string, string> properties = AnalyticsErrorHelper.CreateProperties("element", elementBase.ToString());
            string description = elementBase.Description;
            AnalyticsErrorHelper.Exception(ex, properties, description, nameof (ReplaceInjectedElementDescription), 844);
          }
          if (elementBase.Description.StartsWith("<p>"))
            str += elementBase.Description.Substring(3, elementBase.Description.Length - 3);
          else if (elementBase.Description.StartsWith("<p "))
          {
            if (Debugger.IsAttached)
              Debugger.Break();
          }
          else
            str += "</p>";
          childNode.InnerXml = str;
        }
      }
    }
  }

  protected virtual string GenerateSourceUrl(ElementBase element)
  {
    string sourceUrl = string.Empty;
    SourceItem sourceItem = CharacterManager.Current.SourcesManager.SourceItems.FirstOrDefault<SourceItem>((Func<SourceItem, bool>) (x => x.Source.Name.Equals(element.Source)));
    if (sourceItem != null && sourceItem.Source.HasSourceUrl)
      return sourceItem.Source.Url;
    switch (element.Source)
    {
      case "Curse of Strahd":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/curse-strahd";
        break;
      case "Dragon Heist":
      case "Waterdeep Dragon Heist":
      case "Waterdeep: Dragon Heist":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/dragonheist";
        break;
      case "Dungeon Master's Guide":
      case "Dungeon Masters Guide":
      case "Dungeon Master’s Guide":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/dungeon-masters-guide#content";
        break;
      case "Dungeon of the Mad Mage":
      case "Waterdeep Dungeon of the Mad Mage":
      case "Waterdeep: Dungeon of the Mad Mage":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/waterdeep-dungeon-mad-mage";
        break;
      case "Monster Manual":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/monster-manual#content";
        break;
      case "Mordenkainen's Tome of Foes":
      case "Mordenkainens Tome of Foes":
      case "Mordenkainen’s Tome of Foes":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/mordenkainens-tome-foes";
        break;
      case "Out of the Abyss":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/outoftheabyss";
        break;
      case "Player's Handbook":
      case "Players Handbook":
      case "Player’s Handbook":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/rpg_playershandbook#content";
        break;
      case "Princes of the Apocalypse":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/princes-apocalypse#content";
        break;
      case "Storm King's Thunder":
      case "Storm Kings Thunder":
      case "Storm King’s Thunder":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/storm-kings-thunder";
        break;
      case "Sword Coast Adventurer's Guide":
      case "Sword Coast Adventurers Guide":
      case "Sword Coast Adventurer’s Guide":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/sc-adventurers-guide#content";
        break;
      case "System Reference Document":
        sourceUrl = "http://dnd.wizards.com/articles/features/systems-reference-document-srd";
        break;
      case "Tales from the Yawning Portal":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/tales-yawning-portal";
        break;
      case "Tomb of Annihilation":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/tomb-annihilation";
        break;
      case "Volo's Guide to Monsters":
      case "Volos Guide to Monsters":
      case "Volo’s Guide to Monsters":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/volos-guide-to-monsters";
        break;
      case "Xanathar's Guide to Everything":
      case "Xanathars Guide to Everything":
      case "Xanathar’s Guide to Everything":
        sourceUrl = "http://dnd.wizards.com/products/tabletop-games/rpg-products/xanathars-guide-everything ";
        break;
      default:
        Logger.Info($"no source url available for '{element.Name}'");
        break;
    }
    return sourceUrl;
  }

  public virtual void OnHandleEvent(ElementDescriptionDisplayRequestEvent args)
  {
    this.HandleDisplayRequest(args);
  }

  public virtual void OnHandleEvent(HtmlDisplayRequestEvent args)
  {
    this.HandleHtmlDisplayRequest(args);
  }

  private void HandleLinkedDescriptionRequest(string id)
  {
  }

  public bool IsSpeechEnabled
  {
    get => this._isSpeechEnabled;
    set => this.SetProperty<bool>(ref this._isSpeechEnabled, value, nameof (IsSpeechEnabled));
  }

  public bool IsSpeechActive
  {
    get => this._isSpeechActive;
    set => this.SetProperty<bool>(ref this._isSpeechActive, value, nameof (IsSpeechActive));
  }

  public string SelectedText
  {
    get => this._selectedText;
    set => this.SetProperty<string>(ref this._selectedText, value, nameof (SelectedText));
  }

  public ICommand StartSpeechCommand => (ICommand) new RelayCommand(new Action(this.StartSpeech));

  public ICommand StopSpeechCommand => (ICommand) new RelayCommand(new Action(this.StopSpeech));

  private void StartSpeech()
  {
    try
    {
      if (this.SelectedText.Length > 0)
      {
        SpeechService.Default.StartSpeech(this.SelectedText);
      }
      else
      {
        string xml = this.Description.Replace("</h1>", "</h1>__ENTER__").Replace("</h2>", "</h2>__ENTER__").Replace("</h3>", "</h3>__ENTER__").Replace("</h4>", "</h4>__ENTER__").Replace("</h5>", "</h5>__ENTER__").Replace("</h6>", "</h6>__ENTER__").Replace("<br/>", "<br/>__ENTER__").Replace("</p>", "</p>__ENTER__").Replace("d10", "d 10").Replace("d12", "d 12").Replace("d20", "d 20").Replace("—", " , ").Replace(" cp ", " copper pieces ").Replace(" cp)", " copper pieces)").Replace(" cp.", " copper pieces.").Replace(" sp ", " silver pieces ").Replace(" sp)", " silver pieces)").Replace(" sp.", " silver pieces.").Replace(" ep ", " electrum pieces ").Replace(" ep)", " electrum pieces)").Replace(" ep.", " electrum pieces.").Replace(" gp ", " gold pieces ").Replace(" gp)", " gold pieces)").Replace(" gp.", " gold pieces.").Replace(" pp ", " platinum pieces ").Replace(" pp)", " platinum pieces)").Replace(" pp.", " platinum pieces.");
        XmlDocument xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(xml);
        SpeechService.Default.StartSpeech(xmlDocument.InnerText.Replace("__ENTER__", Environment.NewLine));
      }
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }

  private void StopSpeech() => SpeechService.Default.StopSpeech();

  private void _speechService_SpeechStarted(object sender, EventArgs e)
  {
    this.IsSpeechActive = true;
  }

  private void _speechService_SpeechStopped(object sender, EventArgs e)
  {
    this.IsSpeechActive = false;
  }

  public virtual void OnHandleEvent(ResourceDocumentDisplayRequestEvent args)
  {
    string resourceWebDocument = DataManager.Current.GetResourceWebDocument(args.ResourceFilename);
    if (!resourceWebDocument.Contains("<body>"))
      Logger.Warning($"the contents of '{args.ResourceFilename}' needs to be in a <body> tag");
    this.Description = resourceWebDocument.Contains("<body>") ? resourceWebDocument : $"<body>{resourceWebDocument}</body>";
  }

  void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
  {
    this.UpdateTheme();
  }

  private void UpdateTheme()
  {
    this.IsDarkStyle = ApplicationContext.Current.Settings.Theme.Contains("Dark");
    this.OnPropertyChanged("Description");
    this.Description += Environment.NewLine;
  }

  public void OnHandleEvent(CharacterLoadingCompletedEvent args) => this.UpdateTheme();
}
