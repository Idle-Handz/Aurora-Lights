using System.IO;
// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.MessageDialogService
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe


using Builder.Presentation.Views.Dialogs;
using System;
using System.Diagnostics;
using System.Windows;

#nullable disable
using Builder.Presentation.Properties;
namespace Builder.Presentation.Services;

/// <summary>
/// Instance adapter that implements IMessageDialogService by delegating to the
/// static MessageDialogService methods. Set as MessageDialogContext.Current on startup.
/// </summary>
public sealed class MessageDialogServiceAdapter : Builder.Presentation.Interfaces.IMessageDialogService
{
    public void Show(string message, string? caption = null) => MessageDialogService.Show(message, caption);
    public void ShowException(Exception ex, string? message = null, string? caption = null) =>
        MessageDialogService.ShowException(ex, caption ?? ex.GetType().ToString(), message);
    public bool Confirm(string message, string? caption = null) =>
        MessageBox.Show(message, caption ?? "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
}

public static class MessageDialogService
{
  public static void Show(string message, string title = null)
  {
    MessageDialogService.Show((object) message, title ?? $"{Resources.ApplicationName} ({Resources.ApplicationVersion})");
  }

  private static void Show(object content, string title)
  {
    int num = (int) MessageBox.Show(content.ToString(), title);
  }

  public static void ShowException(Exception ex)
  {
    MessageDialogService.ShowException(ex, ex.GetType().ToString());
  }

  public static void ShowException(Exception ex, string title)
  {
    MessageDialogService.ShowException(ex, title, (string) null);
  }

  public static void ShowException(Exception ex, string title, string introMessage)
  {
    if (Debugger.IsAttached)
    {
      string message = $"{$"{$"{ex.GetType().Name} {(ex.InnerException != null ? "has inner exception" : "with no inner exception")}: {ex.Message}"}{Environment.NewLine}Source: {ex.Source}"}{Environment.NewLine}Trace: {ex.StackTrace}";
      if (ex.InnerException != null)
        message = message + Environment.NewLine + $"Inner Exception: {ex.InnerException}";
      new ExceptionMessageWindow(title, introMessage, message).ShowDialog();
    }
    else
    {
      string message = ex.Message;
      string error;
      string hint;
      if (ex.Data.Contains((object) "filename"))
      {
        string str = ex.Data[(object) "filename"].ToString();
        error = "An error occurred trying to parse a file.";
        hint = ex.Data.Contains((object) "warning") ? ex.Data[(object) "warning"].ToString() : str;
        message = $"{str}\r\n\r\n{ex.Message}";
      }
      else if (ex.Data.Contains((object) "warning"))
      {
        error = "An error occurred trying to parse internal file.";
        hint = ex.Data[(object) "warning"].ToString();
      }
      else if (ex.Data.Contains((object) "404"))
      {
        error = "An error occurred while trying to perform a web request.";
        hint = ex.Data[(object) "404"].ToString();
      }
      else
      {
        error = "An error occurred";
        hint = ex.Message;
        message = $"{ex.Source}\r\n\r\n{ex.StackTrace}";
      }
      new ExceptionWindow(title, error, hint, message).ShowDialog();
    }
  }
}
