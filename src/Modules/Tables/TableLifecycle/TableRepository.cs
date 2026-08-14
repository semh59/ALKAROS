namespace ALKAROS.Tables.TableLifecycle;

public interface ITableRepository
{
    Task<Table?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Table>> GetByZoneAsync(Guid zoneId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Table>> GetUnzonedAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Table table, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically moves the table to <paramref name="target"/> guarded by an
    /// optimistic row version. Returns the new row version; throws
    /// <see cref="InvalidOperationException"/> when no row was updated
    /// (missing row or stale version).
    /// </summary>
    Task<long> UpdateStatusAsync(
        Guid id,
        TableState target,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);
}

public interface IZoneRepository
{
    Task<Zone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Zone?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Zone>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Zone zone, CancellationToken cancellationToken = default);

    Task UpdateAsync(Zone zone, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}