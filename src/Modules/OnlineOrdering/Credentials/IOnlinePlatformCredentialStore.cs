namespace ALKAROS.OnlineOrdering.Credentials;

/// <summary>
/// A change to one platform's settings: <see cref="Values"/> sets fields, <see cref="Cleared"/> removes them;
/// every other stored field stays as it is (so a secret never has to be typed again to change something else).
/// </summary>
public sealed record SaveOnlinePlatformCredentialRequest(
    string Provider,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyCollection<string> Cleared);

/// <summary>What the interface may see of one field: a secret field only says whether it is set.</summary>
public sealed record OnlinePlatformCredentialFieldStatus(string Name, bool IsSecret, bool Configured, string? Value);

public sealed record OnlinePlatformCredentialStatus(
    string Provider,
    IReadOnlyList<OnlinePlatformCredentialFieldStatus> Fields,
    DateTimeOffset? UpdatedAt);

/// <summary>The platform is not one whose settings can be stored.</summary>
public sealed class UnknownOnlinePlatformException : Exception
{
    public UnknownOnlinePlatformException(string provider)
        : base($"'{provider}' is not an online platform with stored settings.")
    {
        Provider = provider;
    }

    public string Provider { get; }
}

/// <summary>Why a settings change is refused; the endpoint turns the code into a Turkish message.</summary>
public enum OnlinePlatformCredentialProblem
{
    NoChange,
    UnknownField,
    SetAndCleared,
    Empty,
    TooLong,
    InvalidCharacters,
    InvalidUrl,
}

public sealed class InvalidOnlinePlatformCredentialException : Exception
{
    public InvalidOnlinePlatformCredentialException(OnlinePlatformCredentialProblem problem, string? field)
        : base($"Online platform setting change refused: {problem}{(field is null ? "" : $" ({field})")}.")
    {
        Problem = problem;
        Field = field;
    }

    public OnlinePlatformCredentialProblem Problem { get; }

    public string? Field { get; }
}

/// <summary>
/// V12-OUI-003: each online platform's API settings. Secret fields are sealed in an AES-256-GCM envelope and
/// never returned over HTTP; every change is written to <c>audit.audit_events</c> (field names only, no value).
/// </summary>
public interface IOnlinePlatformCredentialStore
{
    Task<OnlinePlatformCredentialStatus> GetStatusAsync(string provider, CancellationToken cancellationToken = default);

    Task<OnlinePlatformCredentialStatus> SaveAsync(
        SaveOnlinePlatformCredentialRequest request, Guid? actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// A stored value (secret ones decrypted) for backend calls to the platform, or null when the field is not
    /// stored. Synchronous because <see cref="ALKAROS.Secrets.ISecretProvider"/> is. Never exposed over HTTP.
    /// </summary>
    string? ResolveValue(string provider, string field);
}
