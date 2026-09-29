namespace ALKAROS.CustomerAccounts.CreditTerms;

/// <summary>V1-RMD-440: persistence for <see cref="CustomerCreditTerms"/> (<c>customer_account.credit_terms</c>).</summary>
public interface ICustomerCreditTermsStore
{
    /// <summary>
    /// The customer's terms, <see cref="CustomerCreditTerms.None"/> when none are stored, or null when the customer
    /// does not exist or is anonymized.
    /// </summary>
    Task<CustomerCreditTerms?> GetAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the customer's terms, replacing any earlier ones. Returns null (and writes nothing) when the customer
    /// does not exist or is anonymized.
    /// </summary>
    Task<CustomerCreditTerms?> SetAsync(
        Guid customerId, decimal creditLimit, int? paymentTermDays, Guid updatedBy, CancellationToken cancellationToken = default);
}
