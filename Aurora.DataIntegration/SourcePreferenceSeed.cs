using Aurora.Content.Preparation;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;

namespace Aurora.App.Services;

/// <summary>
/// One-time migration of the content database's per-package switches to source restrictions.
/// The catalog now always loads in full, so a source the user had switched off is kept out of
/// characters through the global default restrictions instead of being missing from the database.
/// </summary>
public static class SourcePreferenceSeed
{
    /// <summary>
    /// Copies switched-off sources into the default restrictions, once. Returns the source names it
    /// added. Requires the element collection to be loaded, because restrictions are stored as
    /// Source element ids.
    /// </summary>
    public static IReadOnlyList<string> SeedDefaultRestrictions(string? databasePath)
    {
        var settings = ApplicationContext.Current?.Settings;
        if (settings == null || settings.SourcePreferencesSeeded)
            return [];

        IReadOnlyList<string> switchedOff;
        try
        {
            switchedOff = ReadSwitchedOffSourceNames(databasePath);
        }
        catch (Exception ex)
        {
            // A database that cannot be read yet is retried on the next launch.
            DebugLogService.Instance.Warn("Source preferences could not be migrated.", ex.ToString());
            return [];
        }

        var idsByName = DataManager.Current.ElementsCollection
            .Where(element => element.Type.Equals("Source", StringComparison.OrdinalIgnoreCase))
            .GroupBy(element => element.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        var restricted = settings.DefaultSourceRestrictions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var added = new List<string>();
        foreach (string name in switchedOff)
        {
            if (!idsByName.TryGetValue(name, out string? id))
            {
                DebugLogService.Instance.Info($"Switched-off source '{name}' has no Source element; not restricted.");
                continue;
            }
            if (restricted.Contains(id, StringComparer.Ordinal)) continue;
            restricted.Add(id);
            added.Add(name);
        }

        settings.DefaultSourceRestrictions = string.Join(",", restricted);
        settings.SourcePreferencesSeeded = true;
        if (added.Count > 0)
        {
            // The user had switched this content off, so keep it off for characters they make next.
            // Both this and the restrictions themselves are editable in Settings.
            settings.ApplyDefaultSourceRestrictionsOnNewCharacter = true;
            DebugLogService.Instance.Info("Migrated switched-off sources to default restrictions: " + string.Join(", ", added));
        }
        settings.Save();
        return added;
    }

    /// <summary>
    /// Names of sources whose every element comes from packages that were switched off. A source that
    /// only partly comes from one (an extension file adding to a book that is otherwise on) keeps
    /// loading, and builder infrastructure is never restricted.
    /// </summary>
    public static IReadOnlyList<string> ReadSwitchedOffSourceNames(string? databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
            return [];

        using var connection = ContentDatabase.OpenReadableConnection(databasePath);
        if (!HasPackagePreferences(connection))
            return [];

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sb.name,
                   SUM(CASE WHEN COALESCE(cp.is_enabled, 1) = 0 THEN 1 ELSE 0 END) AS switched_off,
                   COUNT(*) AS total
            FROM elements e
            JOIN source_files sf ON sf.source_file_id = e.source_file_id
            JOIN content_packages cp ON cp.content_package_id = sf.content_package_id
            JOIN source_books sb ON sb.source_book_id = e.source_book_id
            GROUP BY sb.name
            HAVING switched_off = total
            ORDER BY sb.name
            """;
        var names = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            if (!RequiredContentPolicy.IsRequiredSource(name))
                names.Add(name);
        }
        return names;
    }

    private static bool HasPackagePreferences(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('content_packages') WHERE name = 'is_enabled'";
        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }
}
