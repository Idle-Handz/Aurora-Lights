// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.ViewModels.Development.DeveloperWindowViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Core.Events;
using Builder.Presentation.Events.Developer;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Text;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.ViewModels.Development;

public class DeveloperWindowViewModel : ViewModelBase, ISubscriber<DeveloperWindowStatusUpdateEvent>
{
  private string _statusMessage;
  private string _progressPercentage;
  private string _input;
  private string _output;
  private string _elementName;
  private string _elementType;
  private string _elementSource;
  private string _elementId;

  public DeveloperWindowViewModel()
  {
    this._statusMessage = "Developer Console";
    if (this.IsInDesignMode)
      return;
    this.EventAggregator.Subscribe((object) this);
  }

  public string StatusMessage
  {
    get => this._statusMessage;
    set => this.SetProperty<string>(ref this._statusMessage, value, nameof (StatusMessage));
  }

  public string ProgressPercentage
  {
    get => this._progressPercentage;
    set
    {
      this.SetProperty<string>(ref this._progressPercentage, value, nameof (ProgressPercentage));
    }
  }

  public string ElementName
  {
    get => this._elementName;
    set
    {
      this.SetProperty<string>(ref this._elementName, value, nameof (ElementName));
      this.ElementId = "ID_" + this._elementName.ToUpper();
    }
  }

  public string ElementType
  {
    get => this._elementType;
    set => this.SetProperty<string>(ref this._elementType, value, nameof (ElementType));
  }

  public string ElementSource
  {
    get => this._elementSource;
    set => this.SetProperty<string>(ref this._elementSource, value, nameof (ElementSource));
  }

  public string ElementId
  {
    get => this._elementId;
    set => this.SetProperty<string>(ref this._elementId, value, nameof (ElementId));
  }

  public string Input
  {
    get => this._input;
    set => this.SetProperty<string>(ref this._input, value, nameof (Input));
  }

  public string Output
  {
    get => this._output;
    set => this.SetProperty<string>(ref this._output, value, nameof (Output));
  }

  public ICommand GenerateCommand => (ICommand) new RelayCommand(new Action(this.Generate));

  private void Generate()
  {
    StringBuilder stringBuilder = new StringBuilder();
    stringBuilder.AppendLine($"{DeveloperWindowViewModel.GenerateIndentations(0)}<element name=\"{this.ElementName}\" type=\"{this.ElementType}\" source=\"{this.ElementSource}\" id=\"{this.ElementId.ToUpper()}\" />");
    this.Output = stringBuilder.ToString();
  }

  private static string GenerateIndentations(int amount = 1)
  {
    string indentations = "";
    for (int index = 0; index < amount; ++index)
      indentations += "\t";
    return indentations;
  }

  public void OnHandleEvent(DeveloperWindowStatusUpdateEvent args)
  {
    this.StatusMessage = args.StatusMessage;
  }
}
