using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Settings.CashDrawer;
using ALKAROS.Settings.TypedSettings;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.DualScreen;

/// <summary>Why a cashier opens the drawer without a sale; <see cref="Name"/> is what the till shows.</summary>
public sealed record CashDrawerReasonV1(string Code, string Name);

public sealed record NoSaleDrawerRequestV1(string IdempotencyKey, string? ReasonCode, string? Note);

public sealed record CashDrawerOpenResultV1(bool Delivered, int NoSaleCount, string? Warning, bool Replayed);

public sealed record CashDrawerStatusV1(
    bool Enabled, bool CanKick, int NoSaleCount, int WarningCount, IReadOnlyList<CashDrawerReasonV1> Reasons);

/// <summary>The drawer openings the audit trail holds for one cash session, by kind.</summary>
public sealed record CashDrawerCounts(int SaleOpenings, int NoSaleOpenings, int FailedKicks);

public sealed class CashDrawerDisabledException()
    : Exception("The cash drawer is switched off or has no printer address.");

public sealed class CashDrawerSessionNotOpenException()
    : Exception("The cash session is no longer open or being counted.");

public sealed class InvalidCashDrawerReasonException()
    : Exception("A no-sale opening needs one of the listed reasons; 'Other' needs a note of at most 200 characters.");

/// <summary>
/// Opens the physical cash drawer through the receipt printer: automatically when cash is taken, and by hand as "no
/// sale". Every opening is an audit event of the cash session; none of it touches the cash ledger. Opening the drawer
/// after a payment is best effort and never changes the payment's answer.
/// </summary>
public sealed partial class CashDrawerService(
    NpgsqlDataSource dataSource,
    IAuditEventStore audit,
    IRoleRepository roles,
    ISettingsService settings,
    ICashDrawerKicker kicker,
    ILogger<CashDrawerService> logger)
{
    /// <summary>
    /// A shift has a few legitimate no-sale openings (change, count); from the fifth the manager should look. A code
    /// constant, not a setting.
    /// </summary>
    public const int NoSaleWarningCount = 5;

    private const int MaxNoteLength = 200;
    private const int MaxKeyLength = 64;
    private const string Opened = "cash.drawer.opened";
    private const string KickFailed = "cash.drawer.kick-failed";
    private static readonly TimeSpan AuditTimeLimit = TimeSpan.FromSeconds(5);

    public static readonly IReadOnlyList<CashDrawerReasonV1> Reasons =
    [
        new("ChangeCoins", "Bozuk para"),
        new("Count", "Sayım"),
        new("Other", "Diğer"),
    ];

    public async Task<CashDrawerStatusV1> StatusAsync(Guid cashSessionId, Guid userId, CancellationToken cancellationToken)
    {
        // Read, not asked: a till that merely wonders whether to show the button must not write a denial record.
        var canKick = (await roles.GetPermissionCodesForUserAsync(userId, cancellationToken)).Contains(ApplicationPermissions.CashDrawerKick);
        var enabled = await CashDrawerSettings.GetActiveTargetAsync(settings, cancellationToken) is not null;
        var counts = await CountAsync(dataSource, cashSessionId, cancellationToken);
        return new CashDrawerStatusV1(enabled, canKick, counts.NoSaleOpenings, NoSaleWarningCount, Reasons);
    }

    /// <summary>The audit-trail counts of one session; the close reads them inside its own transaction.</summary>
    public static async Task<CashDrawerCounts> CountAsync(
        NpgsqlDataSource dataSource, Guid cashSessionId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(CountsSql);
        return await ReadCountsAsync(command, cashSessionId, cancellationToken);
    }

    public static async Task<CashDrawerCounts> CountAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid cashSessionId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(CountsSql, connection, transaction);
        return await ReadCountsAsync(command, cashSessionId, cancellationToken);
    }

    /// <summary>
    /// Opens the drawer for a cash payment that was just taken; never throws, the payment already stands. Whether the
    /// pulse reached the printer: null when the drawer is off or the attempt itself broke down before an answer.
    /// </summary>
    public async Task<bool?> KickForSaleAsync(
        Guid cashSessionId, Guid terminalId, Guid billId, Guid userId, string correlationId, CancellationToken cancellationToken)
    {
        bool? delivered = null;
        try
        {
            if (await CashDrawerSettings.GetActiveTargetAsync(settings, cancellationToken) is not { } target)
                return null;
            var result = await kicker.KickAsync(target.Host, target.Port, cancellationToken);
            delivered = result.Delivered;
            if (!result.Delivered)
                KickFailedLog(logger, cashSessionId, result.Failure);
            await AppendAsync(
                result.Delivered ? Opened : KickFailed, cashSessionId, userId, correlationId, reason: "Sale",
                new { kind = "Sale", terminalId, billId, failure = result.Failure });
        }
        catch (Exception exception)
        {
            // Includes a caller that hung up and the audit time limit: the payment stands whatever happens here.
            SaleKickFailedLog(logger, cashSessionId, exception);
        }

        return delivered;
    }

    public async Task<CashDrawerOpenResultV1> OpenWithoutSaleAsync(
        Guid cashSessionId, Guid terminalId, Guid userId, NoSaleDrawerRequestV1 request, CancellationToken cancellationToken)
    {
        var (reasonName, note) = ValidateReason(request);
        var key = request.IdempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
            throw new ArgumentException("IdempotencyKey is required (at most 64 characters).", nameof(request));
        var target = await CashDrawerSettings.GetActiveTargetAsync(settings, cancellationToken)
            ?? throw new CashDrawerDisabledException();
        var correlationId = "nosale:" + key;

        // The record is written before the pulse: a drawer that opens without a record is the one thing this must not do.
        var recorded = await RecordNoSaleAsync(
            cashSessionId, terminalId, userId, request.ReasonCode, correlationId,
            note is null ? reasonName : $"{reasonName}: {note}", note, cancellationToken);
        if (recorded.ReplayDelivered is { } replay)
            return new CashDrawerOpenResultV1(replay, recorded.Count, recorded.Warning, Replayed: true);

        var result = await kicker.KickAsync(target.Host, target.Port, cancellationToken);
        if (!result.Delivered)
        {
            KickFailedLog(logger, cashSessionId, result.Failure);
            await TryAppendFailureAsync(cashSessionId, userId, correlationId, result.Failure);
        }

        return new CashDrawerOpenResultV1(result.Delivered, recorded.Count, recorded.Warning, Replayed: false);
    }

    private sealed record NoSaleRecord(bool? ReplayDelivered, int Count, string? Warning);

    /// <summary>The connection is released before the pulse goes out, so a printer that does not answer holds no connection.</summary>
    private async Task<NoSaleRecord> RecordNoSaleAsync(
        Guid cashSessionId, Guid terminalId, Guid userId, string? reasonCode, string correlationId, string reason, string? note,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // One lock per session serializes the key check and the count; the shared row lock holds the close back while a
        // pulse is recorded, so an opening after the close can never be missing from the closing record.
        await using (var @lock = new NpgsqlCommand(
            """
            SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));
            SELECT status FROM cash.cash_sessions WHERE cash_session_id = @id FOR SHARE;
            """, connection, transaction))
        {
            @lock.Parameters.AddWithValue("key", "cash-drawer:" + cashSessionId.ToString("N"));
            @lock.Parameters.AddWithValue("id", cashSessionId);
            await using var reader = await @lock.ExecuteReaderAsync(cancellationToken);
            await reader.NextResultAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetString(0) is not ("Open" or "Counting"))
                throw new CashDrawerSessionNotOpenException();
        }

        var seen = await CountAsync(connection, transaction, cashSessionId, cancellationToken);
        if (await ReplayAsync(connection, transaction, cashSessionId, correlationId, cancellationToken) is { } replay)
            return new NoSaleRecord(replay, seen.NoSaleOpenings, WarningFor(seen.NoSaleOpenings));

        var count = seen.NoSaleOpenings + 1;
        var warning = WarningFor(count);
        await audit.AppendAsync(
            Event(Opened, cashSessionId, userId, correlationId, reason,
                new { kind = "NoSale", reasonCode, note, terminalId, warned = warning is not null }),
            connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new NoSaleRecord(null, count, warning);
    }

    private static string? WarningFor(int noSaleCount)
        => noSaleCount >= NoSaleWarningCount ? $"Bu vardiyada {noSaleCount} kez satışsız çekmece açıldı." : null;

    private static (string Name, string? Note) ValidateReason(NoSaleDrawerRequestV1 request)
    {
        var reason = Reasons.FirstOrDefault(r => r.Code == request.ReasonCode) ?? throw new InvalidCashDrawerReasonException();
        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note))
            note = null;
        if (note is { Length: > MaxNoteLength } || (note is null && reason.Code == "Other") || note?.Any(char.IsControl) == true)
            throw new InvalidCashDrawerReasonException();
        return (reason.Name, note);
    }

    /// <summary>Null when the key was never used; otherwise whether the earlier opening reached the drawer.</summary>
    private static async Task<bool?> ReplayAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid cashSessionId, string correlationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT bool_or(event_name = 'cash.drawer.opened'), bool_or(event_name = 'cash.drawer.kick-failed')
            FROM audit.audit_events
            WHERE aggregate_type = 'CashSession' AND aggregate_id = @id AND correlation_id = @correlation
              AND event_name IN ('cash.drawer.opened', 'cash.drawer.kick-failed');
            """, connection, transaction);
        command.Parameters.AddWithValue("id", cashSessionId);
        command.Parameters.AddWithValue("correlation", correlationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        if (reader.IsDBNull(0))
            return null;
        return reader.IsDBNull(1) || !reader.GetBoolean(1);
    }

    private async Task TryAppendFailureAsync(Guid cashSessionId, Guid userId, string correlationId, string? failure)
    {
        try
        {
            await AppendAsync(KickFailed, cashSessionId, userId, correlationId, "NoSale", new { kind = "NoSale", failure });
        }
        catch (Exception exception)
        {
            FailureNotRecordedLog(logger, cashSessionId, exception);
        }
    }

    private async Task AppendAsync(string eventName, Guid cashSessionId, Guid userId, string correlationId, string reason, object state)
    {
        // The caller may already have hung up; the record is still worth a short try of its own.
        using var deadline = new CancellationTokenSource(AuditTimeLimit);
        await audit.AppendAsync(Event(eventName, cashSessionId, userId, correlationId, reason, state), deadline.Token);
    }

    private static AuditEvent Event(string eventName, Guid cashSessionId, Guid userId, string correlationId, string reason, object state)
        => new(
            Guid.NewGuid(), eventName, "CashSession", cashSessionId, "User", correlationId, userId, reason,
            afterStateJson: JsonSerializer.Serialize(state, StateJson));

    private static readonly JsonSerializerOptions StateJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private const string CountsSql =
        """
        SELECT count(*) FILTER (WHERE event_name = 'cash.drawer.opened' AND after_state_json->>'kind' = 'Sale'),
               count(*) FILTER (WHERE event_name = 'cash.drawer.opened' AND after_state_json->>'kind' = 'NoSale'),
               count(*) FILTER (WHERE event_name = 'cash.drawer.kick-failed')
        FROM audit.audit_events
        WHERE aggregate_type = 'CashSession' AND aggregate_id = @id
          AND event_name IN ('cash.drawer.opened', 'cash.drawer.kick-failed');
        """;

    private static async Task<CashDrawerCounts> ReadCountsAsync(NpgsqlCommand command, Guid cashSessionId, CancellationToken cancellationToken)
    {
        command.Parameters.AddWithValue("id", cashSessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new CashDrawerCounts(checked((int)reader.GetInt64(0)), checked((int)reader.GetInt64(1)), checked((int)reader.GetInt64(2)));
    }

    [LoggerMessage(EventId = 6944, Level = LogLevel.Warning, Message = "The cash drawer of session {CashSessionId} did not open: {Failure}.")]
    private static partial void KickFailedLog(ILogger logger, Guid cashSessionId, string? failure);

    [LoggerMessage(EventId = 6945, Level = LogLevel.Error, Message = "Opening the cash drawer of session {CashSessionId} after a cash payment failed; the payment stands.")]
    private static partial void SaleKickFailedLog(ILogger logger, Guid cashSessionId, Exception exception);

    [LoggerMessage(EventId = 6946, Level = LogLevel.Error, Message = "The failed drawer opening of session {CashSessionId} could not be recorded.")]
    private static partial void FailureNotRecordedLog(ILogger logger, Guid cashSessionId, Exception exception);
}
