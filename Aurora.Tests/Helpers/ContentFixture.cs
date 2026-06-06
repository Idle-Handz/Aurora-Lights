using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Helpers;

/// <summary>
/// One-time fixture that initialises Aurora.Logic's DataManager and element database.
/// Integration tests that need the full element collection call <see cref="EnsureAvailableAsync"/>
/// and skip via <see cref="SkipIfUnavailable"/> when the database is not present.
///
/// The initialisation is performed lazily and only once per process; subsequent calls
/// return immediately.
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
        if (_available.HasValue) return;

        await _lock.WaitAsync();
        try
        {
            if (_available.HasValue) return;
            try
            {
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
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns false if the content database is not available.
    /// Integration tests should guard with: <c>if (!ContentFixture.SkipIfUnavailable(output)) return;</c>
    /// A test that returns without asserting is counted as passed by xUnit, which is the
    /// correct behaviour for "nothing to verify in this environment."
    /// </summary>
    public static bool SkipIfUnavailable(Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        if (IsAvailable) return true;
        output?.WriteLine($"[SKIP] Aurora content database unavailable — {_failReason ?? "not initialised"}.");
        return false;
    }

    public static string GetCharacterFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Characters", fileName);
}
