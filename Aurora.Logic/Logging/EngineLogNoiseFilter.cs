// Decompiled code in this project still uses the legacy Builder logger directly.
// Keep the noise policy in one place so file logs and the MAUI console agree.
namespace Builder.Presentation.Logging;

public static class EngineLogNoiseFilter
{
  public static bool ShouldSuppressWarning(string? message)
  {
    if (string.IsNullOrWhiteSpace(message))
      return false;

    return IsBenignRequirementProbe(message)
           || IsKnownSpellCompatibilityAttribute(message)
           || IsMissingSpellcastingAttribute(message);
  }

  private static bool IsBenignRequirementProbe(string message)
  {
    return message.Contains("unknown statistics expression key:", StringComparison.OrdinalIgnoreCase)
           || message.Contains("checking statistics expression key:", StringComparison.OrdinalIgnoreCase)
           || (message.Contains("not granting:", StringComparison.OrdinalIgnoreCase)
               && message.Contains("due to not meeting element requirements", StringComparison.OrdinalIgnoreCase));
  }

  // Restored in Builder.Data for legacy parity: original Aurora warned when a Spell select rule on a
  // class-like element carried no 'spellcasting' attribute. That is feedback about how content is
  // written, not about the app, and it repeats for every affected element on every load.
  private static bool IsMissingSpellcastingAttribute(string message)
  {
    return message.Contains("missing 'spellcasting' attribute on select rule", StringComparison.OrdinalIgnoreCase);
  }

  private static bool IsKnownSpellCompatibilityAttribute(string message)
  {
    if (!message.Contains("unable to parse [", StringComparison.OrdinalIgnoreCase))
      return false;

    return message.Contains("[known:", StringComparison.OrdinalIgnoreCase)
           || message.Contains("[allowReplace:", StringComparison.OrdinalIgnoreCase);
  }
}
