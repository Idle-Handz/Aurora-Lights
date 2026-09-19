using Aurora.Content.Preparation;
using Microsoft.Data.Sqlite;
using System.Xml.Linq;
using System.Diagnostics;
using Builder.Data.Files;
using Builder.Data;
using Builder.Presentation.Services.Data;
using System.Xml;

namespace Aurora.Tests.Tests;

public sealed class PreparedContentProjectionTests : IDisposable
{
    private readonly SqliteConnection db = new("Data Source=:memory:");
    private readonly string root = Path.Combine(Path.GetTempPath(), "aurora-projection-" + Guid.NewGuid().ToString("N"));
    public PreparedContentProjectionTests()
    {
        Directory.CreateDirectory(root);
        db.Open();
        Sql("""
            CREATE TABLE content_preparation_metadata(singleton_id,contract_version,catalog_policy,append_policy);
            INSERT INTO content_preparation_metadata VALUES(1,1,'unrestricted','materialized');
            CREATE TABLE content_prepared_elements(aurora_id,base_xml);
            CREATE TABLE v_content_prepared_sources(aurora_id,file_path,relative_path,package_key,package_kind);
            CREATE TABLE v_content_append_operations(file_path,relative_path,package_key,package_kind,ordinal,target_aurora_id,operation_xml,status);
            CREATE TABLE local_override_files(file_path,source_path,status);
            CREATE TABLE local_correction_inputs(path,sha256);
            """);
    }
    private void Sql(string text, params object[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = text;
        for (int i = 0; i < args.Length; i++) cmd.Parameters.AddWithValue("$p" + i, args[i]);
        cmd.ExecuteNonQuery();
    }
    private PreparedCatalogSource Source(string file, string package = "base") => new(Path.Combine(root, file), file, package, "official");
    private static string Element(string id, string text = "base") => $"<element id='{id}' type='Feat' name='{id}' source='Test'><description>{text}</description></element>";
    private void Base(string id, PreparedCatalogSource source)
    {
        Sql("INSERT INTO content_prepared_elements VALUES($p0,$p1)", id, Element(id));
        Sql("INSERT INTO v_content_prepared_sources VALUES($p0,$p1,$p2,$p3,$p4)", id, source.FilePath, source.RelativePath, source.PackageKey, source.PackageKind);
    }
    private void Append(string id, PreparedCatalogSource source, string grant)
        => Sql("INSERT INTO v_content_append_operations VALUES($p0,$p1,$p2,$p3,0,$p4,$p5,'applied')",
            source.FilePath, source.RelativePath, source.PackageKey, source.PackageKind, id,
            $"<append id='{id}'><rules><grant type='Feat' id='{grant}'/></rules></append>");
    private static string[] Grants(PreparedCatalogProjection projection, string id)
        => XElement.Parse(projection.Elements.Single(e => e.AuroraId == id).Xml).Element("rules")!
            .Elements("grant").Select(g => (string)g.Attribute("id")!).ToArray();

    [Fact]
    public void SecondaryXmlAndDatabaseAppendsAreAppliedOnceInFileOrder()
    {
        Base("BASE", Source("base.xml"));
        Append("BASE", Source("z.xml"), "LAST");
        var file = new PreparedCatalogFile(Source("a.xml", "runtime-xml"),
            "<elements>" + Element("SECONDARY") + "<append id='BASE'><rules><grant type='Feat' id='FIRST'/></rules></append></elements>");
        var projection = PreparedCatalogReader.Read(db, runtimeFiles: [file]);
        projection.Elements.Select(e => e.AuroraId).Should().BeEquivalentTo("BASE", "SECONDARY");
        Grants(projection, "BASE").Should().Equal("FIRST", "LAST");
        Grants(PreparedCatalogReader.Read(db, runtimeFiles: [file]), "BASE").Should().Equal("FIRST", "LAST");
    }

    [Fact]
    public void FilteringExtensionsDoesNotModifyPersistedOperations()
    {
        Base("BASE", Source("base.xml"));
        Append("BASE", Source("extension.xml", "disabled"), "EXTRA");
        var filtered = PreparedCatalogReader.Read(db, s => s.PackageKey != "disabled");
        XElement.Parse(filtered.Elements.Single().Xml).Element("rules").Should().BeNull();
        Grants(PreparedCatalogReader.Read(db), "BASE").Should().Equal("EXTRA");
    }

    [Fact]
    public void CorrectedFileReplacesItsDatabaseBaseAndItsOldAppendOperations()
    {
        var source = Source("corrected.xml");
        Base("OLD", source);
        Append("OLD", source, "OBSOLETE");
        var file = new PreparedCatalogFile(source, "<elements>" + Element("NEW", "corrected") +
            "<append id='NEW'><rules><grant type='Feat' id='FIXED'/></rules></append></elements>");
        var projection = PreparedCatalogReader.Read(db, runtimeFiles: [file]);
        projection.Elements.Select(e => e.AuroraId).Should().Equal("NEW");
        Grants(projection, "NEW").Should().Equal("FIXED");
    }

    [Fact]
    public void DatabaseAppendCanTargetASecondaryDefinition()
    {
        Append("SECONDARY", Source("extension.xml"), "LINK");
        var projection = PreparedCatalogReader.Read(db, runtimeFiles:
            [new(Source("extra.xml"), "<elements>" + Element("SECONDARY") + "</elements>")]);
        Grants(projection, "SECONDARY").Should().Equal("LINK");
    }

    [Fact]
    public void HostCannotResurrectFilteredCanonicalId()
    {
        Base("BASE", Source("base.xml", "disabled"));
        var projection = PreparedCatalogReader.Read(db, s => s.PackageKey != "disabled",
            hostDefinitions: [new("BASE", Source("host.xml", "runtime-builtins"), Element("BASE"))]);
        projection.Elements.Should().BeEmpty();
    }

    [Fact]
    public void ConflictingSecondaryDefinitionRequiresExplicitResolution()
    {
        Base("BASE", Source("base.xml"));
        Action read = () => PreparedCatalogReader.Read(db, runtimeFiles:
            [new(Source("extra.xml"), "<elements>" + Element("BASE", "different") + "</elements>")]);
        read.Should().Throw<InvalidDataException>().WithMessage("*BASE*");
        PreparedCatalogReader.Read(db).Elements.Single().Xml.Should().Contain("base");
    }

    [Fact]
    public void OverlappingSecondaryRootDoesNotReadPrimaryFilesAgain()
    {
        var primary = Path.Combine(root, "primary");
        var nested = Path.Combine(primary, "extra");
        var secondary = Path.Combine(root, "secondary");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(secondary);
        File.WriteAllText(Path.Combine(nested, "primary.xml"), "<elements>" + Element("PRIMARY") + "</elements>");
        File.WriteAllText(Path.Combine(secondary, "secondary.xml"), "<elements>" + Element("SECONDARY") + "</elements>");
        var files = RuntimeContentFiles.Read(db, primary, [nested, secondary, secondary]);
        files.Should().ContainSingle();
        files.Single().Xml.Should().Contain("SECONDARY");
    }

    [Fact]
    public void IdenticalIdsAreIgnoredButDifferentIdsRemainSeparate()
    {
        Base("BASE", Source("base.xml"));
        var file = new PreparedCatalogFile(Source("extra.xml"), "<elements>" + Element("BASE") + Element("OTHER") + "</elements>");
        PreparedCatalogReader.Read(db, runtimeFiles: [file]).Elements.Select(e => e.AuroraId)
            .Should().BeEquivalentTo("BASE", "OTHER");
    }

    [Fact]
    public void CurrentLocalCorrectionOverridesDatabaseWithoutRefreshOrRetirement()
    {
        var source = Source("base.xml");
        Base("BASE", source);
        string baseline = "<elements>" + Element("BASE") + "</elements>";
        File.WriteAllText(source.FilePath, baseline);
        string localDirectory = Path.Combine(root, "user", "local");
        Directory.CreateDirectory(localDirectory);
        string localPath = Path.Combine(localDirectory, "fix.xml");
        File.WriteAllText(localPath, LocalCorrectionDocument.Create(
            "<elements>" + Element("BASE", "runtime correction") + "</elements>", baseline,
            "base.xml", [new("fix", "replace", "BASE", null, null)]));
        var files = RuntimeContentFiles.Read(db, root, []);
        PreparedCatalogReader.Read(db, runtimeFiles: files).Elements.Single().Xml.Should().Contain("runtime correction");
        PreparedCatalogReader.Read(db).Elements.Single().Xml.Should().NotContain("runtime correction");
        File.Exists(localPath).Should().BeTrue();
    }

    [Fact]
    public void FuturePreparationContractRemainsProtectedButCannotBeReadAsCurrent()
    {
        Sql("UPDATE content_preparation_metadata SET contract_version=999");
        PreparedCatalogReader.HasPreparationMetadata(db).Should().BeTrue();
        PreparedCatalogReader.IsPrepared(db).Should().BeFalse();
        Action read = () => PreparedCatalogReader.Read(db);
        read.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void LocalAliasKeepsItsOwnSupplierAndSourcePreference()
    {
        var source = Source("base.xml", "official");
        Base("BASE", source);
        string baseline = "<elements>" + Element("BASE") + "</elements>";
        File.WriteAllText(source.FilePath, baseline);
        string localPath = Path.Combine(root, "user", "local", "add.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        Base("LOCAL_ALIAS", Source("user/local/add.xml", "local"));
        File.WriteAllText(localPath, LocalCorrectionDocument.Create(
            "<elements>" + Element("BASE") + Element("LOCAL_ALIAS", "local variant") + "</elements>",
            baseline, "base.xml", [new("add", "add", "LOCAL_ALIAS", null, null)]));
        var files = RuntimeContentFiles.Read(db, root, []);
        var all = PreparedCatalogReader.Read(db, runtimeFiles: files);
        all.Elements.Single(e => e.AuroraId == "LOCAL_ALIAS").Source.PackageKey.Should().Be("local");
        PreparedCatalogReader.Read(db, s => s.PackageKey == "official", runtimeFiles: files)
            .Elements.Select(e => e.AuroraId).Should().Equal("BASE");
        PreparedCatalogReader.Read(db, s => s.PackageKey == "local", runtimeFiles: files)
            .Elements.Select(e => e.AuroraId).Should().Equal("LOCAL_ALIAS");
    }

    [Aurora.Tests.Helpers.TranslatorIntegrationFact]
    [Trait("Category", "TranslatorIntegration")]
    public async Task WindowsTranslatorWriterBuildsFreshDatabaseAndCombinesSecondaryXml()
    {
        string? configured = Environment.GetEnvironmentVariable("AURORA_TEST_TRANSLATOR");
        configured.Should().NotBeNullOrWhiteSpace("the integration runner must supply the Translator executable");
        string executable = Path.GetFullPath(configured!);
        File.Exists(executable).Should().BeTrue($"AURORA_TEST_TRANSLATOR must point to a published executable: {executable}");
        string primary = Path.Combine(root, "primary");
        string secondary = Path.Combine(root, "secondary");
        Directory.CreateDirectory(primary);
        Directory.CreateDirectory(secondary);
        File.WriteAllText(Path.Combine(primary, "test.xml"), "<elements>" + Element("ID_TEST_FEAT_PRIMARY") +
            "<append id='ID_TEST_FEAT_PRIMARY'><rules><grant type='Feat' id='ID_TEST_FEAT_SECONDARY'/></rules></append></elements>");
        File.WriteAllText(Path.Combine(secondary, "extra.xml"), "<elements>" + Element("ID_TEST_FEAT_SECONDARY") + "</elements>");
        string database = Path.Combine(root, "fresh.sqlite");
        using var process = new Process { StartInfo = new(executable)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in new[] { "sqlite-import", primary, database }) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var registration = timeout.Token.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } });
        await process.WaitForExitAsync(timeout.Token);
        string diagnostic = await stdout + await stderr;
        process.ExitCode.Should().Be(0, diagnostic);
        using var connection = ContentDatabase.OpenReadableConnection(database);
        PreparedCatalogReader.IsPrepared(connection).Should().BeTrue();
        PreparedCatalogReader.InputsMatch(connection, [primary]).Should().BeTrue();
        Aurora.Tests.Helpers.TestApplicationContextInstaller.EnsureInstalled();
        var host = DataManager.Current.LoadElementDocumentsFromResource()
            .SelectMany(d => d.DocumentElement!.ChildNodes.Cast<XmlNode>())
            .Where(n => n.Name == "element")
            .Select(n => new PreparedCatalogElement(n.Attributes!["id"]!.Value,
                new("resource://aurora/builtins", "runtime/builtins.xml", "runtime-builtins", "core"), n.OuterXml)).ToArray();
        var projection = PreparedCatalogReader.Read(connection, hostDefinitions: host,
            runtimeFiles: RuntimeContentFiles.Read(connection, primary, [secondary]));
        projection.Elements.Select(e => e.AuroraId).Should().Contain(["ID_TEST_FEAT_PRIMARY", "ID_TEST_FEAT_SECONDARY"]);
        Grants(projection, "ID_TEST_FEAT_PRIMARY").Should().Equal("ID_TEST_FEAT_SECONDARY");
        var candidate = new ElementBaseCollection();
        var fallback = new ElementParser();
        var parsers = ElementParserFactory.GetParsers().ToArray();
        foreach (var entry in projection.Elements)
        {
            var document = new XmlDocument();
            document.LoadXml(entry.Xml);
            var header = fallback.ParseElementHeader(document.DocumentElement!);
            candidate.Add((parsers.FirstOrDefault(p => p.ParserType == header.Type) ?? fallback).ParseElement(document.DocumentElement!));
        }
        var previous = DataManager.Current.ElementsCollection.ToArray();
        DataManager.Current.RunPostProcessing(candidate, includeResources: false, publish: false);
        DataManager.Current.ElementsCollection.Should().Equal(previous);
        candidate.Select(e => e.Id).Should().Contain("ID_TEST_FEAT_SECONDARY");
    }

    public void Dispose()
    {
        db.Dispose();
        Directory.Delete(root, true);
    }
}
