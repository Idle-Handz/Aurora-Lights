// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Telemetry.AnalyticsEventHelper
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Presentation.Services;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Telemetry;

public static class AnalyticsEventHelper
{
  private static string FormatEventName(string input) => input.Replace(" ", "_").ToLowerInvariant();

  private static string GetEventNameAddition(string name)
  {
    string eventNameAddition = "";
    StringComparison comparisonType = StringComparison.InvariantCultureIgnoreCase;
    if (name.StartsWith("a", comparisonType) || name.StartsWith("b", comparisonType) || name.StartsWith("c", comparisonType) || name.StartsWith("d", comparisonType) || name.StartsWith("e", comparisonType) || name.StartsWith("f", comparisonType) || name.StartsWith("g", comparisonType) || name.StartsWith("h", comparisonType) || name.StartsWith("i", comparisonType) || name.StartsWith("j", comparisonType) || name.StartsWith("k", comparisonType) || name.StartsWith("l", comparisonType) || name.StartsWith("m", comparisonType))
      eventNameAddition = "_1";
    else if (name.StartsWith("n", comparisonType) || name.StartsWith("o", comparisonType) || name.StartsWith("p", comparisonType) || name.StartsWith("q", comparisonType) || name.StartsWith("r", comparisonType) || name.StartsWith("s", comparisonType) || name.StartsWith("t", comparisonType) || name.StartsWith("u", comparisonType) || name.StartsWith("v", comparisonType) || name.StartsWith("w", comparisonType) || name.StartsWith("x", comparisonType) || name.StartsWith("y", comparisonType) || name.StartsWith("z", comparisonType))
      eventNameAddition = "_2";
    return eventNameAddition;
  }

  private static string GetNamePropertyAddition(string name)
  {
    string propertyAddition = "";
    StringComparison comparisonType = StringComparison.InvariantCultureIgnoreCase;
    if (name != null)
    {
      string str1 = name;
      if (str1.StartsWith("a", comparisonType) || str1.StartsWith("b", comparisonType))
      {
        propertyAddition += "_ab";
      }
      else
      {
        string str2 = name;
        if (str2.StartsWith("c", comparisonType) || str2.StartsWith("d", comparisonType))
        {
          propertyAddition += "_cd";
        }
        else
        {
          string str3 = name;
          if (str3.StartsWith("e", comparisonType) || str3.StartsWith("f", comparisonType))
          {
            propertyAddition += "_ef";
          }
          else
          {
            string str4 = name;
            if (str4.StartsWith("g", comparisonType) || str4.StartsWith("h", comparisonType))
            {
              propertyAddition += "_gh";
            }
            else
            {
              string str5 = name;
              if (str5.StartsWith("i", comparisonType) || str5.StartsWith("j", comparisonType))
              {
                propertyAddition += "_ij";
              }
              else
              {
                string str6 = name;
                if (str6.StartsWith("k", comparisonType) || str6.StartsWith("l", comparisonType))
                {
                  propertyAddition += "_kl";
                }
                else
                {
                  string str7 = name;
                  if (str7.StartsWith("m", comparisonType) || str7.StartsWith("n", comparisonType))
                  {
                    propertyAddition += "_mn";
                  }
                  else
                  {
                    string str8 = name;
                    if (str8.StartsWith("o", comparisonType) || str8.StartsWith("p", comparisonType))
                    {
                      propertyAddition += "_op";
                    }
                    else
                    {
                      string str9 = name;
                      if (str9.StartsWith("q", comparisonType) || str9.StartsWith("r", comparisonType))
                      {
                        propertyAddition += "_qr";
                      }
                      else
                      {
                        string str10 = name;
                        if (str10.StartsWith("s", comparisonType) || str10.StartsWith("t", comparisonType))
                        {
                          propertyAddition += "_st";
                        }
                        else
                        {
                          string str11 = name;
                          if (str11.StartsWith("u", comparisonType) || str11.StartsWith("v", comparisonType))
                          {
                            propertyAddition += "_uv";
                          }
                          else
                          {
                            string str12 = name;
                            if (str12.StartsWith("w", comparisonType) || str12.StartsWith("x", comparisonType))
                            {
                              propertyAddition += "_wx";
                            }
                            else
                            {
                              string str13 = name;
                              if (str13.StartsWith("y", comparisonType) || str13.StartsWith("z", comparisonType))
                                propertyAddition += "_yz";
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
    return propertyAddition;
  }

  public static void ApplicationEvent(string name)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("application_" + name));
  }

  public static void ApplicationEvent(string name, string key, string value)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("application_" + name), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        key,
        value
      }
    });
  }

  public static void ApplicationStartupEvent(string name)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("application_startup_" + name));
  }

  public static void EquipmentAdd(
    string category,
    string name,
    string source,
    string categorySubType = null)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName(string.IsNullOrWhiteSpace(categorySubType) ? "equipment_add_" + category : $"equipment_add_{category}_{categorySubType}"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        "item" + AnalyticsEventHelper.GetNamePropertyAddition(name),
        name
      },
      {
        nameof (source),
        source
      }
    });
  }

  public static void EquipmentBuy(
    string category,
    string name,
    string source,
    string categorySubType = null)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName(string.IsNullOrWhiteSpace(categorySubType) ? "equipment_buy_" + category : $"equipment_buy_{category}_{categorySubType}"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        "item" + AnalyticsEventHelper.GetNamePropertyAddition(name),
        name
      },
      {
        nameof (source),
        source
      }
    });
  }

  public static void CharacterCreate(string level, string abilityScoreOption)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_create"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (level),
        level
      },
      {
        "ability generation",
        abilityScoreOption
      }
    });
  }

  public static void CharacterSave(
    string characterRace,
    string characterClass,
    string characterBackground,
    string characterLevel,
    bool multiclass,
    bool spellcaster,
    bool companion)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_save"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        "race",
        characterRace
      },
      {
        "class",
        characterClass
      },
      {
        "background",
        characterBackground
      },
      {
        "level",
        characterLevel
      },
      {
        nameof (multiclass),
        multiclass ? "true" : "false"
      },
      {
        nameof (spellcaster),
        spellcaster ? "true" : "false"
      },
      {
        nameof (companion),
        companion ? "true" : "false"
      }
    });
    AnalyticsEventHelper.CharacterSaveRace(characterRace);
    AnalyticsEventHelper.CharacterSaveClass(characterClass);
    AnalyticsEventHelper.CharacterSaveBackground(characterBackground);
  }

  public static void CharacterLoad(bool fromFile = false, bool fromNewFile = false)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_load"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (fromFile),
        fromFile ? "true" : "false"
      },
      {
        nameof (fromNewFile),
        fromNewFile ? "true" : "false"
      }
    });
  }

  public static void CharacterSaveRace(string characterRace)
  {
    if (string.IsNullOrWhiteSpace(characterRace))
      return;
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_save_race"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        "race" + AnalyticsEventHelper.GetNamePropertyAddition(characterRace),
        characterRace
      }
    });
  }

  public static void CharacterSaveClass(string characterClass)
  {
    if (string.IsNullOrWhiteSpace(characterClass))
      return;
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_save_class"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        "class" + AnalyticsEventHelper.GetNamePropertyAddition(characterClass),
        characterClass
      }
    });
    AnalyticsEventHelper.CharacterSaveArchetypeClass(characterClass);
  }

  public static void CharacterSaveArchetypeClass(string characterClass)
  {
    if (string.IsNullOrWhiteSpace(characterClass))
      return;
    try
    {
      ClassProgressionManager progressionManager = CharacterManager.Current.ClassProgressionManagers.FirstOrDefault<ClassProgressionManager>((Func<ClassProgressionManager, bool>) (x => x.ClassElement.Name == characterClass));
      if (!progressionManager.HasArchetype())
        return;
      ElementBase elementBase = progressionManager.GetElements().FirstOrDefault<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Archetype")));
      if (elementBase == null)
        return;
      Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_save_archetype"), (IDictionary<string, string>) new Dictionary<string, string>()
      {
        {
          "archetype" + AnalyticsEventHelper.GetNamePropertyAddition(elementBase.Name),
          elementBase.Name
        }
      });
    }
    catch (Exception)
    {
    }
  }

  public static void CharacterSaveBackground(string background)
  {
    if (string.IsNullOrWhiteSpace(background))
      return;
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_save_background"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (background) + AnalyticsEventHelper.GetNamePropertyAddition(background),
        background
      }
    });
  }

  public static void CharacterSheetPreview(
    bool fillable,
    bool formatted,
    bool itemCards,
    bool attackCards,
    bool spellCards,
    bool featureCards,
    bool legacySpellcasting)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_sheet_preview"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (fillable),
        fillable ? "true" : "false"
      },
      {
        nameof (formatted),
        formatted ? "true" : "false"
      },
      {
        nameof (itemCards),
        itemCards ? "true" : "false"
      },
      {
        nameof (attackCards),
        attackCards ? "true" : "false"
      },
      {
        nameof (spellCards),
        spellCards ? "true" : "false"
      },
      {
        nameof (featureCards),
        featureCards ? "true" : "false"
      },
      {
        nameof (legacySpellcasting),
        legacySpellcasting ? "true" : "false"
      }
    });
  }

  public static void CharacterSheetSave(
    bool fillable,
    bool formatted,
    bool itemCards,
    bool attackCards,
    bool spellCards,
    bool featureCards,
    bool legacySpellcasting)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("character_sheet_save"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (fillable),
        fillable ? "true" : "false"
      },
      {
        nameof (formatted),
        formatted ? "true" : "false"
      },
      {
        nameof (itemCards),
        itemCards ? "true" : "false"
      },
      {
        nameof (attackCards),
        attackCards ? "true" : "false"
      },
      {
        nameof (spellCards),
        spellCards ? "true" : "false"
      },
      {
        nameof (featureCards),
        featureCards ? "true" : "false"
      },
      {
        nameof (legacySpellcasting),
        legacySpellcasting ? "true" : "false"
      }
    });
  }

  public static void SyndicationView(string url)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("syndication_view"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (url),
        url
      }
    });
  }

  public static void CompendiumSearch(string criteria)
  {
    if (string.IsNullOrWhiteSpace(criteria))
      return;
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("compendium_search" + AnalyticsEventHelper.GetEventNameAddition(criteria)), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        "criteria_" + criteria[0].ToString(),
        criteria
      }
    });
  }

  public static void CompendiumSearchTrackDetailed(string criteria)
  {
    if (string.IsNullOrWhiteSpace(criteria))
      return;
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("compendium_search_" + criteria[0].ToString()), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (criteria),
        criteria
      }
    });
  }

  public static void DescriptionPanelSnap(string elementName)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("description_panel_snap"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        "name",
        elementName
      }
    });
  }

  public static void DescriptionPanelReadAloud(string elementName)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("description_panel_read_aloud"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        "name",
        elementName
      }
    });
  }

  public static void SourcesCompendiumLookup(string source)
  {
    if (string.IsNullOrWhiteSpace(source))
      return;
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("sources_compendium_lookup"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (source) + AnalyticsEventHelper.GetNamePropertyAddition(source),
        source
      }
    });
  }

  public static void ContentDownloadIndex(string url, bool bundle = false)
  {
    if (string.IsNullOrWhiteSpace(url))
      return;
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("content_download_index"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (url),
        url
      },
      {
        nameof (bundle),
        bundle ? "true" : "false"
      }
    });
  }

  public static void ContentClear(bool confirmed)
  {
    Microsoft.AppCenter.Analytics.Analytics.TrackEvent(AnalyticsEventHelper.FormatEventName("content_clear"), (IDictionary<string, string>) new Dictionary<string, string>()
    {
      {
        nameof (confirmed),
        confirmed ? "true" : "false"
      }
    });
  }
}
