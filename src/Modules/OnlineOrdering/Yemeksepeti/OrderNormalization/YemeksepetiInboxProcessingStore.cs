using System.Text.Json;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusMapping;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;

public enum InboxProcessingOutcome
{
    OrderCreated,
    OrderAlreadyExists,

    /// <summary>The payload could not become an order (typed <see cref="NormalizationRejection"/>); no order exists.</summary>
    Rejected,

    /// <summary>The provider accepted an order this restaurant could not hold stock for; no order exists.</summary>
    Diverged,

    NoOp,
    UnknownStatus,

    /// <summary>V12-ONL-003: the provider cancelled; the local order was cancelled (holds compensated).</summary>
    OrderCancelled,

    /// <summary>V12-ONL-003: a repeated cancellation of an order already cancelled; nothing was done again.</summary>
    AlreadyCancelled,

    /// <summary>V12-ONL-003: the provider cancelled an order that has no local order.</summary>
    CancelledBeforeOrder,

    /// <summary>V12-ONL-003: a new-order event arrived after the provider had already cancelled that order; no order was created.</summary>
    SkippedCancelledOrder,

    /// <summary>Processing kept failing; closed after <see cref="YemeksepetiInboxProcessingStore.MaxAttempts"/> attempts for review.</summary>
    Failed
}

/// <summary>A stored webhook event claimed for processing inside the caller's transaction.</summary>
public sealed record ClaimedInboxEvent(
    Guid InboxId,
    string ExternalOrderId,
    string ProviderStatus,
    DateTimeOffset ReceivedAt,
    byte[] PayloadEnvelope);

/// <summary>
/// Claims pending webhook events one at a time (<c>FOR UPDATE SKIP LOCKED</c>, so parallel
/// processors never share one) and records each outcome in the same transaction as its effect.
/// </summary>
public static class YemeksepetiInboxProcessingStore
{
    public const int MaxAttempts = 5;

    public static async Task<ClaimedInboxEvent?> ClaimNextAsync(
        IReadOnlyCollection<string> deferredStatuses,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deferredStatuses);
        await using var command = new NpgsqlCommand(
            """
            SELECT inbox_id, external_order_id, provider_status, received_at, payload_envelope
            FROM online_ordering.yemeksepeti_webhook_inbox
            WHERE processed_at IS NULL AND NOT (provider_status = ANY($1))
            ORDER BY processing_attempts, received_at, inbox_id
            LIMIT 1
            FOR UPDATE SKIP LOCKED;
            """, connection, transaction);
        command.Parameters.AddWithValue(deferredStatuses.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;
        return new ClaimedInboxEvent(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<byte[]>(4));
    }

    public static async Task MarkProcessedAsync(
        Guid inboxId,
        InboxProcessingOutcome outcome,
        Guid? orderId,
        object? detail,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE online_ordering.yemeksepeti_webhook_inbox
            SET processed_at = now(), processing_outcome = $2, order_id = $3, outcome_detail = $4
            WHERE inbox_id = $1 AND processed_at IS NULL;
            """, connection, transaction);
        command.Parameters.AddWithValue(inboxId);
        command.Parameters.AddWithValue(outcome.ToString());
        command.Parameters.AddWithValue((object?)orderId ?? DBNull.Value);
        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Jsonb,
            Value = detail is null ? DBNull.Value : JsonSerializer.Serialize(detail)
        });
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidOperationException($"Inbox event '{inboxId}' is not a pending, claimed event.");
    }

    /// <summary>
    /// Records a failed processing attempt on its own connection (the attempt's transaction has
    /// already rolled back). The event is retried later behind events with fewer failures, and
    /// closed as <see cref="InboxProcessingOutcome.Failed"/> once it reaches <see cref="MaxAttempts"/>.
    /// </summary>
    public static async Task RecordFailureAsync(
        Guid inboxId, string error, NpgsqlConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);
        await using var command = new NpgsqlCommand(
            """
            UPDATE online_ordering.yemeksepeti_webhook_inbox
            SET processing_attempts = processing_attempts + 1,
                last_error = left($2, 200),
                processed_at = CASE WHEN processing_attempts + 1 >= $3 THEN now() END,
                processing_outcome = CASE WHEN processing_attempts + 1 >= $3 THEN 'Failed' END
            WHERE inbox_id = $1 AND processed_at IS NULL;
            """, connection);
        command.Parameters.AddWithValue(inboxId);
        command.Parameters.AddWithValue(error);
        command.Parameters.AddWithValue(MaxAttempts);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The status-mapping input (V12-MAP-002) carried by a stored payload: status, delivery kind and
    /// cancellation object. A payload the inbox stored always has an order id and status.
    /// </summary>
    public static YemeksepetiStatusSignal ReadStatusSignal(string externalOrderId, string providerStatus, string rawPayload)
    {
        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;
        var transport = root.TryGetProperty("transport_type", out var transportElement) && transportElement.ValueKind == JsonValueKind.String
            ? transportElement.GetString()
            : null;

        YemeksepetiCancellationSignal? cancellation = null;
        if (root.TryGetProperty("cancellation", out var cancel) && cancel.ValueKind == JsonValueKind.Object)
        {
            cancellation = new YemeksepetiCancellationSignal(
                Text(cancel, "cancelled_by"),
                Text(cancel, "reason"),
                cancel.TryGetProperty("post_picked_up", out var picked) && picked.ValueKind == JsonValueKind.True);
        }

        return new YemeksepetiStatusSignal(externalOrderId, providerStatus, transport, cancellation);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
