namespace ALKAROS.Messaging;

/// <summary>
/// Retry and dead-letter policy of V0-ARC-003 (max 3 attempts, exponential
/// backoff). A message that fails three times is moved to the dead-letter
/// state; each earlier failure schedules the next attempt with
/// base-delay * 2^(attempts so far).
/// </summary>
public static class RetryPolicy
{
    public const int MaxAttempts = 3;

    /// <summary>
    /// The delay before the next attempt after <paramref name="completedAttempts"/>
    /// failed attempts. Only valid below <see cref="MaxAttempts"/>; at the
    /// threshold the message is dead, not retried.
    /// </summary>
    public static TimeSpan NextRetryDelay(int completedAttempts, TimeSpan baseDelay)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(completedAttempts);
        if (baseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
        if (completedAttempts >= MaxAttempts)
            throw new ArgumentOutOfRangeException(
                nameof(completedAttempts),
                $"A message with {MaxAttempts} failed attempts is dead and is never retried.");

        var factor = Math.Pow(2, completedAttempts - 1);
        return TimeSpan.FromMilliseconds(Math.Min(
            baseDelay.TotalMilliseconds * factor,
            TimeSpan.MaxValue.TotalMilliseconds));
    }
}
