// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.Calculator.StatisticValuesGroup
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Services.Calculator;

public class StatisticValuesGroup
{
  private readonly Dictionary<string, int> _values;

  public StatisticValuesGroup(string groupName)
  {
    this.GroupName = groupName;
    this._values = new Dictionary<string, int>();
  }

  public string GroupName { get; }

  public bool IsFinalized { get; set; }

  public void AddValue(string source, int value)
  {
    if (this._values.ContainsKey(source))
      this._values[source] += value;
    else
      this._values.Add(source, value);
  }

  public bool ContainsValue(string source) => this._values.ContainsKey(source);

  [Obsolete]
  public int GetValue(string source)
  {
    return this.ContainsValue(source) ? this._values[source] : throw new KeyNotFoundException(source);
  }

  public Dictionary<string, int> GetValues() => this._values;

  public int Sum()
  {
    return this._values.Sum<KeyValuePair<string, int>>((Func<KeyValuePair<string, int>, int>) (x => x.Value));
  }

  public string GetSummery(bool includeValues = true)
  {
    if (this._values.Count == 0)
      return string.Empty;
    return includeValues ? string.Join(", ", this.GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => $"{x.Key} ({x.Value})"))) : string.Join(", ", this.GetValues().Select<KeyValuePair<string, int>, string>((Func<KeyValuePair<string, int>, string>) (x => x.Key ?? "")));
  }

  public override string ToString() => $"{this.GroupName} [{this.Sum()}]";

  public void Merge(StatisticValuesGroup group)
  {
    if (group == null)
      return;
    foreach (KeyValuePair<string, int> keyValuePair in group.GetValues())
      this.AddValue(keyValuePair.Key, keyValuePair.Value);
  }

  public void Finalized() => this.IsFinalized = true;
}
