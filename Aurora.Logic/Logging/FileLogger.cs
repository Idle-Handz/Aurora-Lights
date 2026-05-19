// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Logging.FileLogger
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using System;
using System.IO;

#nullable disable
namespace Builder.Presentation.Logging;

public class FileLogger : ILogger
{
  private readonly string _directory;
  private readonly string _infoFilename;
  private readonly string _errorsFilename;

  public FileLogger(string directory)
  {
    this._directory = directory;
    this._infoFilename = $"info.{DateTime.Today.ToString("yyyyMMdd")}.log";
    this._errorsFilename = $"errors.{DateTime.Today.ToString("yyyyMMdd")}.log";
    foreach (char invalidFileNameChar in Path.GetInvalidFileNameChars())
      this._infoFilename = this._infoFilename.Replace(invalidFileNameChar.ToString(), "");
    foreach (char invalidFileNameChar in Path.GetInvalidFileNameChars())
      this._errorsFilename = this._errorsFilename.Replace(invalidFileNameChar.ToString(), "");
    this.Info("======================================== New Session ========================================", new object[0]);
    this.Warning("======================================== New Session ========================================", new object[0]);
  }

  public void Debug(string message, params object[] args)
  {
  }

  public void Info(string message, params object[] args)
  {
    try
    {
      string str = message;
      if (args != null)
        str = string.Format(message, args);
      File.AppendAllText(Path.Combine(this._directory, this._infoFilename), FileLogger.GeneratePrefix(Log.Info) + str + Environment.NewLine);
    }
    catch (System.Exception)
    {
    }
  }

  public void Warning(string message, params object[] args)
  {
    try
    {
      string str = message;
      if (args != null)
        str = string.Format(message, args);
      File.AppendAllText(Path.Combine(this._directory, this._errorsFilename), FileLogger.GeneratePrefix(Log.Warning) + str + Environment.NewLine);
    }
    catch (System.Exception)
    {
    }
  }

  public void Exception(System.Exception ex)
  {
    string data = $"{$"{$"{ex.GetType().Name} {(ex.InnerException != null ? (object) "has inner exception" : (object) "with no inner exception")}: {ex.Message}"}{Environment.NewLine}Source: {ex.Source}"}{Environment.NewLine}Trace: {ex.StackTrace}";
    if (ex.InnerException != null)
      data = data + Environment.NewLine + $"Inner Exception: {ex.InnerException}";
    this.WriteLog(Log.Exception, data);
  }

  private static string GeneratePrefix(Log log)
  {
    return $"{DateTime.UtcNow.ToString("u")} | {log.ToString().ToUpper()} | ";
  }

  private void WriteLog(Log log, string data)
  {
    try
    {
      string path2 = this._errorsFilename;
      if (log == Log.Debug)
        path2 = "debug.log";
      File.AppendAllText(Path.Combine(this._directory, path2), FileLogger.GeneratePrefix(log) + data + Environment.NewLine);
    }
    catch (System.Exception)
    {
    }
  }
}
