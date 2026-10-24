// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Models.CharacterSheet.PDF.FontsHelper
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#nullable disable
namespace Builder.Presentation.Models.CharacterSheet.PDF;

public static class FontsHelper
{
  public static BaseFont GetUnicodeFallback() => Aurora.Documents.Writers.PortablePdfFonts.GetBaseFont();

  public static Font GetRegular(float size = 12f)
  {
    return FontsHelper.GetFont("Calibri", "calibri.ttf", size) ?? Aurora.Documents.Writers.PortablePdfFonts.GetFont(size, Font.NORMAL);
  }

  public static Font GetBold(float size = 12f)
  {
    return FontsHelper.GetFont("Calibri Bold", "calibrib.ttf", size) ?? Aurora.Documents.Writers.PortablePdfFonts.GetFont(size, Font.BOLD);
  }

  public static Font GetItalic(float size = 12f)
  {
    return FontsHelper.GetFont("Calibri Italic", "calibrii.ttf", size) ?? Aurora.Documents.Writers.PortablePdfFonts.GetFont(size, Font.ITALIC);
  }

  public static Font GetBoldItalic(float size = 12f)
  {
    return FontsHelper.GetFont("Calibri Bold Italic", "calibriz.ttf", size) ?? Aurora.Documents.Writers.PortablePdfFonts.GetFont(size, Font.BOLDITALIC);
  }

  public static Font GetFont(string fontName, string filename, float size = 0.0f)
  {
    if (OperatingSystem.IsAndroid()) return null;
    try
    {
      string fontPath = FontsHelper.ResolveFontPath(filename);
      if (string.IsNullOrWhiteSpace(fontPath))
        return (Font) null;
      if (!FontFactory.IsRegistered(fontPath))
        FontFactory.Register(fontPath);
      Font font = FontFactory.GetFont(fontName, "Identity-H", true, size);
      return font?.BaseFont != null ? font : (Font) null;
    }
    catch
    {
      return (Font) null;
    }
  }

  private static string ResolveFontPath(string filename)
  {
    foreach (string path in FontsHelper.GetCandidateDirectories().Select(dir => Path.Combine(dir, filename)))
    {
      if (File.Exists(path))
        return path;
    }
    return (string) null;
  }

  private static IEnumerable<string> GetCandidateDirectories()
  {
    string windowsFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
    if (!string.IsNullOrWhiteSpace(windowsFonts))
      yield return windowsFonts;
    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    if (!string.IsNullOrWhiteSpace(userProfile))
    {
      yield return Path.Combine(userProfile, "Library", "Fonts");
      yield return Path.Combine(userProfile, ".fonts");
      yield return Path.Combine(userProfile, ".local", "share", "fonts");
    }
    yield return "/System/Library/Fonts";
    yield return "/Library/Fonts";
    yield return "/usr/share/fonts";
    yield return "/usr/local/share/fonts";
  }
}
