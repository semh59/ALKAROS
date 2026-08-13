namespace ALKAROS.Identity.Authorization;

public interface IPermissionRepository
{
    Task<PermissionEntry?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(PermissionEntry permission, CancellationToken cancellationToken = default);
}