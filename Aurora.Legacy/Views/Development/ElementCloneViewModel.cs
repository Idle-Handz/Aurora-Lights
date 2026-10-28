// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.ElementCloneViewModel
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Presentation.ViewModels.Base;
using System.Xml;

#nullable disable
namespace Builder.Presentation.Views.Development;

public class ElementCloneViewModel : ElementsViewModelBase
{
  private string _left;
  private string _right;
  private bool _isMatch;

  public ElementCloneViewModel()
  {
    this._left = "left";
    this._right = "right";
  }

  public string Left
  {
    get => this._left;
    set
    {
      this.SetProperty<string>(ref this._left, value, nameof (Left));
      this.IsMatch = this.Left.Length == this.Right.Length;
    }
  }

  public string Right
  {
    get => this._right;
    set
    {
      this.SetProperty<string>(ref this._right, value, nameof (Right));
      this.IsMatch = this.Left.Length == this.Right.Length;
    }
  }

  public bool IsMatch
  {
    get => this._isMatch;
    set => this.SetProperty<bool>(ref this._isMatch, value, nameof (IsMatch));
  }

  public override void OnSelectedElementChanged(bool isChanged)
  {
    this.Left = this.SelectedElement.ElementNode.GenerateCleanOutput().Replace("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n", "");
    this.Right = this.SelectedElement.GenerateElementNode().GenerateCleanOutput().Replace("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n", "");
  }

  private XmlNode GenerateXml(ElementBase elementBase) => elementBase.GenerateElementNode();
}
