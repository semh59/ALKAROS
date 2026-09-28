namespace ALKAROS.CustomerData.Profiles;

/// <summary>All four fields are optional - a customer record may be created with only some contact details known.</summary>
public sealed record CreateCustomerProfileRequest(string? Name, string? Phone, string? Email, string? Address);

public sealed record UpdateCustomerContactRequest(string? Name, string? Phone, string? Email, string? Address);

/// <summary>
/// Persistence for customer PII (`customer_data.profiles`, migration 159).
/// Name/phone/email/address are never stored in plaintext - see
/// <see cref="CustomerProfileEncryptionPolicy"/> and
/// `PostgresCustomerProfileStore`. Every mutation is optimistic-concurrency-
/// checked against the caller's last-known <see cref="CustomerProfile.RowVersion"/>.
/// </summary>
public interface ICustomerProfileStore
{
    /// <summary>Creates a new profile and returns its generated id.</summary>
    Task<Guid> CreateAsync(CreateCustomerProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the profile projected for <paramref name="role"/> (see
    /// <see cref="CustomerProfileAccessPolicy"/>), or <c>null</c> if no
    /// customer with this id exists. An anonymized profile's contact fields
    /// are always null, regardless of role.
    /// </summary>
    Task<CustomerProfile?> GetAsync(Guid customerId, CustomerAccessRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored contact fields entirely. Throws
    /// <see cref="CustomerProfileConcurrencyException"/> if
    /// <paramref name="expectedRowVersion"/> is stale, or
    /// <see cref="CustomerProfileNotFoundException"/> if the customer does
    /// not exist, or <see cref="CustomerProfileAnonymizedException"/> if it
    /// was already anonymized (an anonymized record's contact fields are
    /// gone for good, not editable back in).
    /// </summary>
    Task UpdateContactAsync(
        Guid customerId,
        UpdateCustomerContactRequest request,
        int expectedRowVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// V0-CMP-003's disposal action for "Customer PII" is "Anonymize after
    /// retention" (not delete) - the record's existence and id survive
    /// (referential integrity for historical orders/invoices), only the
    /// identifying fields are wiped. Idempotent: anonymizing an already-
    /// anonymized profile is a no-op success, not an error (a retention
    /// sweep that races itself must not fail). Throws
    /// <see cref="CustomerProfileConcurrencyException"/> if
    /// <paramref name="expectedRowVersion"/> is stale, or
    /// <see cref="CustomerProfileNotFoundException"/> if the customer does
    /// not exist.
    /// </summary>
    Task AnonymizeAsync(Guid customerId, int expectedRowVersion, CancellationToken cancellationToken = default);
}
