using Aurora.Importer;
using Microsoft.Data.Sqlite;

namespace Aurora.Tests.Tests;

public sealed class ContentDatabaseRecoveryTests
{
    [Fact]
    public void OpenReadableConnection_RecoversInterruptedRollbackJournal()
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "Aurora.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string sourcePath = Path.Combine(tempDirectory, "source.sqlite");
            string recoveredPath = Path.Combine(tempDirectory, "recovered.sqlite");

            using (var source = new SqliteConnection($"Data Source={sourcePath};Pooling=False"))
            {
                source.Open();
                Execute(source, "PRAGMA journal_mode = DELETE;");
                Execute(source, "PRAGMA cache_size = 1;");
                Execute(source, "CREATE TABLE probe (value INTEGER NOT NULL, padding BLOB NOT NULL);");
                Execute(source, """
                    WITH RECURSIVE rows(value) AS
                    (
                        SELECT 1
                        UNION ALL
                        SELECT value + 1 FROM rows WHERE value < 200
                    )
                    INSERT INTO probe (value, padding)
                    SELECT 1, randomblob(4096) FROM rows;
                    """);

                using var transaction = source.BeginTransaction();
                Execute(source, "UPDATE probe SET value = 2;", transaction);

                File.Copy(sourcePath, recoveredPath);
                File.Copy(sourcePath + "-journal", recoveredPath + "-journal");
            }

            File.Exists(recoveredPath + "-journal").Should().BeTrue();

            using var recovered = AuroraContentImporter.OpenReadableConnection(recoveredPath);
            using var query = recovered.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM probe WHERE value = 1;";
            query.ExecuteScalar().Should().Be(200L);

            File.Exists(recoveredPath + "-journal").Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static void Execute(
        SqliteConnection connection,
        string sql,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
