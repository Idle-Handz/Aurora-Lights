using Aurora.App.Services.Updates;

namespace Aurora.Tests.Tests;

/// <summary>
/// Parser + comparator coverage for the lightweight SemVer used by the in-app update check.
/// The parser must absorb anything a GitHub release tag might throw at it (v-prefix, pre-release
/// suffix, build metadata) without throwing — bad input is a clean false, not an exception.
/// </summary>
public class SemVerTests
{
    // ── Parse: well-formed ────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.2.3",          1, 2, 3, null)]
    [InlineData("v1.2.3",         1, 2, 3, null)]
    [InlineData("V1.2.3",         1, 2, 3, null)]
    [InlineData("0.0.0",          0, 0, 0, null)]
    [InlineData("10.20.30",      10, 20, 30, null)]
    [InlineData("1.2",            1, 2, 0, null)]
    [InlineData("1",              1, 0, 0, null)]
    [InlineData("v1.2.3-alpha",   1, 2, 3, "alpha")]
    [InlineData("1.2.3-rc.4",     1, 2, 3, "rc.4")]
    [InlineData("1.2.3+buildmeta", 1, 2, 3, null)]
    [InlineData("1.2.3-rc.1+abc",  1, 2, 3, "rc.1")]
    [InlineData("  v1.2.3  ",     1, 2, 3, null)] // surrounding whitespace
    public void TryParse_acceptsWellFormed(string text, int major, int minor, int patch, string? pre)
    {
        SemVer.TryParse(text, out var v).Should().BeTrue();
        v.Major.Should().Be(major);
        v.Minor.Should().Be(minor);
        v.Patch.Should().Be(patch);
        v.PreRelease.Should().Be(pre);
    }

    // ── Parse: rejected ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    [InlineData("v")]
    [InlineData("1.2.3.4")]      // 4 dot components
    [InlineData("1.2.3-")]       // trailing dash, empty pre-release
    [InlineData("-1.2.3")]       // negative major
    [InlineData("a.b.c")]
    public void TryParse_rejectsBadInput(string? text)
    {
        SemVer.TryParse(text, out var v).Should().BeFalse();
        v.Should().Be(default(SemVer));
    }

    // ── IsPreRelease + ToString round-trip ───────────────────────────────────

    [Fact]
    public void IsPreRelease_reflectsSuffix()
    {
        SemVer.TryParse("1.2.3-alpha", out var pre).Should().BeTrue();
        pre.IsPreRelease.Should().BeTrue();

        SemVer.TryParse("1.2.3", out var rel).Should().BeTrue();
        rel.IsPreRelease.Should().BeFalse();
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1.2.3-alpha")]
    [InlineData("1.2.3-rc.4")]
    public void ToString_roundTripsThroughParse(string canonical)
    {
        SemVer.TryParse(canonical, out var v).Should().BeTrue();
        v.ToString().Should().Be(canonical);
    }

    // ── CompareTo: core SemVer ordering ──────────────────────────────────────

    [Theory]
    [InlineData("1.0.0",  "2.0.0")]   // major
    [InlineData("1.0.0",  "1.1.0")]   // minor
    [InlineData("1.0.0",  "1.0.1")]   // patch
    public void CompareTo_orderingByPrecedence(string lower, string higher)
    {
        SemVer.TryParse(lower,  out var lo).Should().BeTrue();
        SemVer.TryParse(higher, out var hi).Should().BeTrue();

        (lo < hi).Should().BeTrue();
        (hi > lo).Should().BeTrue();
        (lo <= hi).Should().BeTrue();
        (hi >= lo).Should().BeTrue();
        lo.CompareTo(hi).Should().BeLessThan(0);
        hi.CompareTo(lo).Should().BeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_equalVersionsAreEqual()
    {
        SemVer.TryParse("1.2.3", out var a).Should().BeTrue();
        SemVer.TryParse("v1.2.3", out var b).Should().BeTrue();
        a.CompareTo(b).Should().Be(0);
        (a >= b).Should().BeTrue();
        (a <= b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    // ── SemVer §11: pre-release < release at same M.M.P ──────────────────────

    [Fact]
    public void Release_outranksMatchingPreRelease()
    {
        // SemVer §11: 1.0.0-alpha < 1.0.0
        SemVer.TryParse("1.0.0-alpha", out var pre).Should().BeTrue();
        SemVer.TryParse("1.0.0",       out var rel).Should().BeTrue();
        (pre < rel).Should().BeTrue();
    }

    [Theory]
    // Examples drawn directly from semver.org §11.
    [InlineData("1.0.0-alpha",      "1.0.0-alpha.1")]    // shorter < longer when prefix equal
    [InlineData("1.0.0-alpha.1",    "1.0.0-alpha.beta")] // numeric < non-numeric
    [InlineData("1.0.0-alpha.beta", "1.0.0-beta")]       // ASCII-lex on identifiers
    [InlineData("1.0.0-beta",       "1.0.0-beta.2")]
    [InlineData("1.0.0-beta.2",     "1.0.0-beta.11")]    // numeric compare, not lex (2 < 11)
    [InlineData("1.0.0-beta.11",    "1.0.0-rc.1")]
    public void PreReleaseOrdering_matchesSpec(string lower, string higher)
    {
        SemVer.TryParse(lower,  out var lo).Should().BeTrue();
        SemVer.TryParse(higher, out var hi).Should().BeTrue();
        (lo < hi).Should().BeTrue($"'{lower}' should sort before '{higher}'");
    }

    // ── Build metadata is parsed but ignored for comparison (SemVer §10) ────

    [Fact]
    public void BuildMetadata_doesNotAffectOrdering()
    {
        SemVer.TryParse("1.2.3+build.1",   out var a).Should().BeTrue();
        SemVer.TryParse("1.2.3+build.999", out var b).Should().BeTrue();
        a.CompareTo(b).Should().Be(0);
    }
}
