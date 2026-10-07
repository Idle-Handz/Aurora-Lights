using Aurora.App.Services;
using LocalCorrection = Aurora.Content.Contracts.LocalCorrection;
using LocalCorrectionDocument = Aurora.Content.Contracts.LocalCorrectionDocument;

namespace Aurora.Tests.Tests;

/// <summary>
/// Installed source files may contain local edits. A local match must not become a claim of
/// publisher adoption or permission to retire the correction that protects the fix.
/// </summary>
public sealed class ContentDoctorServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aurora-doctor-" + Guid.NewGuid().ToString("N"));

    private string Upstream => Path.Combine(root, "core", "features.xml");
    private string Override => Path.Combine(root, "user", "local", "fix.xml");

    private const string Baseline =
        "<elements>" +
        "<element name='Old' type='Item' source='Test' id='ID_FIX'><description>old</description></element>" +
        "<element name='Companion' type='Item' source='Test' id='ID_COMPANION'><description>companion</description></element>" +
        "</elements>";

    private static string Corrected => Baseline
        .Replace("name='Old'", "name='Fixed'")
        .Replace(">old</description>", ">corrected</description>");

    public ContentDoctorServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Upstream)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Override)!);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private void WriteOverride(string state = "review-pending") =>
        File.WriteAllText(Override, LocalCorrectionDocument.Create(
            Corrected, Baseline, "core/features.xml",
            [new LocalCorrection("fix", "replace", "ID_FIX", null, null, state)]));

    [Fact]
    public void ACorrectionMissingFromTheInstalledSourceIsStillNeeded()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();

        OverrideFileModel file = ContentDoctorService.Evaluate(Override);

        file.Problem.Should().BeNull();
        file.Corrections.Should().ContainSingle();
        file.Corrections[0].MatchesInstalledSource.Should().BeFalse("the installed source still carries the uncorrected element");
        file.Corrections[0].Summary.Should().Be("Still needed");
        file.InstalledMatchCount.Should().Be(0);
        file.CanRetire.Should().BeFalse();
    }

    [Fact]
    public void ALocallyPatchedSourceIsOnlyReportedAsAnInstalledMatchAndCannotBeAccepted()
    {
        // Editing this installed file does not establish what the publisher ships.
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();
        byte[] originalLocal = File.ReadAllBytes(Override);
        byte[] originalSource = File.ReadAllBytes(Upstream);
        var service = new ContentDoctorService(new ContentDatabaseService());

        OverrideFileModel file = ContentDoctorService.Evaluate(Override);

        file.Corrections[0].MatchesInstalledSource.Should().BeTrue();
        file.Corrections[0].Summary.Should().Be("Matches installed source");
        file.InstalledMatchCount.Should().Be(1);
        file.Status.Should().Be("Matches installed source");
        file.CanRetire.Should().BeFalse();
        Action accept = () => service.AcceptCorrection(file, file.Corrections[0]);
        accept.Should().Throw<InvalidDataException>().WithMessage("*Publisher adoption has not been verified*");
        File.ReadAllBytes(Override).Should().Equal(originalLocal);
        File.ReadAllBytes(Upstream).Should().Equal(originalSource);
    }

    [Fact]
    public void AnApprovedLocalCorrectionIsRecognizedWithoutOfferingToClearIt()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();
        var approved = LocalCorrectionDocument.ApproveLocal(
            File.ReadAllText(Override), Baseline, ["fix"]);
        File.WriteAllText(Override, approved.LocalXml);

        OverrideFileModel file = ContentDoctorService.Evaluate(Override);

        file.Problem.Should().BeNull();
        file.Corrections[0].State.Should().Be("approved-local");
        file.Corrections[0].Summary.Should().Be("Approved locally");
        file.Corrections[0].MatchesInstalledSource.Should().BeFalse();
        file.InstalledMatchCount.Should().Be(0);
        file.CanRetire.Should().BeFalse();
    }

    [Fact]
    public void AnApprovedCorrectionMatchingTheInstalledSourceRemainsProtected()
    {
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();
        var approved = LocalCorrectionDocument.ApproveLocal(
            File.ReadAllText(Override), Corrected, ["fix"]);
        File.WriteAllText(Override, approved.LocalXml);
        approved.ReviewReasons.Should().BeEmpty("local approval suppresses pending-review messages");
        var service = new ContentDoctorService(new ContentDatabaseService());

        OverrideFileModel before = ContentDoctorService.Evaluate(Override);

        before.Corrections[0].State.Should().Be("approved-local");
        before.Corrections[0].MatchesInstalledSource.Should().BeTrue();
        before.Corrections[0].Summary.Should().Be("Matches installed source");
        before.Status.Should().Be("Matches installed source");
        string originalXml = File.ReadAllText(Override);
        Action accept = () => service.AcceptCorrection(before, before.Corrections[0]);
        accept.Should().Throw<InvalidDataException>().WithMessage("*Publisher adoption has not been verified*");

        OverrideFileModel after = ContentDoctorService.Evaluate(Override);
        after.Corrections[0].State.Should().Be("approved-local");
        after.Corrections[0].Accepted.Should().BeFalse();
        after.CanRetire.Should().BeFalse();
        File.ReadAllText(Override).Should().Be(originalXml);
    }

    [Fact]
    public void ADisabledMatchingCorrectionRejectsAnAlteredModel()
    {
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();
        var local = LocalCorrectionDocument.Parse(File.ReadAllText(Override));
        local.Root!.SetAttributeValue("ignore", true);
        File.WriteAllText(Override, local.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        string originalXml = File.ReadAllText(Override);
        var service = new ContentDoctorService(new ContentDatabaseService());

        OverrideFileModel file = ContentDoctorService.Evaluate(Override);

        file.Problem.Should().BeNull();
        file.IsDisabled.Should().BeTrue();
        file.Corrections[0].MatchesInstalledSource.Should().BeTrue("the installed files still match while the correction is disabled");
        file.InstalledMatchCount.Should().Be(0, "disabled corrections are not active matches");
        file.Status.Should().Be("Disabled");
        Action clear = () => service.AcceptCorrection(file with { IsDisabled = false }, file.Corrections[0]);
        clear.Should().Throw<InvalidDataException>().WithMessage("*Enable the local correction file*");
        File.ReadAllText(Override).Should().Be(originalXml);
    }

    [Fact]
    public void ACorrectionDisabledAfterReviewCannotBeCleared()
    {
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();
        OverrideFileModel reviewed = ContentDoctorService.Evaluate(Override);
        reviewed.IsDisabled.Should().BeFalse();
        var local = LocalCorrectionDocument.Parse(File.ReadAllText(Override));
        local.Root!.SetAttributeValue("ignore", true);
        File.WriteAllText(Override, local.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        string disabledXml = File.ReadAllText(Override);
        var service = new ContentDoctorService(new ContentDatabaseService());

        Action clear = () => service.AcceptCorrection(reviewed, reviewed.Corrections[0]);

        clear.Should().Throw<InvalidDataException>().WithMessage("*Enable the local correction file*");
        File.ReadAllText(Override).Should().Be(disabledXml);
    }

    /// <summary>
    /// Existing accepted metadata records intent, not evidence that a publisher shipped the fix.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExistingAcceptanceReportsRecordedRetirementIntentWithoutClaimingPublisherAdoption(bool sourceMatches)
    {
        File.WriteAllText(Upstream, sourceMatches ? Corrected : Baseline);
        WriteOverride("accepted-upstream");
        string originalXml = File.ReadAllText(Override);

        OverrideFileModel after = ContentDoctorService.Evaluate(Override);
        after.Corrections[0].Accepted.Should().BeTrue();
        after.Corrections[0].MatchesInstalledSource.Should().Be(sourceMatches);
        after.Corrections[0].Summary.Should().Be("Marked accepted");
        after.InstalledMatchCount.Should().Be(0);
        after.CanRetire.Should().BeTrue();
        after.Status.Should().Be("Marked for retirement");
        File.ReadAllText(Override).Should().Be(originalXml);
    }

    [Fact]
    public void AnAlteredMatchingOrAcceptedModelCannotAuthorizeAcceptance()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();
        byte[] originalLocal = File.ReadAllBytes(Override);
        byte[] originalSource = File.ReadAllBytes(Upstream);
        var service = new ContentDoctorService(new ContentDatabaseService());
        var file = ContentDoctorService.Evaluate(Override);
        var forged = file.Corrections[0] with { MatchesInstalledSource = true, State = "accepted-upstream" };

        Action accept = () => service.AcceptCorrection(file with { CanRetire = true, Corrections = [forged] }, forged);

        accept.Should().Throw<InvalidDataException>().WithMessage("*Publisher adoption has not been verified*");
        File.ReadAllBytes(Override).Should().Equal(originalLocal);
        File.ReadAllBytes(Upstream).Should().Equal(originalSource);
    }

    [Fact]
    public async Task ALocallyMatchingCorrectionStillProtectsTheFixWhenInstalledContentRegresses()
    {
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();
        byte[] originalLocal = File.ReadAllBytes(Override);
        string db = Path.Combine(root, "content.sqlite");
        await Aurora.Content.ContentImport.ImportAsync(root, db);
        var service = new ContentDoctorService(new ContentDatabaseService());
        var matching = ContentDoctorService.Evaluate(Override);
        Action accept = () => service.AcceptCorrection(matching, matching.Corrections[0]);
        accept.Should().Throw<InvalidDataException>().WithMessage("*Publisher adoption has not been verified*");

        // A later publisher download can replace the locally patched source with the original.
        File.WriteAllText(Upstream, Baseline);
        await Aurora.Content.ContentImport.ImportAsync(root, db);

        var protectedFile = ContentDoctorService.Evaluate(Override);
        protectedFile.Corrections[0].MatchesInstalledSource.Should().BeFalse();
        protectedFile.Corrections[0].State.Should().Be("review-pending");
        protectedFile.CanRetire.Should().BeFalse();
        File.ReadAllBytes(Override).Should().Equal(originalLocal);
        Directory.GetFiles(Path.GetDirectoryName(Override)!, "fix.xml.retired-*").Should().BeEmpty();
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM elements WHERE aurora_id = 'ID_FIX' AND declaration_status = 'effective';";
        command.ExecuteScalar().Should().Be("Fixed", "the correction must survive a source download that still lacks the fix");
    }

    /// <summary>
    /// The hashes carried on the model are the state the reader was shown. If either file moved
    /// since, the write has to be refused rather than applied to something they did not look at.
    /// </summary>
    [Fact]
    public void ClearingIsRefusedWhenTheFileChangedSinceItWasRead()
    {
        File.WriteAllText(Upstream, Corrected);
        WriteOverride();
        var service = new ContentDoctorService(new ContentDatabaseService());
        OverrideFileModel stale = ContentDoctorService.Evaluate(Override);

        File.WriteAllText(Upstream, Baseline.Replace(">old<", ">changed underneath<"));

        Action clearing = () => service.AcceptCorrection(stale, stale.Corrections[0]);
        clearing.Should().Throw<IOException>();
    }

    [Fact]
    public void AnOverrideWhoseUpstreamIsNotInstalledSaysSoInsteadOfFailing()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();
        File.Delete(Upstream);

        OverrideFileModel file = ContentDoctorService.Evaluate(Override);

        file.Problem.Should().NotBeNull();
        file.Status.Should().Be("Cannot be read");
    }

    [Fact]
    public void AFileThatIsGoneIsReportedRatherThanThrowing()
    {
        OverrideFileModel file = ContentDoctorService.Evaluate(Path.Combine(root, "user", "local", "never.xml"));

        file.Problem.Should().Contain("no longer on disk");
        file.Corrections.Should().BeEmpty();
    }

    /// <summary>
    /// The conflict view reports one row per declaration of a duplicated id. Grouping them wrongly
    /// would either hide the alternative or claim the wrong file is the one in use.
    /// </summary>
    [Fact]
    public void ConflictsGroupTheirDeclarationsAndNameTheOneInUse()
    {
        string db = Path.Combine(root, "conflicts.sqlite");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE v_duplicate_aurora_ids (aurora_id TEXT, name TEXT, type_name TEXT,
                    package_name TEXT, relative_path TEXT, is_winner INTEGER);
                INSERT INTO v_duplicate_aurora_ids VALUES
                    ('ID_SHARED', 'Extra Attack', 'Class Feature', 'Players Handbook', 'core/ranger.xml', 0),
                    ('ID_SHARED', 'Extra Attack', 'Class Feature', 'A Homebrew Book', 'homebrew/ranger.xml', 1),
                    ('ID_OTHER', 'Drakewarden', 'Archetype', 'Fizbans', 'supplements/drake.xml', 1),
                    ('ID_OTHER', 'Drakewarden', 'Archetype', 'A Homebrew Book', 'homebrew/drake.xml', 0);
                """;
            command.ExecuteNonQuery();
        }

        IReadOnlyList<ContentConflictModel> conflicts = ContentDoctorService.ReadConflicts(db);

        conflicts.Should().HaveCount(2);
        ContentConflictModel shared = conflicts.Single(c => c.AuroraId == "ID_SHARED");
        shared.Name.Should().Be("Extra Attack");
        shared.Declarations.Should().HaveCount(2);
        shared.Winner!.PackageName.Should().Be("A Homebrew Book");
        shared.SetAside.Should().ContainSingle().Which.PackageName.Should().Be("Players Handbook");
    }

    /// <summary>
    /// A rarity that means nothing is listed with the file to fix. One a single edit from a real
    /// rarity carries the spelling to use; one nothing recognises is listed without a guess. Only
    /// declarations actually in use count. A real rarity in any case, a "varies" value, an infusion
    /// and the content saying "Unknown" are accepted as they are and are not findings.
    /// </summary>
    [Fact]
    public void RarityTyposAreSuggestedWithTheirFileAndOtherUnknownRaritiesAreListedUnguessed()
    {
        string db = Path.Combine(root, "rarity.sqlite");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE resolved_elements_cache (winning_element_id INTEGER);
                CREATE TABLE elements (element_id INTEGER, aurora_id TEXT, name TEXT, source_book_id INTEGER, source_file_id INTEGER);
                CREATE TABLE source_books (source_book_id INTEGER, name TEXT);
                CREATE TABLE source_files (source_file_id INTEGER, relative_path TEXT);
                CREATE TABLE setter_scopes (setter_scope_id INTEGER, owner_element_id INTEGER, owner_kind TEXT);
                CREATE TABLE setter_entries (setter_entry_id INTEGER, setter_scope_id INTEGER, setter_name TEXT, setter_value TEXT);
                INSERT INTO source_books VALUES (1, 'Mordenkainen''s Tome of Marvelous Magic');
                INSERT INTO source_files VALUES (1, 'third-party/dms-guild/tome.xml'), (2, 'reddit/lost-vaults/armors.xml');
                INSERT INTO elements VALUES
                    (1, 'ID_TYPO_VERY', 'Cloak of Typos', 1, 1),
                    (2, 'ID_REAL', 'Staff of Truth', 1, 1),
                    (3, 'ID_MYTHIC', 'Relic of Whatever', 1, 1),
                    (4, 'ID_SET_ASIDE', 'Set Aside Cloak', 1, 1),
                    (5, 'ID_TYPO_UNCOMMON', 'Plain Armor', NULL, 2),
                    (6, 'ID_LOWER_CASE', 'Quiet Ring', 1, 1),
                    (7, 'ID_INFUSION', 'Enhanced Defense', 1, 1),
                    (8, 'ID_SAYS_UNKNOWN', 'Odd Trinket', 1, 1),
                    (9, 'ID_VARIES', 'Potion of Whatever', 1, 1),
                    (10, 'ID_RANGE', 'Wand of Many Rarities', 1, 1);
                INSERT INTO resolved_elements_cache VALUES (1), (2), (3), (5), (6), (7), (8), (9), (10);
                INSERT INTO setter_scopes VALUES (1, 1, 'element'), (2, 2, 'element'), (3, 3, 'element'),
                    (4, 4, 'element'), (5, 5, 'element'), (6, 6, 'element'), (7, 1, 'other'),
                    (8, 7, 'element'), (9, 8, 'element'), (10, 9, 'element'), (11, 10, 'element');
                INSERT INTO setter_entries VALUES
                    (1, 1, 'rarity', 'Vert Rare'),
                    (2, 2, 'rarity', 'Very Rare'),
                    (3, 3, 'RARITY', 'Mythic'),
                    (4, 4, 'rarity', 'Lgendary'),
                    (5, 5, 'rarity', 'unommon'),
                    (6, 6, 'rarity', 'very rare'),
                    (7, 1, 'cost', '25'),
                    (8, 7, 'rarity', 'Wrong Scope'),
                    (9, 8, 'rarity', 'Artificer Infusion'),
                    (10, 9, 'rarity', 'Unknown'),
                    (11, 10, 'rarity', 'Rarity varies by potion type'),
                    (12, 11, 'rarity', 'Rare, Very Rare, or Legendary');
                """;
            command.ExecuteNonQuery();
        }

        IReadOnlyList<SuggestedCorrectionModel> found = ContentDoctorService.ReadSuggestions(db);

        found.Select(item => item.AuroraId).Should().Equal("ID_TYPO_UNCOMMON", "ID_TYPO_VERY", "ID_MYTHIC");
        var typo = found.Single(item => item.AuroraId == "ID_TYPO_VERY");
        typo.Field.Should().Be("rarity");
        typo.Name.Should().Be("Cloak of Typos");
        typo.Source.Should().Be("Mordenkainen's Tome of Marvelous Magic");
        typo.RelativePath.Should().Be("third-party/dms-guild/tome.xml");
        typo.Written.Should().Be("Vert Rare");
        typo.Suggested.Should().Be("Very Rare");
        found.Single(item => item.AuroraId == "ID_TYPO_UNCOMMON").Suggested.Should().Be("Uncommon");
        found.Single(item => item.AuroraId == "ID_TYPO_UNCOMMON").Source.Should().BeEmpty("the element has no source book");

        var mythic = found.Single(item => item.AuroraId == "ID_MYTHIC");
        mythic.HasSuggestion.Should().BeFalse();
        mythic.Written.Should().Be("Mythic");
    }

    [Fact]
    public void ADatabaseWithoutTheSetterTablesYieldsNoSuggestionsRatherThanFailing()
    {
        string db = Path.Combine(root, "no-setters.sqlite");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE unrelated (x INTEGER)";
            command.ExecuteNonQuery();
        }

        ContentDoctorService.ReadSuggestions(db).Should().BeEmpty();
    }

    [Fact]
    public void AnAbsentOrOlderDatabaseReportsUnavailableDiagnosticsRatherThanAnEmptySuccess()
    {
        ContentDoctorService.ReadSnapshot(Path.Combine(root, "missing.sqlite")).Problem
            .Should().Contain("No content database");

        string empty = Path.Combine(root, "empty.sqlite");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={empty};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE unrelated (x INTEGER)";
            command.ExecuteNonQuery();
        }

        ContentDoctorService.ReadSnapshot(empty).Problem.Should().Contain("could not be read",
            "an older database cannot prove that no overrides, conflicts or skipped files exist");
    }

    [Fact]
    public void ACorruptDatabaseCannotReportHealthyEmptyDiagnostics()
    {
        string db = Path.Combine(root, "corrupt.sqlite");
        File.WriteAllText(db, "This is not a SQLite database.");

        ContentDoctorService.ReadSnapshot(db).Problem.Should().Contain("could not be read");
    }

    [Fact]
    public void MalformedCorrectionXmlIsAnIndividualFileProblem()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();
        File.WriteAllText(Override, File.ReadAllText(Override).Replace("</elements>", ""));

        ContentDoctorService.Evaluate(Override).Problem.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SnapshotReadsTheCurrentImportAndHonorsCancellation()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();
        File.WriteAllText(Path.Combine(root, "broken.xml"), "<elements><element");
        string db = Path.Combine(root, "content.sqlite");
        await Aurora.Content.ContentImport.ImportAsync(root, db, skipUnusableContent: true);

        ContentDoctorSnapshot snapshot = ContentDoctorService.ReadSnapshot(db);

        snapshot.Problem.Should().BeNull();
        snapshot.Files.Should().ContainSingle().Which.Corrections.Should().ContainSingle();
        snapshot.Skipped.Should().Contain(s => s.RelativePath == "broken.xml" && s.Kind == "unreadable");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action load = () => ContentDoctorService.ReadSnapshot(db, cancellation.Token);
        load.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task ARetiredOverrideIsNotReportedAsAMissingActiveFile()
    {
        File.WriteAllText(Upstream, Corrected);
        WriteOverride("accepted-upstream");
        string db = Path.Combine(root, "content.sqlite");
        await Aurora.Content.ContentImport.ImportAsync(root, db);

        File.Exists(Override).Should().BeFalse("the import retired the accepted override");
        Directory.GetFiles(Path.GetDirectoryName(Override)!, "fix.xml.retired-*").Should().ContainSingle();
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db};Mode=ReadOnly;Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT status FROM local_override_files WHERE file_path = $path";
            command.Parameters.AddWithValue("$path", Override);
            command.ExecuteScalar().Should().Be("retired", "retirement history remains in the database");
        }

        ContentDoctorSnapshot snapshot = ContentDoctorService.ReadSnapshot(db);

        snapshot.Problem.Should().BeNull();
        snapshot.Files.Should().BeEmpty("a deliberately retired correction no longer needs an active XML file");
    }

    [Fact]
    public async Task AMissingActiveOverrideStillReportsAFileProblem()
    {
        File.WriteAllText(Upstream, Baseline);
        WriteOverride();
        string db = Path.Combine(root, "content.sqlite");
        await Aurora.Content.ContentImport.ImportAsync(root, db);
        File.Delete(Override);

        ContentDoctorSnapshot snapshot = ContentDoctorService.ReadSnapshot(db);

        snapshot.Problem.Should().BeNull();
        snapshot.Files.Should().ContainSingle().Which.Problem.Should().Contain("no longer on disk",
            "only confirmed retirement may suppress a missing override warning");
    }
}
