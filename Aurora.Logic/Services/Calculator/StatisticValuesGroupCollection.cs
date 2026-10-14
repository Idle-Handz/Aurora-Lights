// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.Calculator.StatisticValuesGroupCollection
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Services.Calculator;

public class StatisticValuesGroupCollection : List<StatisticValuesGroup>
{
  public bool ContainsGroup(string groupName)
  {
    if (groupName == null)
      return false;
    if (groupName.StartsWith("-"))
      groupName = groupName.Substring(1);
    return this.Any(x => x.GroupName.Equals(groupName));
  }

  public void AddGroup(StatisticValuesGroup group)
  {
    StatisticValuesGroup group1 = this.GetGroup(group.GroupName, false);
    if (group1 != null)
    {
      foreach (KeyValuePair<string, int> keyValuePair in group.GetValues())
        group1.AddValue(keyValuePair.Key, keyValuePair.Value);
    }
    else
      this.Add(group);
  }

  public StatisticValuesGroup GetGroup(string groupName, bool createNonExisting = true)
  {
    if (groupName.StartsWith("-"))
      groupName = groupName.Substring(1, groupName.Length - 1);
    StatisticValuesGroup existing = this.SingleOrDefault(x => x.GroupName.Equals(groupName));
    if (existing != null)
      return existing;
    if (!createNonExisting)
      return (StatisticValuesGroup) null;
    StatisticValuesGroup group = new StatisticValuesGroup(groupName);
    this.Add(group);
    return group;
  }

  public int GetValue(string groupName)
  {
    return groupName == null ? 0 : this.GetGroup(groupName, false)?.Sum() ?? 0;
  }
}
