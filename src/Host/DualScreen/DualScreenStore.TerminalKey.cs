using System.Data;
using Npgsql;

namespace ALKAROS.Host.DualScreen;

public enum TerminalKeyResult
{
    /// <summary>The terminal had no key yet; the caller must hand the new key to the browser.</summary>
    Claimed,
    Accepted,
    /// <summary>The terminal belongs to another browser.</summary>
    Rejected,
}

public sealed record TerminalSummary(Guid TerminalId, bool Bound, DateTimeOffset CreatedAt, string? LastUserName, DateTimeOffset? LastLoginAt);

public sealed partial class DualScreenStore
{
    private const string TerminalKeyPrefix = "tk-";
    private const int MaximumListedTerminals = 500;

    /// <summary>
    /// A terminal id only names a terminal; the browser's secret key proves it is the same browser. The first login
    /// claims the terminal (also for terminals that existed before keys did), later logins must present that key.
    /// </summary>
    public async Task<(TerminalKeyResult Result, string? NewKey)> ClaimTerminalKeyAsync(
        Guid terminalId, string? presentedKey, CancellationToken cancellationToken)
    {
        await EnsureTerminalAsync(terminalId, cancellationToken);

        // One statement claims an unclaimed terminal, so of several browsers claiming at once exactly one update matches.
        var (raw, hash) = DualScreenToken.Create(TerminalKeyPrefix);
        await using (var claim = _dataSource.CreateCommand(
            "UPDATE customer_display.terminals SET terminal_key_hash = @hash, row_version = row_version + 1, updated_at = now() WHERE terminal_id = @terminal_id AND terminal_key_hash IS NULL;"))
        {
            claim.Parameters.AddWithValue("hash", hash);
            claim.Parameters.AddWithValue("terminal_id", terminalId);
            if (await claim.ExecuteNonQueryAsync(cancellationToken) == 1)
                return (TerminalKeyResult.Claimed, raw);
        }

        await using var select = _dataSource.CreateCommand(
            "SELECT terminal_key_hash FROM customer_display.terminals WHERE terminal_id = @terminal_id;");
        select.Parameters.AddWithValue("terminal_id", terminalId);
        return await select.ExecuteScalarAsync(cancellationToken) is string storedHash
            && DualScreenToken.Matches(presentedKey ?? string.Empty, storedHash)
                ? (TerminalKeyResult.Accepted, null)
                : (TerminalKeyResult.Rejected, null);
    }

    /// <summary>
    /// Releases a terminal whose browser data was lost, so the next login claims it again. The audit row commits with
    /// the change. False when no such terminal exists.
    /// </summary>
    public async Task<bool> ResetTerminalKeyAsync(
        Guid terminalId,
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task> audit,
        CancellationToken cancellationToken)
    {
        EnsureNotEmpty(terminalId, nameof(terminalId));
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using (var update = CreateCommand(connection, transaction,
            "UPDATE customer_display.terminals SET terminal_key_hash = NULL, row_version = row_version + 1, updated_at = now() WHERE terminal_id = @terminal_id RETURNING terminal_id;"))
        {
            update.Parameters.AddWithValue("terminal_id", terminalId);
            if (await update.ExecuteScalarAsync(cancellationToken) is null)
                return false;
        }

        await audit(connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TerminalSummary>> ListTerminalsAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT t.terminal_id, t.terminal_key_hash IS NOT NULL, t.created_at, last_login.display_name, last_login.created_at
            FROM customer_display.terminals t
            LEFT JOIN LATERAL (
                SELECT u.display_name, s.created_at
                FROM identity.device_sessions s
                JOIN identity.users u ON u.user_id = s.user_id
                WHERE s.device_id = 'cashier:' || t.terminal_id::text
                ORDER BY s.created_at DESC
                LIMIT 1
            ) last_login ON TRUE
            ORDER BY last_login.created_at DESC NULLS LAST, t.created_at DESC
            LIMIT {MaximumListedTerminals};
            """);
        var terminals = new List<TerminalSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            terminals.Add(new TerminalSummary(
                reader.GetGuid(0),
                reader.GetBoolean(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return terminals;
    }
}
