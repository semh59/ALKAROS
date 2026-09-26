using System.Globalization;
using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// V12-ONL-009: a platform's order polling has failed (or been rate limited) <see cref="FailureStreak"/> times in a
/// row. Orders are not lost — the cursor waits — but while it lasts, orders the platform's webhook did not deliver
/// are not reaching the restaurant. The poller retries on its own; the case's next action is to check the platform
/// connection. The divergence ends with the next successful poll. The key names the failure streak (its start), so
/// a later streak of the same platform is a case of its own.
/// </summary>
public sealed class ProviderPollingFailingSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-polling:";

    /// <summary>The same threshold the poller documents (OnlineOrderPoller.FailureStreakForCase).</summary>
    public const int FailureStreak = 3;

    private readonly NpgsqlDataSource _dataSource;

    public ProviderPollingFailingSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.ProviderPollingFailing;
    public string Kind => OnlineOrderDivergenceKind.ProviderPollingFailing;
    public bool RequiresManualResolution => false;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT s.provider, s.failing_since, s.consecutive_failures, s.last_error
            FROM online_ordering.provider_poll_state s
            WHERE s.consecutive_failures >= $1
            ORDER BY s.provider
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(FailureStreak);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var provider = reader.GetString(0);
            var failingSince = reader.GetFieldValue<DateTimeOffset>(1);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.CheckChannelConnection, Channel: provider,
                Reason: reader.IsDBNull(3) ? null : reader.GetString(3), Provider: provider);
            return new DetectedDiscrepancy(
                string.Create(CultureInfo.InvariantCulture, $"{DeduplicationPrefix}{provider}:{failingSince.ToUnixTimeMilliseconds()}"),
                CaseType.OnlineOrderMismatch,
                $"online_ordering.provider_poll_state:{provider}",
                $"{provider}:polling",
                0m,
                CaseSeverity.Medium,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
