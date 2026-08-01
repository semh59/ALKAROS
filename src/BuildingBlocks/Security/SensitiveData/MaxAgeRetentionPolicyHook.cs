namespace ALKAROS.SensitiveData;

/// <summary>
/// <see cref="IRetentionPolicyHook"/> that expires envelopes older than a
/// configured maximum age. The age is supplied by the policy owner, keeping
/// the boundary free of business retention decisions.
/// </summary>
public sealed class MaxAgeRetentionPolicyHook : IRetentionPolicyHook
{
    private readonly TimeSpan _maxAge;

    public MaxAgeRetentionPolicyHook(TimeSpan maxAge)
    {
        if (maxAge < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxAge), "maxAge must be non-negative.");
        _maxAge = maxAge;
    }

    /// <inheritdoc/>
    public bool IsExpired(SensitiveEnvelope envelope, DateTimeOffset now) =>
        now - envelope.CreatedAt > _maxAge;
}
