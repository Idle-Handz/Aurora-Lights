using Aurora.Content;
using Aurora.Content.Preparation;

namespace Aurora.App.Services;

/// <summary>Reads database diagnostics without treating the library's display limit as a health total.</summary>
internal static class ContentDatabaseHealthReader
{
    internal static ContentDatabaseHealthReport? Read(string databasePath)
    {
        var report = ContentDatabaseReader.ReadHealth(databasePath);
        if (report is null) return null;

        // Aurora.Content 0.9.0 caps source-integrity groups at 24, but its status and impact
        // totals are computed from those groups. Recovered findings can fill the cap and hide
        // every manual-review finding. Keep sample limits, but include all groups in totals.
        var groups = report.Groups.Where(group => group.Area != "source-integrity").ToList();
        using var connection = ContentDatabase.OpenReadableConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(issue_kind, ''), COALESCE(owner_type_name, ''), COALESCE(relative_path, ''), COUNT(*)
            FROM v_source_integrity_issues
            GROUP BY issue_kind, owner_type_name, relative_path
            ORDER BY COUNT(*) DESC, issue_kind, relative_path
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string reason = reader.GetString(0);
            groups.Add(new ContentDatabaseHealthIssueGroup(
                SourceIntegrityImpact(reason), "source-integrity", "review", reason,
                reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        }

        // Status and impact totals are derived properties, so replacing the complete group
        // collection also corrects those summaries without changing the library's contract.
        return report with { IssueGroups = groups };
    }

    private static ContentDatabaseTrustImpact SourceIntegrityImpact(string reason) => reason switch
    {
        "grant-target-id-in-name-attribute" or
        "recovered-grant-target-id-in-name-attribute" or
        "duplicate-element-signature-in-file" => ContentDatabaseTrustImpact.AutoRecovered,
        _ => ContentDatabaseTrustImpact.ManualReview
    };
}
