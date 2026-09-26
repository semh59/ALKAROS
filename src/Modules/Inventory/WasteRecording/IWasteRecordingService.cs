namespace ALKAROS.Inventory.WasteRecording;

public interface IWasteRecordingService
{
    Task<WasteRecordingResult> RecordWasteAsync(RecordWasteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-310: records the waste (ledger movement, waste record, guarded on-hand decrement) inside the
    /// caller's transaction, so it commits or rolls back together with the caller's other writes. A repeat with
    /// the same idempotency key returns the existing record. With a null <paramref name="transaction"/> it runs in
    /// a transaction of its own.
    /// </summary>
    Task<WasteRecordingResult> RecordWasteAsync(
        RecordWasteRequest request, Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
    Task<WasteRecord?> GetWasteRecordByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WasteRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WasteRecord>> GetWasteRecordsBySourceAsync(string wasteSource, Guid sourceReferenceId, CancellationToken cancellationToken = default);
}
