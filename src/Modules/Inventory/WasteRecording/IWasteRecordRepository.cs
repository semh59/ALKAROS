namespace ALKAROS.Inventory.WasteRecording;

public interface IWasteRecordRepository
{
    Task InsertAsync(WasteRecord record, CancellationToken ct = default);
    Task<WasteRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WasteRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<IReadOnlyList<WasteRecord>> GetBySourceAsync(string wasteSource, Guid sourceReferenceId, CancellationToken ct = default);
}
