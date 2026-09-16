namespace Aurora.Tests.Helpers;

/// <summary>External-process coverage is opt-in; missing prerequisites are reported as skipped, not passed.</summary>
public sealed class TranslatorIntegrationFactAttribute : FactAttribute
{
    public TranslatorIntegrationFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "The external Translator executable integration requires Windows.";
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AURORA_TEST_TRANSLATOR")))
            Skip = "Requires a separately published Translator. Run tools/test-translator-integration.ps1 -TranslatorExecutable <path>.";
    }
}
