// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.strings
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

#nullable disable
namespace Builder.Presentation;

#pragma warning disable CS8981
[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
public class strings
#pragma warning restore CS8981
{
  private static ResourceManager resourceMan;
  private static CultureInfo resourceCulture;

  internal strings()
  {
  }

  [EditorBrowsable(EditorBrowsableState.Advanced)]
  public static ResourceManager ResourceManager
  {
    get
    {
      if (strings.resourceMan == null)
        strings.resourceMan = new ResourceManager("Builder.Presentation.strings", typeof (strings).Assembly);
      return strings.resourceMan;
    }
  }

  [EditorBrowsable(EditorBrowsableState.Advanced)]
  public static CultureInfo Culture
  {
    get => strings.resourceCulture;
    set => strings.resourceCulture = value;
  }

  public static string Aurora_Start_Welcome
  {
    get
    {
      return strings.ResourceManager.GetString(nameof (Aurora_Start_Welcome), strings.resourceCulture);
    }
  }
}
