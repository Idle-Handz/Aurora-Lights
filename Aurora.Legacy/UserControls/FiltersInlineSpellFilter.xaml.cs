// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.UserControls.Filters.InlineSpellFilter
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Extensions;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

#nullable disable
namespace Builder.Presentation.UserControls.Filters;

public partial class InlineSpellFilter : UserControl
{
  public InlineSpellFilter() => this.InitializeComponent();

  public IEnumerable<ElementBase> Filter<T>(IEnumerable<T> input) where T : ElementBase
  {
    InlineSpellFilterViewModel viewModel = this.GetViewModel<InlineSpellFilterViewModel>();
    viewModel.SourceSpells = (IEnumerable<Spell>) (input as List<Spell>);
    viewModel.ApplyFilter();
    return (IEnumerable<ElementBase>) new List<Spell>(viewModel.FilteredSpells);
  }

}
