using Aurora.Content.Preparation;
using Builder.Data;
using Builder.Data.Files;
using Builder.Presentation.Services.Data;
using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Xml;

/// <summary>A small executable oracle for append behavior in the actual pinned package and Legacy loader.</summary>
internal static class LegacyAppendAudit
{
    internal sealed record Result(bool Success, string LibraryVersion, string FixtureDirectory, CaseResult[] Cases);

    internal sealed record CaseResult(bool Success, bool HasBaseDescription, string FixtureDirectory,
        string[] LegacyFileOrder, string LegacyDescription, string PreparedDescription,
        string[] LegacyGrantOrder, string[] PreparedGrantOrder);

    internal static Result Run(string caseRoot)
    {
        // The caller requires a disposable case marker. Keep the authored fixture for review.
        string root = Path.Combine(caseRoot, "append-audit-" + Guid.NewGuid().ToString("N"));
        CaseResult[] cases =
        [
            RunCase(Path.Combine(root, "with-description"), hasBaseDescription: true),
            RunCase(Path.Combine(root, "without-description"), hasBaseDescription: false)
        ];
        return new(cases.All(result => result.Success),
            typeof(PreparedCatalogReader).Assembly.GetName().Version?.ToString() ?? "unknown", root, cases);
    }

    private static CaseResult RunCase(string root, bool hasBaseDescription)
    {
        string definition = "<element name='Fixture' type='Feat' source='Test' id='ID_APPEND_AUDIT'>" +
            (hasBaseDescription ? "<description><p>Base</p></description>" : "") + "</element>";
        var files = new Dictionary<string, string>
        {
            ["core/base.xml"] = "<elements>" + definition + "</elements>",
            ["supplements/b.xml"] = "<elements><append id='ID_APPEND_AUDIT'><rules><grant type='Feat' id='ID_SUPPLEMENT'/></rules></append></elements>",
            ["homebrew/a.xml"] = "<elements><append id='ID_APPEND_AUDIT'><description><p>Extra</p></description><rules><grant type='Feat' id='ID_HOMEBREW'/></rules></append></elements>"
        };
        foreach (var (relative, xml) in files)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, xml);
        }

        var manager = DataManager.Current;
        var ordered = (List<FileInfo>)typeof(DataManager).GetMethod("GetCustomFiles",
            BindingFlags.Instance | BindingFlags.NonPublic, [typeof(string)])!.Invoke(manager, [root])!;
        var appends = ordered.SelectMany(file => ElementsFile.FromFile(file).ExtendNodes).ToArray();
        var parser = new ElementParser();
        ElementBase legacy = Parse(definition);
        typeof(DataManager).GetMethod("AppendElements", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, [appends, new ElementBaseCollection([legacy]), parser, parser,
                ElementParserFactory.GetParsers().ToList()]);

        using var database = new SqliteConnection("Data Source=:memory:");
        database.Open();
        using (var command = database.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE content_preparation_metadata(singleton_id,contract_version,catalog_policy,append_policy);
                INSERT INTO content_preparation_metadata VALUES(1,2,'unrestricted','materialized');
                CREATE TABLE content_prepared_elements(aurora_id,base_xml);
                CREATE TABLE v_content_prepared_sources(aurora_id,file_path,relative_path,package_key,package_kind);
                CREATE TABLE v_content_append_operations(file_path,relative_path,package_key,package_kind,ordinal,target_aurora_id,operation_xml,status);
                """;
            command.ExecuteNonQuery();
        }
        PreparedCatalogSource Source(string relative) => new(Path.Combine(root, relative), relative, "audit", "homebrew");
        var runtimeFiles = files.Where(file => file.Key != "core/base.xml")
            .Select(file => new PreparedCatalogFile(Source(file.Key), file.Value)).ToArray();
        var prepared = Parse(PreparedCatalogReader.Read(database,
            hostDefinitions: [new("ID_APPEND_AUDIT", Source("core/base.xml"), definition)],
            runtimeFiles: runtimeFiles).Elements.Single().Xml);
        string[] legacyGrants = legacy.GetGrantRules().Select(rule => rule.Attributes.Id).ToArray();
        string[] preparedGrants = prepared.GetGrantRules().Select(rule => rule.Attributes.Id).ToArray();
        string expectedDescription = hasBaseDescription ? "<p>Base</p>" : "";
        string[] expectedGrants = ["ID_SUPPLEMENT", "ID_HOMEBREW"];
        return new(legacy.Description == expectedDescription && legacyGrants.SequenceEqual(expectedGrants) &&
            legacy.Description == prepared.Description && legacyGrants.SequenceEqual(preparedGrants),
            hasBaseDescription, root,
            ordered.Select(file => Path.GetRelativePath(root, file.FullName).Replace('\\', '/')).ToArray(),
            legacy.Description, prepared.Description, legacyGrants, preparedGrants);
    }

    private static ElementBase Parse(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return new ElementParser().ParseElement(document.DocumentElement!);
    }
}
