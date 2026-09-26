using ALKAROS.Reconciliation.CaseFoundation;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// V12-REC-001: the two things a manager does with an online order case.
///
/// <para>Retry performs the case's safe next action once. The effect is conditional on the source still
/// being in the failed state (a dead outbox message, a refused or failed provider event), so two managers
/// retrying at once produce exactly one effect; the case row is locked for the whole retry so a retry
/// never runs against a case being closed at the same moment. The attempt is recorded in the same
/// transaction as its effect, so a retry that found nothing left to do is still on record, and the case
/// timeline then gets a note through the V1-REC-001 service.</para>
///
/// <para>Resolve checks the case's own source again: a divergence the source still shows cannot be marked
/// resolved. Only a divergence the source can never show ending (a cancellation after handover) is
/// resolved on a person's recorded decision alone. Every resolution carries a note and goes through the
/// V1-REC-001 transition, which audits it and refuses a stale version.</para>
/// </summary>
public sealed class OnlineOrderReconciliationActions
{
    private static readonly Dictionary<(string Action, bool Requeued), string> RetryNotes = new()
    {
        [(OnlineOrderNextAction.ReprocessProviderEvent, true)] = "Yeniden deneme: sağlayıcı olayı yeniden işlenmek üzere sıraya alındı.",
        [(OnlineOrderNextAction.ReprocessProviderEvent, false)] = "Yeniden deneme: yeniden işlenecek bir sağlayıcı olayı kalmamış.",
        [(OnlineOrderNextAction.ResendProviderCancellation, true)] = "Yeniden deneme: sağlayıcıya iptal bildirimi yeniden gönderilecek.",
        [(OnlineOrderNextAction.ResendProviderCancellation, false)] = "Yeniden deneme: yeniden gönderilecek bir iptal bildirimi kalmamış.",
        [(OnlineOrderNextAction.ResendProviderUpdate, true)] = "Yeniden deneme: sağlayıcıya durum bildirimi yeniden gönderilecek.",
        [(OnlineOrderNextAction.ResendProviderUpdate, false)] = "Yeniden deneme: yeniden gönderilecek bir durum bildirimi kalmamış.",
    };

    private readonly NpgsqlDataSource _dataSource;
    private readonly IReadOnlyList<IOnlineOrderSourcePair> _sourcePairs;
    private readonly IReconciliationService _reconciliationService;
    private readonly IProviderEventReprocessing _reprocessing;

    public OnlineOrderReconciliationActions(
        NpgsqlDataSource dataSource,
        IReadOnlyList<IOnlineOrderSourcePair> sourcePairs,
        IReconciliationService reconciliationService,
        IProviderEventReprocessing reprocessing)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _reprocessing = reprocessing ?? throw new ArgumentNullException(nameof(reprocessing));
        _sourcePairs = sourcePairs ?? throw new ArgumentNullException(nameof(sourcePairs));
        _reconciliationService = reconciliationService ?? throw new ArgumentNullException(nameof(reconciliationService));
    }

    public async Task<OnlineOrderRetryResult> RetryAsync(Guid caseId, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("A retry needs an actor.", nameof(actorId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        string caseType;
        string status;
        string? detailsJson;
        await using (var select = new NpgsqlCommand(
            "SELECT case_type, status, details::text FROM reconciliation.cases WHERE case_id = $1 FOR UPDATE;",
            connection, transaction))
        {
            select.Parameters.AddWithValue(caseId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return new OnlineOrderRetryResult(OnlineOrderRetryOutcome.CaseNotFound, null);
            caseType = reader.GetString(0);
            status = reader.GetString(1);
            detailsJson = reader.IsDBNull(2) ? null : reader.GetString(2);
        }

        var details = OnlineOrderCaseDetails.TryParse(detailsJson);
        if (caseType != nameof(CaseType.OnlineOrderMismatch) || details is null)
            return new OnlineOrderRetryResult(OnlineOrderRetryOutcome.NotAnOnlineOrderCase, null);
        if (status is not (nameof(CaseStatus.Open) or nameof(CaseStatus.Investigating) or nameof(CaseStatus.Escalated)))
            return new OnlineOrderRetryResult(OnlineOrderRetryOutcome.CaseNotActive, details.NextAction);

        var (affected, sourceRef) = details.NextAction switch
        {
            OnlineOrderNextAction.ReprocessProviderEvent when details.InboxId is { } inboxId =>
                (await _reprocessing.ReopenForReprocessingAsync(inboxId, connection, transaction, cancellationToken).ConfigureAwait(false),
                 $"online_ordering.yemeksepeti_webhook_inbox:{inboxId}"),
            OnlineOrderNextAction.ResendProviderCancellation when details.ExternalOrderId is { } externalOrderId =>
                (await RequeueDeadUpdatesForOrderAsync(externalOrderId, connection, transaction, cancellationToken).ConfigureAwait(false),
                 $"yemeksepeti:order:{externalOrderId}"),
            OnlineOrderNextAction.ResendProviderUpdate when details.OutboxMessageId is { } messageId =>
                (await RequeueDeadUpdateAsync(messageId, connection, transaction, cancellationToken).ConfigureAwait(false),
                 $"outbox_messages:{messageId}"),
            _ => (-1, string.Empty),
        };
        if (affected < 0)
            return new OnlineOrderRetryResult(OnlineOrderRetryOutcome.NotRetryable, details.NextAction);

        var requeued = affected > 0;
        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO reconciliation.online_order_retry_attempts (attempt_id, case_id, action, outcome, source_ref, performed_by)
            VALUES ($1, $2, $3, $4, $5, $6);
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(Guid.NewGuid());
            insert.Parameters.AddWithValue(caseId);
            insert.Parameters.AddWithValue(details.NextAction);
            insert.Parameters.AddWithValue(requeued ? nameof(OnlineOrderRetryOutcome.Requeued) : nameof(OnlineOrderRetryOutcome.NothingToRetry));
            insert.Parameters.AddWithValue(sourceRef);
            insert.Parameters.AddWithValue(actorId);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        await _reconciliationService.AddCaseNoteAsync(
            new AddCaseNoteRequest(caseId, RetryNotes[(details.NextAction, requeued)], actorId), cancellationToken).ConfigureAwait(false);
        return new OnlineOrderRetryResult(
            requeued ? OnlineOrderRetryOutcome.Requeued : OnlineOrderRetryOutcome.NothingToRetry, details.NextAction);
    }

    public async Task<OnlineOrderResolveResult> ResolveAsync(
        Guid caseId, int expectedVersion, string note, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("A resolution needs an actor.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A resolution needs a note saying how it was settled.", nameof(note));

        var record = await _reconciliationService.GetCaseByIdAsync(caseId, cancellationToken).ConfigureAwait(false);
        if (record is null)
            return new OnlineOrderResolveResult(OnlineOrderResolveOutcome.CaseNotFound, null);

        var details = OnlineOrderCaseDetails.TryParse(record.DetailsJson);
        var source = details is null ? null : _sourcePairs.FirstOrDefault(pair => pair.Kind == details.Kind);
        if (record.CaseType != CaseType.OnlineOrderMismatch || source is null)
            return new OnlineOrderResolveResult(OnlineOrderResolveOutcome.NotAnOnlineOrderCase, record);

        if (!source.RequiresManualResolution)
        {
            var current = await source.ScanAsync(cancellationToken).ConfigureAwait(false);
            if (current.Any(discrepancy => discrepancy.DeduplicationKey == record.DeduplicationKey))
                return new OnlineOrderResolveResult(OnlineOrderResolveOutcome.StillDiverged, record);
        }

        var resolved = await _reconciliationService.TransitionCaseStatusAsync(
            new TransitionCaseStatusRequest(caseId, CaseStatus.Resolved, expectedVersion, actorId, note.Trim()),
            cancellationToken).ConfigureAwait(false);
        return new OnlineOrderResolveResult(OnlineOrderResolveOutcome.Resolved, resolved);
    }

    private static async Task<int> RequeueDeadUpdatesForOrderAsync(
        string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            WITH dead_updates AS MATERIALIZED (
                SELECT id, convert_from(payload_envelope, 'UTF8')::jsonb->>'externalOrderId' AS external_order_id
                FROM outbox_messages
                WHERE event_type = $1 AND status = 'dead')
            UPDATE outbox_messages m
            SET status = 'pending', attempt_count = 0, next_retry_at = NULL, claimed_at = NULL, last_error = NULL
            FROM dead_updates d
            WHERE m.id = d.id AND d.external_order_id = $2 AND m.status = 'dead';
            """, connection, transaction);
        command.Parameters.AddWithValue(OnlineOrderSourceScan.StatusUpdateEventType);
        command.Parameters.AddWithValue(externalOrderId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> RequeueDeadUpdateAsync(
        Guid messageId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE outbox_messages
            SET status = 'pending', attempt_count = 0, next_retry_at = NULL, claimed_at = NULL, last_error = NULL
            WHERE id = $1 AND event_type = $2 AND status = 'dead';
            """, connection, transaction);
        command.Parameters.AddWithValue(messageId);
        command.Parameters.AddWithValue(OnlineOrderSourceScan.StatusUpdateEventType);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
