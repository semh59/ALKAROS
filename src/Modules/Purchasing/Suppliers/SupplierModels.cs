using System;
using System.Collections.Generic;

namespace ALKAROS.Purchasing.Suppliers;

public sealed record CreateSupplierCommand(
    string Code,
    string Name,
    string? TaxNumber = null,
    string? TaxOffice = null,
    string? Phone = null,
    string? Email = null,
    bool Active = true);

public sealed record UpdateSupplierCommand(
    Guid Id,
    string Name,
    string? TaxNumber = null,
    string? TaxOffice = null,
    string? Phone = null,
    string? Email = null);

public sealed record SupplierSummaryDto(
    Guid Id,
    string Code,
    string Name,
    bool Active);

public sealed record SupplierViewDto(
    Guid Id,
    string Code,
    string Name,
    string? TaxNumber,
    string? TaxOffice,
    string? Phone,
    string? Email,
    bool Active,
    bool IsMasked,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public static class SupplierAccessPolicy
{
    private static readonly HashSet<string> AuthorizedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "ITAdmin",
        "Manager",
        "Finance",
        "PurchasingManager"
    };

    public static bool CanViewSensitiveData(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }
        return AuthorizedRoles.Contains(role.Trim());
    }

    public static SupplierViewDto ProjectToView(Supplier supplier, string? role)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        bool authorized = CanViewSensitiveData(role);
        if (authorized)
        {
            return new SupplierViewDto(
                supplier.Id,
                supplier.Code,
                supplier.Name,
                supplier.TaxNumber,
                supplier.TaxOffice,
                supplier.Phone,
                supplier.Email,
                supplier.Active,
                IsMasked: false,
                supplier.CreatedAt,
                supplier.UpdatedAt);
        }

        // Masked for privacy / KVKK minimization (V0-CMP-003)
        return new SupplierViewDto(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            TaxNumber: supplier.TaxNumber != null ? MaskTaxNumber(supplier.TaxNumber) : null,
            TaxOffice: supplier.TaxOffice != null ? "***" : null,
            Phone: supplier.Phone != null ? MaskPhone(supplier.Phone) : null,
            Email: supplier.Email != null ? MaskEmail(supplier.Email) : null,
            supplier.Active,
            IsMasked: true,
            supplier.CreatedAt,
            supplier.UpdatedAt);
    }

    private static string MaskTaxNumber(string taxNumber)
    {
        if (taxNumber.Length <= 4) return "***";
        return string.Concat(new string('*', taxNumber.Length - 4), taxNumber.AsSpan(taxNumber.Length - 4));
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length <= 4) return "***";
        return string.Concat("***-***", phone.AsSpan(phone.Length - 4));
    }

    private static string MaskEmail(string email)
    {
        var atIdx = email.IndexOf('@');
        if (atIdx <= 1) return "***@***";
        return string.Concat(email.AsSpan(0, 1), "***@", email.AsSpan(atIdx + 1));
    }
}
