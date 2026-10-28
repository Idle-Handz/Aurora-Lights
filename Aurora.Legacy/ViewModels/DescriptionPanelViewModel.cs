// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.DescriptionPanelViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Presentation.Events.Application;
using Builder.Presentation.Events.Global;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Media;
using System.Xml;

#nullable disable
namespace Builder.Presentation.ViewModels;

[Obsolete("old descrition panel view model - use DescriptionPanelViewModelBase")]
public class DescriptionPanelViewModel : 
  ViewModelBase,
  ISubscriber<ExpanderHandlerSelectionChanged>,
  ISubscriber<ResourceDocumentDisplayRequestEvent>,
  ISubscriber<SettingsChangedEvent>
{
  protected readonly string _originalSheet;
  private string _styleSheet;
  private string _elementDescription;

  public DescriptionPanelViewModel()
  {
    if (this.IsInDesignMode)
    {
      this.InitializeDesignData();
    }
    else
    {
      this._originalSheet = DataManager.Current.GetResourceWebDocument("stylesheet.css");
      this._styleSheet = DataManager.Current.GetResourceWebDocument("stylesheet.css");
      this.EventAggregator.Subscribe((object) this);
    }
  }

  public string StyleSheet
  {
    get => this._styleSheet;
    set => this.SetProperty<string>(ref this._styleSheet, value, nameof (StyleSheet));
  }

  public string ElementDescription
  {
    get => this._elementDescription;
    set
    {
      this.SetProperty<string>(ref this._elementDescription, value, nameof (ElementDescription));
    }
  }

  public ElementBase CurrentElement { get; set; }

  public virtual void OnHandleEvent(ExpanderHandlerSelectionChanged args)
  {
    this.CurrentElement = args.SelectedElement;
    if (args.SelectedElement == null)
      return;
    if (args.SelectedElement.HasGeneratedDescription)
    {
      this.ElementDescription = args.SelectedElement.GeneratedDescription;
      this.EventAggregator.Send<ElementDescriptionDisplayRequestEvent>(new ElementDescriptionDisplayRequestEvent(args.SelectedElement));
    }
    else
    {
      if (args.SelectedElement.Description.Contains("<br>"))
      {
        Logger.Warning($"the description of '{args.SelectedElement.Name}' contains <br> tags, please use <br/> since it's loaded into a xmldocument");
        args.SelectedElement.Description = args.SelectedElement.Description.Replace("<br>", "<br/>");
      }
      this.InitializeStyleSheet(ApplicationContext.Current.Settings.Accent);
      StringBuilder documentBuilder = new StringBuilder($"<h2>{args.SelectedElement.Name.ToUpper()}</h2>");
      switch (args.SelectedElement.Type)
      {
        case "Spell":
          Spell selectedElement1 = (Spell) args.SelectedElement;
          documentBuilder.AppendLine($"<p><i>{selectedElement1.GetShortDescription()}</i></p>");
          documentBuilder.AppendLine("<p>");
          documentBuilder.AppendLine($"<b>Casting Time:</b> {selectedElement1.CastingTime}<br/>");
          documentBuilder.AppendLine($"<b>Range:</b> {selectedElement1.Range}<br/>");
          documentBuilder.AppendLine($"<b>Components:</b> {selectedElement1.GetComponentsString()}<br/>");
          documentBuilder.AppendLine($"<b>Duration:</b> {selectedElement1.Duration}<br/>");
          documentBuilder.AppendLine("</p>");
          break;
        case "Item":
          Item selectedElement2 = (Item) args.SelectedElement;
          documentBuilder.AppendLine("<p>");
          documentBuilder.AppendLine($"<b>Category:</b> {selectedElement2.Category}<br/>");
          documentBuilder.AppendLine($"<b>Cost:</b> {selectedElement2.Cost} {selectedElement2.CurrencyAbbreviation}<br/>");
          documentBuilder.AppendLine($"<b>Weight:</b> {selectedElement2.Weight}<br/>");
          documentBuilder.AppendLine("</p>");
          break;
        case "Companion":
          MonsterElement selectedElement3 = (MonsterElement) args.SelectedElement;
          documentBuilder.AppendLine("<p>");
          documentBuilder.AppendLine($"<b>Alignment:</b> {selectedElement3.Alignment}<br/>");
          documentBuilder.AppendLine("</p>");
          break;
      }
      this.AppendDescription(documentBuilder, args.SelectedElement);
      if (string.IsNullOrWhiteSpace(args.SelectedElement.SourceUrl))
      {
        args.SelectedElement.SourceUrl = DescriptionPanelViewModel.GetSourceUrl(args.SelectedElement);
        if (string.IsNullOrWhiteSpace(args.SelectedElement.SourceUrl) && ApplicationContext.Current.Settings.SearchMissingSourceOnline)
          args.SelectedElement.SourceUrl = $"https://www.google.com/search?q={WebUtility.UrlEncode(args.SelectedElement.Source)}+{WebUtility.UrlEncode(args.SelectedElement.Name)}";
      }
      StringBuilder stringBuilder = documentBuilder;
      string str;
      if (!string.IsNullOrWhiteSpace(args.SelectedElement.SourceUrl))
        str = $"<h6>SOURCE</h6><p class=\"flavor\"><a href=\"{args.SelectedElement.SourceUrl}\">{args.SelectedElement.Source}</a></p>";
      else
        str = $"<h6>SOURCE</h6><p class=\"flavor\">{args.SelectedElement.Source}</p>";
      stringBuilder.Append(str);
      this.ElementDescription = $"<body>{documentBuilder}</body>";
      args.SelectedElement.GeneratedDescription = this.ElementDescription;
      this.EventAggregator.Send<ElementDescriptionDisplayRequestEvent>(new ElementDescriptionDisplayRequestEvent(args.SelectedElement));
    }
  }

  private void AppendDescription(StringBuilder documentBuilder, ElementBase element)
  {
    if (element.Description.Contains("<div element="))
    {
      XmlDocument xmlDocument = new XmlDocument();
      xmlDocument.LoadXml($"<div>{element.Description}</div>");
      if (xmlDocument.DocumentElement != null)
      {
        foreach (XmlNode childNode in xmlDocument.DocumentElement.ChildNodes)
        {
          if (childNode.Name == "div" && childNode.ContainsAttribute(nameof (element)))
          {
            string injectedElementId = childNode.GetAttributeValue(nameof (element));
            ElementBase elementBase = DataManager.Current.ElementsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id == injectedElementId));
            if (elementBase == null)
            {
              Logger.Warning($"the injected element '{element.Name}' with the id '{injectedElementId}' is not found.");
            }
            else
            {
              childNode.InnerXml = $"<h5 class=\"h5-enhance\">{elementBase.Name.ToUpper()}</h5>";
              childNode.InnerXml += elementBase.Description;
            }
          }
        }
      }
      documentBuilder.AppendLine(xmlDocument.InnerXml);
    }
    else
      documentBuilder.AppendLine(element.Description);
  }

  public virtual void OnHandleEvent(ResourceDocumentDisplayRequestEvent args)
  {
    this.InitializeStyleSheet(ApplicationContext.Current.Settings.Accent);
    string resourceWebDocument = DataManager.Current.GetResourceWebDocument(args.ResourceFilename);
    if (!resourceWebDocument.Contains("<body>"))
      Logger.Warning($"the contents of '{args.ResourceFilename}' needs to be in a <body> tag");
    this.ElementDescription = resourceWebDocument.Contains("<body>") ? resourceWebDocument : $"<body>{resourceWebDocument}</body>";
  }

  private static string GetSourceUrl(ElementBase element)
  {
    string sourceUrl = string.Empty;
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

  protected override void InitializeDesignData()
  {
    this.ElementDescription = DataManager.Current.GetResourceWebDocument("description-panel-design-data.html");
  }

  [Obsolete]
  protected virtual void InitializeStyleSheet(string accentName)
  {
    string str = ApplicationManager.Current.GetHighlightColor().ToString();
    SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb((byte) 1, (byte) 2, (byte) 3));
    this.StyleSheet = this._originalSheet.Replace("2A363B", str.Substring(3, 6));
  }

  void ISubscriber<SettingsChangedEvent>.OnHandleEvent(SettingsChangedEvent args)
  {
    this.InitializeStyleSheet(args.Settings.Accent);
  }
}
