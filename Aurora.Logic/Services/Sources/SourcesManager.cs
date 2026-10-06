using Builder.Presentation.Services.Sources;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.Sources.SourcesManager
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Elements;
using Builder.Presentation.Models.Sources;

using Builder.Presentation.Services.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Services.Sources;

public class SourcesManager : ISourceRestrictionsProvider
{
  public const string WizardsGroupName = "Wizards of the Coast";
  public const string AdventurersLeagueGroupName = "Adventurers League";
  public const string UnearthedArcanaGroupName = "Unearthed Arcana";
  public const string ThirdPartyGroupName = "Third Party";
  public const string HomebrewGroupName = "Homebrew";
  public const string UndefinedSources = "Undefined Sources";
  public const string RequiredGroupName = "Required builder sources";

  public SourcesManager() => this.InitializeSources();

  public event EventHandler SourceRestrictionsApplied;

  public ObservableCollection<SourcesGroup> SourceGroups { get; } = new ObservableCollection<SourcesGroup>();

  public ObservableCollection<SourceItem> SourceItems { get; } = new ObservableCollection<SourceItem>();

  public ObservableCollection<SourceItem> RestrictedSources { get; } = new ObservableCollection<SourceItem>();

  public void ApplyRestrictions(bool reprocess = false)
  {
    this.RestrictedSources.Clear();
    foreach (SourceItem source in this.GetUncheckedSources())
      this.RestrictedSources.Add(source);
    ApplicationContext.Current.SendStatusMessage("Your source restrictions have been updated.");
    this.OnSourceRestrictionsApplied();
    if (!reprocess)
      return;
    CharacterManager.Current.ReprocessCharacter();
  }

  public void ClearRestrictions(bool apply = true, bool reprocess = false)
  {
    foreach (SourcesGroup sourceGroup in (Collection<SourcesGroup>) this.SourceGroups)
      sourceGroup.SetIsChecked(new bool?(true), true);
    if (!apply)
      return;
    this.ApplyRestrictions(reprocess);
  }

  public IEnumerable<string> GetUndefinedRestrictedSourceNames()
  {
    return this.RestrictedSources.Where<SourceItem>((Func<SourceItem, bool>) (x => x.Parent.Name.Equals("Undefined Sources", StringComparison.OrdinalIgnoreCase))).Select<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.Name));
  }

  public IEnumerable<string> GetRestrictedElementIds()
  {
    List<string> restrictedElementIds = new List<string>();
    foreach (SourceItem restrictedSource in (Collection<SourceItem>) this.RestrictedSources)
      restrictedElementIds.AddRange(restrictedSource.Elements.Select<ElementHeader, string>((Func<ElementHeader, string>) (x => x.Id)));
    return (IEnumerable<string>) restrictedElementIds;
  }

  /// <summary>
  /// Rebuilds the list from the catalog as it stands now. The list is a snapshot taken when this
  /// manager is constructed, which for <see cref="CharacterManager.Current"/> is whenever something
  /// first touches the singleton - possibly before content has finished loading, which leaves every
  /// source missing, and always before a content refresh replaces the catalog. The manager itself is
  /// kept so that anything listening to <see cref="SourceRestrictionsApplied"/> stays attached, and
  /// whatever is restricted now is re-applied to the rebuilt list.
  /// </summary>
  public void Refresh()
  {
    List<string> restricted = this.RestrictedSources.Select<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.Id)).ToList<string>();
    this.RestrictedSources.Clear();
    this.SourceItems.Clear();
    this.SourceGroups.Clear();
    this.InitializeSources();
    if (restricted.Count > 0)
      this.Load((IEnumerable<string>) restricted);
  }

  private void InitializeSources()
  {
    foreach (SourceItem sourceItem in DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Source", StringComparison.OrdinalIgnoreCase))).Cast<Source>().OrderBy<Source, string>((Func<Source, string>) (x => x.ReleaseDate)).ThenBy<Source, string>((Func<Source, string>) (x => x.Name)).Select<Source, SourceItem>((Func<Source, SourceItem>) (x => new SourceItem(x.CopyWithDetachedXml()))))
      this.SourceItems.Add(sourceItem);
    // Internal/Core often have only source labels, without their own Source declaration.
    // Show the infrastructure actually present in the catalog instead of hiding those labels.
    foreach (string name in DataManager.Current.ElementsCollection.Select(element => element.Source)
      .Where(name => RequiredContentPolicy.IsRequiredSource(name)).Select(name => name.Trim())
      .Distinct(StringComparer.OrdinalIgnoreCase))
    {
      if (!this.SourceItems.Any(item => string.Equals(item.Source.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        this.SourceItems.Add(new SourceItem(new Source
        {
          ElementHeader = new ElementHeader(name, "Source", name, name),
          Description = "<p>Required builder infrastructure. Always enabled.</p>"
        }));
    }
    foreach (SourcesGroup group in this.CreateGroups())
      this.SourceGroups.Add(group);
    Logger.Info("sources: listing {0} source(s) from {1} element(s)", (object) this.SourceItems.Count, (object) DataManager.Current.ElementsCollection.Count);
  }

  private IEnumerable<SourcesGroup> CreateGroups()
  {
    List<SourcesGroup> groups = new List<SourcesGroup>();
    SourcesGroup sourcesGroup1 = new SourcesGroup("Wizards of the Coast");
    SourcesGroup sourcesGroup2 = new SourcesGroup("Adventurers League");
    SourcesGroup sourcesGroup3 = new SourcesGroup("Unearthed Arcana");
    SourcesGroup sourcesGroup4 = new SourcesGroup("Third Party");
    SourcesGroup sourcesGroup5 = new SourcesGroup("Homebrew");
    SourcesGroup undefinedGroup = new SourcesGroup("Undefined Sources");
    SourcesGroup requiredGroup = new SourcesGroup(RequiredGroupName, allowUnchecking: false);
    Queue<SourceItem> source1 = new Queue<SourceItem>();
    foreach (SourceItem sourceItem in (Collection<SourceItem>) this.SourceItems)
    {
      if (RequiredContentPolicy.IsRequiredSource(sourceItem.Source.Name, sourceItem.Source.Id))
      {
        requiredGroup.Sources.Add(sourceItem);
        continue;
      }
      if (sourceItem.Source.IsOfficialContent)
      {
        if (sourceItem.Source.IsAdventureLeagueContent)
          sourcesGroup2.Sources.Add(sourceItem);
        else if (sourceItem.Source.IsPlaytestContent && !sourceItem.Source.IsCoreContent && !sourceItem.Source.IsSupplementContent)
          sourcesGroup3.Sources.Add(sourceItem);
        else
          source1.Enqueue(sourceItem);
      }
      else if (sourceItem.Source.IsThirdPartyContent)
        sourcesGroup4.Sources.Add(sourceItem);
      else if (sourceItem.Source.IsHomebrewContent)
        sourcesGroup5.Sources.Add(sourceItem);
      else
        undefinedGroup.Sources.Add(sourceItem);
    }
    List<SourceItem> sourceItemList1 = new List<SourceItem>();
    List<SourceItem> sourceItemList2 = new List<SourceItem>();
    List<SourceItem> sourceItemList3 = new List<SourceItem>();
    while (source1.Any<SourceItem>())
    {
      SourceItem sourceItem = source1.Dequeue();
      if (sourceItem.Source.IsCoreContent)
      {
        // Core rulebooks remain selectable; only builder infrastructure is mandatory.
        sourceItemList1.Add(sourceItem);
      }
      else if (sourceItem.Source.IsSupplementContent)
        sourceItemList2.Add(sourceItem);
      else
        sourceItemList3.Add(sourceItem);
    }
    foreach (SourceItem sourceItem in sourceItemList1)
      sourcesGroup1.Sources.Add(sourceItem);
    foreach (SourceItem sourceItem in sourceItemList2)
      sourcesGroup1.Sources.Add(sourceItem);
    foreach (SourceItem sourceItem in sourceItemList3)
      sourcesGroup1.Sources.Add(sourceItem);
    if (requiredGroup.Sources.Any())
      groups.Add(requiredGroup);
    groups.Add(sourcesGroup1);
    if (sourcesGroup2.Sources.Any<SourceItem>())
      groups.Add(sourcesGroup2);
    if (sourcesGroup3.Sources.Any<SourceItem>())
      groups.Add(sourcesGroup3);
    if (sourcesGroup4.Sources.Any<SourceItem>())
    {
      List<SourceItem> list = sourcesGroup4.Sources.OrderBy<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.Author)).ThenBy<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.ReleaseDate)).ThenBy<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.Name)).ToList<SourceItem>();
      sourcesGroup4.Sources.Clear();
      foreach (SourceItem sourceItem in list)
        sourcesGroup4.Sources.Add(sourceItem);
      groups.Add(sourcesGroup4);
    }
    if (sourcesGroup5.Sources.Any<SourceItem>())
    {
      List<SourceItem> list = sourcesGroup5.Sources.OrderBy<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.Author)).ThenBy<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.ReleaseDate)).ThenBy<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.Name)).ToList<SourceItem>();
      sourcesGroup5.Sources.Clear();
      foreach (SourceItem sourceItem in list)
        sourcesGroup5.Sources.Add(sourceItem);
      groups.Add(sourcesGroup5);
    }
    this.GetUndefinedSourceNames(undefinedGroup);
    if (undefinedGroup.Sources.Any<SourceItem>())
      groups.Add(undefinedGroup);
    foreach (SourcesGroup parent in groups)
    {
      foreach (SourceItem source2 in (Collection<SourceItem>) parent.Sources)
        source2.SetParent(parent);
      parent.SetIsChecked(new bool?(true), true);
    }
    return (IEnumerable<SourcesGroup>) groups;
  }

  private IEnumerable<string> GetUndefinedSourceNames(SourcesGroup undefinedGroup)
  {
    IEnumerable<ElementBase> elementBases = DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => !x.Type.Equals("Source") && !x.Type.Equals("Internal") && !x.Type.Equals("Core") && !x.Type.Equals("Ability Score Improvement") && !x.Type.Equals("Level") && !x.Type.Equals("Multiclass") && !x.Type.Equals("Skill") && !x.Type.Equals("Support")));
    // Match the first declared source, as FirstOrDefault did, without scanning every
    // source twice for every catalog element during a refresh on the UI thread.
    var sourcesByName = new Dictionary<string, SourceItem>(StringComparer.OrdinalIgnoreCase);
    foreach (SourceItem item in this.SourceItems)
      if (item.Source.Name != null)
        sourcesByName.TryAdd(item.Source.Name, item);
    var undefinedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    List<string> source2 = new List<string>();
    foreach (ElementBase elementBase in elementBases)
    {
      string elementSourceName = elementBase.Source;
      if (!RequiredContentPolicy.IsRequiredSource(elementSourceName))
      {
        if (elementSourceName != null && sourcesByName.TryGetValue(elementSourceName, out SourceItem sourceItem))
          sourceItem.Elements.Add(elementBase.ElementHeader);
        else if (undefinedNames.Add(elementSourceName))
          source2.Add(elementSourceName);
      }
    }
    foreach (string str in source2)
    {
      Source source3 = new Source();
      source3.ElementHeader = new ElementHeader(str, "Source", str, str);
      source3.Author = "Missing Source Details";
      source3.Description = "<p>Create an element of type 'Source' so it can be classified with a proper description.</p>";
      Source source4 = source3;
      undefinedGroup.Sources.Add(new SourceItem(source4));
    }
    return (IEnumerable<string>) source2;
  }

  public void Load(IEnumerable<string> sources)
  {
    this.ClearRestrictions(false);
    foreach (SourcesGroup sourceGroup in (Collection<SourcesGroup>) this.SourceGroups)
    {
      foreach (SourceItem source in (Collection<SourceItem>) sourceGroup.Sources)
      {
        if (sources.Contains<string>(source.Source.Id))
          source.SetIsChecked(new bool?(false), false, true);
      }
    }
    this.ApplyRestrictions();
  }

  public void LoadDefaults()
  {
    try
    {
      string sourceRestrictions = ApplicationContext.Current.Settings.DefaultSourceRestrictions;
      if (string.IsNullOrWhiteSpace(sourceRestrictions))
      {
        // No defaults configured means every source is allowed, which is still a rule: anything
        // the previously loaded character restricted has to be released rather than left standing.
        if (this.RestrictedSources.Count > 0)
          this.ClearRestrictions();
        return;
      }
      this.Load((IEnumerable<string>) sourceRestrictions.Split(','));
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (LoadDefaults));
      MessageDialogContext.Current?.ShowException(ex);
    }
  }

  public void StoreDefaults()
  {
    ApplicationContext.Current.Settings.DefaultSourceRestrictions =
      string.Join(",", this.GetUncheckedSources().Select(source => source.Source.Id));
    ApplicationContext.Current.SendStatusMessage("Your default source restrictions have been saved.");
  }

  private IEnumerable<SourceItem> GetUncheckedSources() => this.SourceGroups
    .Where(group => group.AllowUnchecking)
    .SelectMany(group => group.Sources)
    .Where(source => source.AllowUnchecking && source.IsChecked == false);

  public IEnumerable<ElementBase> GetOrderedElements(IEnumerable<ElementBase> elements)
  {
    List<Source> list1 = this.SourceItems.Select<SourceItem, Source>((Func<SourceItem, Source>) (x => x.Source)).ToList<Source>();
    List<Source> list2 = list1.Where<Source>((Func<Source, bool>) (x => x.IsOfficialContent)).OrderBy<Source, int>((Func<Source, int>) (x => !x.IsCoreContent ? 1 : 0)).ThenBy<Source, string>((Func<Source, string>) (x => x.ReleaseDate)).ThenBy<Source, string>((Func<Source, string>) (x => x.Name)).ToList<Source>();
    list1.Where<Source>((Func<Source, bool>) (x => !x.IsOfficialContent)).ToList<Source>();
    ElementBaseCollection orderedElements = new ElementBaseCollection();
    foreach (Source source1 in list2)
    {
      Source source = source1;
      IEnumerable<ElementBase> elements1 = elements.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Source.Equals(source.Name)));
      orderedElements.AddRange(elements1);
    }
    foreach (ElementBase element in elements)
    {
      if (!orderedElements.Contains(element))
        orderedElements.Add(element);
    }
    if (orderedElements.Count != elements.Count<ElementBase>() && Debugger.IsAttached)
      Debugger.Break();
    return (IEnumerable<ElementBase>) orderedElements;
  }

  public IEnumerable<string> GetRestrictedSources()
  {
    return this.RestrictedSources.Select<SourceItem, string>((Func<SourceItem, string>) (x => x.Source.Name));
  }

  public IEnumerable<string> GetUndefinedRestrictedSources()
  {
    return this.GetUndefinedRestrictedSourceNames();
  }

  public IEnumerable<string> GetRestrictedElements() => this.GetRestrictedElementIds();

  protected virtual void OnSourceRestrictionsApplied()
  {
    EventHandler restrictionsApplied = this.SourceRestrictionsApplied;
    if (restrictionsApplied == null)
      return;
    restrictionsApplied((object) this, EventArgs.Empty);
  }
}
