// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.QuickSearchService
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Events;
using Builder.Presentation.Services;
using Builder.Presentation.Services.QuickBar.Commands;
using Builder.Presentation.Services.QuickBar.Commands.Base;
using Builder.Presentation.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Views;

public class QuickSearchService
{
  private readonly List<QuickBarCommand> _commands;

  private static IEventAggregator EventAggregator => ApplicationManager.Current.EventAggregator;

  private QuickSearchService()
  {
    this._commands = new List<QuickBarCommand>()
    {
      (QuickBarCommand) new QuickBarBundleCommand(),
      (QuickBarCommand) new QuickBarPortraitsCommand(),
      (QuickBarCommand) new QuickBarCustomCommand(),
      (QuickBarCommand) new QuickSearchDataCommand(),
      (QuickBarCommand) new QuickSearchLogsCommand(),
      (QuickBarCommand) new QuickBarDiagnosticsCommand(),
      (QuickBarCommand) new QuickBarDiceCommand(),
      (QuickBarCommand) new QuickBarFetchCommand()
    };
  }

  public static QuickSearchService Current { get; } = new QuickSearchService();

  public void Search(string criteria)
  {
    AnalyticsEventHelper.CompendiumSearch(criteria);
    this.ExecuteSearch(criteria);
  }

  private static bool ContainsCommand(string criteria)
  {
    if (!criteria.StartsWith("@"))
      return false;
    return criteria.TrimStart('@').Split(' ')[0] != null;
  }

  private static string GetCommand(string criteria)
  {
    return criteria.TrimStart('@').Split(' ')[0].ToLower();
  }

  private static string GetCommandParameter(string criteria)
  {
    string command = QuickSearchService.GetCommand(criteria);
    return criteria.Replace("@" + command, "").Trim();
  }

  public bool EvaluateCriteria(string criteria)
  {
    if (string.IsNullOrWhiteSpace(criteria) || criteria.Equals("@"))
      return false;
    if (!QuickSearchService.ContainsCommand(criteria))
      return true;
    string cmd = QuickSearchService.GetCommand(criteria);
    if (this._commands.Any<QuickBarCommand>((Func<QuickBarCommand, bool>) (x => x.CommandName.Equals(cmd))))
      return true;
    if (cmd != null)
    {
      string str = cmd;
      if (str == "dndweekend" || str == "greek" || str == "nl" || str == "dutch")
        return true;
    }
    return false;
  }

  private void ExecuteSearch(string criteria)
  {
    if (!this.EvaluateCriteria(criteria))
    {
      if (!criteria.StartsWith("@"))
        return;
      MessageDialogService.Show($"The '{QuickSearchService.GetCommand(criteria)}' command is invalid.");
    }
    else if (QuickSearchService.ContainsCommand(criteria))
    {
      string command1 = QuickSearchService.GetCommand(criteria);
      string commandParameter = QuickSearchService.GetCommandParameter(criteria);
      foreach (QuickBarCommand command2 in this._commands)
      {
        if (command2.CommandName.Equals(command1))
        {
          command2.Execute(commandParameter);
          return;
        }
      }
      MessageDialogService.Show("Unknown Command");
    }
    else
      QuickSearchService.EventAggregator.Send<QuickSearchBarEventArgs>(new QuickSearchBarEventArgs(criteria)
      {
        IsSearch = true
      });
  }
}
