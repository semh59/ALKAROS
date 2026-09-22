namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// Append-only persistence for <see cref="OffsiteBackupReceipt"/>. Deliberately
/// exposes no update or delete method — a receipt, once recorded, is
/// immutable for the lifetime of the process (the "immutable artifact
/// metadata" requirement enforced at the type level, not just by convention).
/// </summary>
public interface IOffsiteBackupReceiptStore
{
    Task RecordAsync(OffsiteBackupReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>Every recorded receipt, newest first, bounded by <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<OffsiteBackupReceipt>> GetAllAsync(int limit = 500, CancellationToken cancellationToken = default);

    /// <summary>Recorded receipts for one data class, newest first, bounded by <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<OffsiteBackupReceipt>> GetByDataClassAsync(DataClass dataClass, int limit = 500, CancellationToken cancellationToken = default);
}
