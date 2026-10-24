// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Services.SpeechService
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core.Logging;
using System;
using System.Speech.Synthesis;

#nullable disable
namespace Builder.Presentation.Services;

public sealed class SpeechService
{
  private static SpeechService _instance;
  private SpeechSynthesizer _speech;
  private bool _unavailable;

  private SpeechService() { }

  private void _speech_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
  {
    this.StopSpeech();
  }

  public event EventHandler SpeechStarted;

  public event EventHandler SpeechStopped;

  public static SpeechService Default
  {
    get
    {
      if (SpeechService._instance == null)
        SpeechService._instance = new SpeechService();
      return SpeechService._instance;
    }
  }

  private SpeechSynthesizer GetSynthesizer()
  {
    if (_unavailable) return null;
    if (_speech != null) return _speech;
    try
    {
      _speech = new SpeechSynthesizer();
      _speech.SpeakCompleted += new EventHandler<SpeakCompletedEventArgs>(this._speech_SpeakCompleted);
      return _speech;
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof(SpeechService));
      _unavailable = true;
      return null;
    }
  }

  public void StartSpeech(string input)
  {
    var synth = GetSynthesizer();
    if (synth == null) return;
    try
    {
      this.StopSpeech();
      synth.SpeakAsync(input);
      this.OnSpeechStarted();
    }
    catch (Exception ex)
    {
      Logger.Exception(ex, nameof (StartSpeech));
      MessageDialogService.ShowException(ex);
    }
  }

  public void StopSpeech()
  {
    if (_speech == null) return;
    _speech.SpeakAsyncCancelAll();
    this.OnSpeechStopped();
  }

  private void OnSpeechStarted()
  {
    EventHandler speechStarted = this.SpeechStarted;
    if (speechStarted == null)
      return;
    speechStarted((object) this, EventArgs.Empty);
  }

  private void OnSpeechStopped()
  {
    EventHandler speechStopped = this.SpeechStopped;
    if (speechStopped == null)
      return;
    speechStopped((object) this, EventArgs.Empty);
  }
}
