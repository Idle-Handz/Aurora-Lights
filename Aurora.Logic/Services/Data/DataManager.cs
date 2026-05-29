// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.Data.DataManager
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Files;
using Builder.Data.Rules;
using Builder.Data.Strings;
using Builder.Presentation.Events.Data;
using Builder.Presentation.Logging;
using Builder.Presentation.Models;
using Builder.Presentation.Utilities;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

#nullable disable
namespace Builder.Presentation.Services.Data;

public sealed class DataManager
{
  private const string LocalAppDataRootDirectoryName = "5e Character Builder";
  private const string LocalAppDataApplicationElementsDirectoryName = "elements";
  private const string LocalAppDataLogsDirectoryName = "logs";
  private const string LocalAppDataAssetsDirectoryName = "assets";
  private const string UserDocumentsRootDirectoryName = "5e Character Builder";
  private const string UserDocumentsPortraitsDirectoryName = "portraits";
  private const string UserDocumentsCustomElementsDirectoryName = "custom";
  private const string UserDocumentsGalleryDirectoryName = "gallery";
  private const string UserDocumentsCompanionGalleryDirectoryName = "companions";
  private const string UserDocumentsSymbolsGalleryDirectoryName = "symbols";
  private const string IndexFileExtension = ".index";
  private const string ElementsDataFileExtension = ".xml";
  private const string CharacterFileExtension = ".dnd5e";
  private readonly IEventAggregator _eventAggregator;

  private DataManager() => this._eventAggregator = ApplicationContext.Current.EventAggregator;

  public event EventHandler<DataManagerProgressChanged> ProgressChanged;

  public event EventHandler InitializingData;

  public event EventHandler InitializingDataCompleted;

  private void OnProgressChanged(DataManagerProgressChanged e)
  {
    EventHandler<DataManagerProgressChanged> progressChanged = this.ProgressChanged;
    if (progressChanged == null)
      return;
    progressChanged((object) this, e);
  }

  private void OnInitializingData()
  {
    EventHandler initializingData = this.InitializingData;
    if (initializingData == null)
      return;
    initializingData((object) this, EventArgs.Empty);
  }

  private void OnInitializingDataCompleted()
  {
    EventHandler initializingDataCompleted = this.InitializingDataCompleted;
    if (initializingDataCompleted == null)
      return;
    initializingDataCompleted((object) this, EventArgs.Empty);
  }

  public static DataManager Current { get; } = new DataManager();

  public ElementBaseCollection ElementsCollection { get; } = new ElementBaseCollection();

  public string LocalAppDataRootDirectory { get; private set; }

  public string LocalAppDataApplicationElementsDirectory { get; private set; }

  public string LocalAppDataLogsDirectory { get; private set; }

  public string LocalAppDataAssetsDirectory { get; private set; }

  public string UserDocumentsRootDirectory { get; private set; }

  public string UserDocumentsPortraitsDirectory { get; private set; }

  public string UserDocumentsCustomElementsDirectory { get; private set; }

  public string UserDocumentsCompanionGalleryDirectory { get; private set; }

  public string UserDocumentsSymbolsGalleryDirectory { get; private set; }

  public bool IsElementsCollectionPopulated { get; private set; }

  public void InitializeDirectories()
  {
    try
    {
      this.LocalAppDataRootDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LocalAppDataRootDirectoryName);
      this.LocalAppDataApplicationElementsDirectory = Path.Combine(this.LocalAppDataRootDirectory, LocalAppDataApplicationElementsDirectoryName);
      this.LocalAppDataLogsDirectory = Path.Combine(this.LocalAppDataRootDirectory, LocalAppDataLogsDirectoryName);
      this.LocalAppDataAssetsDirectory = Path.Combine(this.LocalAppDataRootDirectory, LocalAppDataAssetsDirectoryName);
      DataManager.CreateDirectory(this.LocalAppDataRootDirectory);
      DataManager.CreateDirectory(this.LocalAppDataApplicationElementsDirectory);
      DataManager.CreateDirectory(this.LocalAppDataLogsDirectory);
      DataManager.CreateDirectory(this.LocalAppDataAssetsDirectory);
    }
    catch (Exception ex)
    {
      Logger.Warning("unable to create appdata folders");
      Logger.Exception(ex, nameof (InitializeDirectories));
    }
    try
    {
      this.UserDocumentsRootDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), UserDocumentsRootDirectoryName);
      if (!string.IsNullOrWhiteSpace(ApplicationContext.Current.Settings.DocumentsRootDirectory))
      {
        if (Directory.Exists(ApplicationContext.Current.Settings.DocumentsRootDirectory))
        {
          this.UserDocumentsRootDirectory = ApplicationContext.Current.Settings.DocumentsRootDirectory;
        }
        else
        {
          MessageDialogContext.Current?.Show($"The root {ApplicationContext.Current.Settings.DocumentsRootDirectory} does not exist. Falling back to the default directory.");
          ApplicationContext.Current.Settings.DocumentsRootDirectory = "";
        }
      }
      this.UserDocumentsCompanionGalleryDirectory = Path.Combine(this.UserDocumentsRootDirectory, UserDocumentsGalleryDirectoryName, UserDocumentsCompanionGalleryDirectoryName);
      this.UserDocumentsSymbolsGalleryDirectory = Path.Combine(this.UserDocumentsRootDirectory, UserDocumentsGalleryDirectoryName, UserDocumentsSymbolsGalleryDirectoryName);
      this.UserDocumentsPortraitsDirectory = Path.Combine(this.UserDocumentsRootDirectory, UserDocumentsPortraitsDirectoryName);
      this.UserDocumentsCustomElementsDirectory = Path.Combine(this.UserDocumentsRootDirectory, UserDocumentsCustomElementsDirectoryName);
      DataManager.CreateDirectory(this.UserDocumentsRootDirectory);
      DataManager.CreateDirectory(this.UserDocumentsPortraitsDirectory);
      DataManager.CreateDirectory(this.UserDocumentsCustomElementsDirectory);
      DataManager.CreateDirectory(this.UserDocumentsCompanionGalleryDirectory);
      DataManager.CreateDirectory(this.UserDocumentsSymbolsGalleryDirectory);
      DataManager.CreateDirectory(Path.Combine(this.UserDocumentsCustomElementsDirectory, "user"));
      if (string.IsNullOrWhiteSpace(ApplicationContext.Current.Settings.DocumentsRootDirectory))
        ApplicationContext.Current.Settings.DocumentsRootDirectory = this.UserDocumentsRootDirectory;
    }
    catch (Exception ex)
    {
      Logger.Warning("unable to create user documents folders");
      Logger.Exception(ex, nameof (InitializeDirectories));
    }
    Logger.Info("Directories Initialized");
  }

  public void InitializeFileLogger()
  {
    Logger.RegisterLogger((ILogger) new FileLogger(this.LocalAppDataLogsDirectory));
  }

  public void InitializeFileWatcher()
  {
    FileSystemWatcher fileSystemWatcher = Directory.Exists(this.UserDocumentsRootDirectory) ? new FileSystemWatcher(this.UserDocumentsRootDirectory)
    {
      Filter = "*.dnd5e",
      EnableRaisingEvents = true
    } : throw new Exception("Directories are not initialized. Call InitializeFolders before InitializeFileWatcher");
  }

  public async Task<IEnumerable<ElementBase>> InitializeElementDataAsync()
  {
    this.IsElementsCollectionPopulated = false;
    List<ElementBase> elementBaseList = new List<ElementBase>();
    List<ElementParser> elementParserCollection = ElementParserFactory.GetParsers().ToList<ElementParser>();
    ElementParser defaultParser = new ElementParser();
    ElementParser elementParser = new ElementParser();
    List<Exception> exceptions = new List<Exception>();
    ElementBaseCollection coreElements = new ElementBaseCollection();
    int elementNodeCount = 0;
    DataManagerProgressChanged args = new DataManagerProgressChanged("Initializing Core", 0, true);
    this._eventAggregator.Send<DataManagerProgressChanged>(args);
    int count = 0;
    List<XmlDocument> xmlDocumentList = this.LoadElementDocumentsFromResource();
    foreach (XmlDocument xmlDocument in xmlDocumentList)
    {
      AuroraXmlCompatibilityRepair.RepairDocument(xmlDocument);
      ElementHeader elementHeader = (ElementHeader) null;
      try
      {
        ++count;
        args.ProgressPercentage = DataManager.GetPercentage((double) count, (double) xmlDocumentList.Count);
        this._eventAggregator.Send<DataManagerProgressChanged>(args);
        List<XmlNode> list = xmlDocument.DocumentElement.ChildNodes.Cast<XmlNode>().Where<XmlNode>((Func<XmlNode, bool>) (x => x.NodeType != XmlNodeType.Comment && x.Name.Equals("element"))).ToList<XmlNode>();
        elementNodeCount += list.Count;
        foreach (XmlNode elementNode in list)
        {
          try
          {
            ElementHeader header = elementParser.ParseElementHeader(elementNode);
            elementHeader = header;
            if (elementParser.ParserType != header.Type)
              elementParser = elementParserCollection.FirstOrDefault<ElementParser>((Func<ElementParser, bool>) (x => x.ParserType == header.Type)) ?? defaultParser;
            ElementBase element = elementParser.ParseElement(elementNode);
            if (coreElements.Select<ElementBase, string>((Func<ElementBase, string>) (e => e.Id)).Contains<string>(element.Id))
              exceptions.Add((Exception) new DuplicateElementException(element.Name, "resource filename"));
            else
              coreElements.Add(element);
            this._eventAggregator.Send<DataManagerProgressChanged>(args);
          }
          catch (Exception ex)
          {
            Logger.Warning($"'{ex.GetType()}' in parsing the resource data files on {elementHeader}");
            Logger.Exception(ex, nameof (InitializeElementDataAsync));
            ex.Data[(object) "warning"] = (object) $"'{ex.GetType()}' in parsing the resource data files on {elementHeader}";
            exceptions.Add(ex);
          }
        }
      }
      catch (Exception ex)
      {
        Logger.Warning($"'{ex.GetType()}' in parsing the resource data files on {elementHeader}");
        Logger.Exception(ex, nameof (InitializeElementDataAsync));
        ex.Data[(object) "warning"] = (object) $"'{ex.GetType()}' in parsing the resource data files on {elementHeader}";
        exceptions.Add(ex);
      }
    }
    Logger.Info("loaded {0} core elements from {1} element nodes", (object) coreElements.Count, (object) elementNodeCount);
    args.ProgressMessage = "Initializing Custom Elements";
    args.ProgressPercentage = 0;
    this._eventAggregator.Send<DataManagerProgressChanged>(args);
    await Task.Delay(50);
    int currentFileCount = 0;
    List<FileInfo> customFiles = this.GetCustomFiles();
    List<XmlNode> appendNotes = new List<XmlNode>();
    foreach (FileInfo file in customFiles)
    {
      try
      {
        Logger.Info($"parsing {file}");
        ElementsFile ef = ElementsFile.FromFile(file);
        ++currentFileCount;
        args.ProgressMessage = ef.Info.DisplayName ?? "";
        args.ProgressPercentage = DataManager.GetPercentage((double) currentFileCount, (double) customFiles.Count);
        this._eventAggregator.Send<DataManagerProgressChanged>(args);
        if (ef.Ignore)
        {
          Logger.Warning($"ignore {file}");
        }
        else
        {
          ElementBaseCollection applicationElements = new ElementBaseCollection();
          XmlDocument xmlDocument = await DataManager.CreateXmlDocument(file.FullName);
          AuroraXmlCompatibilityRepair.RepairDocument(xmlDocument);
          ObservableCollection<XmlNode> elementNodes = ef.ElementNodes;
          if (xmlDocument.DocumentElement != null)
          {
            List<XmlNode> list = xmlDocument.DocumentElement.ChildNodes.Cast<XmlNode>().Where<XmlNode>((Func<XmlNode, bool>) (x => x.NodeType != XmlNodeType.Comment && x.Name.Equals("element"))).ToList<XmlNode>();
            elementNodeCount += list.Count;
            foreach (XmlNode elementNode in list)
            {
              ElementHeader header = elementParser.ParseElementHeader(elementNode);
              if (elementParser.ParserType != header.Type)
                elementParser = elementParserCollection.FirstOrDefault<ElementParser>((Func<ElementParser, bool>) (p => p.ParserType == header.Type)) ?? defaultParser;
              applicationElements.Add(elementParser.ParseElement(elementNode));
            }
          }
          foreach (ElementBase elementBase1 in (Collection<ElementBase>) applicationElements)
          {
            ElementBase element = elementBase1;
            if (coreElements.Select<ElementBase, string>((Func<ElementBase, string>) (e => e.Id)).Contains<string>(element.Id))
            {
              ElementBase elementBase2 = coreElements.Single<ElementBase>((Func<ElementBase, bool>) (x => x.Id == element.Id));
              if (elementBase2.Type != element.Type)
                exceptions.Add((Exception) new DuplicateElementException(element.Name, file.Name));
              coreElements.Remove(elementBase2);
            }
            coreElements.Add(element);
          }
          appendNotes.AddRange((IEnumerable<XmlNode>) ef.ExtendNodes);
          ef = (ElementsFile) null;
          applicationElements = (ElementBaseCollection) null;
        }
      }
      catch (ElementsFileLoadException ex)
      {
        Logger.Warning(ex.Message);
        Logger.Exception((Exception) ex, nameof (InitializeElementDataAsync));
        ex.Data.Add((object) "filename", (object) file.FullName);
        exceptions.Add((Exception) ex);
      }
      catch (Exception ex)
      {
        Logger.Warning("'{0}' in parsing {1}", (object) ex.GetType(), (object) file.FullName);
        Logger.Exception(ex, nameof (InitializeElementDataAsync));
        ex.Data.Add((object) "filename", (object) file.FullName);
        exceptions.Add(ex);
      }
    }
    args.ProgressMessage = "Processing elements...";
    this._eventAggregator.Send<DataManagerProgressChanged>(args);
    await Task.Delay(10);
    this.AppendElements((IEnumerable<XmlNode>) appendNotes, coreElements, elementParser, defaultParser, elementParserCollection);
    args.ProgressMessage = $"{coreElements.Count}/{elementNodeCount} elements loaded";
    args.ProgressPercentage = 100;
    Logger.Info(args.ProgressMessage);
    if (exceptions.Any<Exception>())
    {
      List<Exception> list1 = exceptions.Where<Exception>((Func<Exception, bool>) (x => x.GetType() == typeof (MissingSetterException))).ToList<Exception>();
      if (list1.Any<Exception>())
      {
        foreach (Exception exception in list1)
          exceptions.Remove(exception);
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("There are missing setter nodes on the following elements:").AppendLine();
        foreach (Exception exception in list1)
          stringBuilder.AppendLine("\t" + exception.Message);
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("These will not be available until these missing setters have been added.").AppendLine().AppendLine();
        MessageDialogContext.Current?.Show(stringBuilder.ToString());
      }
      List<Exception> list2 = exceptions.Where<Exception>((Func<Exception, bool>) (x => x.GetType() == typeof (DuplicateElementException))).ToList<Exception>();
      if (list2.Any<Exception>())
      {
        foreach (Exception exception in list2)
          exceptions.Remove(exception);
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("There are duplicated element id's on the following elements:").AppendLine();
        foreach (Exception exception in list2)
          stringBuilder.AppendLine("\t" + exception.Message);
        stringBuilder.AppendLine("original elements have been replaced with the custom elements with the same id");
        if (Debugger.IsAttached)
          MessageDialogContext.Current?.Show(stringBuilder.ToString());
      }
      if (exceptions.Any<Exception>())
      {
        Exception ex = exceptions.First<Exception>();
        object obj1 = ex.Data.Contains((object) "filename") ? ex.Data[(object) "filename"] : (object) "internal";
        object obj2 = ex.Data.Contains((object) "warning") ? ex.Data[(object) "warning"] : (object) "";
        MessageDialogContext.Current?.ShowException(ex, "Error(s) parsing data files", exceptions.Count > 1 ? $"{exceptions.Count} exceptions occurred while parsing the data files. The first one is shown below, the others can be found in the logs.\r\nFile: {obj1}\r\nInfo: {obj2}" : $"An exception occurred while parsing the data files.\r\nFile: {obj1}\r\nInfo: {obj2}");
      }
    }
    this.ElementsCollection.Clear();
    this.ElementsCollection.AddRange((IEnumerable<ElementBase>) coreElements);
    this._eventAggregator.Send<ElementsCollectionPopulatedEvent>(new ElementsCollectionPopulatedEvent());
    args.ProgressMessage = "Finalizing Content";
    this._eventAggregator.Send<DataManagerProgressChanged>(args);
    List<ElementBase> list3 = this.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Support"))).ToList<ElementBase>();
    string[] strArray = new string[11]
    {
      "(",
      ")",
      ",",
      "&",
      "|",
      "!",
      "[",
      "]",
      ":",
      "'",
      "�"
    };
    foreach (ElementBase elements in (Collection<ElementBase>) this.ElementsCollection)
    {
      foreach (string str in strArray)
      {
        if (elements.Id.Contains(str))
        {
          Logger.Warning($"INVALID ID ON: {elements} ({elements.Id})");
          break;
        }
      }
      List<string> stringList = new List<string>();
      foreach (string support1 in elements.Supports)
      {
        string support = support1;
        if (support.StartsWith("ID_INTERNAL_SUPPORT"))
        {
          ElementBase elementBase = list3.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(support)));
          if (elementBase != null)
            stringList.Add(elementBase.Name);
        }
      }
      foreach (string str in stringList)
      {
        if (!elements.Supports.Contains(str))
          elements.Supports.Add(str);
      }
    }
    IEnumerable<ElementBase> source1 = this.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Class Feature"));
    IEnumerable<ElementBase> source2 = source1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Id.StartsWith("ID_INTERNAL_TEMPLATE_CLASS_FEATURE_ABILITY_4")));
    IEnumerable<ElementBase> source3 = source1.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Id.StartsWith("ID_INTERNAL_TEMPLATE_CLASS_FEATURE_FEAT_4")));
    ElementBase original1 = source2.FirstOrDefault<ElementBase>();
    ElementBase original2 = source3.FirstOrDefault<ElementBase>();
    Dictionary<string, string> dictionary = new Dictionary<string, string>();
    List<string> stringList1 = new List<string>();
    foreach (Class @class in this.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Class")).Cast<Class>().ToList<Class>())
    {
      if (@class.CanMulticlass)
      {
        ElementBase element = elementParserCollection.FirstOrDefault<ElementParser>((Func<ElementParser, bool>) (x => x.ParserType == "Multiclass")).ParseElement(@class.ElementNode);
        this.ElementsCollection.Add(element);
        @class.Requirements = @class.HasRequirements ? $"({@class.Requirements})&&!{element.Id}" : "!" + element.Id;
        @class.Rules.Add((RuleBase) new GrantRule(@class.ElementHeader)
        {
          Attributes = {
            Type = "Grants",
            Name = InternalGrants.MulticlassPrerequisite,
            Requirements = $"{InternalOptions.AllowMulticlassing}&&({element.Requirements})&&!{element.Id}"
          }
        });
        element.Requirements = $"!{@class.Id}&&({element.Requirements})";
        dictionary.Add(@class.Id, element.Id);
      }
      if (!stringList1.Contains(@class.Name))
      {
        ElementBaseCollection elements = new ElementBaseCollection();
        int[] numArray = new int[9]
        {
          4,
          6,
          8,
          10,
          12,
          14,
          16 /*0x10*/,
          18,
          19
        };
        foreach (int num in numArray)
        {
          string name = @class.Name;
          if (name.Contains(","))
          {
            Logger.Warning(name + " contains ','");
            if (Debugger.IsAttached)
              Debugger.Break();
          }
          ElementBase elementBase3 = original1.Copy<ElementBase>();
          string str1 = $"ID_INTERNAL_CLASS_FEATURE_ASI_{num}_{name.ToUpperInvariant()}";
          if (!ElementsHelper.ValidateID(str1))
            str1 = ElementsHelper.SanitizeID(str1);
          elementBase3.ElementHeader = new ElementHeader($"Ability Score Improvement ({num})", "Class Feature", "Player�s Handbook", str1);
          elementBase3.GetSelectRules().First<SelectRule>().Attributes.Name = $"Ability Score Increase ({name.ToUpperInvariant()} {num})";
          elementBase3.GetSelectRules().First<SelectRule>().Attributes.RequiredLevel = num;
          elementBase3.GetSelectRules().First<SelectRule>().RenewIdentifier();
          elementBase3.GetSelectRules().First<SelectRule>().ElementHeader = elementBase3.ElementHeader;
          elementBase3.Supports.Add("Improvement Option");
          elementBase3.Supports.Add(name);
          elementBase3.Supports.Add(num.ToString());
          elementBase3.IsExtended = true;
          elementBase3.IncludeInCompendium = false;
          elements.Add(elementBase3);
          ElementBase elementBase4 = original2.Copy<ElementBase>();
          string str2 = $"ID_INTERNAL_CLASS_FEATURE_FEAT_{num}_{name.ToUpperInvariant()}";
          if (!ElementsHelper.ValidateID(str2))
            str2 = ElementsHelper.SanitizeID(str2);
          elementBase4.ElementHeader = new ElementHeader($"Feat ({num})", "Class Feature", "Player�s Handbook", str2);
          elementBase4.GetSelectRules().First<SelectRule>().Attributes.Name = $"Feat ({name.ToUpperInvariant()} {num})";
          elementBase4.GetSelectRules().First<SelectRule>().Attributes.RequiredLevel = num;
          elementBase4.GetSelectRules().First<SelectRule>().RenewIdentifier();
          elementBase4.GetSelectRules().First<SelectRule>().ElementHeader = elementBase4.ElementHeader;
          elementBase4.Supports.Add("Improvement Option");
          elementBase4.Supports.Add(name);
          elementBase4.Supports.Add(num.ToString());
          elementBase4.IsExtended = true;
          elementBase4.IncludeInCompendium = false;
          elements.Add(elementBase4);
        }
        this.ElementsCollection.AddRange((IEnumerable<ElementBase>) elements);
        stringList1.Add(@class.Name);
      }
    }
    if (true)
    {
      Stopwatch stopwatch = Stopwatch.StartNew();
      SpellScrollContentGenerator contentGenerator = new SpellScrollContentGenerator();
      ElementBase elementBase = this.ElementsCollection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals("ID_WOTC_DMG_MAGIC_ITEM_SPELL_SCROLL_CANTRIP")));
      ElementBaseCollection elementsCollection = this.ElementsCollection;
      MagicItemElement template = elementBase as MagicItemElement;
      List<ElementBase> elements = contentGenerator.Generate((IEnumerable<ElementBase>) elementsCollection, template);
      this.ElementsCollection.AddRange((IEnumerable<ElementBase>) elements);
      stopwatch.Stop();
      Logger.Warning($"generating {elements.Count} scrolls took {stopwatch.ElapsedMilliseconds}ms");
    }
    InternalElementsGenerator elementsGenerator = new InternalElementsGenerator();
    this.ElementsCollection.AddRange((IEnumerable<ElementBase>) elementsGenerator.GenerateInternalFeats((IEnumerable<ElementBase>) this.ElementsCollection));
    this.ElementsCollection.AddRange((IEnumerable<ElementBase>) elementsGenerator.GenerateInternalLanguages((IEnumerable<ElementBase>) this.ElementsCollection));
    this.ElementsCollection.AddRange((IEnumerable<ElementBase>) elementsGenerator.GenerateInternalProficiency((IEnumerable<ElementBase>) this.ElementsCollection));
    this.ElementsCollection.AddRange((IEnumerable<ElementBase>) elementsGenerator.GenerateInternalAsi((IEnumerable<ElementBase>) this.ElementsCollection));
    this.ElementsCollection.AddRange((IEnumerable<ElementBase>) elementsGenerator.GenerateInternalSpells((IEnumerable<ElementBase>) this.ElementsCollection));
    if (Debugger.IsAttached || ApplicationContext.Current.IsInDeveloperMode)
      this.ElementsCollection.AddRange((IEnumerable<ElementBase>) elementsGenerator.GenerateInternalIgnore((IEnumerable<ElementBase>) this.ElementsCollection));
    this.InitializeItemDetails(coreElements);
    if (Debugger.IsAttached)
    {
      IEnumerable<ElementBase> source4 = this.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => !ElementsHelper.ValidateID(x.Id) && !x.Type.Equals("Ignore")));
      Logger.Warning($"found {source4.Count<ElementBase>()} invalid IDs");
      foreach (ElementBase elementBase in source4)
        Logger.Warning($"invalid ID on {elementBase} [{elementBase.Id}]");
    }
    this.IsElementsCollectionPopulated = true;
    IEnumerable<ElementBase> elementBases = (IEnumerable<ElementBase>) coreElements;
    elementParserCollection = (List<ElementParser>) null;
    defaultParser = (ElementParser) null;
    elementParser = (ElementParser) null;
    exceptions = (List<Exception>) null;
    coreElements = (ElementBaseCollection) null;
    args = (DataManagerProgressChanged) null;
    customFiles = (List<FileInfo>) null;
    appendNotes = (List<XmlNode>) null;
    return elementBases;
  }

  private void InitializeItemDetails(ElementBaseCollection collection)
  {
    foreach (Item obj in collection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Weapon"))).Cast<Item>())
    {
      List<string> source1 = new List<string>();
      List<string> source2 = new List<string>();
      List<string> source3 = new List<string>();
      foreach (string support1 in obj.Supports)
      {
        string support = support1;
        ElementBase elementBase = collection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(support)));
        if (elementBase != null)
        {
          switch (elementBase.Type)
          {
            case "Weapon Category":
              source1.Add(elementBase.Name);
              continue;
            case "Weapon Group":
              source2.Add(elementBase.Name.Replace("Weapon Group (", "").Trim(')', ' '));
              continue;
            case "Weapon Property":
              if (!elementBase.Name.Equals("Special"))
              {
                source3.Add(elementBase.Name);
                continue;
              }
              continue;
            default:
              continue;
          }
        }
      }
      obj.WeaponGroups.AddRange((IEnumerable<string>) source2.OrderBy<string, string>((Func<string, string>) (x => x)));
      obj.WeaponProperties.AddRange((IEnumerable<string>) source3.OrderBy<string, string>((Func<string, string>) (x => x)));
      obj.Keywords.AddRange(source3.OrderBy<string, string>((Func<string, string>) (x => x)).Select<string, string>((Func<string, string>) (x => x.Trim().ToLower())));
      obj.Keywords.AddRange(source1.OrderBy<string, string>((Func<string, string>) (x => x)).Select<string, string>((Func<string, string>) (x => x.Trim().ToLower())));
    }
    foreach (Item obj in collection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Armor"))).Cast<Item>())
    {
      List<string> source = new List<string>();
      foreach (string support2 in obj.Supports)
      {
        string support = support2;
        ElementBase elementBase = collection.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals(support)));
        if (elementBase != null)
        {
          switch (elementBase.Type)
          {
            case "Armor Group":
              source.Add(elementBase.Name.Replace("Armor Group (", "").Trim(')', ' '));
              continue;
            default:
              continue;
          }
        }
      }
      obj.ArmorGroups.AddRange((IEnumerable<string>) source.OrderBy<string, string>((Func<string, string>) (x => x)));
    }
  }

  /// <summary>
  /// Runs the post-processing steps that finalize <see cref="ElementsCollection"/> after all
  /// base elements have been loaded (from XML or from the SQLite DB loader).
  /// Resolves internal support tags, synthesizes multiclass and ASI feature elements,
  /// generates spell scrolls and internal derived elements, then fires the populated event.
  /// </summary>
  public void RunPostProcessing()
  {
    List<ElementParser> parsers = ElementParserFactory.GetParsers().ToList<ElementParser>();

    // Load embedded resource elements (Level, internal templates, etc.) that are not in
    // the custom content folder and therefore not in the SQLite DB.
    var existingIds = new HashSet<string>(
      this.ElementsCollection.Select<ElementBase, string>((Func<ElementBase, string>) (e => e.Id)),
      StringComparer.OrdinalIgnoreCase);
    ElementParser resourceParser = new ElementParser();
    foreach (XmlDocument resourceDoc in this.LoadElementDocumentsFromResource())
    {
      if (resourceDoc.DocumentElement == null) continue;
      AuroraXmlCompatibilityRepair.RepairDocument(resourceDoc);
      foreach (XmlNode elementNode in resourceDoc.DocumentElement.ChildNodes
        .Cast<XmlNode>()
        .Where<XmlNode>((Func<XmlNode, bool>) (x => x.NodeType != XmlNodeType.Comment && x.Name.Equals("element"))))
      {
        try
        {
          ElementHeader header = resourceParser.ParseElementHeader(elementNode);
          if (resourceParser.ParserType != header.Type)
            resourceParser = parsers.FirstOrDefault<ElementParser>((Func<ElementParser, bool>) (p => p.ParserType == header.Type)) ?? new ElementParser();
          ElementBase element = resourceParser.ParseElement(elementNode);
          if (existingIds.Add(element.Id))
            this.ElementsCollection.Add(element);
        }
        catch (Exception ex)
        {
          Logger.Warning($"RunPostProcessing: skipped resource element: {ex.Message}");
        }
      }
    }

    // Resolve ID_INTERNAL_SUPPORT_* tags to their display names.
    List<ElementBase> supportElements = this.ElementsCollection
      .Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Support")))
      .ToList<ElementBase>();

    foreach (ElementBase el in (Collection<ElementBase>) this.ElementsCollection)
    {
      List<string> toAdd = new List<string>();
      foreach (string tag in el.Supports)
      {
        if (tag.StartsWith("ID_INTERNAL_SUPPORT"))
        {
          ElementBase found = supportElements.FirstOrDefault<ElementBase>(
            (Func<ElementBase, bool>) (x => x.Id.Equals(tag)));
          if (found != null)
            toAdd.Add(found.Name);
        }
      }
      foreach (string name in toAdd)
      {
        if (!el.Supports.Contains(name))
          el.Supports.Add(name);
      }
    }

    // Synthesize multiclass elements and per-class ASI/Feat features.
    IEnumerable<ElementBase> classFeatures = this.ElementsCollection
      .Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Class Feature"));
    ElementBase asiAbilityTemplate = classFeatures
      .FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.StartsWith("ID_INTERNAL_TEMPLATE_CLASS_FEATURE_ABILITY_4")));
    ElementBase asiFeatTemplate = classFeatures
      .FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.StartsWith("ID_INTERNAL_TEMPLATE_CLASS_FEATURE_FEAT_4")));

    List<string> processedClassNames = new List<string>();
    int[] asiLevels = new int[] { 4, 6, 8, 10, 12, 14, 16, 18, 19 };

    foreach (Class @class in this.ElementsCollection
      .Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type == "Class"))
      .Cast<Class>().ToList<Class>())
    {
      if (@class.CanMulticlass)
      {
        ElementBase mc = parsers
          .FirstOrDefault<ElementParser>((Func<ElementParser, bool>) (x => x.ParserType == "Multiclass"))
          .ParseElement(@class.ElementNode);
        this.ElementsCollection.Add(mc);
        @class.Requirements = @class.HasRequirements
          ? $"({@class.Requirements})&&!{mc.Id}"
          : "!" + mc.Id;
        @class.Rules.Add((RuleBase) new GrantRule(@class.ElementHeader)
        {
          Attributes = {
            Type = "Grants",
            Name = InternalGrants.MulticlassPrerequisite,
            Requirements = $"{InternalOptions.AllowMulticlassing}&&({mc.Requirements})&&!{mc.Id}"
          }
        });
        mc.Requirements = $"!{@class.Id}&&({mc.Requirements})";
      }

      if (!processedClassNames.Contains(@class.Name))
      {
        ElementBaseCollection asiElements = new ElementBaseCollection();
        foreach (int level in asiLevels)
        {
          string className = @class.Name;
          if (asiAbilityTemplate != null)
          {
            ElementBase copy = asiAbilityTemplate.Copy<ElementBase>();
            string id = $"ID_INTERNAL_CLASS_FEATURE_ASI_{level}_{className.ToUpperInvariant()}";
            if (!ElementsHelper.ValidateID(id)) id = ElementsHelper.SanitizeID(id);
            copy.ElementHeader = new ElementHeader(
              $"Ability Score Improvement ({level})", "Class Feature", "Player\u2019s Handbook", id);
            copy.GetSelectRules().First<SelectRule>().Attributes.Name =
              $"Ability Score Increase ({className.ToUpperInvariant()} {level})";
            copy.GetSelectRules().First<SelectRule>().Attributes.RequiredLevel = level;
            copy.GetSelectRules().First<SelectRule>().RenewIdentifier();
            copy.GetSelectRules().First<SelectRule>().ElementHeader = copy.ElementHeader;
            copy.Supports.Add("Improvement Option");
            copy.Supports.Add(className);
            copy.Supports.Add(level.ToString());
            copy.IsExtended = true;
            copy.IncludeInCompendium = false;
            asiElements.Add(copy);
          }
          if (asiFeatTemplate != null)
          {
            ElementBase copy = asiFeatTemplate.Copy<ElementBase>();
            string id = $"ID_INTERNAL_CLASS_FEATURE_FEAT_{level}_{className.ToUpperInvariant()}";
            if (!ElementsHelper.ValidateID(id)) id = ElementsHelper.SanitizeID(id);
            copy.ElementHeader = new ElementHeader(
              $"Feat ({level})", "Class Feature", "Player\u2019s Handbook", id);
            copy.GetSelectRules().First<SelectRule>().Attributes.Name =
              $"Feat ({className.ToUpperInvariant()} {level})";
            copy.GetSelectRules().First<SelectRule>().Attributes.RequiredLevel = level;
            copy.GetSelectRules().First<SelectRule>().RenewIdentifier();
            copy.GetSelectRules().First<SelectRule>().ElementHeader = copy.ElementHeader;
            copy.Supports.Add("Improvement Option");
            copy.Supports.Add(className);
            copy.Supports.Add(level.ToString());
            copy.IsExtended = true;
            copy.IncludeInCompendium = false;
            asiElements.Add(copy);
          }
        }
        this.ElementsCollection.AddRange((IEnumerable<ElementBase>) asiElements);
        processedClassNames.Add(@class.Name);
      }
    }

    // Generate spell scrolls.
    SpellScrollContentGenerator scrollGen = new SpellScrollContentGenerator();
    ElementBase scrollTemplate = this.ElementsCollection
      .FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id.Equals("ID_WOTC_DMG_MAGIC_ITEM_SPELL_SCROLL_CANTRIP")));
    MagicItemElement magicTemplate = scrollTemplate as MagicItemElement;
    List<ElementBase> scrolls = scrollGen.Generate(
      (IEnumerable<ElementBase>) this.ElementsCollection, magicTemplate);
    this.ElementsCollection.AddRange((IEnumerable<ElementBase>) scrolls);

    // Generate internal derived elements.
    InternalElementsGenerator internalGen = new InternalElementsGenerator();
    this.ElementsCollection.AddRange(
      (IEnumerable<ElementBase>) internalGen.GenerateInternalFeats((IEnumerable<ElementBase>) this.ElementsCollection));
    this.ElementsCollection.AddRange(
      (IEnumerable<ElementBase>) internalGen.GenerateInternalLanguages((IEnumerable<ElementBase>) this.ElementsCollection));
    this.ElementsCollection.AddRange(
      (IEnumerable<ElementBase>) internalGen.GenerateInternalProficiency((IEnumerable<ElementBase>) this.ElementsCollection));
    this.ElementsCollection.AddRange(
      (IEnumerable<ElementBase>) internalGen.GenerateInternalAsi((IEnumerable<ElementBase>) this.ElementsCollection));
    this.ElementsCollection.AddRange(
      (IEnumerable<ElementBase>) internalGen.GenerateInternalSpells((IEnumerable<ElementBase>) this.ElementsCollection));
    if (Debugger.IsAttached || ApplicationContext.Current.IsInDeveloperMode)
      this.ElementsCollection.AddRange(
        (IEnumerable<ElementBase>) internalGen.GenerateInternalIgnore((IEnumerable<ElementBase>) this.ElementsCollection));

    this.InitializeItemDetails(this.ElementsCollection);
    this.IsElementsCollectionPopulated = true;
    this._eventAggregator.Send<ElementsCollectionPopulatedEvent>(new ElementsCollectionPopulatedEvent());
  }

  [Obsolete("this method creates copies which is not used yet")]
  public IEnumerable<ElementBase> GetDataElements()
  {
    return (IEnumerable<ElementBase>) this.ElementsCollection.Select<ElementBase, ElementBase>((Func<ElementBase, ElementBase>) (element => element.Copy<ElementBase>())).ToList<ElementBase>();
  }

  public CharacterFile LoadCharacterFile(string filepath)
  {
    CharacterFile characterFile = new CharacterFile(filepath);
    characterFile.InitializeDisplayPropertiesFromFilePath();
    return characterFile;
  }

  public IEnumerable<CharacterFile> LoadCharacterFiles()
  {
    List<FileInfo> files = DataManager.GetFiles(this.UserDocumentsRootDirectory, "*.dnd5e", false);
    foreach (string characterFileLocation in this.GetCharacterFileLocations())
    {
      FileInfo fileInfo = new FileInfo(characterFileLocation);
      if (fileInfo.Exists && !files.Select<FileInfo, string>((Func<FileInfo, string>) (x => x.FullName)).Contains<string>(fileInfo.FullName))
        files.Add(new FileInfo(characterFileLocation));
    }
    List<CharacterFile> characterFileList = new List<CharacterFile>();
    foreach (FileInfo fileInfo in files)
    {
      try
      {
        characterFileList.Add(this.LoadCharacterFile(fileInfo.FullName));
      }
      catch (Exception ex)
      {
        Logger.Exception(ex, nameof (LoadCharacterFiles));
        MessageDialogContext.Current?.ShowException(ex);
      }
    }
    return (IEnumerable<CharacterFile>) characterFileList;
  }

  public string GetCombinedCharacterFilePath(string filename, string directory = null)
  {
    foreach (char invalidFileNameChar in Path.GetInvalidFileNameChars())
      filename = filename.Replace(invalidFileNameChar.ToString(), "");
    return directory == null ? Path.Combine(this.UserDocumentsRootDirectory, filename + ".dnd5e") : Path.Combine(directory, filename + ".dnd5e");
  }

  private List<string> GetCharacterFileLocations()
  {
    string path = Path.Combine(this.LocalAppDataRootDirectory, "characters.aurora");
    return File.Exists(path) ? ((IEnumerable<string>) File.ReadAllLines(path)).ToList<string>() : new List<string>();
  }

  private void SaveCharacterFileLocations(IEnumerable<string> paths)
  {
    File.WriteAllLines(Path.Combine(this.LocalAppDataRootDirectory, "characters.aurora"), paths);
  }

  public void AppendCharacterFileLocation(string path)
  {
    List<string> characterFileLocations = this.GetCharacterFileLocations();
    if (!characterFileLocations.Contains(path))
      characterFileLocations.Add(path);
    this.SaveCharacterFileLocations((IEnumerable<string>) characterFileLocations);
  }

  public void RemoveNonExistingCharacterFileLocations()
  {
    List<string> characterFileLocations = this.GetCharacterFileLocations();
    foreach (string path in characterFileLocations.ToList<string>())
    {
      if (!File.Exists(path))
        characterFileLocations.Remove(path);
    }
    this.SaveCharacterFileLocations((IEnumerable<string>) characterFileLocations);
  }

  public string GetResourceWebDocument(string resourceFilename)
  {
    string name = "Builder.Presentation.Resources.Documents." + resourceFilename;
    using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
    {
      if (manifestResourceStream == null)
        return (string) null;
      using (StreamReader streamReader = new StreamReader(manifestResourceStream, Encoding.Default))
        return streamReader.ReadToEnd();
    }
  }

  public void CopyElementsFromResources()
  {
    foreach (string name in ((IEnumerable<string>) Assembly.GetExecutingAssembly().GetManifestResourceNames()).Where<string>((Func<string, bool>) (x => x.StartsWith("Builder.Presentation.Resources.Data.ApplicationElements"))))
    {
      Logger.Info("Getting Resource Stream: {0}", (object) name);
      using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
      {
        if (manifestResourceStream != null)
        {
          using (StreamReader streamReader = new StreamReader(manifestResourceStream, Encoding.Default))
          {
            string end = streamReader.ReadToEnd();
            File.WriteAllText(Path.Combine(this.LocalAppDataApplicationElementsDirectory, name.Replace("Builder.Presentation.Resources.Data.ApplicationElements.", "")), end);
          }
        }
      }
    }
  }

  public void CopyPortraitsFromResources()
  {
    Assembly executingAssembly = Assembly.GetExecutingAssembly();
    foreach (string name in ((IEnumerable<string>) executingAssembly.GetManifestResourceNames()).Where<string>((Func<string, bool>) (x => x.StartsWith("Builder.Presentation.Resources.SamplePortraits"))))
    {
      string path2 = name.Replace("Builder.Presentation.Resources.SamplePortraits.", "");
      string str = Path.Combine(this.UserDocumentsPortraitsDirectory, path2);
      if (!File.Exists(str))
      {
        using (Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(name))
        {
          if (manifestResourceStream != null)
          {
            DataManager.CopyStreamToFile(manifestResourceStream, str);
            Logger.Info("Embedded portrait image '{0}' copied to the portraits directory", (object) path2);
          }
        }
      }
    }
    this.CopyNewPortraitsFromResources();
  }

  public void CopyNewPortraitsFromResources()
  {
    Assembly executingAssembly = Assembly.GetExecutingAssembly();
    foreach (string name in ((IEnumerable<string>) executingAssembly.GetManifestResourceNames()).Where<string>((Func<string, bool>) (x => x.StartsWith("Builder.Presentation.Resources.Portraits"))))
    {
      string path2 = name.Replace("Builder.Presentation.Resources.Portraits.", "");
      string str = Path.Combine(this.UserDocumentsPortraitsDirectory, path2);
      if (!File.Exists(str))
      {
        using (Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(name))
        {
          if (manifestResourceStream != null)
          {
            DataManager.CopyStreamToFile(manifestResourceStream, str);
            Logger.Info("Embedded portrait image '{0}' copied to the portraits directory", (object) path2);
          }
        }
      }
    }
  }

  public void CopyCompanionPortraitsFromResources()
  {
    Assembly executingAssembly = Assembly.GetExecutingAssembly();
    foreach (string name in ((IEnumerable<string>) executingAssembly.GetManifestResourceNames()).Where<string>((Func<string, bool>) (x => x.StartsWith("Builder.Presentation.Resources.Gallery.CompanionPortraits"))))
    {
      string path2 = name.Replace("Builder.Presentation.Resources.Gallery.CompanionPortraits.", "");
      string str = Path.Combine(this.UserDocumentsCompanionGalleryDirectory, path2);
      if (!File.Exists(str))
      {
        using (Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(name))
        {
          if (manifestResourceStream != null)
          {
            DataManager.CopyStreamToFile(manifestResourceStream, str);
            Logger.Info("Embedded companion portrait image '{0}' copied to the portraits directory", (object) path2);
          }
        }
      }
    }
  }

  public void CopySymbolsFromResources()
  {
    Assembly executingAssembly = Assembly.GetExecutingAssembly();
    foreach (string name in ((IEnumerable<string>) executingAssembly.GetManifestResourceNames()).Where<string>((Func<string, bool>) (x => x.StartsWith("Builder.Presentation.Resources.Gallery.OrganizationSymbols"))))
    {
      string path2 = name.Replace("Builder.Presentation.Resources.Gallery.OrganizationSymbols.", "");
      string str = Path.Combine(this.UserDocumentsSymbolsGalleryDirectory, path2);
      if (!File.Exists(str))
      {
        using (Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(name))
        {
          if (manifestResourceStream != null)
          {
            DataManager.CopyStreamToFile(manifestResourceStream, str);
            Logger.Info("Embedded organization symbol image '{0}' copied to the gallery directory", (object) path2);
          }
        }
      }
    }
  }

  public void CopyDragonmarksFromResources()
  {
    Assembly executingAssembly = Assembly.GetExecutingAssembly();
    foreach (string name in ((IEnumerable<string>) executingAssembly.GetManifestResourceNames()).Where<string>((Func<string, bool>) (x => x.StartsWith("Builder.Presentation.Resources.Gallery.Dragonmarks"))))
    {
      string path2 = name.Replace("Builder.Presentation.Resources.Gallery.Dragonmarks.", "");
      string str = Path.Combine(this.UserDocumentsSymbolsGalleryDirectory, path2);
      if (!File.Exists(str))
      {
        using (Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(name))
        {
          if (manifestResourceStream != null)
          {
            DataManager.CopyStreamToFile(manifestResourceStream, str);
            Logger.Info("Embedded dragonmark image '{0}' copied to the gallery directory", (object) path2);
          }
        }
      }
    }
  }

  [Obsolete]
  private void CopySpellcardBackground(Assembly assembly)
  {
    string filename = Path.Combine(this.LocalAppDataRootDirectory, "spellcard-background.jpg");
    using (Stream manifestResourceStream = assembly.GetManifestResourceStream("Builder.Presentation.Resources.spellcard-background.jpg"))
    {
      if (manifestResourceStream == null)
        return;
      DataManager.CopyStreamToFile(manifestResourceStream, filename);
      Logger.Info("Embedded spellcard background image '{0}' copied to the local app directory", (object) "spellcard-background.jpg");
    }
  }

  private static void CopyStreamToFile(Stream source, string path)
  {
    using Stream fileStream = File.Create(path);
    source.CopyTo(fileStream);
  }

  public List<XmlDocument> LoadElementDocumentsFromResource()
  {
    Assembly executingAssembly = Assembly.GetExecutingAssembly();
    IEnumerable<string> strings = ((IEnumerable<string>) executingAssembly.GetManifestResourceNames()).Where<string>((Func<string, bool>) (x => x.StartsWith("Builder.Presentation.Resources.Data")));
    List<XmlDocument> xmlDocumentList = new List<XmlDocument>();
    foreach (string name in strings)
    {
      if (!name.EndsWith(".xml"))
        Logger.Warning("loading non-elements resource file: '{0}'", (object) name);
      using (Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(name))
      {
        if (manifestResourceStream != null)
        {
          XmlDocument xmlDocument = new XmlDocument();
          xmlDocument.Load(manifestResourceStream);
          xmlDocumentList.Add(xmlDocument);
        }
      }
    }
    return xmlDocumentList;
  }

  private static List<FileInfo> GetFiles(
    string directory,
    string pattern = "*.*",
    bool includeSubdirectories = true)
  {
    Logger.Info("getting files from {0} using the {1} pattern", (object) directory, (object) pattern);
    List<FileInfo> files = new List<FileInfo>();
    files.AddRange(((IEnumerable<string>) Directory.GetFiles(directory, pattern)).Select<string, FileInfo>((Func<string, FileInfo>) (f => new FileInfo(f))));
    if (includeSubdirectories)
    {
      foreach (string directory1 in Directory.GetDirectories(directory))
        files.AddRange((IEnumerable<FileInfo>) DataManager.GetFiles(directory1, pattern));
    }
    return files;
  }

  private List<FileInfo> GetCustomFiles()
  {
    List<FileInfo> all = this.GetCustomFiles(this.UserDocumentsCustomElementsDirectory);

    // Current content root plus explicitly configured additional content directories.
    // The legacy single-directory setting is intentionally ignored so stale defaults do not
    // continue influencing content after the active content root has moved.
    foreach (string dir in ApplicationContext.Current.Settings.AdditionalCustomDirectories)
    {
      if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
      if (dir.Equals(this.UserDocumentsCustomElementsDirectory, StringComparison.OrdinalIgnoreCase)) continue;
      all.AddRange(this.GetCustomFiles(dir));
    }

    return all;
  }

  private List<FileInfo> GetCustomFiles(string path)
  {
    try
    {
      List<FileInfo> files = DataManager.GetFiles(path, "*.xml");
      List<FileInfo> includedFiles = new List<FileInfo>();
      List<FileInfo> list = files.Where<FileInfo>((Func<FileInfo, bool>) (x => !DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "ignore")))).ToList<FileInfo>();
      includedFiles.AddRange(list.Where<FileInfo>((Func<FileInfo, bool>) (x => DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "srd")))));
      includedFiles.AddRange(list.Where<FileInfo>((Func<FileInfo, bool>) (x => DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "system-reference-document")))));
      list.RemoveAll((Predicate<FileInfo>) (info => includedFiles.Contains(info)));
      includedFiles.AddRange(list.Where<FileInfo>((Func<FileInfo, bool>) (x => DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "core")))));
      includedFiles.AddRange(list.Where<FileInfo>((Func<FileInfo, bool>) (x => DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "supplements")))));
      includedFiles.AddRange(list.Where<FileInfo>((Func<FileInfo, bool>) (x => DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "unearthed-arcana")))));
      includedFiles.AddRange(list.Where<FileInfo>((Func<FileInfo, bool>) (x => DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "third-party")))));
      includedFiles.AddRange(list.Where<FileInfo>((Func<FileInfo, bool>) (x => DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "homebrew")))));
      list.RemoveAll((Predicate<FileInfo>) (info => includedFiles.Contains(info)));
      List<FileInfo> user = list.Where<FileInfo>((Func<FileInfo, bool>) (x => x.Directory != null && DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "user")) && x.Directory.Name.Equals("user"))).ToList<FileInfo>();
      list.RemoveAll((Predicate<FileInfo>) (info => user.Contains(info)));
      List<FileInfo> userIndices = list.Where<FileInfo>((Func<FileInfo, bool>) (x => x.Directory != null && DataManager.IsPathInDirectory(x.FullName, Path.Combine(path, "user")))).ToList<FileInfo>();
      list.RemoveAll((Predicate<FileInfo>) (info => userIndices.Contains(info)));
      List<FileInfo> root = list.Where<FileInfo>((Func<FileInfo, bool>) (x => x.DirectoryName != null && DataManager.PathsEqual(x.DirectoryName, path))).ToList<FileInfo>();
      list.RemoveAll((Predicate<FileInfo>) (info => root.Contains(info)));
      includedFiles.AddRange((IEnumerable<FileInfo>) list);
      list.RemoveAll((Predicate<FileInfo>) (info => includedFiles.Contains(info)));
      includedFiles.AddRange((IEnumerable<FileInfo>) userIndices);
      includedFiles.AddRange((IEnumerable<FileInfo>) root);
      includedFiles.AddRange((IEnumerable<FileInfo>) user);
      int count1 = includedFiles.Count;
      int count2 = files.Count;
      return includedFiles;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (GetCustomFiles));
    }
    Logger.Warning("Getting all files and do not ignore any files or return them in any order");
    return DataManager.GetFiles(path, "*.xml");
  }

  private static void CreateDirectory(string directoryName)
  {
    if (Directory.Exists(directoryName))
      return;
    Logger.Info("Creating Directory: '{0}'", (object) directoryName);
    Directory.CreateDirectory(directoryName);
  }

  private static async Task<XmlDocument> CreateXmlDocument(string filepath)
  {
    XmlDocument xmlDocument1;
    using (StreamReader reader = new StreamReader(filepath))
    {
      string endAsync = await reader.ReadToEndAsync();
      XmlDocument xmlDocument2 = new XmlDocument();
      xmlDocument2.LoadXml(endAsync);
      xmlDocument1 = xmlDocument2;
    }
    return xmlDocument1;
  }

  public static int GetPercentage(double count, double totalCount)
  {
    return (int) Math.Round(100.0 * count / totalCount);
  }

  private static bool IsPathInDirectory(string filePath, string directoryPath)
  {
    string str1 = DataManager.NormalizePath(filePath);
    string str2 = DataManager.NormalizePath(directoryPath) + Path.DirectorySeparatorChar;
    return str1.StartsWith(str2, StringComparison.OrdinalIgnoreCase);
  }

  private static string NormalizePath(string path)
  {
    return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
  }

  private static bool PathsEqual(string left, string right)
  {
    return string.Equals(DataManager.NormalizePath(left), DataManager.NormalizePath(right), StringComparison.OrdinalIgnoreCase);
  }

  private void AppendElements(
    IEnumerable<XmlNode> appendNodes,
    ElementBaseCollection coreElements,
    ElementParser elementParser,
    ElementParser defaultParser,
    List<ElementParser> elementParserCollection)
  {
    foreach (XmlNode appendNode in appendNodes)
    {
      AuroraXmlCompatibilityRepair.RepairNode(appendNode);
      if (appendNode.ContainsAttribute("id"))
      {
        string appendId = appendNode.GetAttributeValue("id");
        string appendType = appendNode.ContainsAttribute("type") ? appendNode.GetAttributeValue("type") : "";
        if (elementParser.ParserType != appendType)
          elementParser = elementParserCollection.FirstOrDefault<ElementParser>((Func<ElementParser, bool>) (p => p.ParserType == appendType)) ?? defaultParser;
        ElementBase elementBase = coreElements.FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Id == appendId));
        if (elementBase == null)
        {
          Logger.Warning("unable to extend non existing element: " + appendId);
        }
        else
        {
          try
          {
            ElementBase element = elementParser.ParseElement(appendNode, elementBase.ElementHeader);
            bool flag = false;
            if (element.HasSupports)
            {
              foreach (string support in element.Supports)
              {
                if (!elementBase.Supports.Contains(support))
                  elementBase.Supports.Add(support);
              }
              flag = true;
            }
            if (element.ElementSetters.Any<ElementSetters.Setter>())
            {
              foreach (ElementSetters.Setter elementSetter in (List<ElementSetters.Setter>) element.ElementSetters)
              {
                if (!elementBase.ElementSetters.ContainsSetter(elementSetter.Name))
                  elementBase.ElementSetters.Add(elementSetter);
              }
              flag = true;
            }
            if (element.HasRules)
            {
              elementBase.Rules.AddRange((IEnumerable<RuleBase>) element.Rules);
              flag = true;
            }
            if (element.HasSpellcastingInformation)
            {
              if (!elementBase.HasSpellcastingInformation)
              {
                elementBase.SpellcastingInformation = element.SpellcastingInformation;
                flag = true;
              }
              else
                Logger.Warning("unable to extend spellcasting on existing spellcasting at: " + appendId);
            }
            if (flag)
              elementBase.IsExtended = true;
          }
          catch (Exception ex)
          {
            Console.WriteLine((object) ex);
            throw;
          }
        }
      }
    }
  }
}
