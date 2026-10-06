using Aurora.App.Services;
using Aurora.Content.Contracts;
using System.Xml.Linq;

namespace Aurora.Tests.Tests;

/// <summary>
/// Writing an override puts a file in the reader's content folder, so the tests are about what the importer
/// will read from it, what is never touched, and everything that has to be refused instead of guessed.
/// </summary>
public sealed class OverrideAuthoringTests : IDisposable
{
    private const string Relative = "third-party/pub/tome.xml";

    private static readonly DateOnly Today = new(2026, 10, 7);

    private readonly string root = Path.Combine(Path.GetTempPath(), "aurora-override-" + Guid.NewGuid().ToString("N"));

    private string UpstreamPath => Path.Combine(root, "third-party", "pub", "tome.xml");

    private string OverridePath => Path.Combine(root, "user", "local", OverrideAuthoring.Folder, "third-party", "pub", "tome.xml");

    // Indented, with an entity, a comment and an unrelated setter: all of it has to come through unchanged.
    private const string Upstream =
        "<elements>\n" +
        "    <info>\n" +
        "        <name>Test Tome</name>\n" +
        "        <update version=\"1.0.0\"><file name=\"tome.xml\" url=\"https://example.test/tome.xml\" /></update>\n" +
        "    </info>\n" +
        "    <!-- the crown -->\n" +
        "    <element name=\"Crown\" type=\"Magic Item\" source=\"Test Tome\" id=\"ID_TYPO_ONE\">\n" +
        "        <description><p>Rules &amp; text.</p></description>\n" +
        "        <setters>\n" +
        "            <set name=\"rarity\">Vert Rare</set>\n" +
        "            <set name=\"attunement\">true</set>\n" +
        "        </setters>\n" +
        "    </element>\n" +
        "    <element name=\"Rattle\" type=\"Magic Item\" source=\"Test Tome\" id=\"ID_TYPO_TWO\">\n" +
        "        <setters><set name=\"rarity\">Lgendary</set></setters>\n" +
        "    </element>\n" +
        "    <element name=\"Plain Ring\" type=\"Magic Item\" source=\"Test Tome\" id=\"ID_CLEAN\">\n" +
        "        <setters><set name=\"rarity\">Rare</set></setters>\n" +
        "    </element>\n" +
        "    <element name=\"Trinket\" type=\"Item\" source=\"Test Tome\" id=\"ID_OTHER\">\n" +
        "        <setters><set name=\"cost\" currency=\"gp\">5</set></setters>\n" +
        "    </element>\n" +
        "</elements>\n";

    public OverrideAuthoringTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UpstreamPath)!);
        File.WriteAllText(UpstreamPath, Upstream);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static SuggestedCorrectionModel Repair(
        string id, string name, string written, string suggested, string relative = Relative, string field = "rarity") =>
        new(field, id, name, "Test Tome", relative, written, suggested);

    private static SuggestedCorrectionModel Crown(string relative = Relative) =>
        Repair("ID_TYPO_ONE", "Crown", "Vert Rare", "Very Rare", relative);

    private static SuggestedCorrectionModel Rattle(string relative = Relative) =>
        Repair("ID_TYPO_TWO", "Rattle", "Lgendary", "Legendary", relative);

    private static string RarityOf(XDocument document, string id) =>
        document.Root!.Elements("element").Single(e => (string?)e.Attribute("id") == id)
            .Descendants("set").Single(s => (string?)s.Attribute("name") == "rarity").Value;

    private PlannedOverride PlanBoth() => OverrideAuthoring.Plan(root, [Crown(), Rattle()], Today);

    private OverrideAuthoringException Refusal(Action action) =>
        action.Should().Throw<OverrideAuthoringException>().Which;

    // ── What the importer will read ──────────────────────────────────────────

    [Fact]
    public void A_plan_reads_back_as_exactly_the_repairs_and_nothing_else()
    {
        var plan = PlanBoth();

        plan.SourcePath.Should().Be(Relative);
        plan.OverridePath.Should().Be(OverridePath);
        plan.Repairs.Select(repair => repair.Key).Should().Equal("rarity-repair-ID_TYPO_ONE", "rarity-repair-ID_TYPO_TWO");

        var evaluation = LocalCorrectionDocument.Evaluate(plan.OverrideXml, Upstream);
        evaluation.ReviewReasons.Should().BeEquivalentTo(["rarity-repair-ID_TYPO_ONE: pinned", "rarity-repair-ID_TYPO_TWO: pinned"]);
        evaluation.CanRetire.Should().BeFalse("upstream still has the typos, so the override is still doing work");
        evaluation.SuppressedIds.Should().BeEmpty();
        evaluation.Corrections.Should().OnlyContain(c => c.Operation == "replace" && c.State == "review-pending" && c.OriginalFingerprint != null);

        var effective = LocalCorrectionDocument.Parse(evaluation.EffectiveXml);
        RarityOf(effective, "ID_TYPO_ONE").Should().Be("Very Rare");
        RarityOf(effective, "ID_TYPO_TWO").Should().Be("Legendary");
        RarityOf(effective, "ID_CLEAN").Should().Be("Rare");

        // Every other element is exactly what upstream has.
        var upstream = LocalCorrectionDocument.Parse(Upstream);
        foreach (string untouched in new[] { "ID_CLEAN", "ID_OTHER" })
        {
            LocalCorrectionDocument.Fingerprint(effective.Root!.Elements("element").Single(e => (string?)e.Attribute("id") == untouched))
                .Should().Be(LocalCorrectionDocument.Fingerprint(upstream.Root!.Elements("element").Single(e => (string?)e.Attribute("id") == untouched)));
        }
    }

    [Fact]
    public void Each_repair_records_what_was_done_when_and_that_it_is_not_accepted_upstream()
    {
        var plan = OverrideAuthoring.Plan(root, [Crown()], Today);

        var reason = LocalCorrectionDocument.Evaluate(plan.OverrideXml, Upstream).Corrections.Single().Reason;

        reason.Should().StartWith("2026-10-07 user-authorized local repair");
        reason.Should().Contain("\"Vert Rare\"").And.Contain("\"Very Rare\"").And.Contain("Crown").And.EndWith("Not accepted upstream.");
    }

    [Fact]
    public void The_rest_of_the_file_keeps_its_layout()
    {
        // The library re-serializes the file, which writes this platform's line endings; layout is the point.
        string text = PlanBoth().OverrideXml.Replace("\r\n", "\n");

        text.Should().Contain("    <!-- the crown -->\n");
        text.Should().Contain("        <description><p>Rules &amp; text.</p></description>\n");
        text.Should().Contain("            <set name=\"attunement\">true</set>\n");
        text.Should().Contain("<set name=\"rarity\">Very Rare</set>").And.NotContain("Vert Rare</set>");
        text.Should().Contain("<set name=\"cost\" currency=\"gp\">5</set>");
    }

    [Fact]
    public void A_path_as_the_database_spells_it_with_backslashes_is_the_same_path()
    {
        var plan = OverrideAuthoring.Plan(root, [Crown(@"third-party\pub\tome.xml")], Today);

        plan.SourcePath.Should().Be(Relative, "source-path is written with forward slashes");
        plan.OverridePath.Should().Be(OverridePath);
    }

    // ── Preparing changes nothing; writing does ──────────────────────────────

    [Fact]
    public void Planning_writes_nothing()
    {
        string before = LocalCorrectionDocument.FileFingerprint(UpstreamPath);

        PlanBoth();

        Directory.Exists(Path.Combine(root, "user")).Should().BeFalse();
        LocalCorrectionDocument.FileFingerprint(UpstreamPath).Should().Be(before);
    }

    [Fact]
    public void Writing_puts_the_override_where_the_importer_looks_and_leaves_the_upstream_alone()
    {
        string before = LocalCorrectionDocument.FileFingerprint(UpstreamPath);
        var plan = PlanBoth();

        string written = OverrideAuthoring.Write(plan);

        written.Should().Be(OverridePath);
        File.ReadAllText(OverridePath).Should().Be(plan.OverrideXml);
        LocalCorrectionDocument.FileFingerprint(UpstreamPath).Should().Be(before, "the original content file is never modified");
        Directory.GetFiles(Path.GetDirectoryName(OverridePath)!).Should().ContainSingle("no temporary file is left behind");

        // The importer's own entry point: it finds the content root from the location and applies the repairs.
        var evaluation = LocalCorrectionDocument.FromFile(OverridePath, root);
        evaluation.Should().NotBeNull();
        evaluation!.SourcePath.Should().Be(Relative);
        RarityOf(LocalCorrectionDocument.Parse(evaluation.EffectiveXml), "ID_TYPO_ONE").Should().Be("Very Rare");
    }

    [Fact]
    public void Writing_never_replaces_a_file_that_is_already_there()
    {
        var plan = PlanBoth();
        Directory.CreateDirectory(Path.GetDirectoryName(OverridePath)!);
        File.WriteAllText(OverridePath, "something the reader put here");

        Refusal(() => OverrideAuthoring.Write(plan)).Message.Should().Contain("already exists");

        File.ReadAllText(OverridePath).Should().Be("something the reader put here");
        Directory.GetFiles(Path.GetDirectoryName(OverridePath)!).Should().ContainSingle();
    }

    [Fact]
    public void Writing_refuses_when_the_content_file_changed_since_it_was_prepared()
    {
        var plan = PlanBoth();
        File.WriteAllText(UpstreamPath, Upstream.Replace("Test Tome</name>", "Test Tome, revised</name>"));

        Refusal(() => OverrideAuthoring.Write(plan)).Message.Should().Contain("changed since");

        File.Exists(OverridePath).Should().BeFalse();
    }

    // ── What is refused rather than guessed ──────────────────────────────────

    [Fact]
    public void A_second_override_for_the_same_file_is_refused_even_from_another_folder()
    {
        OverrideAuthoring.Write(PlanBoth());

        string existing = Refusal(() => OverrideAuthoring.Plan(root, [Crown()], Today)).Message;

        existing.Should().Contain("already exists").And.Contain(OverridePath);

        // Wherever the existing override sits under user/local, it counts.
        File.Move(OverridePath, Path.Combine(root, "user", "local", "moved.xml"));
        Refusal(() => OverrideAuthoring.Plan(root, [Crown()], Today)).Message.Should().Contain("moved.xml");
    }

    [Fact]
    public void Nothing_to_repair_is_refused()
    {
        Refusal(() => OverrideAuthoring.Plan(root, [], Today)).Message.Should().Contain("nothing to repair");
    }

    [Fact]
    public void Only_a_rarity_typo_with_a_suggested_spelling_can_be_written()
    {
        Refusal(() => OverrideAuthoring.Plan(root, [Repair("ID_TYPO_ONE", "Crown", "Vert Rare", "Very Rare", field: "cost")], Today))
            .Message.Should().Contain("rarity typo");
        Refusal(() => OverrideAuthoring.Plan(root, [new SuggestedCorrectionModel("rarity", "ID_TYPO_ONE", "Crown", "Test Tome", Relative, "Vert Rare", null)], Today))
            .Message.Should().Contain("suggested spelling");
    }

    [Fact]
    public void Repairs_from_different_files_are_refused_because_one_override_covers_one_file()
    {
        Refusal(() => OverrideAuthoring.Plan(root, [Crown(), Rattle("third-party/pub/other.xml")], Today))
            .Message.Should().Contain("same file");
    }

    [Theory]
    [InlineData("user/local/something.xml")]
    [InlineData("../outside.xml")]
    [InlineData("third-party/pub/tome.txt")]
    public void A_file_that_is_not_authoritative_content_inside_the_content_folder_is_refused(string relative)
    {
        Refusal(() => OverrideAuthoring.Plan(root, [Crown(relative)], Today)).Message.Should().Contain("cannot be corrected");
    }

    [Fact]
    public void A_content_file_that_is_gone_is_refused()
    {
        Refusal(() => OverrideAuthoring.Plan(root, [Crown("third-party/pub/gone.xml")], Today))
            .Message.Should().Contain("no longer on disk");
    }

    [Fact]
    public void A_repair_for_an_element_the_file_no_longer_declares_is_refused()
    {
        Refusal(() => OverrideAuthoring.Plan(root, [Repair("ID_NOT_THERE", "Ghost", "Vert Rare", "Very Rare")], Today))
            .Message.Should().Contain("no longer declared");
    }

    [Fact]
    public void An_id_declared_twice_cannot_have_a_repair_aimed_at_one_of_them()
    {
        File.WriteAllText(UpstreamPath, Upstream.Replace("</elements>",
            "<element name=\"Crown Again\" type=\"Magic Item\" source=\"Test Tome\" id=\"ID_TYPO_ONE\">" +
            "<setters><set name=\"rarity\">Vert Rare</set></setters></element></elements>"));

        Refusal(() => OverrideAuthoring.Plan(root, [Crown()], Today)).Message.Should().Contain("declared 2 times");
    }

    [Fact]
    public void A_file_that_no_longer_says_what_was_suggested_is_refused()
    {
        File.WriteAllText(UpstreamPath, Upstream.Replace("Vert Rare", "Very Rare"));

        Refusal(() => OverrideAuthoring.Plan(root, [Crown()], Today)).Message.Should().Contain("no longer says");
    }

    [Fact]
    public void A_file_that_is_not_valid_content_is_refused()
    {
        File.WriteAllText(UpstreamPath, "<elements><element");
        Refusal(() => OverrideAuthoring.Plan(root, [Crown()], Today)).Message.Should().Contain("could not be read");

        File.WriteAllText(UpstreamPath, "<other/>");
        Refusal(() => OverrideAuthoring.Plan(root, [Crown()], Today)).Message.Should().Contain("could not be read");
    }

    [Fact]
    public void A_file_that_already_carries_correction_metadata_is_not_authoritative_and_is_refused()
    {
        string already = LocalCorrectionDocument.Create(Upstream, Upstream, Relative, []);
        File.WriteAllText(UpstreamPath, already);

        Refusal(() => OverrideAuthoring.Plan(root, [Crown()], Today)).Message.Should().Contain("correction metadata");
    }
}
