using Aurora.Content;
using Aurora.Content.Preparation;
using Microsoft.Data.Sqlite;

namespace Aurora.App.Services;

/// <summary>Reads complete actionable diagnostics and their owner files from one database snapshot.</summary>
internal static class ContentDatabaseHealthReader
{
    private const string RecoveredReasons = """
        'grant-target-id-in-name-attribute', 'recovered-grant-target-id-in-name-attribute',
        'duplicate-element-signature-in-file'
        """;
    private const string UnresolvedSources = """
        FROM v_unresolved_loader_link_diagnostics AS diagnostic
        LEFT JOIN elements AS owner ON owner.element_id = diagnostic.owner_element_id
        LEFT JOIN source_files AS source ON source.source_file_id = owner.source_file_id
        """;

    internal static ContentDatabaseHealthReport? Read(string databasePath)
    {
        if (!File.Exists(databasePath)) return null;
        using var connection = ContentDatabase.OpenReadableConnection(databasePath);
        using var transaction = connection.BeginTransaction(deferred: true);
        var groups = new List<ContentDatabaseHealthIssueGroup>();
        var samples = new List<ContentDatabaseHealthIssueSample>();

        void ReadRows(string sql, Action<SqliteDataReader> consume)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            while (reader.Read()) consume(reader);
        }

        // Join the normalized owner identity, not Aurora ID: multiple declarations of
        // an ID must not multiply diagnostic rows or attribute them to another supplier.
        ReadRows($"""
            SELECT diagnostic.diagnostic_status, COALESCE(diagnostic.diagnostic_reason, 'actionable'),
                   diagnostic.link_kind, COALESCE(source.relative_path, ''), COUNT(*)
            {UnresolvedSources}
            GROUP BY diagnostic.diagnostic_status, diagnostic.diagnostic_reason,
                     diagnostic.link_kind, source.relative_path
            ORDER BY source.relative_path, diagnostic.link_kind, diagnostic.diagnostic_status
            """, reader => groups.Add(new(
                reader.GetString(0) == "actionable" ? ContentDatabaseTrustImpact.Blocking : ContentDatabaseTrustImpact.Expected,
                "unresolved-link", reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetInt32(4))));

        ReadRows("""
            SELECT COALESCE(issue_kind, ''), COALESCE(owner_type_name, ''), COALESCE(relative_path, ''), COUNT(*)
            FROM v_source_integrity_issues
            GROUP BY issue_kind, owner_type_name, relative_path
            ORDER BY COUNT(*) DESC, issue_kind, relative_path
            """, reader => groups.Add(new(SourceIntegrityImpact(reader.GetString(0)),
                "source-integrity", "review", reader.GetString(0), reader.GetString(1),
                reader.GetString(2), reader.GetInt32(3))));

        ReadRows($"""
            SELECT diagnostic.diagnostic_status, COALESCE(diagnostic.diagnostic_reason, 'actionable'),
                   diagnostic.link_kind, COALESCE(source.relative_path, ''),
                   COALESCE(diagnostic.owner_aurora_id, diagnostic.owner_name, ''),
                   COALESCE(diagnostic.unresolved_key, ''), COALESCE(diagnostic.unresolved_text, '')
            {UnresolvedSources}
            WHERE diagnostic.diagnostic_status = 'actionable'
            ORDER BY source.relative_path, diagnostic.link_kind, diagnostic.owner_aurora_id, diagnostic.link_id
            """, reader => samples.Add(new(ContentDatabaseTrustImpact.Blocking, "unresolved-link",
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6))));

        // Manual-review findings are complete. Only already recovered informational
        // examples keep a display limit; they cannot crowd an actionable finding out.
        foreach (bool recovered in new[] { false, true })
        {
            ReadRows($"""
                SELECT COALESCE(issue_kind, ''), COALESCE(owner_type_name, ''), COALESCE(relative_path, ''),
                       COALESCE(owner_aurora_id, owner_name, ''), COALESCE(issue_key, ''), COALESCE(issue_text, '')
                FROM v_source_integrity_issues
                WHERE COALESCE(issue_kind, '') {(recovered ? "IN" : "NOT IN")} ({RecoveredReasons})
                ORDER BY issue_kind, relative_path, owner_name, issue_key
                {(recovered ? "LIMIT 12" : "")}
                """, reader => samples.Add(new(SourceIntegrityImpact(reader.GetString(0)),
                    "source-integrity", "review", reader.GetString(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5))));
        }

        var projectionSamples = new List<ContentDatabaseHealthIssueSample>();
        ReadRows("""
            SELECT type.type_name, COALESCE(source.relative_path, ''), owner.aurora_id
            FROM v_resolved_elements AS resolved
            JOIN elements AS owner ON owner.element_id = resolved.winning_element_id
            JOIN element_types AS type ON type.element_type_id = owner.element_type_id
            LEFT JOIN source_files AS source ON source.source_file_id = owner.source_file_id
            LEFT JOIN spells AS spell ON spell.element_id = owner.element_id
            LEFT JOIN items AS item ON item.element_id = owner.element_id
            LEFT JOIN companions AS companion ON companion.element_id = owner.element_id
            WHERE (type.type_name = 'Spell' AND spell.element_id IS NULL)
               OR (type.type_name = 'Item' AND item.element_id IS NULL)
               OR (type.type_name = 'Companion' AND companion.element_id IS NULL)
            ORDER BY source.relative_path, type.type_name, owner.aurora_id
            """, reader => projectionSamples.Add(new(ContentDatabaseTrustImpact.Blocking,
                "projection", "missing-row", "missing-resolved-row", reader.GetString(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(2), $"The effective {reader.GetString(0)} has no typed database record.")));
        samples.AddRange(projectionSamples);

        // All totals come from the same snapshot and retain the shared contract's
        // accounting. Projection issues already contribute to BlockingIssueCount,
        // so they get detailed samples but no additional counted issue groups.
        return new ContentDatabaseHealthReport(
            groups.Where(group => group.Area == "unresolved-link" && group.Status == "actionable").Sum(group => group.Count),
            groups.Where(group => group.Area == "unresolved-link" && group.Status != "actionable").Sum(group => group.Count),
            groups.Where(group => group.Area == "source-integrity").Sum(group => group.Count),
            projectionSamples.Count(sample => sample.Kind == "Spell"),
            projectionSamples.Count(sample => sample.Kind == "Item"),
            projectionSamples.Count(sample => sample.Kind == "Companion"),
            groups, samples.OrderBy(sample => sample.Impact).ThenBy(sample => sample.FilePath, StringComparer.Ordinal)
                .ThenBy(sample => sample.Owner, StringComparer.Ordinal).ToArray());
    }

    private static ContentDatabaseTrustImpact SourceIntegrityImpact(string reason) => reason switch
    {
        "grant-target-id-in-name-attribute" or
        "recovered-grant-target-id-in-name-attribute" or
        "duplicate-element-signature-in-file" => ContentDatabaseTrustImpact.AutoRecovered,
        _ => ContentDatabaseTrustImpact.ManualReview
    };
}
