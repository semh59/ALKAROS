namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>Lifecycle of an <c>identity.authorization_grants</c> row.</summary>
public enum GrantStatus
{
    /// <summary>Raised, awaiting delegation or a manager decision.</summary>
    Pending,

    /// <summary>Authorized. The one command instance may proceed.</summary>
    Granted,

    /// <summary>Refused.</summary>
    Denied,
}

/// <summary>Which resolution step decided a terminal grant.</summary>
public enum PolicyPath
{
    /// <summary>A policy row (or the own-check guard) — no human in the loop.</summary>
    Auto,

    /// <summary>An active time-boxed delegation covered it (V1-IAM-021).</summary>
    Delegation,

    /// <summary>A manager approved or denied it (V1-IAM-020).</summary>
    Manual,
}

/// <summary>Text mappings for the <c>status</c> and <c>policy_path</c> columns.</summary>
public static class GrantText
{
    public static string Status(GrantStatus status) => status switch
    {
        GrantStatus.Pending => "pending",
        GrantStatus.Granted => "granted",
        GrantStatus.Denied => "denied",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static GrantStatus Status(string text) => text switch
    {
        "pending" => GrantStatus.Pending,
        "granted" => GrantStatus.Granted,
        "denied" => GrantStatus.Denied,
        _ => throw new ArgumentException($"Unknown grant status '{text}'.", nameof(text)),
    };

    public static string Path(PolicyPath path) => path switch
    {
        PolicyPath.Auto => "auto",
        PolicyPath.Delegation => "delegation",
        PolicyPath.Manual => "manual",
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, null),
    };

    public static PolicyPath Path(string text) => text switch
    {
        "auto" => PolicyPath.Auto,
        "delegation" => PolicyPath.Delegation,
        "manual" => PolicyPath.Manual,
        _ => throw new ArgumentException($"Unknown policy path '{text}'.", nameof(text)),
    };
}
