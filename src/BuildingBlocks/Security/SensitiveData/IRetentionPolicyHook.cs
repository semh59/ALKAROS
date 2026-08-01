namespace ALKAROS.SensitiveData;

/// <summary>
/// Retention-policy hook: decides whether a stored envelope is expired and
/// may be disposed. The boundary supplies the hook and the stored timestamp
/// (<see cref="SensitiveEnvelope.CreatedAt"/>); the retention period itself
/// is owned by the policy configuration and is not decided here.
/// </summary>
public interface IRetentionPolicyHook
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="envelope"/> is older than
    /// the configured retention period as of <paramref name="now"/>.
    /// </summary>
    bool IsExpired(SensitiveEnvelope envelope, DateTimeOffset now);
}
