// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterFile
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Rules;
using Builder.Presentation.Events.Shell;
using Builder.Presentation.Views.Sliders;
using Builder.Presentation.UserControls.Spellcasting;
using Builder.Presentation.Extensions;
using Builder.Presentation.Models.Collections;
using Builder.Presentation.Models.Equipment;
using Builder.Presentation.Models.Helpers;
using Builder.Presentation.Models.NewFolder1;
using Builder.Presentation.Models.Sources;
using Builder.Presentation.Services;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services.Calculator;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Telemetry;
using Builder.Presentation.Utilities;
using Builder.Presentation.ViewModels;
using Builder.Presentation.ViewModels.Shell.Items;
using Builder.Presentation.ViewModels.Shell.Manage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

#nullable disable
namespace Builder.Presentation.Models;

public class CharacterFile : ObservableObject
{
    private const string RootNodeName = "character";
    private const string DisplayPropertiesNodeName = "display-properties";
    private const string BuildNodeName = "build";
    private const string AbilitiesNodeName = "abilities";
    private const string AppearanceNodeName = "appearance";
    private const string ElementsNodeName = "elements";
    private const string SumNodeName = "sum";
    private const string DisplayPropertiesIsFavorite = "favorite";
    private const string DisplayPropertiesName = "name";
    private const string DisplayPropertiesRace = "race";
    private const string DisplayPropertiesClass = "class";
    private const string DisplayPropertiesBackground = "background";
    private const string DisplayPropertiesLevel = "level";
    private const string DisplayPropertiesLocalPortrait = "local-portrait";
    private const string DisplayPropertiesBase64Portrait = "base64-portrait";
    private XmlDocument _document;
    private string _filepath;
    private bool _isInitialized;
    private bool _isNew;
    private bool _isFavorite;
    private string _displayName;
    private string _displayRace;
    private string _displayClass;
    private string _displayArchetype;
    private string _displayBackground;
    private string _displayLevel;
    private string _displayPortraitFilePath;
    private string _displayPortraitBase64;
    private string _fileName;
    private string _displayVersion;
    private string _collectionGroupName;

    public CharacterFile(string filepath)
    {
        this._filepath = filepath;
        this._displayVersion = "1.0.166.7407";
    }

    public string FilePath
    {
        get => this._filepath;
        set => this.SetProperty<string>(ref this._filepath, value, nameof(FilePath));
    }

    public string FileName
    {
        get => this._fileName;
        set => this.SetProperty<string>(ref this._fileName, value, nameof(FileName));
    }

    public string DisplayVersion
    {
        get => this._displayVersion;
        set => this.SetProperty<string>(ref this._displayVersion, value, nameof(DisplayVersion));
    }

    public bool IsInitialized
    {
        get => this._isInitialized;
        set => this.SetProperty<bool>(ref this._isInitialized, value, nameof(IsInitialized));
    }

    public bool IsNew
    {
        get => this._isNew;
        set => this.SetProperty<bool>(ref this._isNew, value, nameof(IsNew));
    }

    public bool IsFavorite
    {
        get => this._isFavorite;
        set => this.SetProperty<bool>(ref this._isFavorite, value, nameof(IsFavorite));
    }

    public string CollectionGroupName
    {
        get => this._collectionGroupName;
        set
        {
            this.SetProperty<string>(ref this._collectionGroupName, value, nameof(CollectionGroupName));
        }
    }

    public string DisplayName
    {
        get => this._displayName;
        set => this.SetProperty<string>(ref this._displayName, value, nameof(DisplayName));
    }

    public string DisplayRace
    {
        get => this._displayRace;
        set
        {
            this.SetProperty<string>(ref this._displayRace, value, nameof(DisplayRace));
            this.OnPropertyChanged("DisplayBuild");
        }
    }

    public string DisplayClass
    {
        get => this._displayClass;
        set
        {
            this.SetProperty<string>(ref this._displayClass, value, nameof(DisplayClass));
            this.OnPropertyChanged("DisplayBuild");
        }
    }

    public string DisplayArchetype
    {
        get => this._displayArchetype;
        set => this.SetProperty<string>(ref this._displayArchetype, value, nameof(DisplayArchetype));
    }

    public string DisplayBackground
    {
        get => this._displayBackground;
        set => this.SetProperty<string>(ref this._displayBackground, value, nameof(DisplayBackground));
    }

    public string DisplayLevel
    {
        get => this._displayLevel;
        set
        {
            this.SetProperty<string>(ref this._displayLevel, value, nameof(DisplayLevel));
            this.OnPropertyChanged("DisplayBuild");
        }
    }

    public string DisplayPortraitFilePath
    {
        get => this._displayPortraitFilePath;
        set
        {
            this.SetProperty<string>(ref this._displayPortraitFilePath, value, nameof(DisplayPortraitFilePath));
        }
    }

    public string DisplayPortraitBase64
    {
        get => this._displayPortraitBase64;
        set
        {
            this.SetProperty<string>(ref this._displayPortraitBase64, value, nameof(DisplayPortraitBase64));
        }
    }

    public string DisplayBuild => $"Level {this.DisplayLevel} {this.DisplayRace} {this.DisplayClass}";

    public void InitializeDisplayPropertiesFromCharacter(Character character)
    {
        this.DisplayName = character.Name;
        this.DisplayRace = character.Race;
        this.DisplayClass = character.Class;
        this.DisplayArchetype = character.Archetype;
        this.DisplayBackground = character.Background;
        this.DisplayLevel = character.Level.ToString();
        this.DisplayPortraitFilePath = character.PortraitFilename;
    }

    public void InitializeDisplayPropertiesFromFilePath()
    {
        try
        {
            this.IsInitialized = false;
            this._document = new XmlDocument();
            this._document.Load(this._filepath);
            XmlElement xmlElement1 = this._document["character"];
            XmlElement xmlElement2 = xmlElement1?["display-properties"];
            this.ReadInformationNode((XmlNode)xmlElement1);
            if (xmlElement1.ContainsAttribute("version"))
                this.DisplayVersion = xmlElement1.GetAttributeValue("version");
            if (xmlElement2 == null)
            {
                MessageDialogContext.Current?.Show("unable to load display properties for " + this._filepath);
                this.IsInitialized = false;
            }
            else
            {
                foreach (XmlAttribute attribute in (XmlNamedNodeMap)xmlElement2.Attributes)
                {
                    switch (attribute.Name)
                    {
                        case "favorite":
                            this.IsFavorite = Convert.ToBoolean(attribute.Value);
                            continue;
                        default:
                            Logger.Warning("unhandled display property attribute {0} in character file '{1}'", (object)attribute.Name, (object)this._filepath);
                            continue;
                    }
                }
                foreach (XmlNode childNode in xmlElement2.ChildNodes)
                {
                    string innerText = childNode.InnerText;
                    string name = childNode.Name;
                    if (name != null)
                    {
                        switch (name)
                        {
                            case "portrait":
                                try
                                {
                                    this.DisplayPortraitFilePath = childNode["local"].GetInnerText();
                                    this.DisplayPortraitBase64 = childNode["base64"].GetInnerText();
                                    continue;
                                }
                                catch (Exception ex)
                                {
                                    Logger.Exception(ex, nameof(InitializeDisplayPropertiesFromFilePath));
                                    continue;
                                }
                            case "background":
                                this.DisplayBackground = innerText;
                                continue;
                            case "archetype":
                                this.DisplayArchetype = innerText;
                                continue;
                            case "name":
                                this.DisplayName = innerText;
                                continue;
                            case "race":
                                this.DisplayRace = innerText;
                                continue;
                            case "level":
                                this.DisplayLevel = innerText;
                                continue;
                            case "class":
                                this.DisplayClass = innerText;
                                continue;
                        }
                    }
                    Logger.Warning("unhandled display property element {0} in character file '{1}'", (object)childNode.Name, (object)this._filepath);
                }
                this.FileName = new FileInfo(this._filepath).Name;
                this.SaveRemotePortrait();
                this.IsInitialized = true;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(InitializeDisplayPropertiesFromFilePath));
            MessageDialogContext.Current?.ShowException(ex);
        }
    }

    public void UpdateGroupName(string newGroup)
    {
        try
        {
            this._document = new XmlDocument();
            this._document.Load(this._filepath);
            XmlNode parentNode = this._document.DocumentElement.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("information")));
            if (parentNode == null)
            {
                parentNode = this._document.DocumentElement.AppendChild(this._document.CreateNode(XmlNodeType.Element, "information", (string)null));
                parentNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "group", (string)null));
            }
            if (parentNode.ContainsChildNode("group"))
                parentNode.GetChildNode("group").InnerText = newGroup;
            this._document.Save(this._filepath);
            this.CollectionGroupName = newGroup;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(UpdateGroupName));
            MessageDialogContext.Current?.ShowException(ex);
        }
    }

    public bool Save() => this.Save(CharacterManager.Current.Character);

    public bool Save(Character character)
    {
        BuildDocument(character);
        using (XmlTextWriter w = new XmlTextWriter(this._filepath, Encoding.UTF8))
        {
            w.Formatting = Formatting.Indented;
            w.IndentChar = '\t';
            w.Indentation = 1;
            this._document.Save((XmlWriter)w);
            return true;
        }
    }

    /// <summary>
    /// Serializes <paramref name="character"/> into the same XML form that <see cref="Save(Character)"/>
    /// writes to disk, and returns the bytes. Used by CharacterContext to capture in-memory tab state
    /// before swapping the CharacterManager singleton to a different tab's character.
    /// </summary>
    public byte[] SerializeCharacter(Character character)
    {
        BuildDocument(character);
        using var ms = new MemoryStream();
        using (XmlTextWriter w = new XmlTextWriter(ms, Encoding.UTF8))
        {
            w.Formatting = Formatting.Indented;
            w.IndentChar = '\t';
            w.Indentation = 1;
            this._document.Save((XmlWriter)w);
        }
        return ms.ToArray();
    }

    private void BuildDocument(Character character)
    {
        this._document = new XmlDocument();
        XmlNode parentNode = this._document.AppendChild(this._document.CreateNode(XmlNodeType.Element, nameof(character), (string)null));
        Dictionary<string, string> attributesDictionary = new Dictionary<string, string>()
        {
            { "version", "1.0.166.7407" },
            { "preview", "false" },
        };
        parentNode.AppendAttributes(attributesDictionary);
        parentNode.AppendChild((XmlNode)this._document.CreateComment(" Aurora - https://www.aurorabuilder.com "));
        parentNode.AppendChild((XmlNode)this._document.CreateComment(" information "));
        this.WriteInformationNode(parentNode);
        parentNode.AppendChild((XmlNode)this._document.CreateComment(" display data "));
        parentNode.AppendChild(this.CreateDisplayPropertiesNode(character));
        parentNode.AppendChild((XmlNode)this._document.CreateComment(" build data "));
        parentNode.AppendChild(this.CreateBuildNode(character));
        parentNode.AppendChild((XmlNode)this._document.CreateComment(" restricted sources "));
        parentNode.AppendChild(this.CreateRestrictedSourcesNode());
    }

    public async Task<CharacterFile.LoadResult> Load() => await this.Load(this._filepath);

    public async Task<CharacterFile.LoadResult> Load(string filepath)
    {
        int currentProgress = 0;
        int progressMax = 8;
        await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
        await this.SendCharacterLoadingScreenStatusUpdate("loading character file");
        this.FilePath = filepath;
        this.InitializeDisplayPropertiesFromFilePath();
        progressMax += int.Parse(this.DisplayLevel);
        ++currentProgress;
        await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
        if (!this.IsInitialized)
            return new CharacterFile.LoadResult(false, "character file not initialized");
        if (this._document.DocumentElement == null)
            throw new NullReferenceException("DocumentElement not found on " + this._filepath);
        this.ReadInformationNode((XmlNode)this._document.DocumentElement);
        XmlNode buildNode = (XmlNode)this._document.DocumentElement["build"];
        if (buildNode == null)
            throw new NullReferenceException("buildNode not found on " + this._filepath);
        string attributeValue1 = this._document.DocumentElement.GetAttributeValue("version");
        this._document.DocumentElement.GetAttributeAsBoolean("preview");
        Version version = new Version(attributeValue1);
        Stopwatch sw = Stopwatch.StartNew();
        Character character = await CharacterManager.Current.New(false);
        CharacterManager.Current.Status.IsLoaded = false;
        ++currentProgress;
        await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
        XmlNode inputNode = (XmlNode)buildNode["input"];
        character.Name = inputNode != null ? inputNode["name"].GetInnerText() : throw new NullReferenceException("inputNode not found on " + this._filepath);
        character.PlayerName = inputNode["player-name"].GetInnerText();
        character.Gender = inputNode["gender"].GetInnerText();
        int result1;
        character.Experience = int.TryParse(inputNode["experience"].GetInnerText(), out result1) ? result1 : 0;
        character.Backstory = inputNode["backstory"].GetInnerText();
        XmlNode xmlNode1 = (XmlNode)inputNode["currency"];
        if (xmlNode1 == null)
            throw new NullReferenceException("currencyNode not found on " + this._filepath);
        character.Inventory.Coins.Set((long)int.Parse(xmlNode1["copper"].GetInnerText()), (long)int.Parse(xmlNode1["silver"].GetInnerText()), (long)int.Parse(xmlNode1["electrum"].GetInnerText()), (long)int.Parse(xmlNode1["gold"].GetInnerText()), (long)int.Parse(xmlNode1["platinum"].GetInnerText()));
        character.Inventory.Equipment = xmlNode1["equipment"].GetInnerText();
        character.Inventory.Treasure = xmlNode1["treasure"].GetInnerText();
        XmlNode xmlNode2 = (XmlNode)inputNode["organization"];
        character.OrganisationName = xmlNode2 != null ? xmlNode2["name"].GetInnerText() : throw new NullReferenceException("organizationNode not found on " + this._filepath);
        character.OrganisationSymbol = xmlNode2["symbol"].GetInnerText();
        character.Allies = xmlNode2["allies"].GetInnerText();
        character.AdditionalFeatures = ((XmlNode)inputNode["additional-features"] ?? throw new NullReferenceException("featuresNode not found on " + this._filepath)).GetInnerText();
        this.ReadCharacterNotesNode(inputNode, character);
        this.ReadQuestItemsNode(inputNode, character);
        this.ReadAppearanceNode((XmlNode)buildNode["appearance"] ?? throw new NullReferenceException("appearanceNode not found on " + this._filepath), character);
        XmlNode xmlNode3 = (XmlNode)buildNode["abilities"];
        if (xmlNode3 == null)
            throw new NullReferenceException("abilitiesNode not found on " + this._filepath);
        AbilitiesCollection abilities = character.Abilities;
        abilities.Strength.BaseScore = Convert.ToInt32(xmlNode3[abilities.Strength.Name.ToLowerInvariant()].GetInnerText());
        abilities.Dexterity.BaseScore = Convert.ToInt32(xmlNode3[abilities.Dexterity.Name.ToLowerInvariant()].GetInnerText());
        abilities.Constitution.BaseScore = Convert.ToInt32(xmlNode3[abilities.Constitution.Name.ToLowerInvariant()].GetInnerText());
        abilities.Intelligence.BaseScore = !xmlNode3.ContainsChildNode(abilities.Intelligence.Name.ToLower()) ? Convert.ToInt32(xmlNode3[abilities.Intelligence.Name.ToLowerInvariant()].GetInnerText()) : Convert.ToInt32(xmlNode3[abilities.Intelligence.Name.ToLower()].GetInnerText());
        abilities.Wisdom.BaseScore = Convert.ToInt32(xmlNode3[abilities.Wisdom.Name.ToLowerInvariant()].GetInnerText());
        abilities.Charisma.BaseScore = Convert.ToInt32(xmlNode3[abilities.Charisma.Name.ToLowerInvariant()].GetInnerText());
        int availablePoints = abilities.CalculateAvailablePoints();
        int int32 = Convert.ToInt32(xmlNode3.GetAttributeValue("available-points"));
        if (int32 != availablePoints)
            Logger.Warning("availablePoints ({0}) differs from the calculatedAvailablePoints ({1})", (object)int32, (object)availablePoints);
        XmlNode sourcesNode = (XmlNode)this._document.DocumentElement["sources"];
        if (sourcesNode != null)
        {
            ++currentProgress;
            await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
            await this.SendCharacterLoadingScreenStatusUpdate("Setting Source Restrictions");
            this.ReadRestrictedSourcesNodes(sourcesNode);
        }
        else
            Logger.Warning("no sources in " + this.FileName);
        ++currentProgress;
        await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
        await this.SendCharacterLoadingScreenStatusUpdate("preparing character");
        XmlNode elementsNode = (XmlNode)buildNode["elements"];
        if (elementsNode == null)
            throw new NullReferenceException("elementsNode not found on " + this._filepath);
        int.Parse(elementsNode.GetAttributeValue("level-count"));
        int optionCount = 0;
        ++currentProgress;
        await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
        XmlNode elementNode;
        foreach (XmlNode childNode1 in elementsNode.ChildNodes)
        {
            elementNode = childNode1;
            string type = elementNode.GetAttributeValue("type");
            if (type == "Option")
            {
                ++optionCount;
                await this.SendCharacterLoadingScreenStatusUpdate("applying character options");
                string attributeValue2 = elementNode.GetAttributeValue("id");
                ElementBase element = DataManager.Current.ElementsCollection.GetElement(attributeValue2);
                if (element != null)
                    CharacterManager.Current.RegisterElement(element);
                else
                    Logger.Warning("unable to load the option with the id: " + attributeValue2);
            }
            if (type == "Level")
            {
                LevelElement element = DataManager.Current.ElementsCollection.GetElement(elementNode.GetAttributeValue("id")).AsElement<LevelElement>();
                await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
                await this.SendCharacterLoadingScreenStatusUpdate("applying character elements");
                if (element.Level == 1)
                {
                    CharacterManager.Current.RegisterElement((ElementBase)element);
                    await this.ReadChildElements(elementNode, (ElementBase)element);
                }
                else
                {
                    bool flag1 = false;
                    if (elementNode.ContainsAttribute("multiclass"))
                        flag1 = Convert.ToBoolean(elementNode.GetAttributeValue("multiclass"));
                    if (flag1)
                    {
                        bool flag2 = false;
                        if (elementNode.ContainsAttribute("starting"))
                            flag2 = Convert.ToBoolean(elementNode.GetAttributeValue("starting"));
                        if (flag2)
                        {
                            CharacterManager.Current.NewMulticlass();
                            await this.ReadChildElements(elementNode, (ElementBase)element);
                        }
                        else
                            CharacterManager.Current.LevelUpMulti(DataManager.Current.ElementsCollection.GetElement(elementNode.GetAttributeValue("class")).AsElement<Multiclass>());
                    }
                    else
                        CharacterManager.Current.LevelUpMain();
                }
                ++currentProgress;
                await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
                element = (LevelElement)null;
            }
            type = (string)null;
            elementNode = (XmlNode)null;
        }
        foreach (XmlNode childNode in elementsNode.ChildNodes)
        {
            elementNode = childNode;
            string attributeValue3 = elementNode.GetAttributeValue("type");
            if (attributeValue3 == "Level" || attributeValue3 == "Option")
            {
                ElementBase element = DataManager.Current.ElementsCollection.GetElement(elementNode.GetAttributeValue("id"));
                await this.ReadChildElements(elementNode, element);
                if (elementNode.ContainsAttribute("rndhp"))
                {
                    int[] array = ((IEnumerable<string>)elementNode.GetAttributeValue("rndhp").Split(',')).Select<string, int>(new Func<string, int>(int.Parse)).ToArray<int>();
                    CharacterManager.Current.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>)(x => x.LevelElements.Contains(element)))?.SetRandomHitPointsArray(array);
                }
            }
            elementNode = (XmlNode)null;
        }
        int result2;
        character.Experience = int.TryParse(inputNode["experience"].GetInnerText(), out result2) ? result2 : 0;
        this.ReadDefensesNode(buildNode, character);
        this.ReadCompanionNode(buildNode, character);
        await this.SendCharacterLoadingScreenStatusUpdate("Writing Background");
        if (!character.BackgroundStory.EqualsOriginalContent(inputNode["backstory"].GetInnerText()))
            character.BackgroundStory.Content = inputNode["backstory"].GetInnerText();
        XmlNode backgroundNode = (XmlNode)inputNode["background"];
        if (backgroundNode != null)
            this.ParseBackgroundInput(backgroundNode, character);
        this.ParseBackgroundCharacteristicsInput(inputNode, character);
        await this.SendCharacterLoadingScreenStatusUpdate("Scribing Spells");
        XmlNode node1 = (XmlNode)buildNode["magic"];
        if (node1 != null)
        {
            IEnumerable<XmlNode> xmlNodes = node1.NonCommentChildNodes().Where<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("spellcasting")));
            List<SpellcastingInformation> list = CharacterManager.Current.GetSpellcastingInformations().ToList<SpellcastingInformation>();
            foreach (XmlNode node2 in xmlNodes)
            {
                string name = node2.GetAttributeValue("name");
                string source = node2.GetAttributeValue("source");
                SpellcastingInformation information = list.FirstOrDefault<SpellcastingInformation>((Func<SpellcastingInformation, bool>)(x => x.Name.Equals(name) && x.ElementHeader.Id.Equals(source)));
                if (information != null && information.Prepare)
                {
                    XmlNode node3 = node2.NonCommentChildNodes().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("spells")));
                    if (node3 != null)
                    {
                        foreach (XmlNode node4 in node3.NonCommentChildNodes().Where<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("spell") && x.ContainsAttribute("prepared"))))
                            SpellcastingSectionContext.Current.SetPrepareSpell(information, node4.GetAttributeValue("id"));
                    }
                }
            }
            node1.NonCommentChildNodes().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("additional")));
        }
        CharacterManager.Current.ReprocessCharacter();
        await this.SendCharacterLoadingScreenStatusUpdate("preparing inventory");
        XmlNode xmlNode4 = (XmlNode)buildNode["equipment"];
        if (xmlNode4 != null)
        {
            int count = xmlNode4.ChildNodes.Count;
            int num = 0;
            foreach (XmlNode node5 in xmlNode4.ChildNodes.Cast<XmlNode>().Where<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("storage"))))
            {
                string attributeValue4 = node5.GetAttributeValue("name");
                if (num == 0)
                    character.Inventory.StoredItems1.Name = attributeValue4;
                if (num == 1)
                    character.Inventory.StoredItems2.Name = attributeValue4;
                ++num;
            }
            foreach (XmlNode node6 in xmlNode4.ChildNodes.Cast<XmlNode>().Where<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("item"))))
            {
                string attributeValue5 = node6.GetAttributeValue("id");
                ElementBase element = DataManager.Current.ElementsCollection.GetElement(attributeValue5);
                if (element == null && attributeValue5.Contains("ID_WOTC_ITEM"))
                    element = DataManager.Current.ElementsCollection.GetElement(attributeValue5.Replace("ID_WOTC_ITEM", "ID_WOTC_PHB_ITEM"));
                if (element == null && attributeValue5.Contains("ID_WOTC_WEAPON"))
                    element = DataManager.Current.ElementsCollection.GetElement(attributeValue5.Replace("ID_WOTC_WEAPON", "ID_WOTC_PHB_WEAPON"));
                if (element == null)
                {
                    Logger.Warning("unable to add " + node6.GetAttributeValue("name"));
                }
                else
                {
                    ElementBase adorner = (ElementBase)null;
                    XmlNode xmlNode5 = node6.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("items")));
                    if (xmlNode5 != null)
                    {
                        XmlNode node7 = xmlNode5.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("adorner")));
                        if (node7 != null)
                            adorner = DataManager.Current.ElementsCollection.GetElement(node7.GetAttributeValue("id"));
                    }
                    RefactoredEquipmentItem refactoredEquipmentItem = new RefactoredEquipmentItem(element as Item, adorner as Item);
                    if (node6.ContainsAttribute("identifier"))
                        refactoredEquipmentItem.SetIdentifier(node6.GetAttributeValue("identifier"));
                    if (node6.ContainsAttribute("amount"))
                        refactoredEquipmentItem.Amount = Convert.ToInt32(node6.GetAttributeValue("amount"));
                    XmlNode node8 = node6.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("equipped")));
                    if (node8 != null)
                    {
                        refactoredEquipmentItem.IsEquipped = Convert.ToBoolean(node8.InnerText);
                        if (node8.ContainsAttribute("location"))
                            refactoredEquipmentItem.EquippedLocation = node8.GetAttributeValue("location");
                    }
                    XmlNode xmlNode6 = node6.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("attunement")));
                    if (xmlNode6 != null)
                        refactoredEquipmentItem.IsAttuned = Convert.ToBoolean(xmlNode6.InnerText);
                    XmlNode node9 = node6.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("details")));
                    if (node9 != null)
                    {
                        if (node9.ContainsAttribute("card"))
                            refactoredEquipmentItem.ShowCard = Convert.ToBoolean(node9.GetAttributeValue("card"));
                        refactoredEquipmentItem.AlternativeName = node9["name"].GetInnerText();
                        refactoredEquipmentItem.Notes = node9["notes"].GetInnerText();
                    }
                    if (node6.ContainsAttribute("aquired"))
                    {
                        refactoredEquipmentItem.AquisitionParent = new ElementHeader(node6.GetAttributeValue("aquired"), "", "", "");
                        refactoredEquipmentItem.HasAquisitionParent = true;
                    }
                    if (node6.ContainsAttribute("hidden"))
                        refactoredEquipmentItem.IncludeInEquipmentPageInventory = !node6.GetAttributeAsBoolean("hidden");
                    refactoredEquipmentItem.IncludeInEquipmentPageDescriptionSidebar = node6.ContainsAttribute("sidebar") && node6.GetAttributeAsBoolean("sidebar");
                    XmlNode xmlNode7 = node6.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("storage")));
                    if (xmlNode7 != null)
                    {
                        InventoryStorage storage = character.Inventory.GetStorage(xmlNode7.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("location"))).GetInnerText());
                        refactoredEquipmentItem.Store(storage);
                    }
                    character.Inventory.Items.Add(refactoredEquipmentItem);
                    ++num;
                }
            }
            if (count != num)
                Logger.Warning("======= NOT ALL EQUIPMENT LOADED");
        }
        XmlNode attacksNode = (XmlNode)inputNode["attacks"];
        if (attacksNode != null)
            this.ParseAttacksSection(attacksNode, character);
        int index = 0;
        foreach (XmlNode childNode in elementsNode.ChildNodes)
        {
            string attributeValue6 = childNode.GetAttributeValue("type");
            if (attributeValue6 == "Weapon" || attributeValue6 == "Armor" || attributeValue6 == "Item" || attributeValue6 == "Magic Item")
            {
                string id = childNode.GetAttributeValue("id");
                List<RefactoredEquipmentItem> items = character.Inventory.Items.Where<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>)(x => x.Item.Id.Equals(id))).ToList<RefactoredEquipmentItem>();
                if (items.Count > 0)
                {
                    if (items.Count == 1 && index > 0)
                        index = 0;
                    Item element = items[index].Item;
                    await this.ReadChildElements(childNode, (ElementBase)element);
                    ++index;
                    if (items.Count <= index)
                        index = 0;
                }
                else
                {
                    ElementBase element = DataManager.Current.ElementsCollection.GetElement(id);
                    await this.ReadChildElements(childNode, element);
                    index = 0;
                }
                items = (List<RefactoredEquipmentItem>)null;
            }
        }
        ++currentProgress;
        await this.SendCharacterLoadingScreenProgressUpdate(currentProgress.IsPercetageOf(progressMax));
        await this.SendCharacterLoadingScreenStatusUpdate("performing validation");
        int elementSaveCount = Convert.ToInt32(((XmlNode)buildNode["sum"] ?? throw new NullReferenceException("sumNode not found on " + this._filepath)).GetAttributeValue("element-count"));
        int count1 = CharacterManager.Current.GetElements().Count;
        if (elementSaveCount != count1)
        {
            int difference = elementSaveCount - count1;
            Logger.Warning($"the sum of the saved elements ({elementSaveCount}) differs from the sum that is loaded ({count1})");
            bool validCount = false;
            for (int count = 0; count < 10; ++count)
            {
                await Task.Delay(250);
                if (elementSaveCount == CharacterManager.Current.GetElements().Count)
                {
                    validCount = true;
                    break;
                }
                Logger.Info("waiting for trailing elements to be registered ");
            }
            if (validCount)
            {
                sw.Stop();
                Logger.Warning($"{character} loaded in {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                sw.Stop();
                Logger.Warning($"{character} loaded in {sw.ElapsedMilliseconds}ms without all elements ({difference})");
            }
            if (difference > 0)
            {
                await this.SendCharacterLoadingScreenProgressUpdate(100);
                await this.SendCharacterLoadingScreenStatusUpdate("E10A", false);
                return new CharacterFile.LoadResult(false, $"character not fully prepared, {difference} item{(difference > 1 ? (object)"(s)" : (object)"")} could not be set");
            }
            await this.SendCharacterLoadingScreenProgressUpdate(100);
            await this.SendCharacterLoadingScreenStatusUpdate("E10B");
            return new CharacterFile.LoadResult(true);
        }
        await this.SendCharacterLoadingScreenProgressUpdate(100);
        await this.SendCharacterLoadingScreenStatusUpdate("E10B");
        sw.Stop();
        Logger.Warning($"{character} loaded in {sw.ElapsedMilliseconds}ms");
        return new CharacterFile.LoadResult(true, "E10B");
    }

    private Task SendCharacterLoadingScreenStatusUpdate(string message, bool success = true)
    {
        ApplicationContext.Current.EventAggregator.Send<CharacterLoadingSliderStatusUpdateEvent>(new CharacterLoadingSliderStatusUpdateEvent(message, success));
        return Task.CompletedTask;
    }

    private Task SendCharacterLoadingScreenProgressUpdate(int progress)
    {
        ApplicationContext.Current.EventAggregator.Send<CharacterLoadingSliderProgressEvent>(new CharacterLoadingSliderProgressEvent(progress));
        return Task.CompletedTask;
    }

    private void LoadSpellOverride(ObservableSpell spellslot, XmlNode node)
    {
        spellslot.Name = node.GetInnerText();
        spellslot.IsPrepared = Convert.ToBoolean(node.GetAttributeValue("prepared"));
    }

    private XmlNode CreateDisplayPropertiesNode(Character character)
    {
        XmlNode node1 = this._document.CreateNode(XmlNodeType.Element, "display-properties", (string)null);
        Dictionary<string, string> attributesDictionary = new Dictionary<string, string>()
    {
      {
        "favorite",
        this.IsFavorite.ToString().ToLowerInvariant()
      }
    };
        this.AppendAttributes(node1, attributesDictionary);
        XmlNode node2 = this._document.CreateNode(XmlNodeType.Element, "name", (string)null);
        node2.InnerText = character.Name;
        XmlNode node3 = this._document.CreateNode(XmlNodeType.Element, "race", (string)null);
        node3.InnerText = character.Race;
        XmlNode node4 = this._document.CreateNode(XmlNodeType.Element, "class", (string)null);
        node4.InnerText = character.Class;
        XmlNode node5 = this._document.CreateNode(XmlNodeType.Element, "archetype", (string)null);
        node5.InnerText = character.Archetype;
        XmlNode node6 = this._document.CreateNode(XmlNodeType.Element, "background", (string)null);
        node6.InnerText = character.Background;
        XmlNode node7 = this._document.CreateNode(XmlNodeType.Element, "level", (string)null);
        node7.InnerText = character.Level.ToString();
        XmlNode node8 = this._document.CreateNode(XmlNodeType.Element, "portrait", (string)null);
        node8.AppendChild((XmlNode)this._document.CreateElement("companion")).InnerText = character.Companion.Portrait.ToString();
        node8.AppendChild((XmlNode)this._document.CreateElement("local")).InnerText = character.PortraitFilename;
        Path.GetFileName(character.PortraitFilename);
        if (System.IO.File.Exists(character.PortraitFilename))
        {
            XmlNode node9 = this._document.CreateNode(XmlNodeType.Element, "base64", (string)null);
            XmlCDataSection cdataSection = this._document.CreateCDataSection(Convert.ToBase64String(System.IO.File.ReadAllBytes(character.PortraitFilename)));
            node9.AppendChild((XmlNode)cdataSection);
            node8.AppendChild(node9);
        }
        node1.AppendChild(node2);
        node1.AppendChild(node3);
        node1.AppendChild(node4);
        node1.AppendChild(node5);
        node1.AppendChild(node6);
        node1.AppendChild(node7);
        node1.AppendChild(node8);
        return node1;
    }

    private XmlNode CreateBuildNode(Character character)
    {
        XmlNode node1 = this._document.CreateNode(XmlNodeType.Element, "build", (string)null);
        node1.AppendChild(this.CreateInputNode(character));
        node1.AppendChild(this.CreateAppearanceNode(character));
        node1.AppendChild(this.CreateAbilitiesNode(character));
        XmlNode node2 = node1.AppendChild(this._document.CreateNode(XmlNodeType.Element, "elements", (string)null));
        this.AppendAttribute(node2, "level-count", CharacterManager.Current.Elements.Count<ElementBase>((Func<ElementBase, bool>)(x => x.Type == "Level")).ToString());
        this.AppendAttribute(node2, "registered-count", CharacterManager.Current.Elements.Count.ToString());
        foreach (ElementBase element in CharacterManager.Current.Elements.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type.Equals("Option"))))
        {
            XmlNode node3 = this._document.CreateNode(XmlNodeType.Element, "element", (string)null);
            Dictionary<string, string> attributesDictionary = new Dictionary<string, string>()
      {
        {
          "type",
          element.Type
        },
        {
          "name",
          element.Name
        },
        {
          "id",
          element.Id
        }
      };
            this.AppendAttributes(node3, attributesDictionary);
            node2.AppendChild(node3);
            this.CreateRuleNodes(element, node3);
        }
        foreach (ElementBase element in CharacterManager.Current.Elements.Where<ElementBase>((Func<ElementBase, bool>)(e => e.Type == "Level")))
        {
            XmlNode node4 = this._document.CreateNode(XmlNodeType.Element, "element", (string)null);
            Dictionary<string, string> attributesDictionary = new Dictionary<string, string>()
      {
        {
          "type",
          element.Type
        },
        {
          "name",
          element.Name
        },
        {
          "id",
          element.Id
        }
      };
            foreach (ClassProgressionManager progressionManager in (Collection<ClassProgressionManager>)CharacterManager.Current.ClassProgressionManagers)
            {
                LevelElement levelElement = element.AsElement<LevelElement>();
                int num = progressionManager.IsMainClass ? 1 : 0;
                if (progressionManager.LevelElements.Contains(element))
                {
                    if (progressionManager.IsMulticlass)
                    {
                        attributesDictionary.Add("multiclass", "true");
                        if (levelElement.Level == progressionManager.StartingLevel)
                        {
                            attributesDictionary.Add("starting", "true");
                            attributesDictionary.Add("rndhp", string.Join<int>(",", (IEnumerable<int>)progressionManager.GetRandomHitPointsArrayAsync()));
                        }
                        attributesDictionary.Add("class", progressionManager.ClassElement.Id);
                    }
                    else if (progressionManager.IsMainClass && levelElement.Level == progressionManager.StartingLevel)
                        attributesDictionary.Add("rndhp", string.Join<int>(",", (IEnumerable<int>)progressionManager.GetRandomHitPointsArrayAsync()));
                }
            }
            this.AppendAttributes(node4, attributesDictionary);
            node2.AppendChild(node4);
            this.CreateRuleNodes(element, node4);
        }
        foreach (ElementBase element in CharacterManager.Current.Elements.Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type.Equals("Weapon") || x.Type.Equals("Armor") || x.Type.Equals("Item") || x.Type.Equals("Magic Item"))))
        {
            if (element.ContainsSelectRules || element.ContainsGrantRules)
            {
                XmlNode node5 = this._document.CreateNode(XmlNodeType.Element, "element", (string)null);
                Dictionary<string, string> attributesDictionary = new Dictionary<string, string>()
        {
          {
            "type",
            element.Type
          },
          {
            "name",
            element.Name
          },
          {
            "id",
            element.Id
          }
        };
                node5.AppendAttributes(attributesDictionary);
                node2.AppendChild(node5);
                this.CreateRuleNodes(element, node5);
            }
        }
        this.WriteDefensesNode(node1, character);
        this.WriteCompanionNode(node1, character);
        node1.AppendChild(this.CreateEquipmentNode());
        node1.AppendChild(this.CreateSumNode());
        node1.AppendChild(this.CreateMagicNode());
        return node1;
    }

    private XmlNode CreateRestrictedSourcesNode()
    {
        XmlNode node = this._document.CreateNode(XmlNodeType.Element, "sources", (string)null);
        XmlNode parentNode = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "restricted", (string)null));
        foreach (SourceItem restrictedSource in (Collection<SourceItem>)CharacterManager.Current.SourcesManager.RestrictedSources)
            parentNode.AppendChild("source", restrictedSource.Source.Name).AppendAttribute("id", restrictedSource.Source.Id);
        foreach (string restrictedElementId in CharacterManager.Current.SourcesManager.GetRestrictedElementIds())
            parentNode.AppendChild("element", restrictedElementId);
        return node;
    }

    private void ReadRestrictedSourcesNodes(XmlNode sourcesNode)
    {
        XmlNode xmlNode = (XmlNode)sourcesNode["restricted"];
        if (xmlNode == null)
            return;
        List<string> sources = new List<string>();
        List<string> stringList = new List<string>();
        foreach (XmlNode node in xmlNode.ChildNodes.Cast<XmlNode>())
        {
            if (node.NodeType == XmlNodeType.Element)
            {
                switch (node.Name)
                {
                    case "source":
                        sources.Add(node.GetAttributeValue("id"));
                        continue;
                    case "element":
                        stringList.Add(node.GetInnerText());
                        continue;
                    default:
                        continue;
                }
            }
        }
        CharacterManager.Current.SourcesManager.Load((IEnumerable<string>)sources);
        CharacterManager.Current.SourcesManager.ApplyRestrictions();
    }

    private XmlNode CreateInputNode(Character character)
    {
        XmlNode node = this._document.CreateNode(XmlNodeType.Element, "input", (string)null);
        XmlNode xmlNode1 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "name", (string)null));
        XmlNode xmlNode2 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "gender", (string)null));
        XmlNode xmlNode3 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "player-name", (string)null));
        XmlNode xmlNode4 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "experience", (string)null));
        xmlNode1.InnerText = character.Name;
        xmlNode2.InnerText = character.Gender;
        xmlNode3.InnerText = character.PlayerName;
        string str = character.Experience.ToString();
        xmlNode4.InnerText = str;
        XmlNode parentNode1 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "attacks", (string)null));
        parentNode1.AppendChild("description", character.AttacksSection.AttacksAndSpellcasting, true);
        foreach (AttackSectionItem attackSectionItem in (Collection<AttackSectionItem>)character.AttacksSection.Items)
        {
            XmlNode parentNode2 = parentNode1.AppendChild(this._document.CreateNode(XmlNodeType.Element, "attack", (string)null));
            parentNode2.AppendAttribute("identifier", attackSectionItem.EquipmentItem?.Identifier ?? "");
            parentNode2.AppendAttribute("name", attackSectionItem.Name.Content);
            parentNode2.AppendAttribute("range", attackSectionItem.Range.Content);
            parentNode2.AppendAttribute("attack", attackSectionItem.Attack.Content);
            parentNode2.AppendAttribute("damage", attackSectionItem.Damage.Content);
            parentNode2.AppendAttribute("displayed", attackSectionItem.IsDisplayed ? "true" : "false");
            if (attackSectionItem.LinkedAbility != null)
                parentNode2.AppendAttribute("ability", attackSectionItem.LinkedAbility?.Name);
            parentNode2.AppendChild("description", attackSectionItem.Description.Content, true);
        }
        node.AppendChild("backstory", character.BackgroundStory.Content, true);
        node.AppendChild("background-trinket", character.Trinket.Content);
        node.AppendChild("background-traits", character.FillableBackgroundCharacteristics.Traits.Content);
        node.AppendChild("background-ideals", character.FillableBackgroundCharacteristics.Ideals.Content);
        node.AppendChild("background-bonds", character.FillableBackgroundCharacteristics.Bonds.Content);
        node.AppendChild("background-flaws", character.FillableBackgroundCharacteristics.Flaws.Content);
        XmlNode parentNode3 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "background", (string)null)).AppendChild(this._document.CreateNode(XmlNodeType.Element, "feature", (string)null));
        parentNode3.AppendAttribute("name", character.BackgroundFeatureName.Content);
        parentNode3.AppendChild("description", character.BackgroundFeatureDescription.Content, true);
        XmlNode parentNode4 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "organization", (string)null));
        parentNode4.AppendChild("name", character.OrganisationName);
        parentNode4.AppendChild("symbol", character.OrganisationSymbol);
        parentNode4.AppendChild("allies", character.Allies, true);
        node.AppendChild("additional-features", character.AdditionalFeatures, true);
        XmlNode parentNode5 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "currency", (string)null));
        parentNode5.AppendChild("copper", character.Inventory.Coins.Copper.ToString());
        long num = character.Inventory.Coins.Silver;
        parentNode5.AppendChild("silver", num.ToString());
        num = character.Inventory.Coins.Electrum;
        parentNode5.AppendChild("electrum", num.ToString());
        num = character.Inventory.Coins.Gold;
        parentNode5.AppendChild("gold", num.ToString());
        num = character.Inventory.Coins.Platinum;
        parentNode5.AppendChild("platinum", num.ToString());
        parentNode5.AppendChild("equipment", character.Inventory.Equipment, true);
        parentNode5.AppendChild("treasure", character.Inventory.Treasure, true);
        this.WriteCharacterNotesNode(node, character);
        this.WriteQuestItemsNode(node, character);
        return node;
    }

    private XmlNode CreateAppearanceNode(Character character)
    {
        XmlNode node = this._document.CreateNode(XmlNodeType.Element, "appearance", (string)null);
        node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "portrait", (string)null)).InnerText = character.PortraitFilename;
        XmlNode xmlNode1 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "age", (string)null));
        XmlNode xmlNode2 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "height", (string)null));
        XmlNode xmlNode3 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "weight", (string)null));
        XmlNode xmlNode4 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "eyes", (string)null));
        XmlNode xmlNode5 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "skin", (string)null));
        XmlNode xmlNode6 = node.AppendChild(this._document.CreateNode(XmlNodeType.Element, "hair", (string)null));
        xmlNode1.InnerText = character.AgeField.Content;
        xmlNode2.InnerText = character.HeightField.Content;
        xmlNode3.InnerText = character.WeightField.Content;
        xmlNode4.InnerText = character.Eyes;
        xmlNode5.InnerText = character.Skin;
        string hair = character.Hair;
        xmlNode6.InnerText = hair;
        return node;
    }

    private XmlNode CreateAbilitiesNode(Character character)
    {
        XmlNode node1 = this._document.CreateNode(XmlNodeType.Element, "abilities", (string)null);
        XmlNode node2 = this._document.CreateNode(XmlNodeType.Element, character.Abilities.Strength.Name.ToLowerInvariant(), (string)null);
        node2.InnerText = character.Abilities.Strength.BaseScore.ToString();
        XmlNode node3 = this._document.CreateNode(XmlNodeType.Element, character.Abilities.Dexterity.Name.ToLowerInvariant(), (string)null);
        node3.InnerText = character.Abilities.Dexterity.BaseScore.ToString();
        XmlNode node4 = this._document.CreateNode(XmlNodeType.Element, character.Abilities.Constitution.Name.ToLowerInvariant(), (string)null);
        node4.InnerText = character.Abilities.Constitution.BaseScore.ToString();
        XmlNode node5 = this._document.CreateNode(XmlNodeType.Element, character.Abilities.Intelligence.Name.ToLowerInvariant(), (string)null);
        node5.InnerText = character.Abilities.Intelligence.BaseScore.ToString();
        XmlNode node6 = this._document.CreateNode(XmlNodeType.Element, character.Abilities.Wisdom.Name.ToLowerInvariant(), (string)null);
        node6.InnerText = character.Abilities.Wisdom.BaseScore.ToString();
        XmlNode node7 = this._document.CreateNode(XmlNodeType.Element, character.Abilities.Charisma.Name.ToLowerInvariant(), (string)null);
        node7.InnerText = character.Abilities.Charisma.BaseScore.ToString();
        node1.AppendChild(node2);
        node1.AppendChild(node3);
        node1.AppendChild(node4);
        node1.AppendChild(node5);
        node1.AppendChild(node6);
        node1.AppendChild(node7);
        this.AppendAttribute(node1, "available-points", character.Abilities.AvailablePoints.ToString());
        return node1;
    }

    [Obsolete]
    private XmlNode CreateSpellsNode(Character character)
    {
        XmlNode node1 = this._document.CreateNode(XmlNodeType.Element, "spells", (string)null);
        this.AppendAttribute(node1, "class", character.SpellcastingCollection.SpellcastingClass);
        this.AppendAttribute(node1, "ability", character.SpellcastingCollection.SpellcastingAbility);
        this.AppendAttribute(node1, "dc", character.SpellcastingCollection.SpellcastingDifficultyClass);
        this.AppendAttribute(node1, "attack", character.SpellcastingCollection.SpellcastingAttackBonus);
        XmlNode node2 = this._document.CreateNode(XmlNodeType.Element, "cantrips", (string)null);
        XmlNode node3 = this._document.CreateNode(XmlNodeType.Element, "spells-1st", (string)null);
        XmlNode node4 = this._document.CreateNode(XmlNodeType.Element, "spells-2nd", (string)null);
        XmlNode node5 = this._document.CreateNode(XmlNodeType.Element, "spells-3rd", (string)null);
        XmlNode node6 = this._document.CreateNode(XmlNodeType.Element, "spells-4th", (string)null);
        XmlNode node7 = this._document.CreateNode(XmlNodeType.Element, "spells-5th", (string)null);
        XmlNode node8 = this._document.CreateNode(XmlNodeType.Element, "spells-6th", (string)null);
        XmlNode node9 = this._document.CreateNode(XmlNodeType.Element, "spells-7th", (string)null);
        XmlNode node10 = this._document.CreateNode(XmlNodeType.Element, "spells-8th", (string)null);
        XmlNode node11 = this._document.CreateNode(XmlNodeType.Element, "spells-9th", (string)null);
        node2.AppendChild(this.CreateCantripNode(character.SpellcastingCollection.CantripSlot1));
        node2.AppendChild(this.CreateCantripNode(character.SpellcastingCollection.CantripSlot2));
        node2.AppendChild(this.CreateCantripNode(character.SpellcastingCollection.CantripSlot3));
        node2.AppendChild(this.CreateCantripNode(character.SpellcastingCollection.CantripSlot4));
        node2.AppendChild(this.CreateCantripNode(character.SpellcastingCollection.CantripSlot5));
        node2.AppendChild(this.CreateCantripNode(character.SpellcastingCollection.CantripSlot6));
        node2.AppendChild(this.CreateCantripNode(character.SpellcastingCollection.CantripSlot7));
        node2.AppendChild(this.CreateCantripNode(character.SpellcastingCollection.CantripSlot8));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot1));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot2));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot3));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot4));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot5));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot6));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot7));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot8));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot9));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot10));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot11));
        node3.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell1Slot12));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot1));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot2));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot3));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot4));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot5));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot6));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot7));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot8));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot9));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot10));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot11));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot12));
        node4.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell2Slot13));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot1));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot2));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot3));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot4));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot5));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot6));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot7));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot8));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot9));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot10));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot11));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot12));
        node5.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell3Slot13));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot1));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot2));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot3));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot4));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot5));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot6));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot7));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot8));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot9));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot10));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot11));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot12));
        node6.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell4Slot13));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot1));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot2));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot3));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot4));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot5));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot6));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot7));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot8));
        node7.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell5Slot9));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot1));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot2));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot3));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot4));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot5));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot6));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot7));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot8));
        node8.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell6Slot9));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot1));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot2));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot3));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot4));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot5));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot6));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot7));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot8));
        node9.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell7Slot9));
        node10.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell8Slot1));
        node10.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell8Slot2));
        node10.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell8Slot3));
        node10.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell8Slot4));
        node10.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell8Slot5));
        node10.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell8Slot6));
        node10.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell8Slot7));
        node11.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell9Slot1));
        node11.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell9Slot2));
        node11.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell9Slot3));
        node11.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell9Slot4));
        node11.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell9Slot5));
        node11.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell9Slot6));
        node11.AppendChild(this.CreateSpellNode(character.SpellcastingCollection.Spell9Slot7));
        this.AppendAttribute(node3, "slots", "0");
        this.AppendAttribute(node4, "slots", "0");
        this.AppendAttribute(node5, "slots", "0");
        this.AppendAttribute(node6, "slots", "0");
        this.AppendAttribute(node7, "slots", "0");
        this.AppendAttribute(node8, "slots", "0");
        this.AppendAttribute(node9, "slots", "0");
        this.AppendAttribute(node10, "slots", "0");
        this.AppendAttribute(node11, "slots", "0");
        node1.AppendChild(node2);
        node1.AppendChild(node3);
        node1.AppendChild(node4);
        node1.AppendChild(node5);
        node1.AppendChild(node6);
        node1.AppendChild(node7);
        node1.AppendChild(node8);
        node1.AppendChild(node9);
        node1.AppendChild(node10);
        node1.AppendChild(node11);
        return node1;
    }

    [Obsolete]
    private XmlNode CreateCantripNode(string cantrip)
    {
        XmlNode node = this._document.CreateNode(XmlNodeType.Element, nameof(cantrip), (string)null);
        node.InnerText = string.IsNullOrWhiteSpace(cantrip) ? " " : cantrip;
        return node;
    }

    [Obsolete]
    private XmlNode CreateSpellNode(ObservableSpell spell)
    {
        XmlNode node = this._document.CreateNode(XmlNodeType.Element, nameof(spell), (string)null);
        this.AppendAttribute(node, "prepared", spell.IsPrepared ? "true" : "false");
        node.InnerText = string.IsNullOrWhiteSpace(spell.Name) ? " " : spell.Name;
        return node;
    }

    private void CreateRuleNodes(ElementBase element, XmlNode parentNode)
    {
        foreach (SelectRule selectRule in element.GetSelectRules())
        {
            ProgressionManager progressManager = CharacterManager.Current.GetProgressManager(selectRule);
            if (progressManager == null)
                Logger.Warning($"not creating rules for selectionrule: {selectRule}, not registed with any manager req:({selectRule.Attributes.Requirements})");
            else if (selectRule.Attributes.MeetsLevelRequirement(progressManager.ProgressionLevel))
            {
                for (int index = 1; index <= selectRule.Attributes.Number; ++index)
                {
                    XmlNode node = this._document.CreateNode(XmlNodeType.Element, nameof(element), (string)null);
                    parentNode.AppendChild(node);
                    Dictionary<string, string> dictionary1 = new Dictionary<string, string>();
                    dictionary1.Add("type", selectRule.Attributes.Type);
                    dictionary1.Add("name", selectRule.Attributes.Name);
                    int num = selectRule.Attributes.RequiredLevel;
                    dictionary1.Add("requiredLevel", num.ToString());
                    Dictionary<string, string> attributesDictionary = dictionary1;
                    if (selectRule.Attributes.MultipleNumberCount)
                        attributesDictionary.Add("number", index.ToString());
                    attributesDictionary.Add("checksum", CharacterFileVerification.GenerateCrC(selectRule, index));
                    if (selectRule.Attributes.IsList)
                    {
                        attributesDictionary.Add("isList", "true");
                        SelectionRuleListItem registeredElement = SelectionRuleExpanderContext.Current.GetRegisteredElement(selectRule, index) as SelectionRuleListItem;
                        Dictionary<string, string> dictionary2 = attributesDictionary;
                        string str;
                        if (registeredElement == null)
                        {
                            str = (string)null;
                        }
                        else
                        {
                            num = registeredElement.ID;
                            str = num.ToString();
                        }
                        dictionary2.Add("registered", str);
                        if (registeredElement != null)
                            node.InnerText = registeredElement.Text;
                        this.AppendAttributes(node, attributesDictionary);
                    }
                    else
                    {
                        try
                        {
                            int retrainLevel = SelectionRuleExpanderContext.Current.GetRetrainLevel(selectRule, index);
                            if (retrainLevel > 0)
                                attributesDictionary.Add("replaceLevel", retrainLevel.ToString());
                            ElementBase registeredElement = SelectionRuleExpanderContext.Current.GetRegisteredElement(selectRule, index) as ElementBase;
                            attributesDictionary.Add("registered", registeredElement?.Id);
                            this.AppendAttributes(node, attributesDictionary);
                            if (registeredElement != null)
                                this.CreateRuleNodes(registeredElement, node);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"error trying to get registered element for rule: {selectRule}");
                            Logger.Exception(ex, nameof(CreateRuleNodes));
                            throw;
                        }
                    }
                }
            }
        }
        try
        {
            foreach (ElementBase ruleElement in (Collection<ElementBase>)element.RuleElements)
            {
                XmlNode node = this._document.CreateNode(XmlNodeType.Element, nameof(element), (string)null);
                parentNode.AppendChild(node);
                Dictionary<string, string> attributesDictionary = new Dictionary<string, string>()
        {
          {
            "type",
            ruleElement.Type
          },
          {
            "name",
            ruleElement.Name
          },
          {
            "id",
            ruleElement.Id
          }
        };
                this.AppendAttributes(node, attributesDictionary);
                this.CreateRuleNodes(ruleElement, node);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"error trying to get registered element for: {element}");
            Logger.Exception(ex, nameof(CreateRuleNodes));
        }
    }

    private XmlNode CreateEquipmentNode()
    {
        XmlNode node1 = this._document.CreateNode(XmlNodeType.Element, "equipment", (string)null);
        if (CharacterManager.Current.Character.Inventory.StoredItems1.IsInUse())
        {
            XmlNode node2 = this._document.CreateNode(XmlNodeType.Element, "storage", (string)null);
            node2.AppendAttribute("name", CharacterManager.Current.Character.Inventory.StoredItems1.Name);
            node1.AppendChild(node2);
        }
        if (CharacterManager.Current.Character.Inventory.StoredItems2.IsInUse())
        {
            XmlNode node3 = this._document.CreateNode(XmlNodeType.Element, "storage", (string)null);
            node3.AppendAttribute("name", CharacterManager.Current.Character.Inventory.StoredItems2.Name);
            node1.AppendChild(node3);
        }
        foreach (RefactoredEquipmentItem refactoredEquipmentItem in (Collection<RefactoredEquipmentItem>)CharacterManager.Current.Character.Inventory.Items)
        {
            XmlNode node4 = this._document.CreateNode(XmlNodeType.Element, "item", (string)null);
            Dictionary<string, string> attributesDictionary = new Dictionary<string, string>()
      {
        {
          "identifier",
          refactoredEquipmentItem.Identifier
        },
        {
          "name",
          refactoredEquipmentItem.Name
        },
        {
          "id",
          refactoredEquipmentItem.Item.Id
        }
      };
            this.AppendAttributes(node4, attributesDictionary);
            if (refactoredEquipmentItem.Amount > 1)
                this.AppendAttribute(node4, "amount", refactoredEquipmentItem.Amount.ToString());
            if (refactoredEquipmentItem.HasAquisitionParent)
                this.AppendAttribute(node4, "aquired", refactoredEquipmentItem.AquisitionParent.Name);
            if (!refactoredEquipmentItem.IncludeInEquipmentPageInventory)
                this.AppendAttribute(node4, "hidden", "true");
            if (refactoredEquipmentItem.IncludeInEquipmentPageDescriptionSidebar)
                this.AppendAttribute(node4, "sidebar", "true");
            if (refactoredEquipmentItem.IsEquipped)
            {
                XmlNode node5 = this._document.CreateNode(XmlNodeType.Element, "equipped", (string)null);
                node5.InnerText = refactoredEquipmentItem.IsEquipped.ToString().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(refactoredEquipmentItem.EquippedLocation) && refactoredEquipmentItem.EquippedLocation.Contains("Versatile"))
                    this.AppendAttribute(node5, "versatile", "true");
                if (!string.IsNullOrWhiteSpace(refactoredEquipmentItem.EquippedLocation))
                    this.AppendAttribute(node5, "location", refactoredEquipmentItem.EquippedLocation);
                node4.AppendChild(node5);
            }
            if (refactoredEquipmentItem.IsAttuned)
            {
                XmlNode node6 = this._document.CreateNode(XmlNodeType.Element, "attunement", (string)null);
                node6.InnerText = refactoredEquipmentItem.IsAttuned.ToString().ToLowerInvariant();
                node4.AppendChild(node6);
            }
            XmlNode node7 = this._document.CreateNode(XmlNodeType.Element, "items", (string)null);
            if (refactoredEquipmentItem.IsAdorned)
            {
                XmlNode parentNode = node7.AppendChild(this._document.CreateNode(XmlNodeType.Element, "adorner", (string)null));
                parentNode.AppendAttribute("name", refactoredEquipmentItem.AdornerItem.Name);
                parentNode.AppendAttribute("id", refactoredEquipmentItem.AdornerItem.Id);
                node4.AppendChild(node7);
            }
            XmlNode parentNode1 = node4.AppendChild(this._document.CreateNode(XmlNodeType.Element, "details", (string)null));
            parentNode1.AppendAttribute("card", refactoredEquipmentItem.ShowCard ? "true" : "false");
            XmlNode xmlNode1 = parentNode1.AppendChild(this._document.CreateNode(XmlNodeType.Element, "name", (string)null));
            XmlNode xmlNode2 = parentNode1.AppendChild(this._document.CreateNode(XmlNodeType.Element, "notes", (string)null));
            xmlNode1.InnerText = refactoredEquipmentItem.AlternativeName ?? "";
            string str = refactoredEquipmentItem.Notes ?? "";
            xmlNode2.InnerText = str;
            if (refactoredEquipmentItem.IsStored)
                node4.AppendChild(this._document.CreateNode(XmlNodeType.Element, "storage", (string)null)).AppendChild(this._document.CreateNode(XmlNodeType.Element, "location", (string)null)).InnerText = refactoredEquipmentItem.Storage.Name;
            node1.AppendChild(node4);
        }
        return node1;
    }

    private XmlNode CreateSumNode()
    {
        XmlNode node1 = this._document.CreateNode(XmlNodeType.Element, "sum", (string)null);
        List<ElementBase> list = CharacterManager.Current.GetElements().ToList<ElementBase>();
        this.AppendAttribute(node1, "element-count", list.Count.ToString());
        foreach (ElementBase elementBase in list)
        {
            XmlNode node2 = this._document.CreateNode(XmlNodeType.Element, "element", (string)null);
            Dictionary<string, string> attributesDictionary = new Dictionary<string, string>()
      {
        {
          "type",
          elementBase.Type
        },
        {
          "id",
          elementBase.Id
        }
      };
            this.AppendAttributes(node2, attributesDictionary);
            node1.AppendChild(node2);
        }
        return node1;
    }

    /// <summary>
    /// Builds a <see cref="SelectionElement"/> list for spells that belong to a given
    /// spellcasting class when no WPF SpellcasterSelectionControlViewModel is available.
    /// Matches spells from <paramref name="allSpells"/> to the class by grant-rule setter
    /// ("spellcasting" == className) or select-rule spellcasting name attribute.
    /// Prepared state is read from the ISpellcastingSectionHandler (MauiSpellcastingSectionHandler).
    /// </summary>
    private static IEnumerable<SelectionElement> BuildMauiKnownSpells(
        IEnumerable<ElementBase> allSpells,
        SpellcastingInformation info,
        ISpellcastingSectionHandler handler)
    {
        var spells = allSpells.Cast<Spell>().ToList();

        // Spells granted via a grant rule whose "spellcasting" setter matches this class.
        var granted = spells.Where(s =>
            s.Aquisition.WasGranted &&
            s.Aquisition.GrantRule.Setters.ContainsSetter("spellcasting") &&
            s.Aquisition.GrantRule.Setters.GetSetter("spellcasting").Value
                .Equals(info.Name, StringComparison.OrdinalIgnoreCase)).ToList();

        // Spells selected via a select rule whose SpellcastingName matches this class.
        var selected = spells.Where(s =>
            s.Aquisition.WasSelected &&
            s.Aquisition.SelectRule.Attributes.ContainsSpellcastingName() &&
            s.Aquisition.SelectRule.Attributes.SpellcastingName
                .Equals(info.Name, StringComparison.OrdinalIgnoreCase)).ToList();

        // Prepared IDs (non-always-prepared spells the user marked). Supplied via interface
        // so no dependency on the concrete MAUI handler type.
        IReadOnlyCollection<string> preparedIds = handler?.GetPreparedIds(info.Name)
            ?? Array.Empty<string>();

        // Warn about registered spells whose acquisition type is neither granted nor selected —
        // these are silently skipped and would be invisible in any serialize/deserialize cycle.
        foreach (var s in spells.Where(s => !s.Aquisition.WasGranted && !s.Aquisition.WasSelected))
            Logger.Warning("BuildMauiKnownSpells: spell '{0}' has unknown acquisition type for class '{1}' — skipped", s.Id, info.Name);

        var emittedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in granted.Concat(selected).GroupBy(x => x.Id).Select(g => g.First()))
        {
            emittedIds.Add(s.Id);
            bool isChosen = s.Aquisition.WasGranted
                ? s.Aquisition.GrantRule.IsAlwaysPrepared()
                : s.Aquisition.SelectRule.IsAlwaysPrepared();
            if (!isChosen && info.Prepare)
                isChosen = preparedIds.Contains(s.Id);
            yield return new SelectionElement((ElementBase)s) { IsChosen = isChosen };
        }

        // For prepared casters (Artificer, Cleric, Druid, …) the user prepares spells from
        // the full class list — those spells are NOT registered on the character via grant/select
        // rules, so they are absent from the granted/selected lists above. Emit them here so
        // the serialised XML records <spell prepared="true"/> entries that survive a round-trip.
        if (info.Prepare)
        {
            foreach (var id in preparedIds)
            {
                if (emittedIds.Contains(id)) continue;
                var element = DataManager.Current.ElementsCollection.GetElement(id);
                if (element is Spell preparedSpell)
                    yield return new SelectionElement((ElementBase)preparedSpell) { IsChosen = true };
            }
        }
    }

    private XmlNode CreateMagicNode()
    {
        CharacterManager current1 = CharacterManager.Current;
        var current2 = SpellcastingSectionContext.Current;
        List<ElementBase> list = current1.GetElements().Where<ElementBase>((Func<ElementBase, bool>)(x => x.Type.Equals("Spell"))).ToList<ElementBase>();
        int count = list.Count;
        XmlElement element1 = this._document.CreateElement("magic");
        int num;
        if (current1.Status.HasMulticlass)
        {
            element1.AppendAttribute("multiclass", "true");
            StatisticValuesGroup group = current1.StatisticsCalculator.StatisticValues.GetGroup("multiclass:spellcasting:level", false);
            if (group != null)
            {
                XmlElement parentNode = element1;
                num = group.Sum();
                string str = num.ToString();
                parentNode.AppendAttribute("level", str);
            }
        }
        foreach (SpellcastingInformation spellcastingInformation in current1.GetSpellcastingInformations())
        {
            if (!spellcastingInformation.IsExtension)
            {
                SpellcasterSelectionControlViewModel viewModel = current2.GetSpellcasterSectionViewModel(spellcastingInformation.UniqueIdentifier);
                XmlNode parentNode1 = element1.AppendChild((XmlNode)this._document.CreateElement("spellcasting"));
                parentNode1.AppendAttribute("name", spellcastingInformation.Name);
                parentNode1.AppendAttribute("ability", spellcastingInformation.AbilityName);
                // When viewModel is null (non-WPF host), compute stats directly from StatisticsCalculator.
                var sv = current1.StatisticsCalculator.StatisticValues;
                num = viewModel?.InformationHeader.SpellAttackModifier ?? sv.GetValue(spellcastingInformation.GetSpellcasterSpellAttackStatisticName());
                parentNode1.AppendAttribute("attack", num.ToString());
                num = viewModel?.InformationHeader.SpellSaveDc ?? sv.GetValue(spellcastingInformation.GetSpellcasterSpellSaveStatisticName());
                parentNode1.AppendAttribute("dc", num.ToString());
                parentNode1.AppendAttribute("source", spellcastingInformation.ElementHeader.Id);
                XmlNode parentNode2 = parentNode1.AppendChild((XmlNode)this._document.CreateElement("slots"));
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                num = viewModel?.InformationHeader.Slot1 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(1));
                dictionary.Add("s1", num.ToString());
                num = viewModel?.InformationHeader.Slot2 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(2));
                dictionary.Add("s2", num.ToString());
                num = viewModel?.InformationHeader.Slot3 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(3));
                dictionary.Add("s3", num.ToString());
                num = viewModel?.InformationHeader.Slot4 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(4));
                dictionary.Add("s4", num.ToString());
                num = viewModel?.InformationHeader.Slot5 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(5));
                dictionary.Add("s5", num.ToString());
                num = viewModel?.InformationHeader.Slot6 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(6));
                dictionary.Add("s6", num.ToString());
                num = viewModel?.InformationHeader.Slot7 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(7));
                dictionary.Add("s7", num.ToString());
                num = viewModel?.InformationHeader.Slot8 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(8));
                dictionary.Add("s8", num.ToString());
                num = viewModel?.InformationHeader.Slot9 ?? sv.GetValue(spellcastingInformation.GetSlotStatisticName(9));
                dictionary.Add("s9", num.ToString());
                Dictionary<string, string> attributesDictionary = dictionary;
                parentNode2.AppendAttributes(attributesDictionary);
                XmlNode xmlNode1 = parentNode1.AppendChild((XmlNode)this._document.CreateElement("cantrips"));
                XmlNode xmlNode2 = parentNode1.AppendChild((XmlNode)this._document.CreateElement("spells"));
                IEnumerable<SelectionElement> knownSpells = viewModel != null
                    ? (IEnumerable<SelectionElement>)viewModel.KnownSpells
                    : BuildMauiKnownSpells(list, spellcastingInformation, current2);
                foreach (SelectionElement selectionElement in knownSpells
                    .OrderByDescending<SelectionElement, bool>((Func<SelectionElement, bool>)(x => x.IsChosen))
                    .ThenBy<SelectionElement, int>((Func<SelectionElement, int>)(x => x.Element.AsElement<Spell>().Level))
                    .ThenBy<SelectionElement, string>((Func<SelectionElement, string>)(x => x.Element.Name)))
                {
                    bool flag1 = list.Remove(selectionElement.Element);
                    Spell spell = selectionElement.Element.AsElement<Spell>();
                    XmlElement element2 = this._document.CreateElement("spell");
                    element2.AppendAttribute("name", spell.Name);
                    XmlElement parentNode3 = element2;
                    num = spell.Level;
                    string str = num.ToString();
                    parentNode3.AppendAttribute("level", str);
                    element2.AppendAttribute("id", spell.Id);
                    if (spell.Level == 0)
                    {
                        xmlNode1.AppendChild((XmlNode)element2);
                    }
                    else
                    {
                        bool isPrepareRequired = viewModel?.IsPrepareSpellsRequired ?? spellcastingInformation.Prepare;
                        if (isPrepareRequired)
                        {
                            bool flag2 = viewModel != null
                                ? ((IEnumerable<ElementBase>)viewModel.PreparedSpells).Contains<ElementBase>(selectionElement.Element)
                                : selectionElement.IsChosen;
                            if (!selectionElement.IsChosen)
                                selectionElement.IsChosen = flag2;
                            if (selectionElement.IsChosen)
                                element2.AppendAttribute("prepared", selectionElement.IsChosen ? "true" : "false");
                            if (flag1)
                                element2.AppendAttribute("always-prepared", "true");
                        }
                        if (flag1)
                            element2.AppendAttribute("known", "true");
                        xmlNode2.AppendChild((XmlNode)element2);
                    }
                }
            }
        }
        if (list.Any<ElementBase>())
        {
            XmlNode xmlNode = element1.AppendChild((XmlNode)this._document.CreateElement("additional"));
            foreach (Spell spell in list.Cast<Spell>())
            {
                XmlElement element3 = this._document.CreateElement("spell");
                element3.AppendAttribute("name", spell.Name);
                element3.AppendAttribute("level", spell.Level.ToString());
                element3.AppendAttribute("id", spell.Id);
                if (spell.Aquisition.WasGranted)
                    element3.AppendAttribute("source", spell.Aquisition.GrantRule.ElementHeader.Name);
                else if (spell.Aquisition.WasSelected)
                    element3.AppendAttribute("source", spell.Aquisition.SelectRule.ElementHeader.Name);
                xmlNode.AppendChild((XmlNode)element3);
            }
        }
        return (XmlNode)element1;
    }

    private XmlNode WriteInformationNode(XmlNode parentNode)
    {
        XmlNode xmlNode = parentNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "information", (string)null));
        xmlNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "group", (string)null)).InnerText = this.CollectionGroupName;
        xmlNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "generationOption", (string)null)).InnerText = $"{ApplicationContext.Current.Settings.AbilitiesGenerationOption}";
        return xmlNode;
    }

    private void ReadInformationNode(XmlNode parentNode)
    {
        XmlNode parentNode1 = parentNode.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("information")));
        if (parentNode1 == null)
        {
            this.CollectionGroupName = "Characters";
        }
        else
        {
            if (parentNode1.ContainsChildNode("group"))
                this.CollectionGroupName = parentNode1.GetChildNode("group").GetInnerText();
            if (!parentNode1.ContainsChildNode("generationOption"))
                return;
            int result;
            if (!int.TryParse(parentNode1.GetChildNode("generationOption").GetInnerText(), out result))
                return;
            try
            {
                ApplicationContext.Current.Settings.AbilitiesGenerationOption = result;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, nameof(ReadInformationNode));
                AnalyticsErrorHelper.Exception(ex, method: nameof(ReadInformationNode), line: 2444);
            }
        }
    }

    private XmlNode WriteDefensesNode(XmlNode parentNode, Character character)
    {
        XmlNode xmlNode = parentNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "defenses", (string)null));
        xmlNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "conditional", (string)null)).InnerText = character.ConditionalArmorClassField.Content;
        return xmlNode;
    }

    private void ReadDefensesNode(XmlNode parentNode, Character character)
    {
        XmlNode xmlNode = parentNode.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("defenses")));
        if (xmlNode == null)
            return;
        XmlElement node = xmlNode["conditional"];
        string innerText = node != null ? node.GetInnerText() : (string)null;
        character.ConditionalArmorClassField.SetIfNotEqualOriginalContent(innerText);
    }

    private XmlNode WriteCompanionNode(XmlNode parentNode, Character character)
    {
        Companion companion = character.Companion;
        XmlNode parentNode1 = parentNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "companion", (string)null));
        parentNode1.AppendAttribute("name", companion.CompanionName.Content);
        XmlNode parentNode2 = parentNode1.AppendChild(this._document.CreateNode(XmlNodeType.Element, "attributes", (string)null));
        foreach (AbilityItem abilityItem in companion.Abilities.GetCollection())
            parentNode2.AppendChild(abilityItem.Name.ToLowerInvariant(), abilityItem.FinalScore.ToString());
        XmlNode parentNode3 = parentNode1.AppendChild(this._document.CreateNode(XmlNodeType.Element, "saves", (string)null));
        foreach (SavingThrowItem savingThrowItem in companion.SavingThrows.GetCollection())
            parentNode3.AppendChild("save", savingThrowItem.FinalBonus.ToString()).AppendAttribute("ability", savingThrowItem.KeyAbility.Name.ToLowerInvariant());
        XmlNode parentNode4 = parentNode1.AppendChild(this._document.CreateNode(XmlNodeType.Element, "skills", (string)null));
        foreach (SkillItem skillItem in companion.Skills.GetCollection())
            parentNode4.AppendChild("skill", skillItem.FinalBonus.ToString()).AppendAttribute("name", skillItem.Name);
        XmlNode parentNode5 = parentNode1.AppendChild(this._document.CreateNode(XmlNodeType.Element, "portrait", (string)null));
        parentNode5.AppendAttribute("location", "local");
        parentNode5.InnerText = companion.Portrait.ToString();
        return parentNode1;
    }

    private void ReadCompanionNode(XmlNode parentNode, Character character)
    {
        XmlNode node1 = parentNode.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("companion")));
        if (node1 == null)
            return;
        Companion companion = character.Companion;
        if (!companion.CompanionName.EqualsOriginalContent(node1.GetAttributeValue("name")))
            companion.CompanionName.Content = node1.GetAttributeValue("name");
        XmlNode node2 = node1.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("portrait")));
        if (!node2.HasAttributes() || !node2.GetAttributeValue("location").Equals("local") || companion.Portrait.EqualsOriginalContent(node2.GetInnerText()))
            return;
        companion.Portrait.Content = node2.GetInnerText();
    }

    private XmlNode WriteCharacterNotesNode(XmlNode parentNode, Character character)
    {
        XmlNode xmlNode = parentNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "notes", (string)null));
        XmlNode parentNode1 = xmlNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "note", (string)null));
        parentNode1.AppendAttribute("column", "left");
        parentNode1.InnerText = character.Notes1;
        XmlNode parentNode2 = xmlNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "note", (string)null));
        parentNode2.AppendAttribute("column", "right");
        parentNode2.InnerText = character.Notes2;
        return xmlNode;
    }

    private void ReadCharacterNotesNode(XmlNode parentNode, Character character)
    {
        XmlNode xmlNode = parentNode.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("notes")));
        if (xmlNode == null)
            return;
        foreach (XmlNode node in xmlNode.ChildNodes.Cast<XmlNode>().Where<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("note"))))
        {
            if (node.ContainsAttribute("column"))
            {
                switch (node.GetAttributeValue("column"))
                {
                    case "left":
                        character.Notes1 = node.GetInnerText();
                        continue;
                    case "right":
                        character.Notes2 = node.GetInnerText();
                        continue;
                    default:
                        continue;
                }
            }
        }
    }

    private XmlNode WriteQuestItemsNode(XmlNode parentNode, Character character)
    {
        XmlNode xmlNode = parentNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, "quest", (string)null));
        xmlNode.InnerText = character.Inventory.QuestItems;
        return xmlNode;
    }

    private void ReadQuestItemsNode(XmlNode parentNode, Character character)
    {
        XmlNode node = parentNode.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("quest")));
        if (node == null)
            return;
        character.Inventory.QuestItems = node.GetInnerText();
    }

    private void ReadAppearanceNode(XmlNode appearanceNode, Character character)
    {
        character.PortraitFilename = appearanceNode["portrait"].GetInnerText();
        character.AgeField.Content = appearanceNode["age"].GetInnerText();
        character.HeightField.Content = appearanceNode["height"].GetInnerText();
        character.WeightField.Content = appearanceNode["weight"].GetInnerText();
        character.Eyes = appearanceNode["eyes"].GetInnerText();
        character.Skin = appearanceNode["skin"].GetInnerText();
        character.Hair = appearanceNode["hair"].GetInnerText();
    }

    private async Task ReadChildElements(XmlNode elementNode, ElementBase element)
    {
        foreach (XmlNode childNode in elementNode.ChildNodes)
        {
            if (childNode.ContainsAttribute("registered"))
            {
                string registeredElementId = childNode.GetAttributeValue("registered");
                if (!string.IsNullOrWhiteSpace(registeredElementId))
                {
                    string type = childNode.GetAttributeValue("type");
                    string name = childNode.GetAttributeValue("name");
                    bool flag = childNode.ContainsAttribute("isList") && Convert.ToBoolean(childNode.GetAttributeValue("isList"));
                    bool hasNumber = childNode.ContainsAttribute("number");
                    int num1 = int.Parse(childNode.GetAttributeValue("requiredLevel"));
                    bool isRetrained = childNode.ContainsAttribute("replaceLevel");
                    int retrainLevel = -1;
                    if (isRetrained)
                        isRetrained = int.TryParse(childNode.GetAttributeValue("replaceLevel"), out retrainLevel);
                    int number = -1;
                    if (hasNumber)
                        hasNumber = int.TryParse(childNode.GetAttributeValue("number"), out number);
                    if ((num1 <= 1 || CharacterManager.Current.Character.Level >= num1) && (!isRetrained || CharacterManager.Current.Character.Level >= retrainLevel))
                    {
                        try
                        {
                            SelectRule listRule;
                            if (flag)
                            {
                                listRule = element.GetSelectRules().Single<SelectRule>((Func<SelectRule, bool>)(x => x.Attributes.IsList && x.Attributes.Name.Equals(name)));
                                int num2 = await CharacterFile.AwaitExpanderCreationAsync(listRule, hasNumber ? number : 1) ? 1 : 0;
                                SelectionRuleExpanderContext.Current.SetRegisteredElement(listRule, registeredElementId, hasNumber ? number : 1);
                                continue;
                            }
                            if (hasNumber)
                            {
                                string existingChecksum = childNode.GetAttributeValue("checksum");
                                List<SelectRule> list = element.GetSelectRules().Where<SelectRule>((Func<SelectRule, bool>)(x => x.Attributes.Type == type && x.Attributes.Number > 1 && x.Attributes.Name == name && x.GetCrC(number) == existingChecksum)).ToList<SelectRule>();
                                if (list.Any<SelectRule>())
                                {
                                    if (list.Count > 1 && Debugger.IsAttached)
                                        Debugger.Break();
                                    listRule = list.First<SelectRule>();
                                    if (await CharacterFile.AwaitExpanderCreationAsync(listRule, number))
                                    {
                                        if (isRetrained)
                                            SelectionRuleExpanderContext.Current.RetrainSpellExpander(listRule, number, retrainLevel);
                                        SelectionRuleExpanderContext.Current.SetRegisteredElement(listRule, registeredElementId, number);
                                        listRule = (SelectRule)null;
                                    }
                                    else
                                        continue;
                                }
                                else
                                    continue;
                            }
                            else
                            {
                                List<SelectRule> list = element.GetSelectRules().Where<SelectRule>((Func<SelectRule, bool>)(x => x.Attributes.Type == type && x.Attributes.Name == name)).ToList<SelectRule>();
                                int count = list.Count;
                                if (list.Count != 1 && childNode.ContainsAttribute("checksum"))
                                {
                                    string existingChecksum = childNode.GetAttributeValue("checksum");
                                    listRule = list.Single<SelectRule>((Func<SelectRule, bool>)(x => x.GetCrC(1) == existingChecksum));
                                    if (!await CharacterFile.AwaitExpanderCreationAsync(listRule))
                                        break;
                                    if (isRetrained)
                                        SelectionRuleExpanderContext.Current.RetrainSpellExpander(listRule, 1, retrainLevel);
                                    SelectionRuleExpanderContext.Current.SetRegisteredElement(listRule, registeredElementId);
                                    listRule = (SelectRule)null;
                                }
                                else
                                {
                                    listRule = element.GetSelectRules().FirstOrDefault<SelectRule>((Func<SelectRule, bool>)(x => x.Attributes.Type == type && x.Attributes.Name == name));
                                    if (listRule == null && Debugger.IsAttached)
                                        Debugger.Break();
                                    if (!await CharacterFile.AwaitExpanderCreationAsync(listRule))
                                        break;
                                    if (isRetrained)
                                        SelectionRuleExpanderContext.Current.RetrainSpellExpander(listRule, 1, retrainLevel);
                                    SelectionRuleExpanderContext.Current.SetRegisteredElement(listRule, registeredElementId);
                                    listRule = (SelectRule)null;
                                }
                            }
                            Logger.Debug("--Registered:" + registeredElementId);
                            if (!CharacterManager.Current.Elements.Any<ElementBase>((Func<ElementBase, bool>)(x => x.Id == registeredElementId)))
                            {
                                Logger.Warning("--not yet registered!" + registeredElementId);
                                await Task.Delay(500);
                                if (!CharacterManager.Current.Elements.Any<ElementBase>((Func<ElementBase, bool>)(x => x.Id == registeredElementId)))
                                    Logger.Warning("--not yet registered!" + registeredElementId);
                                else
                                    Logger.Warning("--yep! after 500ms it is now registered!" + registeredElementId);
                            }
                            ElementBase element1 = CharacterManager.Current.Elements.LastOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id == registeredElementId));
                            if (element1 != null)
                                await this.ReadChildElements(childNode, element1);
                            else
                                Logger.Warning("unable to get element from character elements: {0}", (object)registeredElementId);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"error while reading child element {name} ({type}) -> {registeredElementId}");
                            Logger.Exception(ex, nameof(ReadChildElements));
                        }
                    }
                }
            }
            else
            {
                string id = childNode.GetAttributeValue("id");
                if (string.IsNullOrWhiteSpace(id) && Debugger.IsAttached)
                    Debugger.Break();
                ElementBase element2 = CharacterManager.Current.GetElements().LastOrDefault<ElementBase>((Func<ElementBase, bool>)(x => x.Id == id));
                if (element2 != null)
                    await this.ReadChildElements(childNode, element2);
                else
                    Logger.Warning("unable to get element from character elements: {0}", (object)id);
            }
        }
    }

    private void ParseAttacksSection(XmlNode attacksNode, Character character)
    {
        character.AttacksSection.AttacksAndSpellcasting = attacksNode.FirstChild.GetInnerText();
        if (!this.IsPostCharacterSheetUpdate())
            return;
        List<XmlNode> list = attacksNode.ChildNodes.Cast<XmlNode>().Where<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("attack"))).ToList<XmlNode>();
        character.AttacksSection.Items.Clear();
        foreach (XmlNode node in list)
        {
            try
            {
                AttackSectionItem attackSectionItem = (AttackSectionItem)null;
                if (node.ContainsAttribute("identifier"))
                {
                    string id = node.GetAttributeValue("identifier");
                    RefactoredEquipmentItem equipment = character.Inventory.Items.FirstOrDefault<RefactoredEquipmentItem>((Func<RefactoredEquipmentItem, bool>)(x => x.Identifier.Equals(id, StringComparison.OrdinalIgnoreCase)));
                    if (equipment != null)
                        attackSectionItem = new AttackSectionItem(equipment, false);
                }
                if (attackSectionItem == null)
                    attackSectionItem = new AttackSectionItem(node.GetAttributeValue("name"));
                if (node.ContainsAttribute("name"))
                    attackSectionItem.Name.SetIfNotEqualOriginalContent(node.GetAttributeValue("name"));
                if (node.ContainsAttribute("range"))
                    attackSectionItem.Range.SetIfNotEqualOriginalContent(node.GetAttributeValue("range"));
                if (node.ContainsAttribute("attack"))
                    attackSectionItem.Attack.SetIfNotEqualOriginalContent(node.GetAttributeValue("attack"));
                if (node.ContainsAttribute("damage"))
                    attackSectionItem.Damage.SetIfNotEqualOriginalContent(node.GetAttributeValue("damage"));
                if (node.ContainsAttribute("ability") && attackSectionItem.IsAutomaticAddition)
                {
                    string attributeValue = node.GetAttributeValue("ability");
                    if (!string.IsNullOrWhiteSpace(attributeValue))
                        attackSectionItem.SetLinkedAbility(attributeValue);
                }
                if (node.FirstChild != null)
                    attackSectionItem.Description.SetIfNotEqualOriginalContent(node.FirstChild.GetInnerText());
                attackSectionItem.IsDisplayed = node.GetAttributeAsBoolean("displayed");
                character.AttacksSection.Items.Add(attackSectionItem);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, nameof(ParseAttacksSection));
            }
        }
    }

    private void ParseBackgroundInput(XmlNode backgroundNode, Character character)
    {
        if (!this.IsPostCharacterSheetUpdate())
            return;
        XmlNode node = backgroundNode.ChildNodes.Cast<XmlNode>().FirstOrDefault<XmlNode>((Func<XmlNode, bool>)(x => x.Name.Equals("feature")));
        if (node == null)
            return;
        string attributeValue = node.GetAttributeValue("name");
        string innerText = node.FirstChild.GetInnerText();
        if (!character.BackgroundFeatureName.EqualsOriginalContent(attributeValue))
            character.BackgroundFeatureName.Content = attributeValue;
        if (character.BackgroundFeatureDescription.EqualsOriginalContent(innerText))
            return;
        character.BackgroundFeatureDescription.Content = innerText;
    }

    private void ParseBackgroundCharacteristicsInput(XmlNode inputNode, Character character)
    {
        XmlElement node1 = inputNode["background-traits"];
        string innerText1 = node1 != null ? node1.GetInnerText() : (string)null;
        XmlElement node2 = inputNode["background-ideals"];
        string innerText2 = node2 != null ? node2.GetInnerText() : (string)null;
        XmlElement node3 = inputNode["background-bonds"];
        string innerText3 = node3 != null ? node3.GetInnerText() : (string)null;
        XmlElement node4 = inputNode["background-flaws"];
        string innerText4 = node4 != null ? node4.GetInnerText() : (string)null;
        FillableBackgroundCharacteristics backgroundCharacteristics = character.FillableBackgroundCharacteristics;
        if (!backgroundCharacteristics.Traits.EqualsOriginalContent(innerText1))
            backgroundCharacteristics.Traits.Content = innerText1;
        if (!backgroundCharacteristics.Ideals.EqualsOriginalContent(innerText2))
            backgroundCharacteristics.Ideals.Content = innerText2;
        if (!backgroundCharacteristics.Bonds.EqualsOriginalContent(innerText3))
            backgroundCharacteristics.Bonds.Content = innerText3;
        if (!backgroundCharacteristics.Flaws.EqualsOriginalContent(innerText4))
            backgroundCharacteristics.Flaws.Content = innerText4;
        XmlElement node5 = inputNode["background-trinket"];
        string innerText5 = node5 != null ? node5.GetInnerText() : (string)null;
        if (character.Trinket.EqualsOriginalContent(innerText5))
            return;
        character.Trinket.Content = innerText5;
    }

    private static async Task<bool> AwaitExpanderCreationAsync(SelectRule selectionRule, int number = 1)
    {
        int loopCount = 0;
        while (!SelectionRuleExpanderContext.Current.HasExpander(selectionRule.UniqueIdentifier, number))
        {
            await Task.Delay(10);
            ++loopCount;
            if (loopCount >= 5)
                Logger.Warning($"expander for {selectionRule.Attributes.Name} still does not exist after {loopCount} loops, waiting for it to get added");
            if (loopCount >= 10)
                return false;
        }
        return true;
    }

    [Obsolete]
    private void AppendAttribute(XmlNode node, string name, string value)
    {
        if (node.OwnerDocument == null)
            throw new NullReferenceException("OwnerDocument");
        XmlAttribute attribute = node.OwnerDocument.CreateAttribute(name);
        attribute.Value = value;
        node.Attributes?.Append(attribute);
    }

    [Obsolete]
    private void AppendAttributes(XmlNode node, Dictionary<string, string> attributesDictionary)
    {
        if (node.OwnerDocument == null)
            throw new NullReferenceException("OwnerDocument");
        foreach (KeyValuePair<string, string> attributes in attributesDictionary)
        {
            XmlAttribute attribute = node.OwnerDocument.CreateAttribute(attributes.Key);
            attribute.Value = attributes.Value;
            node.Attributes?.Append(attribute);
        }
    }

    [Obsolete]
    private XmlNode AppendChild(XmlNode parentNode, string nodeName, string innertext, bool isCData)
    {
        XmlNode xmlNode = parentNode.AppendChild(this._document.CreateNode(XmlNodeType.Element, nodeName, (string)null));
        if (isCData)
            xmlNode.AppendChild((XmlNode)this._document.CreateCDataSection(innertext));
        else
            xmlNode.InnerText = innertext;
        return xmlNode;
    }

    public override string ToString()
    {
        return $"{this.DisplayName}, Level {this.DisplayLevel} {this.DisplayRace} {this.DisplayClass}";
    }

    public void SaveRemotePortrait()
    {
        try
        {
            if (System.IO.File.Exists(this.DisplayPortraitFilePath))
                return;
            string str = Path.Combine(DataManager.Current.UserDocumentsPortraitsDirectory, Path.GetFileName(this.DisplayPortraitFilePath));
            if (System.IO.File.Exists(str))
            {
                this.DisplayPortraitFilePath = str;
            }
            else
            {
                GalleryUtilities.SaveBase64AsImage(this.DisplayPortraitBase64, str);
                this.DisplayPortraitFilePath = str;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(SaveRemotePortrait));
            string message = ex.Message;
            MessageDialogContext.Current?.ShowException(ex, message, "Unable to save remote portrait.");
        }
    }

    private bool IsPostCharacterSheetUpdate()
    {
        string attributeValue = this._document.DocumentElement.GetAttributeValue("version");
        int num = this._document.DocumentElement.GetAttributeAsBoolean("preview") ? 1 : 0;
        Version version = new Version(attributeValue);
        return num == 0 || version.CompareTo(new Version("1.18.822")) >= 0;
    }

    public class LoadResult
    {
        public bool Success { get; }

        public string Message { get; }

        public LoadResult(bool success, string message = "")
        {
            this.Success = success;
            this.Message = message;
        }
    }
}
