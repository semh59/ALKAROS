using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ALKAROS.Purchasing.Suppliers;

public sealed class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repository;

    public SupplierService(ISupplierRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Supplier> CreateSupplierAsync(CreateSupplierCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = Supplier.Create(
            command.Code,
            command.Name,
            command.TaxNumber,
            command.TaxOffice,
            command.Phone,
            command.Email,
            command.Active);

        // Pre-check code uniqueness
        var existingByCode = await _repository.GetByCodeAsync(supplier.Code, ct);
        if (existingByCode != null)
        {
            throw new DuplicateSupplierCodeException(supplier.Code);
        }

        // Pre-check tax number uniqueness if provided
        if (supplier.TaxNumber != null)
        {
            var existingByTax = await _repository.GetByTaxNumberAsync(supplier.TaxNumber, ct);
            if (existingByTax != null)
            {
                throw new DuplicateSupplierTaxNumberException(supplier.TaxNumber);
            }
        }

        await _repository.SaveAsync(supplier, ct);
        return supplier;
    }

    public async Task<Supplier> UpdateSupplierAsync(UpdateSupplierCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = await _repository.GetByIdAsync(command.Id, ct)
            ?? throw new SupplierNotFoundException(command.Id);

        // If tax number is changed, check uniqueness
        if (!string.IsNullOrWhiteSpace(command.TaxNumber))
        {
            var normalizedTax = command.TaxNumber.Trim();
            if (!string.Equals(supplier.TaxNumber, normalizedTax, StringComparison.OrdinalIgnoreCase))
            {
                var existingByTax = await _repository.GetByTaxNumberAsync(normalizedTax, ct);
                if (existingByTax != null && existingByTax.Id != supplier.Id)
                {
                    throw new DuplicateSupplierTaxNumberException(normalizedTax);
                }
            }
        }

        supplier.UpdateDetails(
            command.Name,
            command.TaxNumber,
            command.TaxOffice,
            command.Phone,
            command.Email);

        await _repository.UpdateAsync(supplier, ct);
        return supplier;
    }

    public async Task ActivateSupplierAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await _repository.GetByIdAsync(id, ct)
            ?? throw new SupplierNotFoundException(id);

        supplier.Activate();
        await _repository.UpdateAsync(supplier, ct);
    }

    public async Task DeactivateSupplierAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await _repository.GetByIdAsync(id, ct)
            ?? throw new SupplierNotFoundException(id);

        supplier.Deactivate();
        await _repository.UpdateAsync(supplier, ct);
    }

    public async Task<SupplierViewDto> GetSupplierViewAsync(Guid id, string? userRole, CancellationToken ct = default)
    {
        var supplier = await _repository.GetByIdAsync(id, ct)
            ?? throw new SupplierNotFoundException(id);

        return SupplierAccessPolicy.ProjectToView(supplier, userRole);
    }

    public async Task<IReadOnlyList<SupplierSummaryDto>> ListSuppliersAsync(bool? activeOnly = null, CancellationToken ct = default)
    {
        var list = await _repository.ListAsync(activeOnly, ct);
        return list.Select(s => new SupplierSummaryDto(s.Id, s.Code, s.Name, s.Active)).ToList();
    }

    public async Task AssertSupplierCanAcceptOrdersAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await _repository.GetByIdAsync(id, ct)
            ?? throw new SupplierNotFoundException(id);

        supplier.AssertCanAcceptOrders();
    }
}
