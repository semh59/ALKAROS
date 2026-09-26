namespace ALKAROS.OnlineOrdering.Providers.Contracts;

/// <summary>
/// One order event read from a platform's order list. <see cref="EventKey"/> must be the same key the platform's
/// webhook path uses for the same event, so an event that arrives both ways is stored once.
/// </summary>
public sealed record PolledOrderEvent(
    string EventKey,
    string ExternalOrderId,
    string ProviderStatus,
    string? ProviderUpdatedAt,
    ReadOnlyMemory<byte> RawBody);

/// <summary>What one poll read, and the cursor the next poll continues from (null keeps the current one).</summary>
public sealed record OnlineOrderPollPage(IReadOnlyList<PolledOrderEvent> Events, string? NextCursor);

/// <summary>
/// V12-ONL-009: the optional port a platform adapter implements when its orders can (or must) be read by polling —
/// a platform that may switch off an unreachable webhook, or one that only offers an order list.
/// </summary>
public interface IOnlineOrderPollingSource
{
    /// <summary>The platform identity (<see cref="IOnlineOrderProvider.Provider"/>).</summary>
    string Provider { get; }

    /// <summary>How often the platform may be polled; its rate limit decides this.</summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Reads the platform's orders after <paramref name="cursor"/> (null the first time). Throws
    /// <see cref="OnlineOrderPollRateLimitedException"/> when the platform answers "too many requests" and
    /// <see cref="OnlineOrderPollingNotConfiguredException"/> when the platform's settings are not entered.
    /// </summary>
    Task<OnlineOrderPollPage> PollAsync(string? cursor, CancellationToken cancellationToken = default);
}

/// <summary>The platform refused the poll for its rate limit; the next poll waits at least <see cref="RetryAfter"/>.</summary>
public sealed class OnlineOrderPollRateLimitedException : Exception
{
    public OnlineOrderPollRateLimitedException(TimeSpan? retryAfter)
        : base("The platform refused the order poll for its rate limit.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan? RetryAfter { get; }
}

/// <summary>The platform's settings are not entered, so the channel is off; not a failure.</summary>
public sealed class OnlineOrderPollingNotConfiguredException : Exception
{
    public OnlineOrderPollingNotConfiguredException(string provider)
        : base($"Order polling for '{provider}' is not configured.") { }
}
