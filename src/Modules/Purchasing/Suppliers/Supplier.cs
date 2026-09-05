using System;

namespace ALKAROS.Purchasing.Suppliers;

public sealed class Supplier
{
    public Guid Id { get; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? TaxNumber { get; private set; }
    public string? TaxOffice { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool Active { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Supplier(
        Guid id,
        string code,
        string name,
        string? taxNumber,
        string? taxOffice,
        string? phone,
        string? email,
        bool active,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        TaxNumber = NormalizeOptional(taxNumber, 32);
        TaxOffice = NormalizeOptional(taxOffice, 100);
        Phone = NormalizeOptional(phone, 50);
        Email = NormalizeEmail(email);
        Active = active;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Supplier Create(
        string code,
        string name,
        string? taxNumber = null,
        string? taxOffice = null,
        string? phone = null,
        string? email = null,
        bool active = true,
        Guid? id = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Supplier(
            id ?? Guid.NewGuid(),
            code,
            name,
            taxNumber,
            taxOffice,
            phone,
            email,
            active,
            now,
            now);
    }

    public void UpdateDetails(
        string name,
        string? taxNumber,
        string? taxOffice,
        string? phone,
        string? email)
    {
        Name = NormalizeName(name);
        TaxNumber = NormalizeOptional(taxNumber, 32);
        TaxOffice = NormalizeOptional(taxOffice, 100);
        Phone = NormalizeOptional(phone, 50);
        Email = NormalizeEmail(email);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        Active = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        Active = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssertCanAcceptOrders()
    {
        if (!Active)
        {
            throw new InactiveSupplierException($"Supplier '{Code}' ({Id}) is inactive and cannot accept purchase orders.");
        }
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidSupplierDataException("Supplier code is required and cannot be empty.");
        }
        var trimmed = code.Trim().ToUpperInvariant();
        if (trimmed.Length > 64)
        {
            throw new InvalidSupplierDataException("Supplier code cannot exceed 64 characters.");
        }
        return trimmed;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidSupplierDataException("Supplier name is required and cannot be empty.");
        }
        var trimmed = name.Trim();
        if (trimmed.Length > 255)
        {
            throw new InvalidSupplierDataException("Supplier name cannot exceed 255 characters.");
        }
        return trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidSupplierDataException($"Field value cannot exceed {maxLength} characters.");
        }
        return trimmed;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }
        var trimmed = email.Trim();
        if (trimmed.Length > 255)
        {
            throw new InvalidSupplierDataException("Supplier email cannot exceed 255 characters.");
        }
        if (!trimmed.Contains('@') || trimmed.StartsWith('@') || trimmed.EndsWith('@'))
        {
            throw new InvalidSupplierDataException($"Invalid email format: '{trimmed}'.");
        }
        return trimmed;
    }
}
