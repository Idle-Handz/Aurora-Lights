using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;

namespace Builder.Presentation.Services;

/// <summary>
/// Optional host policy consulted before an element is granted to a character, and again whenever
/// grants are re-evaluated. Aurora Legacy registers none and grants exactly as original Aurora did.
/// </summary>
public interface IGrantPolicy
{
    /// <summary>True when <paramref name="granted"/> must not reach the character through <paramref name="rule"/>.</summary>
    bool IsSuppressed(ElementBase granted, GrantRule rule);
}

/// <summary>Holds the host's <see cref="IGrantPolicy"/>, when it registered one.</summary>
public static class GrantPolicyContext
{
    public static IGrantPolicy? Current { get; set; }

    /// <summary>
    /// Asks the registered policy, treating "no policy" and a failing policy as "grant it": a host
    /// policy must never be able to strip content off a character by throwing.
    /// </summary>
    public static bool IsSuppressed(ElementBase granted, GrantRule rule)
    {
        IGrantPolicy? policy = Current;
        if (policy is null || granted is null || rule is null)
            return false;
        try
        {
            return policy.IsSuppressed(granted, rule);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(IsSuppressed));
            return false;
        }
    }
}
