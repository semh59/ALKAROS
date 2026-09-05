using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ALKAROS.Purchasing.Suppliers;

public interface ISupplierService
{
    Task<Supplier> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken ct = default);
    Task<Supplier> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken ct = default);
    Task ActivateSupplierAsync(Guid id, CancellationToken ct = default);
    Task DeactivateSupplierAsync(Guid id, CancellationToken ct = default);
    Task<SupplierViewDto> GetSupplierViewAsync(Guid id, string? userRole, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierSummaryDto>> ListSuppliersAsync(bool? activeOnly = null, CancellationToken ct = default);
    Task AssertSupplierCanAcceptOrdersAsync(Guid id, CancellationToken ct = default);
}
