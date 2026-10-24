// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.DiagnosticsWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Logging;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class DiagnosticsWindowViewModel : ViewModelBase, ILogger
{
  private string _statusMessage;
  private int _progressPercentage;
  private bool _isLoggerEnabled;
  private bool _isDebugLogsEnabled;
  private bool _isInfoLogsEnabled;
  private bool _isWarningLogsEnabled;
  private bool _isExceptionLogsEnabled;

  public DiagnosticsWindowViewModel()
  {
    this._isLoggerEnabled = true;
    this._isWarningLogsEnabled = true;
    this._isExceptionLogsEnabled = true;
    if (this.IsInDesignMode)
    {
      this._statusMessage = "logs all the logs!";
      this._progressPercentage = 67;
      this.Logs.Add(new DiagnosticsLogItem(Log.Info, "Info log"));
      this.Logs.Add(new DiagnosticsLogItem(Log.Debug, "Debug log"));
      this.Logs.Add(new DiagnosticsLogItem(Log.Debug, "Debug log"));
      this.Logs.Add(new DiagnosticsLogItem(Log.Warning, "Warning log"));
      this.Logs.Add(new DiagnosticsLogItem(Log.Exception, "Exception log"));
      this.Logs.Add(new DiagnosticsLogItem(Log.Info, "Info log"));
      this.Logs.Add(new DiagnosticsLogItem(Log.Info, "Info log"));
      this.Logs.Add(new DiagnosticsLogItem(Log.Warning, "Warning log"));
    }
    else
    {
      this.SubscribeWithEventAggregator();
      Logger.RegisterLogger((ILogger) this);
      this.StatusMessage = "Ready.";
    }
  }

  public string StatusMessage
  {
    get => this._statusMessage;
    set => this.SetProperty<string>(ref this._statusMessage, value, nameof (StatusMessage));
  }

  public int ProgressPercentage
  {
    get => this._progressPercentage;
    set => this.SetProperty<int>(ref this._progressPercentage, value, nameof (ProgressPercentage));
  }

  public bool IsLoggerEnabled
  {
    get => this._isLoggerEnabled;
    set
    {
      this.SetProperty<bool>(ref this._isLoggerEnabled, value, nameof (IsLoggerEnabled));
      if (this._isLoggerEnabled)
      {
        Logger.IsEnabled = true;
        Logger.Info("Logger Enabled");
        this.StatusMessage = "Logger Enabled";
      }
      else
      {
        Logger.Info("Logger Disabled");
        Logger.IsEnabled = false;
        this.StatusMessage = "Logger Disabled";
      }
    }
  }

  public ObservableCollection<DiagnosticsLogItem> Logs { get; set; } = new ObservableCollection<DiagnosticsLogItem>();

  public bool IsDebugLogsEnabled
  {
    get => this._isDebugLogsEnabled;
    set => this.SetProperty<bool>(ref this._isDebugLogsEnabled, value, nameof (IsDebugLogsEnabled));
  }

  public bool IsInfoLogsEnabled
  {
    get => this._isInfoLogsEnabled;
    set => this.SetProperty<bool>(ref this._isInfoLogsEnabled, value, nameof (IsInfoLogsEnabled));
  }

  public bool IsWarningLogsEnabled
  {
    get => this._isWarningLogsEnabled;
    set
    {
      this.SetProperty<bool>(ref this._isWarningLogsEnabled, value, nameof (IsWarningLogsEnabled));
    }
  }

  public bool IsExceptionLogsEnabled
  {
    get => this._isExceptionLogsEnabled;
    set
    {
      this.SetProperty<bool>(ref this._isExceptionLogsEnabled, value, nameof (IsExceptionLogsEnabled));
    }
  }

  public RelayCommand ClearLogsCommand => new RelayCommand(new Action(this.ClearLogs));

  private void ClearLogs()
  {
    this.Logs.Clear();
    this.StatusMessage = "Logs Cleared";
  }

  public void Debug(string message, params object[] args)
  {
    if (!this.IsLoggerEnabled || !this.IsDebugLogsEnabled)
      return;
    string data = message;
    if (args != null)
      data = string.Format(message, args);
    this.WriteLog(Log.Debug, data);
  }

  public void Info(string message, params object[] args)
  {
    if (!this.IsLoggerEnabled || !this.IsInfoLogsEnabled)
      return;
    string data = message;
    if (args != null)
      data = string.Format(message, args);
    this.WriteLog(Log.Info, data);
  }

  public void Warning(string message, params object[] args)
  {
    if (!this.IsLoggerEnabled || !this.IsWarningLogsEnabled)
      return;
    string data = message;
    if (args != null)
      data = string.Format(message, args);
    this.WriteLog(Log.Warning, data);
  }

  public void Exception(System.Exception ex)
  {
    if (!this.IsLoggerEnabled || !this.IsExceptionLogsEnabled)
      return;
    string data = $"{$"{$"{ex.GetType().Name} {(ex.InnerException != null ? "has inner exception" : "with no inner exception")}: {ex.Message}"}{Environment.NewLine}Source: {ex.Source}"}{Environment.NewLine}Trace: {ex.StackTrace}";
    if (ex.InnerException != null)
      data = data + Environment.NewLine + $"Inner Exception: {ex.InnerException}";
    this.WriteLog(Log.Exception, data);
  }

  private static string GeneratePrefix(Log log)
  {
    return $"{DateTime.UtcNow.ToString("u")} | {log.ToString().ToUpperInvariant()} | ";
  }

  private void WriteLog(Log log, string data)
  {
    Dispatcher.CurrentDispatcher.InvokeAsync((Action) (() =>
    {
      this.Logs.Insert(0, new DiagnosticsLogItem(log, DiagnosticsWindowViewModel.GeneratePrefix(log) + data));
      if (log != Log.Warning && log != Log.Exception)
        return;
      this.StatusMessage = DiagnosticsWindowViewModel.GeneratePrefix(log) + data;
    }));
  }
}
