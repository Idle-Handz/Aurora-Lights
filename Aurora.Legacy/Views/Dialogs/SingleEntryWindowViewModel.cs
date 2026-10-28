// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Dialogs.SingleEntryWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Presentation.ViewModels.Base;
using System.Threading.Tasks;

#nullable disable
namespace Builder.Presentation.Views.Dialogs;

public class SingleEntryWindowViewModel : ViewModelBase
{
  private string _title;
  private string _header;
  private string _text;

  public SingleEntryWindowViewModel()
  {
    if (!this.IsInDesignMode)
      return;
    this._title = "title";
    this._header = "header";
    this._text = "text";
  }

  public override Task InitializeAsync(InitializationArguments args)
  {
    EntryInitializationArguments initializationArguments = (EntryInitializationArguments) args;
    this.Title = initializationArguments.Title;
    this.Text = initializationArguments.InitialText;
    this.Header = initializationArguments.Header;
    return base.InitializeAsync(args);
  }

  public string Title
  {
    get => this._title;
    set => this.SetProperty<string>(ref this._title, value, nameof (Title));
  }

  public string Header
  {
    get => this._header;
    set => this.SetProperty<string>(ref this._header, value, nameof (Header));
  }

  public string Text
  {
    get => this._text;
    set => this.SetProperty<string>(ref this._text, value, nameof (Text));
  }
}
