// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Tools.CreateSpellcastingViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Core;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

#nullable disable
namespace Builder.Presentation.Views.Tools;

public class CreateSpellcastingViewModel : ViewModelBase
{
  private string _output;
  private string _supportName;

  public CreateSpellcastingViewModel()
  {
    if (this.IsInDesignMode)
      return;
    foreach (ElementBase elementBase in (IEnumerable<Spell>) DataManager.Current.ElementsCollection.Where<ElementBase>((Func<ElementBase, bool>) (x => x.Type.Equals("Spell"))).Cast<Spell>().OrderBy<Spell, int>((Func<Spell, int>) (x => x.Level)).ThenBy<Spell, string>((Func<Spell, string>) (x => x.Source)).ThenBy<Spell, string>((Func<Spell, string>) (x => x.Name)))
      this.Spells.Add(elementBase);
    this.GenerateListCommand = (ICommand) new RelayCommand<object>(new Action<object>(this.GenerateList));
  }

  public ElementBaseCollection Spells { get; } = new ElementBaseCollection();

  public ICommand GenerateListCommand { get; }

  public string Output
  {
    get => this._output;
    set => this.SetProperty<string>(ref this._output, value, nameof (Output));
  }

  public string SupportName
  {
    get => this._supportName;
    set => this.SetProperty<string>(ref this._supportName, value, nameof (SupportName));
  }

  private void GenerateList(object parameter)
  {
    try
    {
      ICollection<object> objects = parameter as ICollection<object>;
      StringBuilder stringBuilder = new StringBuilder();
      foreach (object obj in (IEnumerable<object>) objects)
      {
        ElementBase elementBase = obj as ElementBase;
        stringBuilder.AppendLine($"\t<append id=\"{elementBase.Id}\">");
        stringBuilder.AppendLine($"\t\t<supports>{this.SupportName}</supports>");
        stringBuilder.AppendLine("\t</append>");
      }
      this.Output = stringBuilder.ToString();
    }
    catch (Exception ex)
    {
      MessageDialogService.ShowException(ex);
    }
  }
}
