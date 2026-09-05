using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ALKAROS.Purchasing.Suppliers;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Supplier?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Supplier?> GetByTaxNumberAsync(string taxNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Supplier>> ListAsync(bool? activeOnly = null, CancellationToken ct = default);
    Task SaveAsync(Supplier supplier, CancellationToken ct = default);
    Task UpdateAsync(Supplier supplier, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
