using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>V1-WTR-013: one hand-off note as returned to the receiving waiter.</summary>
public sealed record ServingHandoffNote(
    Guid HandoffNoteId, string Note, string FromDisplayName, DateTimeOffset CreatedAt);

/// <summary>
/// V1-WTR-013: backs the optional context note a departing waiter can leave
/// on <c>/transfer-server</c> ("table 5 is waiting on dessert, table 8
/// complained"). Read-once by design: <see cref="PopPendingAsync"/> marks
/// the row consumed in the same statement it reads it, so the receiving
/// waiter sees it exactly the first time they open a table after the
/// hand-off, never again. At most one pending note per recipient at a
/// time: <see cref="LeaveAsync"/> supersedes (silently consumes) any
/// earlier pending note for the same recipient before inserting the new
/// one, so a waiter who receives two hand-offs before opening any table
/// only ever sees the newer note.
/// </summary>
public sealed class ServingHandoffNoteStore
{
    private const int MaxNoteLength = 200;

    private readonly NpgsqlDataSource _dataSource;
    private readonly ISettingsService _settings;

    public ServingHandoffNoteStore(NpgsqlDataSource dataSource, ISettingsService settings)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Validates and stores a note for a hand-off. A null or whitespace-only
    /// note is a no-op (the note is optional) - the caller does not need to
    /// branch on whether one was given. V1-SET-004: this deployment turning
    /// the whole feature off is the same as the caller having sent no note.
    /// </summary>
    public async Task LeaveAsync(
        Guid fromUserId, Guid toUserId, string? note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(note))
            return;
        if (!await GarsonFeatureToggles.IsEnabledAsync(_settings, GarsonFeature.ShiftHandoffNotes, cancellationToken))
            return;
        var trimmed = note.Trim();
        if (trimmed.Length > MaxNoteLength)
            throw new HandoffNoteTooLongException(trimmed.Length, MaxNoteLength);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var supersede = new NpgsqlCommand(
            """
            UPDATE notifications.serving_handoff_notes
            SET consumed_at = now()
            WHERE to_user_id = @to_user_id AND consumed_at IS NULL;
            """, connection, transaction))
        {
            supersede.Parameters.Add("to_user_id", NpgsqlDbType.Uuid).Value = toUserId;
            await supersede.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO notifications.serving_handoff_notes
                (handoff_note_id, from_user_id, to_user_id, note)
            VALUES (@handoff_note_id, @from_user_id, @to_user_id, @note);
            """, connection, transaction))
        {
            insert.Parameters.Add("handoff_note_id", NpgsqlDbType.Uuid).Value = Guid.NewGuid();
            insert.Parameters.Add("from_user_id", NpgsqlDbType.Uuid).Value = fromUserId;
            insert.Parameters.Add("to_user_id", NpgsqlDbType.Uuid).Value = toUserId;
            insert.Parameters.Add("note", NpgsqlDbType.Text).Value = trimmed;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Reads and consumes <paramref name="toUserId"/>'s pending note in one
    /// round trip, or null when there is none.
    /// </summary>
    public async Task<ServingHandoffNote?> PopPendingAsync(
        Guid toUserId, CancellationToken cancellationToken = default)
    {
        if (!await GarsonFeatureToggles.IsEnabledAsync(_settings, GarsonFeature.ShiftHandoffNotes, cancellationToken))
            return null;

        await using var cmd = _dataSource.CreateCommand(
            """
            UPDATE notifications.serving_handoff_notes n
            SET consumed_at = now()
            WHERE n.to_user_id = @to_user_id AND n.consumed_at IS NULL
            RETURNING n.handoff_note_id, n.note, n.created_at,
                (SELECT display_name FROM identity.users WHERE user_id = n.from_user_id);
            """);
        cmd.Parameters.Add("to_user_id", NpgsqlDbType.Uuid).Value = toUserId;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ServingHandoffNote(
            reader.GetGuid(0), reader.GetString(1), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(2));
    }
}

/// <summary>Raised when a hand-off note exceeds the 200-character cap.</summary>
public sealed class HandoffNoteTooLongException : Exception
{
    public HandoffNoteTooLongException(int actualLength, int maxLength)
        : base($"Hand-off note is {actualLength} characters; the limit is {maxLength}.")
    {
        ActualLength = actualLength;
        MaxLength = maxLength;
    }

    public int ActualLength { get; }
    public int MaxLength { get; }
}
