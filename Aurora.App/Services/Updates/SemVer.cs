namespace Aurora.App.Services.Updates;

/// <summary>
/// Lightweight SemVer 2 parser/comparator sufficient for "is this release newer than the one running."
/// Handles tags shaped like <c>v0.1.0</c>, <c>0.1.0-alpha</c>, <c>v1.2.3-rc.4+build.5</c>. Build metadata
/// (the <c>+...</c> suffix) is parsed but ignored for comparison per SemVer §10.
/// Not a full SemVer implementation — we only need parse + compare here, so anything more elaborate
/// would be the wrong tool. <see cref="TryParse"/> never throws; bad input returns false.
/// </summary>
public readonly record struct SemVer(int Major, int Minor, int Patch, string? PreRelease)
    : IComparable<SemVer>
{
    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    public override string ToString()
        => PreRelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";

    public static bool TryParse(string? text, out SemVer version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Accept and strip a leading 'v' or 'V' (common in GitHub tags: v1.2.3).
        var s = text.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s[1..];

        // Strip the build-metadata suffix (everything from a '+'); SemVer §10 ignores it for ordering.
        int plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];

        // Split off the pre-release suffix (after the first '-').
        string? preRelease = null;
        int dash = s.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = s[(dash + 1)..];
            s = s[..dash];
            if (string.IsNullOrEmpty(preRelease)) return false;
        }

        var parts = s.Split('.');
        if (parts.Length is < 1 or > 3) return false;
        if (!int.TryParse(parts[0], out int major) || major < 0) return false;
        int minor = 0, patch = 0;
        if (parts.Length > 1 && (!int.TryParse(parts[1], out minor) || minor < 0)) return false;
        if (parts.Length > 2 && (!int.TryParse(parts[2], out patch) || patch < 0)) return false;

        version = new SemVer(major, minor, patch, preRelease);
        return true;
    }

    public int CompareTo(SemVer other)
    {
        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;

        // SemVer §11: a version without a pre-release suffix has higher precedence than one with.
        if (PreRelease is null && other.PreRelease is null) return 0;
        if (PreRelease is null) return 1;
        if (other.PreRelease is null) return -1;

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    /// <summary>
    /// SemVer §11 pre-release ordering: split on '.', compare each identifier — numeric identifiers
    /// lower than non-numeric, numeric compared numerically, non-numeric ASCII-lex, fewer identifiers
    /// is lower when all preceding ones are equal.
    /// </summary>
    private static int ComparePreRelease(string a, string b)
    {
        var ap = a.Split('.');
        var bp = b.Split('.');
        int n = Math.Min(ap.Length, bp.Length);
        for (int i = 0; i < n; i++)
        {
            bool aNum = int.TryParse(ap[i], out int ai);
            bool bNum = int.TryParse(bp[i], out int bi);
            if (aNum && bNum)
            {
                int c = ai.CompareTo(bi);
                if (c != 0) return c;
            }
            else if (aNum) return -1;
            else if (bNum) return 1;
            else
            {
                int c = string.CompareOrdinal(ap[i], bp[i]);
                if (c != 0) return c;
            }
        }
        return ap.Length.CompareTo(bp.Length);
    }

    public static bool operator <(SemVer left, SemVer right)  => left.CompareTo(right) <  0;
    public static bool operator >(SemVer left, SemVer right)  => left.CompareTo(right) >  0;
    public static bool operator <=(SemVer left, SemVer right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemVer left, SemVer right) => left.CompareTo(right) >= 0;
}
