namespace ALKAROS.Inventory.WasteRecording;

public interface IWasteRecordingService
{
    Task<WasteRecordingResult> RecordWasteAsync(RecordWasteRequest request, CancellationToken cancellationToken = default);
    Task<WasteRecord?> GetWasteRecordByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WasteRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WasteRecord>> GetWasteRecordsBySourceAsync(string wasteSource, Guid sourceReferenceId, CancellationToken cancellationToken = default);
}
