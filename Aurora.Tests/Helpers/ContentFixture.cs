using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Xunit.Sdk;

namespace Aurora.Tests.Helpers;

/// <summary>
/// One-time fixture that initialises Aurora.Logic's DataManager and element database.
/// Integration tests that need the full element collection call <see cref="EnsureAvailableAsync"/>
/// and fail with a useful diagnostic when the database cannot be loaded.
///
/// The expensive initialisation is performed lazily and only once per process. Each
/// subsequent call resets the singleton-backed test state for the next integration test.
/// </summary>
public static class ContentFixture
{
    private static bool? _available;
    private static string? _failReason;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>True after a successful <see cref="EnsureAvailableAsync"/> call.</summary>
    public static bool IsAvailable => _available == true;

    /// <summary>
    /// Attempts to initialise the Aurora element database. Idempotent — safe to call from
    /// every integration test; the heavy work only runs once.
    /// </summary>
    public static async Task EnsureAvailableAsync()
    {
        if (!_available.HasValue)
        {
            await _lock.WaitAsync();
            try
            {
                if (!_available.HasValue)
                {
                    try
                    {
                        TestApplicationContextInstaller.EnsureInstalled();
                        SelectionRuleExpanderContext.Current ??= new TestSelectionRuleExpanderHandler();
                        SpellcastingSectionContext.Current ??= new TestSpellHandler();
                        DataManager.Current.InitializeDirectories();
                        DataManager.Current.InitializeFileLogger();
                        await DataManager.Current.InitializeElementDataAsync();

                        _available = DataManager.Current.ElementsCollection.Count > 0;
                        if (_available == false)
                            _failReason = "ElementsCollection is empty after initialisation.";
                    }
                    catch (Exception ex)
                    {
                        _available = false;
                        _failReason = $"{ex.GetType().Name}: {ex.Message}";
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        if (_available == true)
        {
            SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
            SpellcastingSectionContext.Current = new TestSpellHandler();
            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            CharacterManager.Current.File = new CharacterFile(
                Path.Combine(Path.GetTempPath(), $"aurora-test-{Guid.NewGuid():N}.dnd5e"));
        }
    }

    /// <summary>
    /// Returns true when the content database is available; otherwise fails the test with
    /// the captured initialization reason. Existing callers retain their guard shape while
    /// no longer turning missing content into a false-green pass.
    /// </summary>
    public static bool SkipIfUnavailable(Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        if (IsAvailable) return true;
        output?.WriteLine($"[FAIL] Aurora content database unavailable — {_failReason ?? "not initialised"}.");
        throw new XunitException(
            $"Aurora content database unavailable: {_failReason ?? "not initialised"}.");
    }

    public static string GetCharacterFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Characters", fileName);

    public static string GetChoiceIdentityFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ChoiceIdentity", fileName);
}
